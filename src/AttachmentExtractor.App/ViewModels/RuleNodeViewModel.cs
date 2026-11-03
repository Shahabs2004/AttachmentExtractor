using System.Collections.ObjectModel;
using AttachmentExtractor.App.Services;
using AttachmentExtractor.Core.Filters;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.ViewModels;

public sealed class RuleNodeViewModel : ObservableObject
{
    private static readonly IReadOnlyList<GroupOperatorOption> AllGroupOperators = new List<GroupOperatorOption>
    {
        new(GroupOperator.And, "همه (AND)"),
        new(GroupOperator.Or, "هر یک (OR)")
    };

    private FilterFieldDescriptor _selectedField;
    private OperatorOption? _selectedOperator;
    private GroupOperatorOption _selectedGroupOperator;
    private string _value;
    private string _value2;
    private bool _isExpanded = true;

    public RuleNodeViewModel(FilterNodeKind kind, RuleNodeViewModel? parent, FilterNode? model = null)
    {
        Kind = kind;
        Parent = parent;
        _selectedField = FilterFieldCatalog.Get(model?.Field ?? FilterField.Subject);
        _value = model?.Value ?? string.Empty;
        _value2 = model?.Value2 ?? string.Empty;
        _selectedGroupOperator = AllGroupOperators.First(
            option => option.Value == (model?.GroupOperator ?? GroupOperator.And));

        OperatorOptions = new ObservableCollection<OperatorOption>();
        RebuildOperatorOptions(model?.Operator);

        if (kind == FilterNodeKind.Group && model is not null)
        {
            foreach (var child in model.Children)
            {
                Children.Add(new RuleNodeViewModel(child.Kind, this, child));
            }
        }
    }

    public FilterNodeKind Kind { get; }

    public RuleNodeViewModel? Parent { get; }

    public ObservableCollection<RuleNodeViewModel> Children { get; } = new();

    public ObservableCollection<OperatorOption> OperatorOptions { get; }

    public IReadOnlyList<FilterFieldDescriptor> FieldOptions => FilterFieldCatalog.All;

    public IReadOnlyList<GroupOperatorOption> GroupOperators => AllGroupOperators;

    public bool IsGroup => Kind == FilterNodeKind.Group;

    public bool IsCondition => Kind == FilterNodeKind.Condition;

    public bool IsRoot => Parent is null;

    public bool HasChildren => Children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public int ConditionCount => IsGroup
        ? Children.Sum(child => child.ConditionCount)
        : 1;

    public string NodeBadge => IsGroup ? GroupBadge : "شرط";

    public string Title
    {
        get
        {
            if (IsGroup)
            {
                var count = Formatters.FormatCount(Children.Count);
                var label = $"گروه {FilterLabels.Group(_selectedGroupOperator.Value)}";
                return IsRoot ? $"گروه ریشه • {FilterLabels.Group(_selectedGroupOperator.Value)}" : $"{label} • {count} مورد";
            }

            var operatorLabel = FilterLabels.Operator(SelectedOperatorValue);
            if (!RequiresValue)
            {
                return $"{_selectedField.Label} {operatorLabel}";
            }

            return ShowsSecondValue
                ? $"{_selectedField.Label} {operatorLabel} «{_value}» و «{_value2}»"
                : $"{_selectedField.Label} {operatorLabel} «{_value}»";
        }
    }

    public FilterFieldDescriptor SelectedField
    {
        get => _selectedField;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedField))
            {
                return;
            }

            _selectedField = value;
            _value = string.Empty;
            _value2 = string.Empty;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(Value2));
            OnPropertyChanged(nameof(InputHint));
            OnPropertyChanged(nameof(ValueHint));

            RebuildOperatorOptions(null);
            RaiseShapeChanged();
        }
    }

    public OperatorOption? SelectedOperator
    {
        get => _selectedOperator;
        set
        {
            if (value is null || _selectedOperator?.Value == value.Value)
            {
                return;
            }

            _selectedOperator = value;
            OnPropertyChanged();

            if (!RequiresValue)
            {
                _value = string.Empty;
                _value2 = string.Empty;
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(Value2));
            }
            else if (!ShowsSecondValue)
            {
                _value2 = string.Empty;
                OnPropertyChanged(nameof(Value2));
            }

            RaiseShapeChanged();
        }
    }

    public GroupOperatorOption SelectedGroupOperator
    {
        get => _selectedGroupOperator;
        set
        {
            if (value is null || _selectedGroupOperator.Value == value.Value)
            {
                return;
            }

            _selectedGroupOperator = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GroupBadge));
            OnPropertyChanged(nameof(GroupOperatorHint));
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string Value2
    {
        get => _value2;
        set
        {
            if (SetProperty(ref _value2, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string GroupBadge => FilterLabels.Group(_selectedGroupOperator.Value);

    public string GroupOperatorHint => _selectedGroupOperator.Value == GroupOperator.And
        ? "همه شرط‌های این گروه باید هم‌زمان برقرار باشند."
        : "برقراری یکی از شرط‌های این گروه کافی است.";

    public string InputHint => IsCondition ? _selectedField.Hint : string.Empty;

    public bool RequiresValue => IsCondition &&
                                 _selectedOperator is not null &&
                                 FilterFieldCatalog.RequiresValue(_selectedOperator.Value);

    public bool ShowsSecondValue => IsCondition &&
                                    _selectedOperator?.Value == ConditionOperator.Between;

    public bool ShowsValueListHint => IsCondition &&
                                      _selectedOperator?.Value is ConditionOperator.In or ConditionOperator.NotIn;

    public string ValueHint
    {
        get
        {
            if (!IsCondition || !RequiresValue)
            {
                return string.Empty;
            }

            if (FilterFieldCatalog.IsDateField(_selectedField.Field))
            {
                return "نمونه: 1404/08/11 یا 2025-11-02";
            }

            if (FilterFieldCatalog.IsSizeField(_selectedField.Field))
            {
                return "نمونه: 2 MB یا 500 KB";
            }

            if (FilterFieldCatalog.IsCountField(_selectedField.Field))
            {
                return "یک عدد صحیح وارد کنید؛ مثال: 1";
            }

            if (FilterFieldCatalog.IsBooleanField(_selectedField.Field))
            {
                return "true/false یا بله/خیر";
            }

            if (FilterFieldCatalog.IsImportanceField(_selectedField.Field))
            {
                return "Low / Normal / High یا کم / معمولی / بالا";
            }

            if (ShowsValueListHint)
            {
                return FilterFieldCatalog.IsAttachmentField(_selectedField.Field)
                    ? "پسوندها را با کاما جدا کنید؛ مثال: pdf, docx"
                    : "مقادیر را با کاما جدا کنید";
            }

            return _selectedField.Hint;
        }
    }

    public ConditionOperator SelectedOperatorValue =>
        _selectedOperator?.Value ?? FilterFieldCatalog.GetDefaultOperator(_selectedField.Field);

    public FilterNode ToFilterNode()
    {
        if (IsGroup)
        {
            var group = FilterNode.NewGroup(_selectedGroupOperator.Value);
            foreach (var child in Children)
            {
                group.Children.Add(child.ToFilterNode());
            }

            return group;
        }

        return FilterNode.NewCondition(
            _selectedField.Field,
            SelectedOperatorValue,
            RequiresValue ? _value.Trim() : string.Empty,
            ShowsSecondValue ? _value2.Trim() : string.Empty);
    }

    public void NotifyChildrenChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(ConditionCount));
    }

    private void RebuildOperatorOptions(ConditionOperator? preferredOperator)
    {
        OperatorOptions.Clear();

        foreach (var conditionOperator in _selectedField.Operators)
        {
            OperatorOptions.Add(new OperatorOption(
                conditionOperator,
                FilterLabels.Operator(conditionOperator)));
        }

        var preferred = preferredOperator ?? FilterFieldCatalog.GetDefaultOperator(_selectedField.Field);
        _selectedOperator = OperatorOptions.FirstOrDefault(option => option.Value == preferred)
                            ?? OperatorOptions.FirstOrDefault();

        OnPropertyChanged(nameof(SelectedOperator));
    }

    private void RaiseShapeChanged()
    {
        OnPropertyChanged(nameof(RequiresValue));
        OnPropertyChanged(nameof(ShowsSecondValue));
        OnPropertyChanged(nameof(ShowsValueListHint));
        OnPropertyChanged(nameof(ValueHint));
        OnPropertyChanged(nameof(Title));
    }
}
