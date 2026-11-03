using System.Globalization;
using System.Text.RegularExpressions;

namespace AttachmentExtractor.Core.Utilities;

public static class ValueParsers
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static bool TryParseNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return double.TryParse(
            TextNormalizer.NormalizeNumberText(text),
            NumberStyles.Float,
            Invariant,
            out value);
    }

    public static bool TryParseSizeBytes(string? text, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = TextNormalizer.NormalizeNumberText(text).ToLowerInvariant();
        double multiplier = 1024d * 1024d;

        if (normalized.Contains("gigabyte") || normalized.Contains("gb") || normalized.Contains("گیگ"))
        {
            multiplier = 1024d * 1024d * 1024d;
        }
        else if (normalized.Contains("megabyte") || normalized.Contains("mb") || normalized.Contains("مگ"))
        {
            multiplier = 1024d * 1024d;
        }
        else if (normalized.Contains("kilobyte") || normalized.Contains("kb") || normalized.Contains("کیلو"))
        {
            multiplier = 1024d;
        }
        else if (normalized.Contains("byte") || normalized.Contains("بایت") || normalized.EndsWith('b'))
        {
            multiplier = 1d;
        }

        var number = new string(normalized
            .Where(ch => char.IsDigit(ch) || ch is '.' or '-' or '+')
            .ToArray());

        if (!double.TryParse(number, NumberStyles.Float, Invariant, out var parsed))
        {
            return false;
        }

        bytes = (long)Math.Round(parsed * multiplier, MidpointRounding.AwayFromZero);
        return true;
    }

    public static bool TryParseDate(string? text, out DateTime value, out bool hasTime)
    {
        value = default;
        hasTime = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = TextNormalizer.NormalizeDigits(text).Trim();
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var datePart = parts.Length > 0 ? parts[0] : normalized;
        var timePart = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;

        hasTime = timePart.Contains(':');
        TryParseTime(timePart, out var hour, out var minute, out var second);

        var dateParts = datePart.Split(new[] { '/', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (dateParts.Length == 3 &&
            int.TryParse(dateParts[0], NumberStyles.Integer, Invariant, out var year) &&
            int.TryParse(dateParts[1], NumberStyles.Integer, Invariant, out var month) &&
            int.TryParse(dateParts[2], NumberStyles.Integer, Invariant, out var day))
        {
            if (year is >= 1200 and < 1700)
            {
                try
                {
                    var calendar = new PersianCalendar();
                    value = calendar.ToDateTime(year, month, day, hour, minute, second, 0);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                value = new DateTime(year, month, day, hour, minute, second);
                return true;
            }
            catch
            {
                return false;
            }
        }

        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd",
            "yyyyMMdd",
            "yyyyMMdd HHmm"
        };

        if (DateTime.TryParseExact(normalized, formats, Invariant, DateTimeStyles.AllowWhiteSpaces, out value))
        {
            hasTime = normalized.Contains(':');
            return true;
        }

        if (DateTime.TryParse(normalized, Invariant, DateTimeStyles.AllowWhiteSpaces, out value))
        {
            hasTime = normalized.Contains(':');
            return true;
        }

        return false;
    }

    public static bool TryParseBool(string? text, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = TextNormalizer.NormalizeDigits(text).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "1":
            case "true":
            case "yes":
            case "y":
            case "on":
            case "بله":
            case "دارد":
            case "فعال":
            case "درست":
                value = true;
                return true;
            case "0":
            case "false":
            case "no":
            case "n":
            case "off":
            case "خیر":
            case "ندارد":
            case "غیرفعال":
            case "نادرست":
                value = false;
                return true;
            default:
                return false;
        }
    }

    public static bool TryParseImportance(string? text, out int value)
    {
        value = 1;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = TextNormalizer.NormalizeDigits(text).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "2":
            case "high":
            case "بالا":
            case "زیاد":
            case "مهم":
                value = 2;
                return true;
            case "1":
            case "normal":
            case "معمولی":
            case "متوسط":
                value = 1;
                return true;
            case "0":
            case "low":
            case "کم":
            case "پایین":
                value = 0;
                return true;
            default:
                return false;
        }
    }

    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var value = extension.Trim().Trim('"', '\'', '«', '»');
        var slash = value.LastIndexOfAny(new[] { '\\', '/' });
        if (slash >= 0)
        {
            value = value[(slash + 1)..];
        }

        value = value.ToLowerInvariant();
        if (value == ".")
        {
            return string.Empty;
        }

        return value.StartsWith('.') ? value : "." + value;
    }

    public static List<string> SplitList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        return text
            .Split(new[] { ',', '،', ';', '؛' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();
    }

    public static bool IsRegexValid(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryParseTime(string text, out int hour, out int minute, out int second)
    {
        hour = 0;
        minute = 0;
        second = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && int.TryParse(parts[0], NumberStyles.Integer, Invariant, out var parsedHour))
        {
            hour = Math.Clamp(parsedHour, 0, 23);
        }

        if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, Invariant, out var parsedMinute))
        {
            minute = Math.Clamp(parsedMinute, 0, 59);
        }

        if (parts.Length > 2 && int.TryParse(parts[2], NumberStyles.Integer, Invariant, out var parsedSecond))
        {
            second = Math.Clamp(parsedSecond, 0, 59);
        }
    }
}
