using System.Globalization;
using System.Text.RegularExpressions;
using AttachmentExtractor.Core.Models;
using AttachmentExtractor.Core.Utilities;

namespace AttachmentExtractor.Core.Filters;

public static class FilterEvaluator
{
    private enum NumericKind
    {
        Number,
        Size,
        Count,
        Importance
    }

    public static bool Matches(FilterNode node, MailMessageInfo message)
    {
        if (node.Kind == FilterNodeKind.Group)
        {
            if (node.Children.Count == 0)
            {
                return node.GroupOperator == GroupOperator.And;
            }

            return node.GroupOperator == GroupOperator.And
                ? node.Children.All(child => Matches(child, message))
                : node.Children.Any(child => Matches(child, message));
        }

        return MatchesCondition(node, message);
    }

    private static bool MatchesCondition(FilterNode condition, MailMessageInfo message)
    {
        if (FilterFieldCatalog.RequiresValue(condition.Operator) &&
            string.IsNullOrWhiteSpace(condition.Value))
        {
            return false;
        }

        if (condition.Operator == ConditionOperator.Between &&
            string.IsNullOrWhiteSpace(condition.Value2))
        {
            return false;
        }

        return condition.Field switch
        {
            FilterField.Subject => EvaluateText(message.Subject, condition.Value, condition.Operator),
            FilterField.Body => EvaluateText(message.Body, condition.Value, condition.Operator),
            FilterField.Sender => EvaluateText(
                $"{message.SenderName} {message.SenderEmail}".Trim(),
                condition.Value,
                condition.Operator),
            FilterField.SenderName => EvaluateText(message.SenderName, condition.Value, condition.Operator),
            FilterField.SenderEmail => EvaluateText(message.SenderEmail, condition.Value, condition.Operator),
            FilterField.SenderDomain => EvaluateText(message.SenderDomain, condition.Value, condition.Operator),
            FilterField.Recipient => EvaluateText(
                $"{message.To} {message.Cc}".Trim(),
                condition.Value,
                condition.Operator),
            FilterField.To => EvaluateText(message.To, condition.Value, condition.Operator),
            FilterField.Cc => EvaluateText(message.Cc, condition.Value, condition.Operator),
            FilterField.FolderPath => EvaluateText(message.FolderPath, condition.Value, condition.Operator),
            FilterField.Category => EvaluateText(message.Categories, condition.Value, condition.Operator),
            FilterField.ReceivedDate => EvaluateDate(message.ReceivedTime, condition),
            FilterField.SentDate => EvaluateDate(message.SentOn, condition),
            FilterField.AttachmentName or FilterField.AttachmentExtension or FilterField.AttachmentSize =>
                EvaluateAttachment(condition, message),
            FilterField.AttachmentCount => EvaluateNumber(
                message.AttachmentCount,
                condition,
                NumericKind.Count),
            FilterField.MessageSize => EvaluateNumber(
                message.SizeBytes,
                condition,
                NumericKind.Size),
            FilterField.Importance => EvaluateImportance(message.Importance, condition),
            FilterField.HasAttachments => EvaluateBool(message.HasAttachments, condition),
            FilterField.IsRead => EvaluateBool(message.IsRead, condition),
            _ => false
        };
    }

    private static bool EvaluateText(string? actual, string value, ConditionOperator conditionOperator)
    {
        var text = actual ?? string.Empty;
        var search = value?.Trim() ?? string.Empty;

        switch (conditionOperator)
        {
            case ConditionOperator.Contains:
                return text.Contains(search, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.NotContains:
                return !text.Contains(search, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.Equals:
                return string.Equals(text, search, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.NotEquals:
                return !string.Equals(text, search, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.StartsWith:
                return text.StartsWith(search, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.EndsWith:
                return text.EndsWith(search, StringComparison.OrdinalIgnoreCase);
            case ConditionOperator.Regex:
                return EvaluateRegex(text, search);
            case ConditionOperator.In:
                return ValueParsers.SplitList(search)
                    .Any(item => string.Equals(text, item, StringComparison.OrdinalIgnoreCase));
            case ConditionOperator.NotIn:
                return !ValueParsers.SplitList(search)
                    .Any(item => string.Equals(text, item, StringComparison.OrdinalIgnoreCase));
            default:
                return false;
        }
    }

    private static bool EvaluateDate(DateTime? actual, FilterNode condition)
    {
        if (!actual.HasValue)
        {
            return false;
        }

        if (!ValueParsers.TryParseDate(condition.Value, out var target, out var targetHasTime))
        {
            return false;
        }

        var value = actual.Value;

        switch (condition.Operator)
        {
            case ConditionOperator.Before:
            case ConditionOperator.LessThan:
                return targetHasTime ? value < target : value.Date < target.Date;
            case ConditionOperator.After:
            case ConditionOperator.GreaterThan:
                return targetHasTime ? value > target : value.Date > target.Date;
            case ConditionOperator.GreaterOrEqual:
                return targetHasTime ? value >= target : value.Date >= target.Date;
            case ConditionOperator.LessOrEqual:
                return targetHasTime ? value <= target : value.Date <= target.Date;
            case ConditionOperator.Equals:
                return targetHasTime ? value == target : value.Date == target.Date;
            case ConditionOperator.NotEquals:
                return targetHasTime ? value != target : value.Date != target.Date;
            case ConditionOperator.Between:
                if (!ValueParsers.TryParseDate(condition.Value2, out var second, out var secondHasTime))
                {
                    return false;
                }

                var useTime = targetHasTime || secondHasTime;
                var minimum = target <= second ? target : second;
                var maximum = target <= second ? second : target;
                return useTime
                    ? value >= minimum && value <= maximum
                    : value.Date >= minimum.Date && value.Date <= maximum.Date;
            default:
                return false;
        }
    }

    private static bool EvaluateNumber(
        long actual,
        FilterNode condition,
        NumericKind kind,
        ConditionOperator? operatorOverride = null)
    {
        if (!TryParseNumeric(condition.Value, kind, out var target))
        {
            return false;
        }

        return CompareNumber(actual, target, operatorOverride ?? condition.Operator, () =>
        {
            if (!TryParseNumeric(condition.Value2, kind, out var second))
            {
                return null;
            }

            return second;
        });
    }

    private static bool EvaluateImportance(int actual, FilterNode condition)
    {
        switch (condition.Operator)
        {
            case ConditionOperator.In:
                return ValueParsers.SplitList(condition.Value)
                    .Any(item => ValueParsers.TryParseImportance(item, out var parsed) && parsed == actual);
            case ConditionOperator.NotIn:
                return !ValueParsers.SplitList(condition.Value)
                    .Any(item => ValueParsers.TryParseImportance(item, out var parsed) && parsed == actual);
            default:
                return EvaluateNumber(actual, condition, NumericKind.Importance);
        }
    }

    private static bool EvaluateBool(bool actual, FilterNode condition)
    {
        return condition.Operator switch
        {
            ConditionOperator.IsTrue => actual,
            ConditionOperator.IsFalse => !actual,
            ConditionOperator.Equals => ValueParsers.TryParseBool(condition.Value, out var expected) && actual == expected,
            ConditionOperator.NotEquals => ValueParsers.TryParseBool(condition.Value, out var expected) && actual != expected,
            _ => false
        };
    }

    private static bool EvaluateAttachment(FilterNode condition, MailMessageInfo message)
    {
        var negative = FilterFieldCatalog.IsNegativeOperator(condition.Operator);
        var positiveOperator = FilterFieldCatalog.ToPositiveOperator(condition.Operator);

        var any = message.Attachments.Any(attachment => condition.Field switch
        {
            FilterField.AttachmentName => EvaluateText(
                attachment.EffectiveName,
                condition.Value,
                positiveOperator),
            FilterField.AttachmentExtension => EvaluateExtension(
                attachment.EffectiveExtension,
                condition.Value,
                positiveOperator),
            FilterField.AttachmentSize => EvaluateNumber(
                attachment.SizeBytes,
                condition,
                NumericKind.Size,
                positiveOperator),
            _ => false
        });

        return negative ? !any : any;
    }

    private static bool EvaluateExtension(
        string? actual,
        string value,
        ConditionOperator conditionOperator)
    {
        var extension = ValueParsers.NormalizeExtension(actual);
        var normalizedValue = ValueParsers.NormalizeExtension(value);

        return conditionOperator switch
        {
            ConditionOperator.Contains => extension.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotContains => !extension.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Equals => string.Equals(extension, normalizedValue, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !string.Equals(extension, normalizedValue, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.StartsWith => extension.StartsWith(normalizedValue, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.EndsWith => extension.EndsWith(normalizedValue, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Regex => EvaluateRegex(extension, value),
            ConditionOperator.In => ValueParsers.SplitList(value)
                .Select(ValueParsers.NormalizeExtension)
                .Any(item => string.Equals(extension, item, StringComparison.OrdinalIgnoreCase)),
            ConditionOperator.NotIn => !ValueParsers.SplitList(value)
                .Select(ValueParsers.NormalizeExtension)
                .Any(item => string.Equals(extension, item, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static bool EvaluateRegex(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(
                input,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseNumeric(string? text, NumericKind kind, out double value)
    {
        switch (kind)
        {
            case NumericKind.Size:
                if (ValueParsers.TryParseSizeBytes(text, out var bytes))
                {
                    value = bytes;
                    return true;
                }

                value = 0;
                return false;
            case NumericKind.Importance:
                if (ValueParsers.TryParseImportance(text, out var importance))
                {
                    value = importance;
                    return true;
                }

                value = 0;
                return false;
            default:
                return ValueParsers.TryParseNumber(text, out value);
        }
    }

    private static bool CompareNumber(
        double actual,
        double target,
        ConditionOperator conditionOperator,
        Func<double?> secondValueFactory)
    {
        const double tolerance = 0.0001;

        switch (conditionOperator)
        {
            case ConditionOperator.Equals:
                return Math.Abs(actual - target) <= tolerance;
            case ConditionOperator.NotEquals:
                return Math.Abs(actual - target) > tolerance;
            case ConditionOperator.GreaterThan:
                return actual > target;
            case ConditionOperator.LessThan:
                return actual < target;
            case ConditionOperator.GreaterOrEqual:
                return actual >= target;
            case ConditionOperator.LessOrEqual:
                return actual <= target;
            case ConditionOperator.Between:
                var second = secondValueFactory();
                if (!second.HasValue)
                {
                    return false;
                }

                var minimum = Math.Min(target, second.Value);
                var maximum = Math.Max(target, second.Value);
                return actual >= minimum && actual <= maximum;
            default:
                return false;
        }
    }
}
