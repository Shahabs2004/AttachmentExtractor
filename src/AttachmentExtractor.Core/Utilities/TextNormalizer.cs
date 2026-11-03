using System.Text;

namespace AttachmentExtractor.Core.Utilities;

public static class TextNormalizer
{
    private const string PersianDigits = "۰۱۲۳۴۵۶۷۸۹";
    private const string ArabicDigits = "٠١٢٣٤٥٦٧٨٩";

    public static string NormalizeDigits(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var persianIndex = PersianDigits.IndexOf(ch);
            if (persianIndex >= 0)
            {
                builder.Append((char)('0' + persianIndex));
                continue;
            }

            var arabicIndex = ArabicDigits.IndexOf(ch);
            if (arabicIndex >= 0)
            {
                builder.Append((char)('0' + arabicIndex));
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    public static string NormalizeNumberText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return NormalizeDigits(text)
            .Replace('٫', '.')
            .Replace('٬', '\0')
            .Replace(",", string.Empty)
            .Replace("\0", string.Empty)
            .Trim();
    }

    public static string CollapseWhitespace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
