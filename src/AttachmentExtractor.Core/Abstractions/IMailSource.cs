using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Abstractions;

public interface IMailSource : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FolderInfo>> GetFoldersAsync(CancellationToken cancellationToken = default);

    Task<ScanResult> ScanAsync(
        ScanRequest request,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken = default);

    Task<ExportResult> ExportAsync(
        ExportOptions options,
        IReadOnlyList<ExportItem> items,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken = default);
}
