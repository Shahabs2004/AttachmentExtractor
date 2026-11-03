using System.Globalization;
using AttachmentExtractor.Core.Services;

namespace AttachmentExtractor.App.Services;

public static class Formatters
{
    private static readonly char[] PersianDigits =
    {
        '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'
    };

    public static string ToPersianDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var buffer = value.ToCharArray();
        for (var index = 0; index < buffer.Length; index++)
        {
            if (buffer[index] is >= '0' and <= '9')
            {
                buffer[index] = PersianDigits[buffer[index] - '0'];
            }
        }

        return new string(buffer);
    }

    public static string FormatCount(long value)
    {
        return ToPersianDigits(
            value.ToString("N0", CultureInfo.InvariantCulture)
                .Replace(',', '٬'));
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "۰ بایت";
        }

        string[] units = { "بایت", "کیلوبایت", "مگابایت", "گیگابایت", "ترابایت" };
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var number = unitIndex == 0 || size >= 100
            ? size.ToString("0", CultureInfo.InvariantCulture)
            : size.ToString("0.#", CultureInfo.InvariantCulture);

        number = ToPersianDigits(number).Replace('.', '٫');
        return $"{number} {units[unitIndex]}";
    }

    public static string FormatJalaliDate(DateTime? dateTime)
    {
        return dateTime.HasValue
            ? ToPersianDigits(JalaliDate.Format(dateTime.Value, "yyyy/MM/dd"))
            : "—";
    }

    public static string FormatJalaliDateTime(DateTime? dateTime)
    {
        return dateTime.HasValue
            ? ToPersianDigits(JalaliDate.Format(dateTime.Value, "yyyy/MM/dd HH:mm"))
            : "—";
    }

    public static string FormatClock(DateTime dateTime)
    {
        return ToPersianDigits(dateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }

    public static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return text[..maxLength].TrimEnd() + " …";
    }
}
