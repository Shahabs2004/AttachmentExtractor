using System.Diagnostics;
using System.Runtime.InteropServices;
using AttachmentExtractor.Core.Abstractions;
using AttachmentExtractor.Core.Filters;
using AttachmentExtractor.Core.Models;
using AttachmentExtractor.Core.Services;
using AttachmentExtractor.Core.Utilities;

namespace AttachmentExtractor.Outlook;

public sealed class OutlookMailSource : IMailSource
{
    private const string PrSmtpAddress = "http://schemas.microsoft.com/mapi/proptag/0x39FE001E";
    private const string PrAttachmentHidden = "http://schemas.microsoft.com/mapi/proptag/0x7FFE000B";
    private const string PrAttachmentContentId = "http://schemas.microsoft.com/mapi/proptag/0x3712001E";
    private const int MailItemClass = 43;
    private const int MaxFolderDepth = 32;

    private readonly StaWorker _worker = new();
    private dynamic _application = null!;
    private dynamic _namespace = null!;
    private volatile bool _connected;
    private bool _disposed;

    public bool IsConnected => _connected;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        return _worker.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureConnected();
        });
    }

    public Task<IReadOnlyList<FolderInfo>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        return _worker.RunAsync<IReadOnlyList<FolderInfo>>(() => GetFoldersCore(cancellationToken));
    }

    public Task<ScanResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        return _worker.RunAsync(() => ScanCore(request, progress, cancellationToken));
    }

    public Task<ExportResult> ExportAsync(
        ExportOptions options,
        IReadOnlyList<ExportItem> items,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        return _worker.RunAsync(() => ExportCore(options, items, progress, cancellationToken));
    }

    private void EnsureConnected()
    {
        if (_connected)
        {
            return;
        }

        ConnectCore();
    }

    private void ConnectCore()
    {
        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType is null)
            {
                throw new OutlookUnavailableException(
                    "Outlook کلاسیک نصب یا در سیستم ثبت نشده است. نسخه کلاسیک Outlook را نصب و یک‌بار اجرا کنید.");
            }

            _application = Activator.CreateInstance(outlookType)
                ?? throw new OutlookUnavailableException("راه‌اندازی Outlook ناموفق بود.");

            _namespace = _application.GetNameSpace("MAPI");

            try
            {
                _ = _namespace.Folders.Count;
            }
            catch
            {
                try
                {
                    _namespace.Logon(Type.Missing, Type.Missing, false, false);
                }
                catch
                {
                    // GetNameSpace usually attaches to the default logged-on profile.
                }

                _ = _namespace.Folders.Count;
            }

            _connected = true;
        }
        catch (OutlookUnavailableException)
        {
            throw;
        }
        catch (COMException ex)
        {
            throw new OutlookUnavailableException(
                "اتصال به Outlook ناموفق بود. پروفایل Outlook، اجرا بودن نسخه کلاسیک و دسترسی برنامه‌ای را بررسی کنید.",
                ex);
        }
        catch (Exception ex)
        {
            throw new OutlookUnavailableException(
                "اتصال به Outlook ناموفق بود. پروفایل Outlook و اجرا بودن نسخه کلاسیک را بررسی کنید.",
                ex);
        }
    }

    private IReadOnlyList<FolderInfo> GetFoldersCore(CancellationToken cancellationToken)
    {
        EnsureConnected();

        var result = new List<FolderInfo>();
        dynamic stores = _namespace.Folders;

        try
        {
            var count = ComHelper.GetInt(() => stores.Count, 0);
            for (var index = 1; index <= count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                dynamic store = null!;
                try
                {
                    store = stores[index];
                    var storeName = ComHelper.GetString(() => store.Name, $"Store {index}");
                    result.Add(MapFolder(store, storeName, 0, cancellationToken));
                }
                finally
                {
                    ComHelper.Release(store);
                }
            }
        }
        finally
        {
            ComHelper.Release(stores);
        }

        return result;
    }

    private static FolderInfo MapFolder(
        dynamic folder,
        string path,
        int depth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var info = new FolderInfo
        {
            Name = ComHelper.GetString(() => folder.Name, path),
            EntryId = ComHelper.GetString(() => folder.EntryID),
            StoreId = ComHelper.GetString(() => folder.StoreID),
            FullPath = path,
            Depth = depth
        };

        if (string.IsNullOrWhiteSpace(info.StoreId))
        {
            info.StoreId = info.EntryId;
        }

        dynamic children = null!;
        try
        {
            children = folder.Folders;
            var count = ComHelper.GetInt(() => children.Count, 0);
            for (var index = 1; index <= count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                dynamic child = null!;
                try
                {
                    child = children[index];
                    var childName = ComHelper.GetString(() => child.Name, $"Folder {index}");
                    info.Children.Add(MapFolder(
                        child,
                        path + "\\" + childName,
                        depth + 1,
                        cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Some service folders are not accessible to the current profile.
                }
                finally
                {
                    ComHelper.Release(child);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // A folder may have no accessible children.
        }
        finally
        {
            ComHelper.Release(children);
        }

        return info;
    }

    private ScanResult ScanCore(
        ScanRequest request,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        EnsureConnected();

        var result = new ScanResult();
        var resolvedFolders = new List<(FolderInfo Info, dynamic Folder)>();

        try
        {
            foreach (var info in request.Folders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                dynamic folder = null!;
                try
                {
                    folder = ResolveFolder(info);
                    resolvedFolders.Add((info, folder));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"دسترسی به پوشه «{info.FullPath}» ممکن نشد: {ex.Message}");
                    ComHelper.Release(folder);
                }
            }

            result.FolderCount = resolvedFolders.Count;

            var totalItems = 0;
            foreach (var (_, folder) in resolvedFolders)
            {
                try
                {
                    dynamic items = folder.Items;
                    try
                    {
                        totalItems += ComHelper.GetInt(() => items.Count, 0);
                    }
                    finally
                    {
                        ComHelper.Release(items);
                    }
                }
                catch
                {
                    // Item count is only used for progress.
                }
            }

            var scanned = 0;
            var matched = 0;
            var foldersDone = 0;
            var lastReport = DateTime.MinValue;
            var stopwatch = Stopwatch.StartNew();

            progress?.Report(new ScanProgress
            {
                TotalItems = totalItems,
                FoldersTotal = resolvedFolders.Count
            });

            foreach (var (info, folder) in resolvedFolders)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Canceled = true;
                    break;
                }

                dynamic items = null!;
                try
                {
                    items = folder.Items;
                    dynamic item = items.GetFirst();

                    while (item is not null)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            result.Canceled = true;
                            ComHelper.Release(item);
                            break;
                        }

                        scanned++;

                        try
                        {
                            if (IsMailItem(item))
                            {
                                var message = MapMessage(item, info, request.IncludeBody);
                                if (FilterEvaluator.Matches(request.Filter, message))
                                {
                                    message.Body = string.Empty;
                                    result.Messages.Add(message);
                                    matched++;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            if (result.Errors.Count < 200)
                            {
                                result.Errors.Add($"خطا در خواندن یک ایمیل در «{info.FullPath}»: {ex.Message}");
                            }
                        }

                        dynamic next = null!;
                        try
                        {
                            next = items.GetNext();
                        }
                        catch
                        {
                            next = null!;
                        }

                        ComHelper.Release(item);
                        item = next;

                        if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 150 || scanned % 50 == 0)
                        {
                            lastReport = DateTime.UtcNow;
                            progress?.Report(new ScanProgress
                            {
                                TotalItems = totalItems,
                                ScannedItems = scanned,
                                MatchedItems = matched,
                                FoldersDone = foldersDone,
                                FoldersTotal = resolvedFolders.Count,
                                CurrentFolder = info.FullPath
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    result.Canceled = true;
                    break;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"خطا در اسکن پوشه «{info.FullPath}»: {ex.Message}");
                }
                finally
                {
                    ComHelper.Release(items);
                    foldersDone++;
                }

                progress?.Report(new ScanProgress
                {
                    TotalItems = totalItems,
                    ScannedItems = scanned,
                    MatchedItems = matched,
                    FoldersDone = foldersDone,
                    FoldersTotal = resolvedFolders.Count,
                    CurrentFolder = info.FullPath
                });
            }

            result.ScannedItems = scanned;
            result.Canceled |= cancellationToken.IsCancellationRequested;

            progress?.Report(new ScanProgress
            {
                TotalItems = totalItems,
                ScannedItems = scanned,
                MatchedItems = matched,
                FoldersDone = foldersDone,
                FoldersTotal = resolvedFolders.Count,
                CurrentFolder = "پایان اسکن"
            });

            _ = stopwatch.Elapsed;
            return result;
        }
        finally
        {
            foreach (var (_, folder) in resolvedFolders)
            {
                ComHelper.Release(folder);
            }
        }
    }

    private static bool IsMailItem(dynamic item)
    {
        try
        {
            var messageClass = ComHelper.GetString(() => item.MessageClass);
            return messageClass.StartsWith("IPM.Note", StringComparison.OrdinalIgnoreCase) ||
                   ComHelper.GetInt(() => item.Class, 0) == MailItemClass;
        }
        catch
        {
            return false;
        }
    }

    private static MailMessageInfo MapMessage(dynamic item, FolderInfo folder, bool includeBody)
    {
        var message = new MailMessageInfo
        {
            EntryId = ComHelper.GetString(() => item.EntryID),
            StoreId = folder.StoreId,
            FolderPath = folder.FullPath,
            Subject = ComHelper.GetString(() => item.Subject),
            SenderName = ComHelper.GetString(() => item.SenderName),
            SenderEmail = GetSenderEmail(item),
            To = ComHelper.GetString(() => item.To),
            Cc = ComHelper.GetString(() => item.CC),
            ReceivedTime = ComHelper.GetDate(() => item.ReceivedTime),
            SentOn = ComHelper.GetDate(() => item.SentOn),
            SizeBytes = ComHelper.GetLong(() => item.Size, 0),
            IsRead = !ComHelper.GetBool(() => item.Unread, false),
            Importance = ComHelper.GetInt(() => item.Importance, 1),
            Categories = ComHelper.GetString(() => item.Categories),
            Body = includeBody ? ComHelper.GetString(() => item.Body) : string.Empty
        };

        dynamic attachments = null!;
        try
        {
            attachments = item.Attachments;
            var count = ComHelper.GetInt(() => attachments.Count, 0);
            for (var index = 1; index <= count; index++)
            {
                dynamic attachment = null!;
                try
                {
                    attachment = attachments[index];
                    message.Attachments.Add(MapAttachment(attachment, index));
                }
                catch
                {
                    // Keep the message even if a single attachment cannot be read.
                }
                finally
                {
                    ComHelper.Release(attachment);
                }
            }
        }
        finally
        {
            ComHelper.Release(attachments);
        }

        return message;
    }

    private static AttachmentInfo MapAttachment(dynamic attachment, int index)
    {
        var fileName = ComHelper.GetString(() => attachment.FileName);
        var displayName = ComHelper.GetString(() => attachment.DisplayName);
        var hidden = false;
        var contentId = string.Empty;

        dynamic propertyAccessor = null!;
        try
        {
            propertyAccessor = attachment.PropertyAccessor;

            try
            {
                var hiddenValue = propertyAccessor.GetProperty(PrAttachmentHidden);
                hidden = hiddenValue is not null &&
                         Convert.ToBoolean(hiddenValue, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                // The property is not available for every attachment type.
            }

            try
            {
                var contentIdValue = propertyAccessor.GetProperty(PrAttachmentContentId);
                contentId = contentIdValue?.ToString() ?? string.Empty;
            }
            catch
            {
                // Inline content id is optional.
            }
        }
        catch
        {
            // PropertyAccessor is not available for some attachment types.
        }
        finally
        {
            ComHelper.Release(propertyAccessor);
        }

        var effectiveName = !string.IsNullOrWhiteSpace(fileName) ? fileName : displayName;

        return new AttachmentInfo
        {
            Index = index,
            FileName = fileName,
            DisplayName = displayName,
            SizeBytes = ComHelper.GetLong(() => attachment.Size, 0),
            IsInline = hidden || !string.IsNullOrWhiteSpace(contentId),
            IsHidden = hidden,
            AttachmentType = ComHelper.GetInt(() => attachment.Type, 0),
            Extension = Path.GetExtension(effectiveName)
        };
    }

    private static string GetSenderEmail(dynamic item)
    {
        var email = ComHelper.GetString(() => item.SenderEmailAddress);
        var emailType = ComHelper.GetString(() => item.SenderEmailType);

        if (!string.Equals(emailType, "EX", StringComparison.OrdinalIgnoreCase) &&
            !email.StartsWith("/O=", StringComparison.OrdinalIgnoreCase))
        {
            return email;
        }

        dynamic sender = null!;
        dynamic propertyAccessor = null!;
        try
        {
            sender = item.Sender;
            propertyAccessor = sender.PropertyAccessor;
            var smtpAddress = propertyAccessor.GetProperty(PrSmtpAddress);
            var smtp = smtpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(smtp))
            {
                return smtp!;
            }
        }
        catch
        {
            // Fall back to the raw Exchange address.
        }
        finally
        {
            ComHelper.Release(propertyAccessor);
            ComHelper.Release(sender);
        }

        return email;
    }

    private dynamic ResolveFolder(FolderInfo info)
    {
        try
        {
            return _namespace.GetFolderFromID(info.EntryId, info.StoreId);
        }
        catch
        {
            // Fall back to a traversal when the store id is not accepted.
        }

        dynamic stores = _namespace.Folders;
        try
        {
            var found = FindFolderInCollection(stores, info.EntryId, 0);
            if (found is not null)
            {
                return found;
            }
        }
        finally
        {
            ComHelper.Release(stores);
        }

        throw new InvalidOperationException($"پوشه «{info.FullPath}» پیدا نشد.");
    }

    private static dynamic FindFolderInCollection(
        dynamic folders,
        string entryId,
        int depth)
    {
        if (depth > MaxFolderDepth)
        {
            return null!;
        }

        var count = ComHelper.GetInt(() => folders.Count, 0);
        for (var index = 1; index <= count; index++)
        {
            dynamic folder = folders[index];
            var keep = false;
            try
            {
                if (string.Equals(
                        ComHelper.GetString(() => folder.EntryID),
                        entryId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    keep = true;
                    return folder;
                }

                dynamic children = null!;
                try
                {
                    children = folder.Folders;
                    var found = FindFolderInCollection(children, entryId, depth + 1);
                    if (found is not null)
                    {
                        keep = true;
                        return found;
                    }
                }
                catch
                {
                    // Ignore inaccessible children.
                }
                finally
                {
                    ComHelper.Release(children);
                }
            }
            finally
            {
                if (!keep)
                {
                    ComHelper.Release(folder);
                }
            }
        }

        return null!;
    }

    private ExportResult ExportCore(
        ExportOptions options,
        IReadOnlyList<ExportItem> items,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        EnsureConnected();

        if (string.IsNullOrWhiteSpace(options.DestinationRoot))
        {
            throw new InvalidOperationException("مسیر مقصد خالی است.");
        }

        var destinationRoot = Path.GetFullPath(options.DestinationRoot);
        LongPath.CreateDirectory(destinationRoot);

        var reserved = CollisionResolver.CreateReservationSet();
        var total = items.Sum(item => item.AttachmentIndexes.Count);
        var result = new ExportResult
        {
            TotalAttachments = total,
            StartedAt = DateTime.Now
        };

        var processed = 0;
        var lastProgressReport = DateTime.MinValue;

        void ReportProgress(string currentFile)
        {
            if (progress is null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - lastProgressReport).TotalMilliseconds < 60 && processed < total)
            {
                return;
            }

            lastProgressReport = now;
            progress.Report(new ExportProgress
            {
                TotalAttachments = total,
                ProcessedAttachments = processed,
                SavedAttachments = result.Saved,
                SkippedAttachments = result.Skipped,
                FailedAttachments = result.Failed,
                BytesSaved = result.BytesSaved,
                CurrentFile = currentFile
            });
        }

        progress?.Report(new ExportProgress { TotalAttachments = total });

        foreach (var exportItem in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Canceled = true;
                break;
            }

            dynamic mail = null!;
            dynamic attachments = null!;

            try
            {
                mail = GetMailItem(exportItem.Message);
                attachments = mail.Attachments;

                foreach (var index in exportItem.AttachmentIndexes.Distinct())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.Canceled = true;
                        break;
                    }

                    var info = exportItem.Message.Attachments.FirstOrDefault(
                        attachment => attachment.Index == index);

                    dynamic attachment = null!;
                    try
                    {
                        attachment = FindAttachment(attachments, index, info?.EffectiveName ?? string.Empty);
                        if (attachment is null)
                        {
                            throw new InvalidOperationException("پیوست موردنظر در ایمیل پیدا نشد.");
                        }

                        if (info is null)
                        {
                            info = new AttachmentInfo
                            {
                                Index = index,
                                FileName = ComHelper.GetString(() => attachment.FileName),
                                DisplayName = ComHelper.GetString(() => attachment.DisplayName),
                                SizeBytes = ComHelper.GetLong(() => attachment.Size, 0)
                            };
                        }

                        var desiredPath = OutputPathBuilder.BuildFullPath(
                            options,
                            exportItem.Message,
                            info);

                        var finalPath = CollisionResolver.Resolve(
                            desiredPath,
                            options.CollisionPolicy,
                            reserved);

                        if (finalPath is null)
                        {
                            result.Skipped++;
                            AddManifestEntry(
                                result,
                                exportItem.Message,
                                info,
                                "رد شد",
                                string.Empty,
                                string.Empty);
                            processed++;
                            ReportProgress(info.EffectiveName);
                            continue;
                        }

                        var directory = Path.GetDirectoryName(finalPath) ?? destinationRoot;
                        LongPath.CreateDirectory(directory);

                        var tempDirectory = Path.Combine(Path.GetTempPath(), "AttachmentExtractorTemp");
                        Directory.CreateDirectory(tempDirectory);
                        var tempPath = Path.Combine(
                            tempDirectory,
                            Guid.NewGuid().ToString("N") + Path.GetExtension(finalPath));

                        try
                        {
                            attachment.SaveAsFile(tempPath);

                            LongPath.Move(
                                tempPath,
                                finalPath,
                                options.CollisionPolicy == CollisionPolicy.Overwrite);

                            if (options.PreserveTimestamps)
                            {
                                var timestamp = exportItem.Message.ReceivedTime
                                                ?? exportItem.Message.SentOn
                                                ?? DateTime.Now;
                                try
                                {
                                    LongPath.SetLastWriteTime(finalPath, timestamp);
                                }
                                catch
                                {
                                    // Preserving timestamps is best effort.
                                }
                            }

                            reserved.Add(finalPath);
                            result.Saved++;
                            result.BytesSaved += info.SizeBytes;
                            AddManifestEntry(
                                result,
                                exportItem.Message,
                                info,
                                "ذخیره شد",
                                finalPath,
                                string.Empty);
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                if (File.Exists(tempPath))
                                {
                                    File.Delete(tempPath);
                                }
                            }
                            catch
                            {
                                // Ignore temp cleanup errors.
                            }

                            result.Failed++;
                            AddManifestEntry(
                                result,
                                exportItem.Message,
                                info,
                                "خطا",
                                string.Empty,
                                ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        AddManifestEntry(
                            result,
                            exportItem.Message,
                            info,
                            "خطا",
                            string.Empty,
                            ex.Message);
                    }
                    finally
                    {
                        ComHelper.Release(attachment);
                    }

                    processed++;
                    ReportProgress(info?.EffectiveName ?? $"Attachment {index}");
                }
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
                break;
            }
            catch (Exception ex)
            {
                foreach (var index in exportItem.AttachmentIndexes)
                {
                    var info = exportItem.Message.Attachments.FirstOrDefault(
                        attachment => attachment.Index == index);
                    result.Failed++;
                    AddManifestEntry(
                        result,
                        exportItem.Message,
                        info,
                        "خطا",
                        string.Empty,
                        ex.Message);
                    processed++;
                }

                ReportProgress(exportItem.Message.Subject);
            }
            finally
            {
                ComHelper.Release(attachments);
                ComHelper.Release(mail);
            }
        }

        result.Canceled |= cancellationToken.IsCancellationRequested;
        result.FinishedAt = DateTime.Now;

        if (options.CreateManifest)
        {
            try
            {
                var manifest = ManifestWriter.Write(destinationRoot, options, result);
                result.ManifestCsvPath = manifest.CsvPath;
                result.ManifestJsonPath = manifest.JsonPath;
            }
            catch (Exception ex)
            {
                result.Entries.Add(new ExportManifestEntry
                {
                    Status = "خطای گزارش",
                    Error = ex.Message
                });
            }
        }

        progress?.Report(new ExportProgress
        {
            TotalAttachments = total,
            ProcessedAttachments = processed,
            SavedAttachments = result.Saved,
            SkippedAttachments = result.Skipped,
            FailedAttachments = result.Failed,
            BytesSaved = result.BytesSaved,
            CurrentFile = result.Canceled ? "لغو شد" : "پایان خروجی"
        });

        return result;
    }

    private dynamic GetMailItem(MailMessageInfo message)
    {
        if (string.IsNullOrWhiteSpace(message.EntryId))
        {
            throw new InvalidOperationException("شناسه ایمیل خالی است.");
        }

        try
        {
            return !string.IsNullOrWhiteSpace(message.StoreId)
                ? _namespace.GetItemFromID(message.EntryId, message.StoreId)
                : _namespace.GetItemFromID(message.EntryId, Type.Missing);
        }
        catch
        {
            return _namespace.GetItemFromID(message.EntryId);
        }
    }

    private static dynamic FindAttachment(
        dynamic attachments,
        int index,
        string expectedName)
    {
        var count = ComHelper.GetInt(() => attachments.Count, 0);

        if (index >= 1 && index <= count)
        {
            dynamic candidate = attachments[index];
            try
            {
                var name = ComHelper.GetString(() => candidate.FileName);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = ComHelper.GetString(() => candidate.DisplayName);
                }

                if (string.IsNullOrWhiteSpace(expectedName) ||
                    string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            catch
            {
                // Fall through to name-based search.
            }

            ComHelper.Release(candidate);
        }

        for (var candidateIndex = 1; candidateIndex <= count; candidateIndex++)
        {
            dynamic candidate = attachments[candidateIndex];
            try
            {
                var name = ComHelper.GetString(() => candidate.FileName);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = ComHelper.GetString(() => candidate.DisplayName);
                }

                if (!string.IsNullOrWhiteSpace(expectedName) &&
                    string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore unreadable attachments.
            }

            ComHelper.Release(candidate);
        }

        return null!;
    }

    private static void AddManifestEntry(
        ExportResult result,
        MailMessageInfo message,
        AttachmentInfo? attachment,
        string status,
        string savedPath,
        string error)
    {
        result.Entries.Add(new ExportManifestEntry
        {
            Status = status,
            ReceivedTime = message.ReceivedTime,
            SenderName = message.SenderName,
            SenderEmail = message.SenderEmail,
            Subject = message.Subject,
            FolderPath = message.FolderPath,
            AttachmentName = attachment?.EffectiveName ?? string.Empty,
            SizeBytes = attachment?.SizeBytes ?? 0,
            SavedPath = savedPath,
            Error = error
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _worker.RunAsync(() =>
            {
                ComHelper.Release(_namespace);
                ComHelper.Release(_application);
                _namespace = null!;
                _application = null!;
                _connected = false;
            }).Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // The worker may already be shutting down.
        }

        _worker.Dispose();
    }
}
