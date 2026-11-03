using System.Collections.ObjectModel;
using System.IO;
using AttachmentExtractor.App.Collections;
using AttachmentExtractor.App.Services;
using AttachmentExtractor.Core.Abstractions;
using AttachmentExtractor.Core.Filters;
using AttachmentExtractor.Core.Models;
using AttachmentExtractor.Core.Services;
using AttachmentExtractor.Core.Utilities;

namespace AttachmentExtractor.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IMailSource _source;
    private readonly SettingsStore _settingsStore;
    private readonly ProfileStore _profileStore;
    private readonly IDialogService _dialogs;
    private readonly AppSettings _settings;
    private readonly List<FilterProfile> _allProfiles = new();

    private CancellationTokenSource? _cts;
    private FilterNode _advancedRoot = FilterNode.NewGroup();
    private bool _disposed;
    private bool _suppressFolderUpdates;
    private bool _suppressMessageUpdates;

    private bool _isConnected;
    private bool _isBusy;
    private bool _isProgressIndeterminate;
    private bool _isFilterPanelExpanded = true;
    private bool _isExportSettingsExpanded;
    private double _progressValue;
    private double _progressMaximum = 1;
    private string _busyText = string.Empty;
    private string _statusMessage = "در حال آماده‌سازی…";
    private string _lastScanSummary = "هنوز اسکنی انجام نشده است.";
    private string _destinationRoot = string.Empty;
    private string _customFolderTemplate = "{SenderEmail}\\{JalaliDate:yyyy-MM}";
    private string _fileNameTemplate = "{OriginalName}";
    private bool _createManifest = true;
    private bool _preserveTimestamps = true;
    private bool _openFolderAfterExport = true;

    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;
    private string _sender = string.Empty;
    private string _senderDomain = string.Empty;
    private string _recipient = string.Empty;
    private string _subject = string.Empty;
    private string _body = string.Empty;
    private string _extension = string.Empty;
    private string _minSizeMb = string.Empty;
    private string _maxSizeMb = string.Empty;
    private string _minAttachmentCount = string.Empty;
    private bool _onlyWithAttachments = true;
    private bool _unreadOnly;
    private bool _includeBody;
    private string _importance = string.Empty;
    private string _category = string.Empty;

    private StructureOption? _selectedStructure;
    private CollisionOption? _selectedCollision;
    private FilterProfile? _selectedProfile;

    private FolderNodeViewModel? _selectedFolder;
    private MessageRowViewModel? _selectedMessage;
    private int _selectedFolderCount;
    private int _selectedMessageCount;
    private int _selectedAttachmentCount;
    private long _selectedBytes;

    public MainViewModel(
        IMailSource source,
        SettingsStore settingsStore,
        ProfileStore profileStore,
        IDialogService dialogs)
    {
        _source = source;
        _settingsStore = settingsStore;
        _profileStore = profileStore;
        _dialogs = dialogs;
        _settings = LoadSettingsSafe();

        StructureOptions = new List<StructureOption>
        {
            new(OutputStructure.Flat, "مسطح (بدون پوشه)"),
            new(OutputStructure.BySender, "بر اساس فرستنده"),
            new(OutputStructure.ByDate, "بر اساس تاریخ (شمسی)"),
            new(OutputStructure.ByFolder, "بر اساس مسیر پوشه"),
            new(OutputStructure.Custom, "قالب سفارشی")
        };

        CollisionOptions = new List<CollisionOption>
        {
            new(CollisionPolicy.Rename, "تغییر نام خودکار"),
            new(CollisionPolicy.Skip, "رد کردن فایل تکراری"),
            new(CollisionPolicy.Overwrite, "بازنویسی فایل موجود")
        };

        ImportanceOptions = new List<ImportanceOption>
        {
            new(string.Empty, "همه"),
            new("2", "بالا"),
            new("1", "معمولی"),
            new("0", "کم")
        };

        _selectedStructure = StructureOptions.FirstOrDefault(
            option => option.Value == _settings.Structure) ?? StructureOptions[0];
        _selectedCollision = CollisionOptions.FirstOrDefault(
            option => option.Value == _settings.CollisionPolicy) ?? CollisionOptions[0];

        _destinationRoot = _settings.LastDestination ?? string.Empty;
        _customFolderTemplate = string.IsNullOrWhiteSpace(_settings.CustomFolderTemplate)
            ? "{SenderEmail}\\{JalaliDate:yyyy-MM}"
            : _settings.CustomFolderTemplate;
        _fileNameTemplate = string.IsNullOrWhiteSpace(_settings.FileNameTemplate)
            ? "{OriginalName}"
            : _settings.FileNameTemplate;
        _createManifest = _settings.CreateManifest;
        _preserveTimestamps = _settings.PreserveTimestamps;
        _openFolderAfterExport = _settings.OpenFolderAfterExport;
        _onlyWithAttachments = _settings.OnlyWithAttachments;

        CreateCommands();
        Log(LogLevel.Info, "برنامه آماده است. در حال اتصال به Outlook…");
    }

    public RangeObservableCollection<FolderNodeViewModel> Folders { get; } = new();

    public RangeObservableCollection<MessageRowViewModel> Messages { get; } = new();

    public ObservableCollection<LogEntryViewModel> Logs { get; } = new();

    public ObservableCollection<FilterProfile> Profiles { get; } = new();

    public IReadOnlyList<StructureOption> StructureOptions { get; }

    public IReadOnlyList<CollisionOption> CollisionOptions { get; }

    public IReadOnlyList<ImportanceOption> ImportanceOptions { get; }

    public AsyncRelayCommand RefreshFoldersCommand { get; private set; } = null!;

    public AsyncRelayCommand ScanCommand { get; private set; } = null!;

    public RelayCommand CancelCommand { get; private set; } = null!;

    public AsyncRelayCommand ExportCommand { get; private set; } = null!;

    public RelayCommand BrowseDestinationCommand { get; private set; } = null!;

    public RelayCommand OpenDestinationCommand { get; private set; } = null!;

    public RelayCommand SelectAllFoldersCommand { get; private set; } = null!;

    public RelayCommand ClearFoldersSelectionCommand { get; private set; } = null!;

    public RelayCommand SelectInboxCommand { get; private set; } = null!;

    public RelayCommand SelectAllMessagesCommand { get; private set; } = null!;

    public RelayCommand ClearMessagesSelectionCommand { get; private set; } = null!;

    public RelayCommand SelectAllAttachmentsCommand { get; private set; } = null!;

    public RelayCommand ClearAttachmentsSelectionCommand { get; private set; } = null!;

    public RelayCommand ClearQuickFiltersCommand { get; private set; } = null!;

    public RelayCommand SetLast7DaysCommand { get; private set; } = null!;

    public RelayCommand SetLast30DaysCommand { get; private set; } = null!;

    public RelayCommand SetCurrentYearCommand { get; private set; } = null!;

    public RelayCommand SetPreviousYearCommand { get; private set; } = null!;

    public RelayCommand EditAdvancedFilterCommand { get; private set; } = null!;

    public RelayCommand ClearAdvancedFilterCommand { get; private set; } = null!;

    public RelayCommand ToggleFilterPanelCommand { get; private set; } = null!;

    public RelayCommand ToggleExportSettingsCommand { get; private set; } = null!;

    public RelayCommand SaveProfileCommand { get; private set; } = null!;

    public RelayCommand DeleteProfileCommand { get; private set; } = null!;

    public RelayCommand ImportProfileCommand { get; private set; } = null!;

    public RelayCommand ExportProfileCommand { get; private set; } = null!;

    public RelayCommand ClearLogsCommand { get; private set; } = null!;

    public string Version => "نسخه ۱٫۱";

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        private set => SetProperty(ref _progressMaximum, Math.Max(1, value));
    }

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    public bool IsFilterPanelExpanded
    {
        get => _isFilterPanelExpanded;
        set
        {
            if (SetProperty(ref _isFilterPanelExpanded, value))
            {
                OnPropertyChanged(nameof(FilterPanelToggleLabel));
            }
        }
    }

    public string FilterPanelToggleLabel => IsFilterPanelExpanded ? "پنهان کردن فیلترها" : "نمایش فیلترها";

    public bool IsExportSettingsExpanded
    {
        get => _isExportSettingsExpanded;
        set
        {
            if (SetProperty(ref _isExportSettingsExpanded, value))
            {
                OnPropertyChanged(nameof(ExportSettingsToggleLabel));
            }
        }
    }

    public string ExportSettingsToggleLabel => IsExportSettingsExpanded ? "بستن تنظیمات" : "تنظیمات بیشتر";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LastScanSummary
    {
        get => _lastScanSummary;
        private set => SetProperty(ref _lastScanSummary, value);
    }

    public string DestinationRoot
    {
        get => _destinationRoot;
        set
        {
            if (SetProperty(ref _destinationRoot, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanOpenDestination));
                RefreshCommandStates();
            }
        }
    }

    public string CustomFolderTemplate
    {
        get => _customFolderTemplate;
        set => SetProperty(ref _customFolderTemplate, value ?? string.Empty);
    }

    public string FileNameTemplate
    {
        get => _fileNameTemplate;
        set => SetProperty(ref _fileNameTemplate, value ?? string.Empty);
    }

    public StructureOption? SelectedStructure
    {
        get => _selectedStructure;
        set
        {
            if (SetProperty(ref _selectedStructure, value))
            {
                OnPropertyChanged(nameof(IsCustomStructure));
            }
        }
    }

    public CollisionOption? SelectedCollision
    {
        get => _selectedCollision;
        set => SetProperty(ref _selectedCollision, value);
    }

    public bool CreateManifest
    {
        get => _createManifest;
        set => SetProperty(ref _createManifest, value);
    }

    public bool PreserveTimestamps
    {
        get => _preserveTimestamps;
        set => SetProperty(ref _preserveTimestamps, value);
    }

    public bool OpenFolderAfterExport
    {
        get => _openFolderAfterExport;
        set => SetProperty(ref _openFolderAfterExport, value);
    }

    public bool IsCustomStructure => SelectedStructure?.Value == OutputStructure.Custom;

    public string DateFrom
    {
        get => _dateFrom;
        set => SetProperty(ref _dateFrom, value ?? string.Empty);
    }

    public string DateTo
    {
        get => _dateTo;
        set => SetProperty(ref _dateTo, value ?? string.Empty);
    }

    public string Sender
    {
        get => _sender;
        set => SetProperty(ref _sender, value ?? string.Empty);
    }

    public string SenderDomain
    {
        get => _senderDomain;
        set => SetProperty(ref _senderDomain, value ?? string.Empty);
    }

    public string Recipient
    {
        get => _recipient;
        set => SetProperty(ref _recipient, value ?? string.Empty);
    }

    public string Subject
    {
        get => _subject;
        set => SetProperty(ref _subject, value ?? string.Empty);
    }

    public string Body
    {
        get => _body;
        set => SetProperty(ref _body, value ?? string.Empty);
    }

    public string Extension
    {
        get => _extension;
        set => SetProperty(ref _extension, value ?? string.Empty);
    }

    public string MinSizeMb
    {
        get => _minSizeMb;
        set => SetProperty(ref _minSizeMb, value ?? string.Empty);
    }

    public string MaxSizeMb
    {
        get => _maxSizeMb;
        set => SetProperty(ref _maxSizeMb, value ?? string.Empty);
    }

    public string MinAttachmentCount
    {
        get => _minAttachmentCount;
        set => SetProperty(ref _minAttachmentCount, value ?? string.Empty);
    }

    public bool OnlyWithAttachments
    {
        get => _onlyWithAttachments;
        set => SetProperty(ref _onlyWithAttachments, value);
    }

    public bool UnreadOnly
    {
        get => _unreadOnly;
        set => SetProperty(ref _unreadOnly, value);
    }

    public bool IncludeBody
    {
        get => _includeBody;
        set => SetProperty(ref _includeBody, value);
    }

    public string Importance
    {
        get => _importance;
        set => SetProperty(ref _importance, value ?? string.Empty);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value ?? string.Empty);
    }

    public FilterProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedProfile));
            RefreshCommandStates();

            if (value is not null)
            {
                ApplyProfile(value);
            }
        }
    }

    public bool HasSelectedProfile => SelectedProfile is not null;

    public FolderNodeViewModel? SelectedFolder
    {
        get => _selectedFolder;
        set => SetProperty(ref _selectedFolder, value);
    }

    public MessageRowViewModel? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            if (!SetProperty(ref _selectedMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedMessage));
            OnPropertyChanged(nameof(HasSelectedMessageAttachments));
            RefreshCommandStates();
        }
    }

    public bool HasSelectedMessage => SelectedMessage is not null;

    public bool HasSelectedMessageAttachments => SelectedMessage?.HasAttachments == true;

    public bool HasMessages => Messages.Count > 0;

    public int SelectedFolderCount
    {
        get => _selectedFolderCount;
        private set
        {
            if (SetProperty(ref _selectedFolderCount, value))
            {
                OnPropertyChanged(nameof(FolderSelectionSummary));
            }
        }
    }

    public string FolderSelectionSummary => SelectedFolderCount == 0
        ? "پوشه‌ای انتخاب نشده"
        : $"{Formatters.FormatCount(SelectedFolderCount)} پوشه انتخاب شده";

    public int SelectedMessageCount
    {
        get => _selectedMessageCount;
        private set
        {
            if (SetProperty(ref _selectedMessageCount, value))
            {
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }
    }

    public int SelectedAttachmentCount
    {
        get => _selectedAttachmentCount;
        private set
        {
            if (SetProperty(ref _selectedAttachmentCount, value))
            {
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }
    }

    public long SelectedBytes
    {
        get => _selectedBytes;
        private set
        {
            if (SetProperty(ref _selectedBytes, value))
            {
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }
    }

    public string SelectionSummary => SelectedMessageCount == 0
        ? "هیچ ایمیلی برای خروجی انتخاب نشده است"
        : $"{Formatters.FormatCount(SelectedMessageCount)} ایمیل و " +
          $"{Formatters.FormatCount(SelectedAttachmentCount)} پیوست انتخاب شده " +
          $"({Formatters.FormatSize(SelectedBytes)})";

    public string ResultSummary
    {
        get
        {
            if (Messages.Count == 0)
            {
                return "نتیجه‌ای برای نمایش نیست";
            }

            var attachmentCount = Messages.Sum(message => message.AttachmentCount);
            var bytes = Messages.Sum(
                message => message.Model.Attachments.Sum(attachment => attachment.SizeBytes));

            return $"{Formatters.FormatCount(Messages.Count)} ایمیل • " +
                   $"{Formatters.FormatCount(attachmentCount)} پیوست • " +
                   Formatters.FormatSize(bytes);
        }
    }

    public string AdvancedFilterSummary
    {
        get
        {
            var (groups, conditions) = FilterAnalyzer.Count(_advancedRoot);
            return conditions == 0
                ? "فیلتر پیشرفته فعال نیست"
                : $"فیلتر پیشرفته: {Formatters.FormatCount(conditions)} شرط در " +
                  $"{Formatters.FormatCount(groups)} گروه";
        }
    }

    public bool CanOpenDestination => !string.IsNullOrWhiteSpace(DestinationRoot);

    private bool CanRefresh => !IsBusy;

    private bool CanScan => !IsBusy && SelectedFolderCount > 0;

    private bool CanCancel => IsBusy;

    private bool CanExport => !IsBusy && SelectedAttachmentCount > 0;

    private bool CanBulkEdit => !IsBusy;

    private bool CanSelectAttachments => !IsBusy && HasSelectedMessageAttachments;

    private bool CanSaveProfile => !IsBusy;

    private bool CanUseProfile => !IsBusy && HasSelectedProfile;

    public async Task InitializeAsync()
    {
        LoadProfiles();
        await LoadFoldersAsync();
    }

    public void ReportUnexpectedError(Exception exception)
    {
        Log(LogLevel.Error, "خطای پیش‌بینی‌نشده: " + exception.Message);
        StatusMessage = "خطای پیش‌بینی‌نشده رخ داد.";
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
            _cts?.Cancel();
        }
        catch
        {
            // Ignore cancellation failures during shutdown.
        }

        SaveSettings();

        try
        {
            _source.Dispose();
        }
        catch
        {
            // The COM worker may already be gone.
        }
    }

    private void CreateCommands()
    {
        RefreshFoldersCommand = new AsyncRelayCommand(LoadFoldersAsync, () => CanRefresh);
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => CanScan);
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => CanExport);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination, () => !IsBusy);
        OpenDestinationCommand = new RelayCommand(
            () => _dialogs.OpenFolder(DestinationRoot),
            () => !IsBusy && CanOpenDestination);

        SelectAllFoldersCommand = new RelayCommand(() => SetAllFolders(true), () => CanBulkEdit);
        ClearFoldersSelectionCommand = new RelayCommand(() => SetAllFolders(false), () => CanBulkEdit);
        SelectInboxCommand = new RelayCommand(SelectInbox, () => CanBulkEdit);

        SelectAllMessagesCommand = new RelayCommand(() => SetAllMessages(true), () => CanBulkEdit && HasMessages);
        ClearMessagesSelectionCommand = new RelayCommand(() => SetAllMessages(false), () => CanBulkEdit && HasMessages);
        SelectAllAttachmentsCommand = new RelayCommand(
            () => SelectedMessage?.SetAllAttachments(true),
            () => CanSelectAttachments);
        ClearAttachmentsSelectionCommand = new RelayCommand(
            () => SelectedMessage?.SetAllAttachments(false),
            () => CanSelectAttachments);

        ClearQuickFiltersCommand = new RelayCommand(ClearQuickFilters, () => CanBulkEdit);
        SetLast7DaysCommand = new RelayCommand(() => ApplyDateRange(JalaliDate.LastDays(7)), () => CanBulkEdit);
        SetLast30DaysCommand = new RelayCommand(() => ApplyDateRange(JalaliDate.LastDays(30)), () => CanBulkEdit);
        SetCurrentYearCommand = new RelayCommand(() => ApplyDateRange(JalaliDate.CurrentYear()), () => CanBulkEdit);
        SetPreviousYearCommand = new RelayCommand(() => ApplyDateRange(JalaliDate.PreviousYear()), () => CanBulkEdit);

        EditAdvancedFilterCommand = new RelayCommand(EditAdvancedFilter, () => CanBulkEdit);
        ClearAdvancedFilterCommand = new RelayCommand(ClearAdvancedFilter, () => CanBulkEdit);
        ToggleFilterPanelCommand = new RelayCommand(
            () => IsFilterPanelExpanded = !IsFilterPanelExpanded,
            () => true);
        ToggleExportSettingsCommand = new RelayCommand(
            () => IsExportSettingsExpanded = !IsExportSettingsExpanded,
            () => true);

        SaveProfileCommand = new RelayCommand(SaveProfile, () => CanSaveProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => CanUseProfile);
        ImportProfileCommand = new RelayCommand(ImportProfile, () => CanSaveProfile);
        ExportProfileCommand = new RelayCommand(ExportProfile, () => CanUseProfile);

        ClearLogsCommand = new RelayCommand(() => Logs.Clear(), () => true);
    }

    private async Task LoadFoldersAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        BeginBusy("در حال اتصال به Outlook و دریافت پوشه‌ها…", indeterminate: true);
        IsConnected = false;

        try
        {
            await _source.ConnectAsync(_cts.Token);
            var folderInfos = await _source.GetFoldersAsync(_cts.Token);

            BuildFolderTree(folderInfos);
            IsConnected = true;

            var totalFolders = FlattenFolders().Count();
            Log(
                LogLevel.Success,
                $"اتصال به Outlook برقرار شد؛ {Formatters.FormatCount(totalFolders)} پوشه دریافت شد.");

            SelectDefaultFolder();
            StatusMessage = "پوشه‌های Outlook بارگذاری شدند.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "دریافت پوشه‌ها لغو شد.";
            Log(LogLevel.Warning, "دریافت پوشه‌ها لغو شد.");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = "اتصال به Outlook برقرار نشد.";
            Log(LogLevel.Error, "اتصال به Outlook ناموفق بود: " + ex.Message);

            _dialogs.ShowError(
                "اتصال به Outlook برقرار نشد.\n\n" + ex.Message +
                "\n\nراهنمای بررسی:\n" +
                "• Outlook کلاسیک نصب و یک‌بار اجرا شده باشد.\n" +
                "• پروفایل Outlook باز و آماده باشد.\n" +
                "• دسترسی به مدل شیء Outlook محدود نشده باشد.",
                "خطای اتصال به Outlook");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            EndBusy();
        }
    }

    private void BuildFolderTree(IReadOnlyList<FolderInfo> folderInfos)
    {
        _suppressFolderUpdates = true;
        Folders.ReplaceAll(folderInfos.Select(info => CreateFolderNode(info, null)));
        _suppressFolderUpdates = false;
        UpdateFolderSelectionSummary();
    }

    private FolderNodeViewModel CreateFolderNode(FolderInfo info, FolderNodeViewModel? parent)
    {
        var node = new FolderNodeViewModel(info, parent);
        node.SelectionChanged += OnFolderSelectionChanged;

        foreach (var child in info.Children)
        {
            node.Children.Add(CreateFolderNode(child, node));
        }

        return node;
    }

    private void SelectDefaultFolder()
    {
        var allFolders = FlattenFolders().ToList();
        var inbox = allFolders.FirstOrDefault(
                        folder => folder.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase))
                    ?? allFolders.FirstOrDefault(
                        folder => folder.Name.Contains("صندوق ورودی", StringComparison.CurrentCultureIgnoreCase));

        if (inbox is null)
        {
            Log(LogLevel.Warning, "پوشه Inbox پیدا نشد؛ پوشه‌های موردنظر را دستی انتخاب کنید.");
            return;
        }

        inbox.SetSelected(true);
        ExpandAncestors(inbox);
        UpdateFolderSelectionSummary();
        Log(LogLevel.Info, $"پوشه «{inbox.Name}» به‌صورت پیش‌فرض انتخاب شد.");
    }

    private IEnumerable<FolderNodeViewModel> FlattenFolders()
    {
        return Walk(Folders);
    }

    private static IEnumerable<FolderNodeViewModel> Walk(IEnumerable<FolderNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in Walk(node.Children))
            {
                yield return child;
            }
        }
    }

    private static void ExpandAncestors(FolderNodeViewModel node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            current.IsExpanded = true;
        }
    }

    private void OnFolderSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressFolderUpdates)
        {
            return;
        }

        UpdateFolderSelectionSummary();
    }

    private void UpdateFolderSelectionSummary()
    {
        SelectedFolderCount = FlattenFolders().Count(folder => folder.IsSelected == true);
        RefreshCommandStates();
    }

    private void SetAllFolders(bool selected)
    {
        _suppressFolderUpdates = true;
        foreach (var folder in Folders)
        {
            folder.SetSelected(selected);
        }

        _suppressFolderUpdates = false;
        UpdateFolderSelectionSummary();
    }

    private void SelectInbox()
    {
        var allFolders = FlattenFolders().ToList();
        var inbox = allFolders.FirstOrDefault(
                        folder => folder.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase))
                    ?? allFolders.FirstOrDefault(
                        folder => folder.Name.Contains("صندوق ورودی", StringComparison.CurrentCultureIgnoreCase));

        if (inbox is null)
        {
            _dialogs.ShowWarning("پوشه‌ای با نام Inbox پیدا نشد.", "پوشه پیدا نشد");
            return;
        }

        SetAllFolders(false);
        inbox.SetSelected(true);
        ExpandAncestors(inbox);
        UpdateFolderSelectionSummary();
        Log(LogLevel.Info, $"پوشه «{inbox.Name}» انتخاب شد.");
    }

    private List<FolderInfo> GetSelectedFolders()
    {
        return FlattenFolders()
            .Where(folder => folder.IsSelected == true)
            .Select(folder => folder.Model)
            .ToList();
    }

    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var folders = GetSelectedFolders();
        if (folders.Count == 0)
        {
            _dialogs.ShowWarning(
                "ابتدا حداقل یک پوشه Outlook را از ستون کنار انتخاب کنید.",
                "پوشه‌ای انتخاب نشده");
            return;
        }

        var filterRoot = BuildScanFilter();
        if (!FilterValidator.Validate(filterRoot, out var error, out _))
        {
            Log(LogLevel.Error, "فیلتر نامعتبر است: " + error);
            _dialogs.ShowError(error, "فیلتر نامعتبر");
            return;
        }

        if (FilterAnalyzer.RequiresBody(filterRoot) && !IncludeBody)
        {
            IncludeBody = true;
            Log(LogLevel.Info, "برای جست‌وجو در متن ایمیل، دریافت متن به‌صورت خودکار فعال شد.");
        }

        var request = new ScanRequest
        {
            Folders = folders,
            Filter = filterRoot,
            IncludeBody = IncludeBody
        };

        _cts = new CancellationTokenSource();
        BeginBusy("در حال اسکن ایمیل‌ها…", indeterminate: true);

        try
        {
            ClearMessages();

            var progress = new Progress<ScanProgress>(OnScanProgress);
            var result = await _source.ScanAsync(request, progress, _cts.Token);

            PopulateMessages(result, IncludeBody);
            LastScanSummary = BuildScanSummary(result);
            StatusMessage = result.Canceled
                ? "اسکن لغو شد."
                : $"اسکن پایان یافت؛ {Formatters.FormatCount(result.Messages.Count)} ایمیل مطابق فیلتر.";

            Log(result.Canceled ? LogLevel.Warning : LogLevel.Success, LastScanSummary);

            if (result.Errors.Count > 0)
            {
                Log(
                    LogLevel.Warning,
                    $"{Formatters.FormatCount(result.Errors.Count)} خطا در هنگام اسکن رخ داد.");
                LogErrors(result.Errors);
            }

            if (result.Messages.Count == 0 && !result.Canceled)
            {
                Log(
                    LogLevel.Info,
                    "هیچ ایمیلی با فیلترهای فعلی پیدا نشد؛ فیلترها یا پوشه‌های انتخاب‌شده را تغییر دهید.");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "اسکن لغو شد.";
            Log(LogLevel.Warning, "اسکن به درخواست کاربر لغو شد.");
        }
        catch (Exception ex)
        {
            StatusMessage = "اسکن ناموفق بود.";
            Log(LogLevel.Error, "اسکن ناموفق بود: " + ex.Message);
            _dialogs.ShowError("اسکن ایمیل‌ها ناموفق بود.\n\n" + ex.Message, "خطای اسکن");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            EndBusy();
            SaveSettings();
        }
    }

    private FilterNode BuildScanFilter()
    {
        var root = FilterNode.NewGroup(GroupOperator.And);
        root.Children.AddRange(QuickFilterBuilder.Build(BuildQuickFilterSettings()));

        if (_advancedRoot.Children.Count > 0)
        {
            root.Children.Add(_advancedRoot.Clone());
        }

        return root;
    }

    private QuickFilterSettings BuildQuickFilterSettings()
    {
        return new QuickFilterSettings
        {
            DateFrom = DateFrom.Trim(),
            DateTo = DateTo.Trim(),
            Sender = Sender.Trim(),
            SenderDomain = SenderDomain.Trim(),
            Recipient = Recipient.Trim(),
            Subject = Subject.Trim(),
            Body = Body.Trim(),
            Extension = Extension.Trim(),
            MinSizeMb = MinSizeMb.Trim(),
            MaxSizeMb = MaxSizeMb.Trim(),
            MinAttachmentCount = MinAttachmentCount.Trim(),
            OnlyWithAttachments = OnlyWithAttachments,
            UnreadOnly = UnreadOnly,
            Importance = Importance,
            Category = Category.Trim()
        };
    }

    private void PopulateMessages(ScanResult result, bool includeBody)
    {
        var rows = result.Messages
            .Select(message => CreateMessageRow(message, includeBody))
            .ToList();

        Messages.ReplaceAll(rows);
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(ResultSummary));

        SelectedMessage = rows.FirstOrDefault();
        UpdateSelectionSummary();
    }

    private MessageRowViewModel CreateMessageRow(MailMessageInfo message, bool includeBody)
    {
        var row = new MessageRowViewModel(message, includeBody);
        row.SelectionChanged += OnMessageSelectionChanged;
        row.AttachmentSelectionChanged += OnMessageAttachmentSelectionChanged;
        return row;
    }

    private void ClearMessages()
    {
        foreach (var row in Messages)
        {
            row.SelectionChanged -= OnMessageSelectionChanged;
            row.AttachmentSelectionChanged -= OnMessageAttachmentSelectionChanged;
        }

        Messages.ReplaceAll(Array.Empty<MessageRowViewModel>());
        SelectedMessage = null;
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(ResultSummary));
        UpdateSelectionSummary();
    }

    private void SetAllMessages(bool selected)
    {
        _suppressMessageUpdates = true;
        foreach (var message in Messages)
        {
            message.IsSelected = selected;
        }

        _suppressMessageUpdates = false;
        UpdateSelectionSummary();
    }

    private void OnMessageSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressMessageUpdates)
        {
            return;
        }

        UpdateSelectionSummary();
    }

    private void OnMessageAttachmentSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressMessageUpdates)
        {
            return;
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var selectedMessages = Messages.Where(message => message.IsSelected).ToList();

        SelectedMessageCount = selectedMessages.Count;
        SelectedAttachmentCount = selectedMessages.Sum(message => message.SelectedAttachmentCount);
        SelectedBytes = selectedMessages.Sum(message => message.SelectedBytes);
        RefreshCommandStates();
    }

    private void OnScanProgress(ScanProgress progress)
    {
        if (progress.TotalItems > 0)
        {
            IsProgressIndeterminate = false;
            ProgressMaximum = progress.TotalItems;
            ProgressValue = progress.ScannedItems;
        }
        else
        {
            IsProgressIndeterminate = true;
        }

        var folder = string.IsNullOrWhiteSpace(progress.CurrentFolder)
            ? "در حال اسکن…"
            : $"اسکن {progress.CurrentFolder}";

        BusyText = progress.TotalItems > 0
            ? $"{folder} — {Formatters.FormatCount(progress.ScannedItems)} از " +
              $"{Formatters.FormatCount(progress.TotalItems)}"
            : folder;

        StatusMessage = BusyText;
    }

    private static string BuildScanSummary(ScanResult result)
    {
        var prefix = result.Canceled ? "اسکن لغو شد — " : "آخرین اسکن: ";
        return prefix +
               $"{Formatters.FormatCount(result.Messages.Count)} ایمیل مطابق فیلتر • " +
               $"{Formatters.FormatCount(result.ScannedItems)} مورد بررسی‌شده • " +
               $"{Formatters.FormatCount(result.FolderCount)} پوشه";
    }

    private async Task ExportAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(DestinationRoot))
        {
            _dialogs.ShowWarning("ابتدا مسیر مقصد را انتخاب کنید.", "مسیر مقصد خالی است");
            return;
        }

        var exportItems = Messages
            .Where(message => message.IsSelected)
            .Select(message => new ExportItem
            {
                Message = message.Model,
                AttachmentIndexes = message.SelectedAttachmentIndexes.ToList()
            })
            .Where(item => item.AttachmentIndexes.Count > 0)
            .ToList();

        if (exportItems.Count == 0)
        {
            _dialogs.ShowWarning(
                "هیچ ایمیل یا پیوستی برای ذخیره انتخاب نشده است. ستون انتخاب ایمیل‌ها و پیوست‌ها را بررسی کنید.",
                "چیزی برای ذخیره نیست");
            return;
        }

        try
        {
            LongPath.CreateDirectory(DestinationRoot);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("مسیر مقصد قابل استفاده نیست.\n\n" + ex.Message, "خطای مسیر مقصد");
            return;
        }

        var options = new ExportOptions
        {
            DestinationRoot = DestinationRoot.Trim(),
            Structure = SelectedStructure?.Value ?? OutputStructure.Flat,
            CustomFolderTemplate = CustomFolderTemplate,
            FileNameTemplate = FileNameTemplate,
            CollisionPolicy = SelectedCollision?.Value ?? CollisionPolicy.Rename,
            CreateManifest = CreateManifest,
            PreserveTimestamps = PreserveTimestamps
        };

        SaveSettings();

        _cts = new CancellationTokenSource();
        BeginBusy("در حال ذخیره پیوست‌ها…", indeterminate: true);

        try
        {
            var progress = new Progress<ExportProgress>(OnExportProgress);
            var result = await _source.ExportAsync(options, exportItems, progress, _cts.Token);

            StatusMessage = $"ذخیره‌سازی پایان یافت؛ {Formatters.FormatCount(result.Saved)} فایل ذخیره شد.";
            var summary = BuildExportSummary(result);

            if (result.Saved > 0)
            {
                Log(LogLevel.Success, summary.Replace("\n", " "));
                if (OpenFolderAfterExport)
                {
                    _dialogs.OpenFolder(options.DestinationRoot);
                }
            }
            else if (result.Failed > 0)
            {
                Log(LogLevel.Error, summary.Replace("\n", " "));
            }
            else
            {
                Log(LogLevel.Warning, summary.Replace("\n", " "));
            }

            LogErrors(result.Entries
                .Where(entry => entry.Status == "خطا")
                .Select(entry => $"{entry.AttachmentName}: {entry.Error}")
                .ToList());

            _dialogs.ShowInfo(summary, result.Canceled ? "خروجی لغو شد" : "نتیجه ذخیره‌سازی");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "ذخیره‌سازی لغو شد.";
            Log(LogLevel.Warning, "ذخیره‌سازی به درخواست کاربر لغو شد.");
        }
        catch (Exception ex)
        {
            StatusMessage = "ذخیره‌سازی ناموفق بود.";
            Log(LogLevel.Error, "ذخیره‌سازی ناموفق بود: " + ex.Message);
            _dialogs.ShowError("ذخیره پیوست‌ها ناموفق بود.\n\n" + ex.Message, "خطای ذخیره‌سازی");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            EndBusy();
            SaveSettings();
        }
    }

    private void OnExportProgress(ExportProgress progress)
    {
        if (progress.TotalAttachments > 0)
        {
            IsProgressIndeterminate = false;
            ProgressMaximum = progress.TotalAttachments;
            ProgressValue = progress.ProcessedAttachments;
        }
        else
        {
            IsProgressIndeterminate = true;
        }

        BusyText = string.IsNullOrWhiteSpace(progress.CurrentFile)
            ? "در حال ذخیره پیوست‌ها…"
            : $"ذخیره {progress.CurrentFile} — " +
              $"{Formatters.FormatCount(progress.ProcessedAttachments)} از " +
              $"{Formatters.FormatCount(progress.TotalAttachments)}";

        StatusMessage = BusyText;
    }

    private static string BuildExportSummary(ExportResult result)
    {
        var lines = new List<string>();

        if (result.Canceled)
        {
            lines.Add("عملیات به درخواست شما لغو شد.");
        }

        lines.Add("ذخیره‌سازی پیوست‌ها پایان یافت.");
        lines.Add($"ذخیره‌شده: {Formatters.FormatCount(result.Saved)}");
        lines.Add($"ردشده: {Formatters.FormatCount(result.Skipped)}");
        lines.Add($"ناموفق: {Formatters.FormatCount(result.Failed)}");
        lines.Add($"حجم ذخیره‌شده: {Formatters.FormatSize(result.BytesSaved)}");

        if (!string.IsNullOrWhiteSpace(result.ManifestCsvPath))
        {
            lines.Add($"گزارش CSV: {result.ManifestCsvPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.ManifestJsonPath))
        {
            lines.Add($"گزارش JSON: {result.ManifestJsonPath}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void BrowseDestination()
    {
        var selected = _dialogs.PickFolder("انتخاب پوشه مقصد", DestinationRoot);
        if (selected is null)
        {
            return;
        }

        DestinationRoot = selected;
        SaveSettings();
        Log(LogLevel.Info, "مسیر مقصد انتخاب شد: " + selected);
    }

    private void ApplyDateRange((string From, string To) range)
    {
        DateFrom = range.From;
        DateTo = range.To;
    }

    private void ClearQuickFilters()
    {
        DateFrom = string.Empty;
        DateTo = string.Empty;
        Sender = string.Empty;
        SenderDomain = string.Empty;
        Recipient = string.Empty;
        Subject = string.Empty;
        Body = string.Empty;
        Extension = string.Empty;
        MinSizeMb = string.Empty;
        MaxSizeMb = string.Empty;
        MinAttachmentCount = string.Empty;
        Importance = string.Empty;
        Category = string.Empty;
        OnlyWithAttachments = true;
        UnreadOnly = false;
        IncludeBody = false;

        _advancedRoot = FilterNode.NewGroup();
        OnPropertyChanged(nameof(AdvancedFilterSummary));
        SelectedProfile = null;

        Log(LogLevel.Info, "فیلترها پاک شدند.");
        RefreshCommandStates();
    }

    private void EditAdvancedFilter()
    {
        var result = _dialogs.EditRule(_advancedRoot);
        if (result is null)
        {
            return;
        }

        _advancedRoot = result;
        OnPropertyChanged(nameof(AdvancedFilterSummary));

        var (_, conditions) = FilterAnalyzer.Count(_advancedRoot);
        Log(
            LogLevel.Info,
            conditions == 0
                ? "فیلتر پیشرفته پاک شد."
                : $"فیلتر پیشرفته به‌روزرسانی شد ({Formatters.FormatCount(conditions)} شرط).");
    }

    private void ClearAdvancedFilter()
    {
        _advancedRoot = FilterNode.NewGroup();
        OnPropertyChanged(nameof(AdvancedFilterSummary));
        Log(LogLevel.Info, "فیلتر پیشرفته پاک شد.");
    }

    private void LoadProfiles()
    {
        try
        {
            var loaded = _profileStore.Load();
            _allProfiles.Clear();
            _allProfiles.AddRange(loaded);
            RefreshProfileCollection(null);

            if (!string.IsNullOrWhiteSpace(_settings.LastProfileName))
            {
                var lastProfile = _allProfiles.FirstOrDefault(
                    profile => profile.Name.Equals(
                        _settings.LastProfileName,
                        StringComparison.CurrentCultureIgnoreCase));

                if (lastProfile is not null)
                {
                    SelectedProfile = lastProfile;
                }
            }

            if (_allProfiles.Count > 0)
            {
                Log(
                    LogLevel.Info,
                    $"{Formatters.FormatCount(_allProfiles.Count)} پروفایل فیلتر بارگذاری شد.");
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "بارگذاری پروفایل‌ها ناموفق بود: " + ex.Message);
        }
    }

    private void RefreshProfileCollection(string? selectName)
    {
        Profiles.Clear();
        foreach (var profile in _allProfiles.OrderBy(
                     profile => profile.Name,
                     StringComparer.CurrentCulture))
        {
            Profiles.Add(profile);
        }

        var match = string.IsNullOrWhiteSpace(selectName)
            ? null
            : Profiles.FirstOrDefault(
                profile => profile.Name.Equals(
                    selectName,
                    StringComparison.CurrentCultureIgnoreCase));

        SelectedProfile = match;
        RefreshCommandStates();
    }

    private void SaveProfile()
    {
        var name = _dialogs.Prompt(
            "ذخیره پروفایل فیلتر",
            "یک نام برای پروفایل فیلتر وارد کنید:",
            SelectedProfile?.Name ?? "پروفایل من");

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        name = name.Trim();
        var existing = _allProfiles.FirstOrDefault(
            profile => profile.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

        if (existing is not null &&
            !_dialogs.Confirm(
                $"پروفایلی با نام «{name}» وجود دارد. جایگزین شود؟",
                "ذخیره پروفایل"))
        {
            return;
        }

        if (existing is not null)
        {
            _allProfiles.Remove(existing);
        }

        var profile = new FilterProfile
        {
            Name = name,
            Quick = BuildQuickFilterSettings(),
            AdvancedRoot = _advancedRoot.Clone(),
            UpdatedAt = DateTime.Now
        };

        _allProfiles.Add(profile);
        PersistProfiles();
        RefreshProfileCollection(name);
        _settings.LastProfileName = name;
        SaveSettings();
        Log(LogLevel.Success, $"پروفایل «{name}» ذخیره شد.");
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var name = SelectedProfile.Name;
        if (!_dialogs.Confirm($"پروفایل «{name}» حذف شود؟", "حذف پروفایل"))
        {
            return;
        }

        _allProfiles.Remove(SelectedProfile);
        PersistProfiles();
        RefreshProfileCollection(null);

        if (string.Equals(_settings.LastProfileName, name, StringComparison.CurrentCultureIgnoreCase))
        {
            _settings.LastProfileName = string.Empty;
        }

        SaveSettings();
        Log(LogLevel.Warning, $"پروفایل «{name}» حذف شد.");
    }

    private void ImportProfile()
    {
        var path = _dialogs.PickOpenFile(
            "ورود پروفایل فیلتر",
            "فایل پروفایل (*.json)|*.json|همه فایل‌ها (*.*)|*.*");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var imported = _profileStore.Import(path);
            var existing = _allProfiles.FirstOrDefault(
                profile => profile.Name.Equals(
                    imported.Name,
                    StringComparison.CurrentCultureIgnoreCase));

            if (existing is not null &&
                !_dialogs.Confirm(
                    $"پروفایلی با نام «{imported.Name}» وجود دارد. جایگزین شود؟",
                    "ورود پروفایل"))
            {
                return;
            }

            if (existing is not null)
            {
                _allProfiles.Remove(existing);
            }

            _allProfiles.Add(imported);
            PersistProfiles();
            RefreshProfileCollection(imported.Name);
            _settings.LastProfileName = imported.Name;
            SaveSettings();
            Log(LogLevel.Success, $"پروفایل «{imported.Name}» وارد شد.");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "ورود پروفایل ناموفق بود: " + ex.Message);
            _dialogs.ShowError("ورود پروفایل ناموفق بود.\n\n" + ex.Message, "خطا در ورود پروفایل");
        }
    }

    private void ExportProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var path = _dialogs.PickSaveFile(
            "ذخیره پروفایل فیلتر",
            "فایل پروفایل (*.json)|*.json",
            SelectedProfile.Name + ".json");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            _profileStore.Export(SelectedProfile, path);
            Log(LogLevel.Success, $"پروفایل «{SelectedProfile.Name}» ذخیره شد: {path}");
            _dialogs.ShowInfo("پروفایل با موفقیت ذخیره شد.", "ذخیره پروفایل");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "ذخیره پروفایل ناموفق بود: " + ex.Message);
            _dialogs.ShowError("ذخیره پروفایل ناموفق بود.\n\n" + ex.Message, "خطا در ذخیره پروفایل");
        }
    }

    private void ApplyProfile(FilterProfile profile)
    {
        var quick = profile.Quick ?? new QuickFilterSettings();

        DateFrom = quick.DateFrom ?? string.Empty;
        DateTo = quick.DateTo ?? string.Empty;
        Sender = quick.Sender ?? string.Empty;
        SenderDomain = quick.SenderDomain ?? string.Empty;
        Recipient = quick.Recipient ?? string.Empty;
        Subject = quick.Subject ?? string.Empty;
        Body = quick.Body ?? string.Empty;
        Extension = quick.Extension ?? string.Empty;
        MinSizeMb = quick.MinSizeMb ?? string.Empty;
        MaxSizeMb = quick.MaxSizeMb ?? string.Empty;
        MinAttachmentCount = quick.MinAttachmentCount ?? string.Empty;
        OnlyWithAttachments = quick.OnlyWithAttachments;
        UnreadOnly = quick.UnreadOnly;
        Importance = quick.Importance ?? string.Empty;
        Category = quick.Category ?? string.Empty;

        _advancedRoot = (profile.AdvancedRoot ?? FilterNode.NewGroup()).Clone();
        OnPropertyChanged(nameof(AdvancedFilterSummary));

        _settings.LastProfileName = profile.Name;
        Log(LogLevel.Info, $"پروفایل «{profile.Name}» بارگذاری شد.");
    }

    private void PersistProfiles()
    {
        try
        {
            _profileStore.SaveAll(_allProfiles);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "ذخیره پروفایل‌ها ناموفق بود: " + ex.Message);
            _dialogs.ShowError(
                "ذخیره فایل پروفایل‌ها ناموفق بود.\n\n" + ex.Message,
                "خطای ذخیره پروفایل‌ها");
        }
    }

    private void Cancel()
    {
        if (_cts is not { IsCancellationRequested: false })
        {
            return;
        }

        _cts.Cancel();
        StatusMessage = "در حال لغو عملیات…";
        Log(LogLevel.Warning, "درخواست لغو عملیات ثبت شد.");
    }

    private void BeginBusy(string text, bool indeterminate)
    {
        IsBusy = true;
        BusyText = text;
        StatusMessage = text;
        IsProgressIndeterminate = indeterminate;
        ProgressValue = 0;
        ProgressMaximum = 1;
        RefreshCommandStates();
    }

    private void EndBusy()
    {
        IsBusy = false;
        IsProgressIndeterminate = false;
        BusyText = string.Empty;
        RefreshCommandStates();
    }

    private AppSettings LoadSettingsSafe()
    {
        try
        {
            return _settingsStore.Load();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            _settings.LastDestination = DestinationRoot;
            _settings.Structure = SelectedStructure?.Value ?? OutputStructure.Flat;
            _settings.CustomFolderTemplate = CustomFolderTemplate;
            _settings.FileNameTemplate = FileNameTemplate;
            _settings.CollisionPolicy = SelectedCollision?.Value ?? CollisionPolicy.Rename;
            _settings.CreateManifest = CreateManifest;
            _settings.PreserveTimestamps = PreserveTimestamps;
            _settings.OpenFolderAfterExport = OpenFolderAfterExport;
            _settings.OnlyWithAttachments = OnlyWithAttachments;
            _settings.LastProfileName = SelectedProfile?.Name ?? _settings.LastProfileName;
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "ذخیره تنظیمات ناموفق بود: " + ex.Message);
        }
    }

    private void Log(LogLevel level, string message)
    {
        Logs.Add(new LogEntryViewModel(level, message));

        while (Logs.Count > 800)
        {
            Logs.RemoveAt(0);
        }
    }

    private void LogErrors(IReadOnlyList<string> errors)
    {
        const int limit = 5;
        for (var index = 0; index < errors.Count && index < limit; index++)
        {
            Log(LogLevel.Warning, errors[index]);
        }

        if (errors.Count > limit)
        {
            Log(
                LogLevel.Warning,
                $"{Formatters.FormatCount(errors.Count - limit)} خطای دیگر در گزارش ثبت شد.");
        }
    }

    private void RefreshCommandStates()
    {
        RefreshFoldersCommand?.RaiseCanExecuteChanged();
        ScanCommand?.RaiseCanExecuteChanged();
        CancelCommand?.RaiseCanExecuteChanged();
        ExportCommand?.RaiseCanExecuteChanged();
        BrowseDestinationCommand?.RaiseCanExecuteChanged();
        OpenDestinationCommand?.RaiseCanExecuteChanged();
        SelectAllFoldersCommand?.RaiseCanExecuteChanged();
        ClearFoldersSelectionCommand?.RaiseCanExecuteChanged();
        SelectInboxCommand?.RaiseCanExecuteChanged();
        SelectAllMessagesCommand?.RaiseCanExecuteChanged();
        ClearMessagesSelectionCommand?.RaiseCanExecuteChanged();
        SelectAllAttachmentsCommand?.RaiseCanExecuteChanged();
        ClearAttachmentsSelectionCommand?.RaiseCanExecuteChanged();
        ClearQuickFiltersCommand?.RaiseCanExecuteChanged();
        SetLast7DaysCommand?.RaiseCanExecuteChanged();
        SetLast30DaysCommand?.RaiseCanExecuteChanged();
        SetCurrentYearCommand?.RaiseCanExecuteChanged();
        SetPreviousYearCommand?.RaiseCanExecuteChanged();
        EditAdvancedFilterCommand?.RaiseCanExecuteChanged();
        ClearAdvancedFilterCommand?.RaiseCanExecuteChanged();
        SaveProfileCommand?.RaiseCanExecuteChanged();
        DeleteProfileCommand?.RaiseCanExecuteChanged();
        ImportProfileCommand?.RaiseCanExecuteChanged();
        ExportProfileCommand?.RaiseCanExecuteChanged();
    }
}
