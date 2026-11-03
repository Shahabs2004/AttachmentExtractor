using System.Collections.ObjectModel;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.ViewModels;

public sealed class FolderNodeViewModel : ObservableObject
{
    private bool? _isSelected;
    private bool _isExpanded;

    public FolderNodeViewModel(FolderInfo model, FolderNodeViewModel? parent)
    {
        Model = model;
        Parent = parent;
    }

    public FolderInfo Model { get; }

    public FolderNodeViewModel? Parent { get; }

    public ObservableCollection<FolderNodeViewModel> Children { get; } = new();

    public event EventHandler? SelectionChanged;

    public string Name => string.IsNullOrWhiteSpace(Model.Name) ? "پوشه" : Model.Name;

    public string FullPath => Model.FullPath;

    public bool HasChildren => Children.Count > 0;

    public bool? IsSelected
    {
        get => _isSelected;
        set => SetSelected(value == true, updateParent: true);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public void SetSelected(bool value)
    {
        SetSelected(value, updateParent: true);
    }

    private void SetSelected(bool value, bool updateParent)
    {
        var changed = _isSelected != value;
        _isSelected = value;

        if (changed)
        {
            OnPropertyChanged(nameof(IsSelected));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        foreach (var child in Children)
        {
            child.SetSelected(value, updateParent: false);
        }

        if (updateParent)
        {
            Parent?.RecalculateSelectionFromChildren();
        }
    }

    private void RecalculateSelectionFromChildren()
    {
        if (Children.Count == 0)
        {
            return;
        }

        bool? state = Children.All(child => child.IsSelected == true)
            ? true
            : Children.All(child => child.IsSelected == false)
                ? false
                : null;

        if (_isSelected != state)
        {
            _isSelected = state;
            OnPropertyChanged(nameof(IsSelected));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        Parent?.RecalculateSelectionFromChildren();
    }
}
