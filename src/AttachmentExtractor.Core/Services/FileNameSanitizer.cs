namespace AttachmentExtractor.Core.Services;

public static class FileNameSanitizer
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string SanitizeFileName(string? name, string fallback = "attachment", int maxLength = 120)
    {
        var value = SanitizeCore(name, maxLength);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = fallback;
        }

        return TruncatePreservingExtension(value, maxLength);
    }

    public static string SanitizeFolderSegment(string? name, string fallback = "folder", int maxLength = 80)
    {
        var value = SanitizeCore(name, maxLength);
        if (string.IsNullOrWhiteSpace(value) || value is "." or "..")
        {
            value = fallback;
        }

        return value.Length > maxLength ? value[..maxLength].TrimEnd('.', ' ') : value;
    }

    public static string SanitizeRelativePath(string? path, int segmentMaxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var segments = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => SanitizeFolderSegment(segment))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        return segments.Count == 0 ? string.Empty : string.Join('\\', segments);
    }

    private static string SanitizeCore(string? name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsControl(ch) || invalid.Contains(ch))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(ch);
            }
        }

        var value = builder.ToString();
        while (value.Contains("__", StringComparison.Ordinal))
        {
            value = value.Replace("__", "_", StringComparison.Ordinal);
        }

        value = value.Trim().TrimEnd('.', ' ');

        if (ReservedNames.Contains(value))
        {
            value = "_" + value;
        }

        if (value.Length > maxLength)
        {
            value = value[..maxLength].TrimEnd('.', ' ');
        }

        return value;
    }

    private static string TruncatePreservingExtension(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var extension = Path.GetExtension(value);
        if (string.IsNullOrEmpty(extension) || extension.Length >= maxLength)
        {
            return value[..maxLength];
        }

        var baseName = Path.GetFileNameWithoutExtension(value);
        var allowedBaseLength = Math.Max(1, maxLength - extension.Length);
        if (baseName.Length > allowedBaseLength)
        {
            baseName = baseName[..allowedBaseLength];
        }

        return baseName + extension;
    }
}
