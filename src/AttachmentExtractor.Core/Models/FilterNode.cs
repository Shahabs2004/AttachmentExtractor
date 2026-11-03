namespace AttachmentExtractor.Core.Models;

public sealed class FilterNode
{
    public FilterNodeKind Kind { get; set; } = FilterNodeKind.Group;

    public GroupOperator GroupOperator { get; set; } = GroupOperator.And;

    public List<FilterNode> Children { get; set; } = new();

    public FilterField Field { get; set; } = FilterField.Subject;

    public ConditionOperator Operator { get; set; } = ConditionOperator.Contains;

    public string Value { get; set; } = string.Empty;

    public string Value2 { get; set; } = string.Empty;

    public static FilterNode NewGroup(GroupOperator groupOperator = GroupOperator.And) =>
        new()
        {
            Kind = FilterNodeKind.Group,
            GroupOperator = groupOperator
        };

    public static FilterNode NewCondition(
        FilterField field,
        ConditionOperator conditionOperator,
        string value = "",
        string value2 = "") =>
        new()
        {
            Kind = FilterNodeKind.Condition,
            Field = field,
            Operator = conditionOperator,
            Value = value,
            Value2 = value2
        };

    public FilterNode Clone()
    {
        var clone = new FilterNode
        {
            Kind = Kind,
            GroupOperator = GroupOperator,
            Field = Field,
            Operator = Operator,
            Value = Value,
            Value2 = Value2
        };

        foreach (var child in Children)
        {
            clone.Children.Add(child.Clone());
        }

        return clone;
    }
}
