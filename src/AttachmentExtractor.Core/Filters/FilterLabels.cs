using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Filters;

public static class FilterLabels
{
    private static readonly Dictionary<ConditionOperator, string> OperatorLabels = new()
    {
        [ConditionOperator.Contains] = "شامل",
        [ConditionOperator.NotContains] = "شامل نباشد",
        [ConditionOperator.Equals] = "برابر",
        [ConditionOperator.NotEquals] = "برابر نباشد",
        [ConditionOperator.StartsWith] = "شروع شود با",
        [ConditionOperator.EndsWith] = "پایان یابد با",
        [ConditionOperator.Regex] = "تطابق Regex",
        [ConditionOperator.GreaterThan] = "بزرگ‌تر از",
        [ConditionOperator.LessThan] = "کوچک‌تر از",
        [ConditionOperator.GreaterOrEqual] = "بزرگ‌تر یا مساوی",
        [ConditionOperator.LessOrEqual] = "کوچک‌تر یا مساوی",
        [ConditionOperator.Between] = "بین",
        [ConditionOperator.Before] = "قبل از",
        [ConditionOperator.After] = "بعد از",
        [ConditionOperator.IsTrue] = "درست باشد",
        [ConditionOperator.IsFalse] = "نادرست باشد",
        [ConditionOperator.In] = "یکی از",
        [ConditionOperator.NotIn] = "هیچ‌کدام نباشد"
    };

    public static string Operator(ConditionOperator conditionOperator) =>
        OperatorLabels.TryGetValue(conditionOperator, out var label)
            ? label
            : conditionOperator.ToString();

    public static string Group(GroupOperator groupOperator) =>
        groupOperator == GroupOperator.And ? "AND" : "OR";

    public static string Importance(int value) => value switch
    {
        2 => "بالا",
        1 => "معمولی",
        _ => "کم"
    };

    public static string Describe(FilterNode node)
    {
        if (node.Kind == FilterNodeKind.Group)
        {
            return $"گروه {Group(node.GroupOperator)}";
        }

        var field = FilterFieldCatalog.Get(node.Field).Label;
        var operatorLabel = Operator(node.Operator);

        if (!FilterFieldCatalog.RequiresValue(node.Operator))
        {
            return $"{field} {operatorLabel}";
        }

        return node.Operator == ConditionOperator.Between
            ? $"{field} {operatorLabel} «{node.Value}» و «{node.Value2}»"
            : $"{field} {operatorLabel} «{node.Value}»";
    }
}
