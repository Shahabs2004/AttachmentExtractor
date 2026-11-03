using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AttachmentExtractor.App.Services;
using AttachmentExtractor.Core.Filters;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.ViewModels;

public sealed class MessageRowViewModel : ObservableObject
{
    private static readonly Regex WhitespaceRegex =
        new(@"\s+", RegexOptions.Compiled);

    private readonly bool _bodyIncluded;
    private bool _isSelected = true;
    private bool _suppressAttachmentEvents;

    public MessageRowViewModel(MailMessageInfo model, bool bodyIncluded)
    {
        Model = model;
        _bodyIncluded = bodyIncluded;

        var hasRegularAttachment = model.Attachments.Any(attachment => !attachment.IsInline);
        foreach (var attachment in model.Attachments)
        {
            var row = new AttachmentRowViewModel(this, attachment);
            row.InitializeSelection(hasRegularAttachment ? !attachment.IsInline : true);
            Attachments.Add(row);
        }
    }

    public MailMessageInfo Model { get; }

    public ObservableCollection<AttachmentRowViewModel> Attachments { get; } = new();

    public event EventHandler? SelectionChanged;

    public event EventHandler? AttachmentSelectionChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Subject => string.IsNullOrWhiteSpace(Model.Subject)
        ? "(بدون موضوع)"
        : Model.Subject;

    public string SenderName => string.IsNullOrWhiteSpace(Model.SenderName)
        ? Model.SenderEmail
        : Model.SenderName;

    public string SenderEmail => Model.SenderEmail;

    public string SenderDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Model.SenderName))
            {
                return string.IsNullOrWhiteSpace(Model.SenderEmail) ? "فرستنده ناشناس" : Model.SenderEmail;
            }

            return string.IsNullOrWhiteSpace(Model.SenderEmail)
                ? Model.SenderName
                : $"{Model.SenderName} — {Model.SenderEmail}";
        }
    }

    public string SenderInitial
    {
        get
        {
            var source = SenderName;
            return string.IsNullOrWhiteSpace(source) ? "؟" : source[..1].ToUpperInvariant();
        }
    }

    public DateTime? ReceivedTime => Model.ReceivedTime;

    public string ReceivedJalali => Formatters.FormatJalaliDateTime(Model.ReceivedTime);

    public string ReceivedGregorian => Model.ReceivedTime?.ToString("yyyy/MM/dd HH:mm") ?? "—";

    public string FolderPath => Model.FolderPath;

    public string FolderName
    {
        get
        {
            var path = Model.FolderPath?.TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(path))
            {
                return "—";
            }

            var index = path.LastIndexOf('\\');
            return index >= 0 && index < path.Length - 1 ? path[(index + 1)..] : path;
        }
    }

    public int AttachmentCount => Attachments.Count;

    public bool HasAttachments => Attachments.Count > 0;

    public string AttachmentSummary => Attachments.Count == 0
        ? "بدون پیوست"
        : $"{Formatters.FormatCount(Attachments.Count)} پیوست";

    public string SizeText => Formatters.FormatSize(Model.SizeBytes);

    public long SizeBytes => Model.SizeBytes;

    public bool IsRead => Model.IsRead;

    public string ImportanceLabel => FilterLabels.Importance(Model.Importance);

    public int ImportanceValue => Model.Importance;

    public string Categories => string.IsNullOrWhiteSpace(Model.Categories) ? "—" : Model.Categories;

    public string To => string.IsNullOrWhiteSpace(Model.To) ? "—" : Model.To;

    public string Cc => string.IsNullOrWhiteSpace(Model.Cc) ? "—" : Model.Cc;

    public string SenderDomain => string.IsNullOrWhiteSpace(Model.SenderDomain) ? "—" : Model.SenderDomain;

    public string BodyPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Model.Body))
            {
                return _bodyIncluded
                    ? "متنی برای نمایش وجود ندارد."
                    : "متن این ایمیل در اسکن دریافت نشده است. برای جست‌وجو یا پیش‌نمایش متن، گزینه «دریافت متن ایمیل» را فعال کنید و دوباره اسکن کنید.";
            }

            var text = WhitespaceRegex.Replace(Model.Body, " ").Trim();
            return Formatters.Truncate(text, 4000);
        }
    }

    public int SelectedAttachmentCount => Attachments.Count(attachment => attachment.IsSelected);

    public string SelectedAttachmentSummary =>
        $"{Formatters.FormatCount(SelectedAttachmentCount)} از {Formatters.FormatCount(AttachmentCount)} پیوست";

    public long SelectedBytes => Attachments
        .Where(attachment => attachment.IsSelected)
        .Sum(attachment => attachment.Model.SizeBytes);

    public IReadOnlyList<int> SelectedAttachmentIndexes => Attachments
        .Where(attachment => attachment.IsSelected)
        .Select(attachment => attachment.Index)
        .ToList();

    public void SetAllAttachments(bool selected)
    {
        _suppressAttachmentEvents = true;
        foreach (var attachment in Attachments)
        {
            attachment.IsSelected = selected;
        }

        _suppressAttachmentEvents = false;
        NotifyAttachmentSelectionChanged();
    }

    internal void NotifyAttachmentSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedAttachmentCount));
        OnPropertyChanged(nameof(SelectedAttachmentSummary));
        OnPropertyChanged(nameof(SelectedBytes));

        if (!_suppressAttachmentEvents)
        {
            AttachmentSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
