using AttachmentExtractor.App.Services;
using AttachmentExtractor.Core.Filters;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.ViewModels;

public sealed class RuleBuilderViewModel : ObservableObject
{
    private RuleNodeViewModel? _selectedNode;

    public RuleBuilderViewModel(FilterNode? filter = null)
    {
        Root = new RuleNodeViewModel(
            FilterNodeKind.Group,
            null,
            filter ?? FilterNode.NewGroup());

        AddGroupCommand = new RelayCommand(AddGroup, () => true);
        AddConditionCommand = new RelayCommand(AddCondition, () => true);
        RemoveCommand = new RelayCommand(RemoveSelected, () => CanRemove);
        MoveUpCommand = new RelayCommand(MoveUp, () => CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, () => CanMoveDown);
        ClearCommand = new RelayCommand(ClearAll, () => Root.Children.Count > 0);
    }

    public RuleNodeViewModel Root { get; }

    public RuleNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsGroupSelected));
                OnPropertyChanged(nameof(IsConditionSelected));
                OnPropertyChanged(nameof(CanRemove));
                OnPropertyChanged(nameof(CanMoveUp));
                OnPropertyChanged(nameof(CanMoveDown));
                RefreshCommands();
            }
        }
    }

    public RelayCommand AddGroupCommand { get; }

    public RelayCommand AddConditionCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand ClearCommand { get; }

    public bool HasSelection => SelectedNode is not null;

    public bool IsGroupSelected => SelectedNode?.IsGroup == true;

    public bool IsConditionSelected => SelectedNode?.IsCondition == true;

    public bool HasRules => Root.Children.Count > 0;

    public string Summary
    {
        get
        {
            var (groups, conditions) = FilterAnalyzer.Count(Root.ToFilterNode());
            if (conditions == 0)
            {
                return "هنوز شرطی ساخته نشده است؛ با دکمه «افزودن شرط» شروع کنید.";
            }

            return $"{Formatters.FormatCount(conditions)} شرط در {Formatters.FormatCount(groups)} گروه";
        }
    }

    public bool CanRemove => SelectedNode is not null && !SelectedNode.IsRoot;

    public bool CanMoveUp
    {
        get
        {
            if (SelectedNode?.Parent is null)
            {
                return false;
            }

            return SelectedNode.Parent.Children.IndexOf(SelectedNode) > 0;
        }
    }

    public bool CanMoveDown
    {
        get
        {
            if (SelectedNode?.Parent is null)
            {
                return false;
            }

            var index = SelectedNode.Parent.Children.IndexOf(SelectedNode);
            return index >= 0 && index < SelectedNode.Parent.Children.Count - 1;
        }
    }

    public bool TryBuild(out FilterNode? result, out string error, out RuleNodeViewModel? invalidNode)
    {
        result = null;
        error = string.Empty;
        invalidNode = null;

        var emptyGroup = FindEmptyGroup(Root);
        if (emptyGroup is not null)
        {
            invalidNode = emptyGroup;
            error = "گروه خالی است؛ حداقل یک شرط یا گروه داخل آن اضافه کنید.";
            return false;
        }

        var built = Root.ToFilterNode();
        if (!FilterValidator.Validate(built, out error, out _))
        {
            invalidNode = FindInvalidCondition(Root);
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "فیلتر واردشده معتبر نیست.";
            }

            return false;
        }

        result = built;
        return true;
    }

    private void AddGroup()
    {
        var parent = ResolveInsertParent();
        var node = new RuleNodeViewModel(
            FilterNodeKind.Group,
            parent,
            FilterNode.NewGroup());

        parent.Children.Add(node);
        parent.IsExpanded = true;
        NotifyParentChain(parent);
        SelectedNode = node;
        OnTreeChanged();
    }

    private void AddCondition()
    {
        var parent = ResolveInsertParent();
        var node = new RuleNodeViewModel(
            FilterNodeKind.Condition,
            parent,
            FilterNode.NewCondition(FilterField.Subject, ConditionOperator.Contains));

        parent.Children.Add(node);
        parent.IsExpanded = true;
        NotifyParentChain(parent);
        SelectedNode = node;
        OnTreeChanged();
    }

    private void RemoveSelected()
    {
        if (SelectedNode?.Parent is not { } parent)
        {
            return;
        }

        parent.Children.Remove(SelectedNode);
        NotifyParentChain(parent);
        SelectedNode = parent;
        OnTreeChanged();
    }

    private void MoveUp()
    {
        if (SelectedNode?.Parent is not { } parent)
        {
            return;
        }

        var index = parent.Children.IndexOf(SelectedNode);
        if (index > 0)
        {
            parent.Children.Move(index, index - 1);
            NotifyParentChain(parent);
            OnTreeChanged();
        }
    }

    private void MoveDown()
    {
        if (SelectedNode?.Parent is not { } parent)
        {
            return;
        }

        var index = parent.Children.IndexOf(SelectedNode);
        if (index >= 0 && index < parent.Children.Count - 1)
        {
            parent.Children.Move(index, index + 1);
            NotifyParentChain(parent);
            OnTreeChanged();
        }
    }

    private void ClearAll()
    {
        Root.Children.Clear();
        Root.NotifyChildrenChanged();
        SelectedNode = null;
        OnTreeChanged();
    }

    private RuleNodeViewModel ResolveInsertParent()
    {
        if (SelectedNode is null)
        {
            return Root;
        }

        return SelectedNode.IsGroup ? SelectedNode : SelectedNode.Parent ?? Root;
    }

    private void OnTreeChanged()
    {
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        RefreshCommands();
    }

    private static void NotifyParentChain(RuleNodeViewModel node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            current.NotifyChildrenChanged();
        }
    }

    private static RuleNodeViewModel? FindEmptyGroup(RuleNodeViewModel node)
    {
        if (node.IsGroup && !node.IsRoot && node.Children.Count == 0)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindEmptyGroup(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static RuleNodeViewModel? FindInvalidCondition(RuleNodeViewModel node)
    {
        if (node.IsCondition)
        {
            var wrapper = FilterNode.NewGroup(GroupOperator.And);
            wrapper.Children.Add(node.ToFilterNode());
            return FilterValidator.Validate(wrapper, out _, out _) ? null : node;
        }

        foreach (var child in node.Children)
        {
            var found = FindInvalidCondition(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void RefreshCommands()
    {
        AddGroupCommand.RaiseCanExecuteChanged();
        AddConditionCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
    }
}
