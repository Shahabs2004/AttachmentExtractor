using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Filters;

public sealed record FilterFieldDescriptor(
    FilterField Field,
    string Label,
    FilterInputKind InputKind,
    IReadOnlyList<ConditionOperator> Operators,
    string Hint);

public static class FilterFieldCatalog
{
    private static readonly ConditionOperator[] TextOperators =
    {
        ConditionOperator.Contains,
        ConditionOperator.NotContains,
        ConditionOperator.Equals,
        ConditionOperator.NotEquals,
        ConditionOperator.StartsWith,
        ConditionOperator.EndsWith,
        ConditionOperator.Regex,
        ConditionOperator.In,
        ConditionOperator.NotIn
    };

    private static readonly ConditionOperator[] DateOperators =
    {
        ConditionOperator.Before,
        ConditionOperator.After,
        ConditionOperator.GreaterOrEqual,
        ConditionOperator.LessOrEqual,
        ConditionOperator.Between,
        ConditionOperator.Equals,
        ConditionOperator.NotEquals
    };

    private static readonly ConditionOperator[] NumberOperators =
    {
        ConditionOperator.Equals,
        ConditionOperator.NotEquals,
        ConditionOperator.GreaterThan,
        ConditionOperator.LessThan,
        ConditionOperator.GreaterOrEqual,
        ConditionOperator.LessOrEqual,
        ConditionOperator.Between
    };

    private static readonly ConditionOperator[] BooleanOperators =
    {
        ConditionOperator.IsTrue,
        ConditionOperator.IsFalse,
        ConditionOperator.Equals,
        ConditionOperator.NotEquals
    };

    private static readonly ConditionOperator[] ImportanceOperators =
    {
        ConditionOperator.Equals,
        ConditionOperator.NotEquals,
        ConditionOperator.In,
        ConditionOperator.NotIn,
        ConditionOperator.GreaterThan,
        ConditionOperator.LessThan
    };

    private static readonly ConditionOperator[] ExtensionOperators =
    {
        ConditionOperator.Contains,
        ConditionOperator.NotContains,
        ConditionOperator.Equals,
        ConditionOperator.NotEquals,
        ConditionOperator.StartsWith,
        ConditionOperator.EndsWith,
        ConditionOperator.In,
        ConditionOperator.NotIn
    };

    public static IReadOnlyList<FilterFieldDescriptor> All { get; } = new List<FilterFieldDescriptor>
    {
        new(FilterField.Subject, "موضوع", FilterInputKind.Text, TextOperators, "بخشی از متن موضوع"),
        new(FilterField.Body, "متن ایمیل", FilterInputKind.Text, TextOperators, "جست‌وجو در بدنه ایمیل؛ در پوشه‌های بزرگ کند است"),
        new(FilterField.Sender, "فرستنده", FilterInputKind.Text, TextOperators, "نام یا ایمیل فرستنده"),
        new(FilterField.SenderName, "نام فرستنده", FilterInputKind.Text, TextOperators, "نام نمایشی فرستنده"),
        new(FilterField.SenderEmail, "ایمیل فرستنده", FilterInputKind.Text, TextOperators, "آدرس ایمیل فرستنده"),
        new(FilterField.SenderDomain, "دامنه فرستنده", FilterInputKind.Text, TextOperators, "مثال: company.com"),
        new(FilterField.Recipient, "گیرنده (To/CC)", FilterInputKind.Text, TextOperators, "جست‌وجو در گیرنده اصلی و رونوشت"),
        new(FilterField.To, "گیرنده اصلی (To)", FilterInputKind.Text, TextOperators, "گیرنده اصلی ایمیل"),
        new(FilterField.Cc, "رونوشت (CC)", FilterInputKind.Text, TextOperators, "گیرندگان رونوشت"),
        new(FilterField.FolderPath, "مسیر پوشه", FilterInputKind.Text, TextOperators, "مسیر پوشه Outlook"),
        new(FilterField.ReceivedDate, "تاریخ دریافت", FilterInputKind.Date, DateOperators, "شمسی: 1404/08/11 — میلادی: 2025-11-02"),
        new(FilterField.SentDate, "تاریخ ارسال", FilterInputKind.Date, DateOperators, "شمسی: 1404/08/11 — میلادی: 2025-11-02"),
        new(FilterField.AttachmentName, "نام پیوست", FilterInputKind.Text, TextOperators, "نام فایل پیوست"),
        new(FilterField.AttachmentExtension, "پسوند پیوست", FilterInputKind.Extension, ExtensionOperators, "مثال: pdf, .xlsx"),
        new(FilterField.AttachmentSize, "اندازه پیوست", FilterInputKind.Size, NumberOperators, "مثال: 2.5 MB یا 500 KB"),
        new(FilterField.AttachmentCount, "تعداد پیوست", FilterInputKind.Count, NumberOperators, "مثال: 1"),
        new(FilterField.HasAttachments, "دارای پیوست", FilterInputKind.Boolean, BooleanOperators, "بدون مقدار"),
        new(FilterField.IsRead, "خوانده شده", FilterInputKind.Boolean, BooleanOperators, "بدون مقدار"),
        new(FilterField.Importance, "اهمیت", FilterInputKind.Importance, ImportanceOperators, "Low / Normal / High یا 0/1/2"),
        new(FilterField.Category, "دسته‌بندی", FilterInputKind.Text, TextOperators, "دسته‌بندی Outlook"),
        new(FilterField.MessageSize, "اندازه ایمیل", FilterInputKind.Size, NumberOperators, "مثال: 5 MB")
    };

    private static readonly Dictionary<FilterField, FilterFieldDescriptor> ByField =
        All.ToDictionary(item => item.Field);

    public static FilterFieldDescriptor Get(FilterField field) =>
        ByField.TryGetValue(field, out var descriptor) ? descriptor : All[0];

    public static IReadOnlyList<ConditionOperator> GetOperators(FilterField field) =>
        Get(field).Operators;

    public static ConditionOperator GetDefaultOperator(FilterField field) =>
        Get(field).Operators.FirstOrDefault();

    public static bool IsAttachmentField(FilterField field) =>
        field is FilterField.AttachmentName or FilterField.AttachmentExtension or FilterField.AttachmentSize;

    public static bool IsDateField(FilterField field) =>
        field is FilterField.ReceivedDate or FilterField.SentDate;

    public static bool IsBooleanField(FilterField field) =>
        field is FilterField.HasAttachments or FilterField.IsRead;

    public static bool IsImportanceField(FilterField field) =>
        field == FilterField.Importance;

    public static bool IsSizeField(FilterField field) =>
        field is FilterField.AttachmentSize or FilterField.MessageSize;

    public static bool IsCountField(FilterField field) =>
        field == FilterField.AttachmentCount;

    public static bool IsNumericField(FilterField field) =>
        IsSizeField(field) || IsCountField(field) || IsImportanceField(field);

    public static bool RequiresValue(ConditionOperator conditionOperator) =>
        conditionOperator is not ConditionOperator.IsTrue and not ConditionOperator.IsFalse;

    public static bool IsNegativeOperator(ConditionOperator conditionOperator) =>
        conditionOperator is ConditionOperator.NotContains or ConditionOperator.NotEquals or ConditionOperator.NotIn;

    public static ConditionOperator ToPositiveOperator(ConditionOperator conditionOperator) =>
        conditionOperator switch
        {
            ConditionOperator.NotContains => ConditionOperator.Contains,
            ConditionOperator.NotEquals => ConditionOperator.Equals,
            ConditionOperator.NotIn => ConditionOperator.In,
            _ => conditionOperator
        };
}
