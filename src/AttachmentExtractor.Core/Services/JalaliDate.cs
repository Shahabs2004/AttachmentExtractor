using System.Globalization;
using AttachmentExtractor.Core.Utilities;

namespace AttachmentExtractor.Core.Services;

public static class JalaliDate
{
    private static readonly PersianCalendar Calendar = new();
    private static readonly CultureInfo PersianCulture = CreatePersianCulture();

    public static (int Year, int Month, int Day) ToJalali(DateTime dateTime)
    {
        return (Calendar.GetYear(dateTime), Calendar.GetMonth(dateTime), Calendar.GetDayOfMonth(dateTime));
    }

    public static string Format(DateTime dateTime, string format = "yyyy/MM/dd HH:mm")
    {
        var year = Calendar.GetYear(dateTime);
        var month = Calendar.GetMonth(dateTime);
        var day = Calendar.GetDayOfMonth(dateTime);

        var result = format
            .Replace("yyyy", year.ToString("0000", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("yy", (year % 100).ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("MMMM", PersianCulture.DateTimeFormat.GetMonthName(month), StringComparison.Ordinal)
            .Replace("MMM", PersianCulture.DateTimeFormat.GetAbbreviatedMonthName(month), StringComparison.Ordinal)
            .Replace("MM", month.ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("dd", day.ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("HH", dateTime.Hour.ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("mm", dateTime.Minute.ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("ss", dateTime.Second.ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal);

        return result;
    }

    public static string Format(DateTime? dateTime, string format = "yyyy/MM/dd HH:mm")
    {
        return dateTime.HasValue ? Format(dateTime.Value, format) : string.Empty;
    }

    public static string FormatPath(DateTime dateTime, string format = "yyyy-MM")
    {
        return Format(dateTime, format);
    }

    public static DateTime? Parse(string? text)
    {
        return ValueParsers.TryParseDate(text, out var value, out _) ? value : null;
    }

    public static string TodayJalali()
    {
        return Format(DateTime.Today, "yyyy/MM/dd");
    }

    public static (string From, string To) LastDays(int days)
    {
        var today = DateTime.Today;
        var from = today.AddDays(-(days - 1));
        return (Format(from, "yyyy/MM/dd"), Format(today, "yyyy/MM/dd"));
    }

    public static (string From, string To) CurrentYear()
    {
        var today = DateTime.Today;
        var year = Calendar.GetYear(today);
        var from = Calendar.ToDateTime(year, 1, 1, 0, 0, 0, 0);
        return (Format(from, "yyyy/MM/dd"), Format(today, "yyyy/MM/dd"));
    }

    public static (string From, string To) PreviousYear()
    {
        var today = DateTime.Today;
        var year = Calendar.GetYear(today) - 1;
        var from = Calendar.ToDateTime(year, 1, 1, 0, 0, 0, 0);
        var to = Calendar.ToDateTime(year, 12, Calendar.GetDaysInMonth(year, 12), 23, 59, 59, 0);
        return (Format(from, "yyyy/MM/dd"), Format(to, "yyyy/MM/dd"));
    }

    private static CultureInfo CreatePersianCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo("fa-IR");
        }
        catch
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
