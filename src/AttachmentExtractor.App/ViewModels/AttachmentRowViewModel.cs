using AttachmentExtractor.App.Services;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.ViewModels;

public sealed class AttachmentRowViewModel : ObservableObject
{
    private readonly MessageRowViewModel _owner;
    private bool _isSelected;

    public AttachmentRowViewModel(MessageRowViewModel owner, AttachmentInfo model)
    {
        _owner = owner;
        Model = model;
    }

    public AttachmentInfo Model { get; }

    public int Index => Model.Index;

    public string FileName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Model.EffectiveName))
            {
                return Model.EffectiveName;
            }

            return $"پیوست {Formatters.ToPersianDigits(Index.ToString())}";
        }
    }

    public string ExtensionBadge
    {
        get
        {
            var extension = Model.EffectiveExtension.TrimStart('.');
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "FILE";
            }

            return extension.Length <= 5
                ? extension.ToUpperInvariant()
                : extension[..5].ToUpperInvariant();
        }
    }

    public string SizeText => Formatters.FormatSize(Model.SizeBytes);

    public bool IsInline => Model.IsInline;

    public string TypeLabel => IsInline ? "درون‌متنی" : "پیوست";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _owner.NotifyAttachmentSelectionChanged();
            }
        }
    }

    internal void InitializeSelection(bool selected)
    {
        _isSelected = selected;
        OnPropertyChanged(nameof(IsSelected));
    }
}
