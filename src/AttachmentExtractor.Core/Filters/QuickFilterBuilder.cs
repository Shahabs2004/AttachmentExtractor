using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Filters;

public static class QuickFilterBuilder
{
    public static List<FilterNode> Build(QuickFilterSettings settings)
    {
        var conditions = new List<FilterNode>();

        void Add(FilterField field, ConditionOperator conditionOperator, string? value, string? value2 = null)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                conditions.Add(FilterNode.NewCondition(
                    field,
                    conditionOperator,
                    value.Trim(),
                    value2?.Trim() ?? string.Empty));
            }
        }

        Add(FilterField.ReceivedDate, ConditionOperator.GreaterOrEqual, settings.DateFrom);
        Add(FilterField.ReceivedDate, ConditionOperator.LessOrEqual, settings.DateTo);
        Add(FilterField.Sender, ConditionOperator.Contains, settings.Sender);
        Add(FilterField.SenderDomain, ConditionOperator.Contains, settings.SenderDomain);
        Add(FilterField.Recipient, ConditionOperator.Contains, settings.Recipient);
        Add(FilterField.Subject, ConditionOperator.Contains, settings.Subject);
        Add(FilterField.Body, ConditionOperator.Contains, settings.Body);
        Add(FilterField.AttachmentSize, ConditionOperator.GreaterOrEqual, settings.MinSizeMb);
        Add(FilterField.AttachmentSize, ConditionOperator.LessOrEqual, settings.MaxSizeMb);
        Add(FilterField.AttachmentCount, ConditionOperator.GreaterOrEqual, settings.MinAttachmentCount);
        Add(FilterField.Category, ConditionOperator.Contains, settings.Category);

        if (!string.IsNullOrWhiteSpace(settings.Extension))
        {
            conditions.Add(FilterNode.NewCondition(
                FilterField.AttachmentExtension,
                ConditionOperator.In,
                settings.Extension.Trim()));
        }

        if (settings.OnlyWithAttachments)
        {
            conditions.Add(FilterNode.NewCondition(
                FilterField.HasAttachments,
                ConditionOperator.IsTrue));
        }

        if (settings.UnreadOnly)
        {
            conditions.Add(FilterNode.NewCondition(
                FilterField.IsRead,
                ConditionOperator.IsFalse));
        }

        if (!string.IsNullOrWhiteSpace(settings.Importance))
        {
            conditions.Add(FilterNode.NewCondition(
                FilterField.Importance,
                ConditionOperator.Equals,
                settings.Importance.Trim()));
        }

        return conditions;
    }
}
