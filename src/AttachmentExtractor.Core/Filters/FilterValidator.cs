using AttachmentExtractor.Core.Models;
using AttachmentExtractor.Core.Utilities;

namespace AttachmentExtractor.Core.Filters;

public static class FilterValidator
{
    public static bool Validate(FilterNode root, out string error, out FilterNode? invalidNode)
    {
        var result = ValidateNode(root);
        error = result.Error;
        invalidNode = result.Node;
        return result.IsValid;
    }

    private static (bool IsValid, string Error, FilterNode? Node) ValidateNode(FilterNode node)
    {
        if (node.Kind == FilterNodeKind.Group)
        {
            foreach (var child in node.Children)
            {
                var childResult = ValidateNode(child);
                if (!childResult.IsValid)
                {
                    return childResult;
                }
            }

            return (true, string.Empty, null);
        }

        var descriptor = FilterFieldCatalog.Get(node.Field);

        if (FilterFieldCatalog.RequiresValue(node.Operator) &&
            string.IsNullOrWhiteSpace(node.Value))
        {
            return (false, $"مقدار شرط «{descriptor.Label}» خالی است.", node);
        }

        if (node.Operator == ConditionOperator.Between &&
            string.IsNullOrWhiteSpace(node.Value2))
        {
            return (false, $"مقدار دوم بازه برای «{descriptor.Label}» خالی است.", node);
        }

        if (node.Operator is ConditionOperator.In or ConditionOperator.NotIn &&
            ValueParsers.SplitList(node.Value).Count == 0)
        {
            return (false, $"برای عملگر «{FilterLabels.Operator(node.Operator)}» حداقل یک مقدار وارد کنید.", node);
        }

        if (node.Operator == ConditionOperator.Regex && !ValueParsers.IsRegexValid(node.Value))
        {
            return (false, $"الگوی Regex برای «{descriptor.Label}» معتبر نیست: {node.Value}", node);
        }

        if (FilterFieldCatalog.IsDateField(node.Field))
        {
            if (!ValueParsers.TryParseDate(node.Value, out _, out _))
            {
                return (false, $"تاریخ «{node.Value}» برای «{descriptor.Label}» معتبر نیست.", node);
            }

            if (node.Operator == ConditionOperator.Between &&
                !ValueParsers.TryParseDate(node.Value2, out _, out _))
            {
                return (false, $"تاریخ دوم «{node.Value2}» برای «{descriptor.Label}» معتبر نیست.", node);
            }
        }
        else if (FilterFieldCatalog.IsSizeField(node.Field))
        {
            if (!ValueParsers.TryParseSizeBytes(node.Value, out _))
            {
                return (false, $"اندازه «{node.Value}» برای «{descriptor.Label}» معتبر نیست. مثال: 2.5 MB", node);
            }

            if (node.Operator == ConditionOperator.Between &&
                !ValueParsers.TryParseSizeBytes(node.Value2, out _))
            {
                return (false, $"اندازه دوم «{node.Value2}» برای «{descriptor.Label}» معتبر نیست.", node);
            }
        }
        else if (FilterFieldCatalog.IsCountField(node.Field))
        {
            if (!ValueParsers.TryParseNumber(node.Value, out _))
            {
                return (false, $"عدد «{node.Value}» برای «{descriptor.Label}» معتبر نیست.", node);
            }

            if (node.Operator == ConditionOperator.Between &&
                !ValueParsers.TryParseNumber(node.Value2, out _))
            {
                return (false, $"عدد دوم «{node.Value2}» برای «{descriptor.Label}» معتبر نیست.", node);
            }
        }
        else if (FilterFieldCatalog.IsImportanceField(node.Field))
        {
            if (node.Operator is ConditionOperator.In or ConditionOperator.NotIn)
            {
                foreach (var item in ValueParsers.SplitList(node.Value))
                {
                    if (!ValueParsers.TryParseImportance(item, out _))
                    {
                        return (false, $"مقدار اهمیت «{item}» معتبر نیست. از Low/Normal/High استفاده کنید.", node);
                    }
                }
            }
            else if (!ValueParsers.TryParseImportance(node.Value, out _))
            {
                return (false, $"مقدار اهمیت «{node.Value}» معتبر نیست. از Low/Normal/High استفاده کنید.", node);
            }
        }
        else if (FilterFieldCatalog.IsBooleanField(node.Field))
        {
            if ((node.Operator is ConditionOperator.Equals or ConditionOperator.NotEquals) &&
                !ValueParsers.TryParseBool(node.Value, out _))
            {
                return (false, $"مقدار «{node.Value}» برای «{descriptor.Label}» معتبر نیست. از true/false یا بله/خیر استفاده کنید.", node);
            }
        }
        else if (descriptor.InputKind == FilterInputKind.Extension &&
                 (node.Operator is ConditionOperator.In or ConditionOperator.NotIn))
        {
            if (ValueParsers.SplitList(node.Value)
                .Select(ValueParsers.NormalizeExtension)
                .Any(item => string.IsNullOrWhiteSpace(item)))
            {
                return (false, $"پسوند واردشده برای «{descriptor.Label}» معتبر نیست.", node);
            }
        }

        return (true, string.Empty, null);
    }
}
