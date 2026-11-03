using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Services;

public static class PathTemplateRenderer
{
    private static readonly Regex TokenRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

    public static string RenderFolderTemplate(string? template, MailMessageInfo message)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var rendered = TokenRegex.Replace(template, match =>
        {
            var token = match.Groups[1].Value.Trim();
            if (token.Equals("Folder", StringComparison.OrdinalIgnoreCase))
            {
                return FileNameSanitizer.SanitizeRelativePath(message.FolderPath);
            }

            var value = ResolveToken(token, message, null);
            var name = GetTokenName(token);
            return IsDateToken(name)
                ? value
                : FileNameSanitizer.SanitizeFolderSegment(value, string.Empty);
        });

        return FileNameSanitizer.SanitizeRelativePath(rendered);
    }

    public static string RenderFileNameTemplate(
        string? template,
        MailMessageInfo message,
        AttachmentInfo attachment)
    {
        var originalName = FileNameSanitizer.SanitizeFileName(
            attachment.EffectiveName,
            $"attachment-{attachment.Index}");
        var originalExtension = attachment.EffectiveExtension;

        if (string.IsNullOrWhiteSpace(template))
        {
            template = "{OriginalName}";
        }

        var hasExtensionToken =
            template.Contains("{Extension}", StringComparison.OrdinalIgnoreCase) ||
            template.Contains("{Ext}", StringComparison.OrdinalIgnoreCase);

        var rendered = TokenRegex.Replace(template, match =>
            ResolveToken(match.Groups[1].Value.Trim(), message, attachment));

        rendered = rendered.Trim().TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(rendered))
        {
            rendered = Path.GetFileNameWithoutExtension(originalName);
        }

        if (!hasExtensionToken &&
            !string.IsNullOrEmpty(originalExtension) &&
            !Path.HasExtension(rendered))
        {
            rendered += originalExtension;
        }

        return FileNameSanitizer.SanitizeFileName(rendered, originalName);
    }

    private static string ResolveToken(
        string token,
        MailMessageInfo message,
        AttachmentInfo? attachment)
    {
        var separator = token.IndexOf(':');
        var name = separator >= 0 ? token[..separator].Trim() : token.Trim();
        var format = separator >= 0 ? token[(separator + 1)..].Trim() : string.Empty;

        return name.ToLowerInvariant() switch
        {
            "originalname" => attachment?.EffectiveName ?? string.Empty,
            "basename" => Path.GetFileNameWithoutExtension(attachment?.EffectiveName ?? string.Empty),
            "extension" => attachment?.EffectiveExtension ?? string.Empty,
            "ext" => (attachment?.EffectiveExtension ?? string.Empty).TrimStart('.'),
            "sender" => message.SenderName,
            "senderemail" => message.SenderEmail,
            "senderdomain" => message.SenderDomain,
            "subject" => message.Subject,
            "folder" => Path.GetFileName(message.FolderPath.TrimEnd('\\', '/')),
            "date" => FormatDate(message, format, jalali: false),
            "jdate" => FormatDate(message, format, jalali: true),
            "jalalidate" => FormatDate(message, format, jalali: true),
            "index" => attachment?.Index.ToString() ?? string.Empty,
            "messagekey" => GetMessageKey(message),
            "year" => (message.ReceivedTime ?? DateTime.Now).Year.ToString("0000", CultureInfo.InvariantCulture),
            "month" => (message.ReceivedTime ?? DateTime.Now).Month.ToString("00", CultureInfo.InvariantCulture),
            "day" => (message.ReceivedTime ?? DateTime.Now).Day.ToString("00", CultureInfo.InvariantCulture),
            "jyear" => JalaliDate.ToJalali(message.ReceivedTime ?? DateTime.Now).Year.ToString("0000", CultureInfo.InvariantCulture),
            "jmonth" => JalaliDate.ToJalali(message.ReceivedTime ?? DateTime.Now).Month.ToString("00", CultureInfo.InvariantCulture),
            "jday" => JalaliDate.ToJalali(message.ReceivedTime ?? DateTime.Now).Day.ToString("00", CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    private static string FormatDate(MailMessageInfo message, string format, bool jalali)
    {
        var date = message.ReceivedTime ?? message.SentOn ?? DateTime.Now;
        if (string.IsNullOrWhiteSpace(format))
        {
            format = jalali ? "yyyy/MM/dd" : "yyyy-MM-dd";
        }

        return jalali ? JalaliDate.Format(date, format) : date.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string GetTokenName(string token)
    {
        var separator = token.IndexOf(':');
        return (separator >= 0 ? token[..separator] : token).Trim();
    }

    private static bool IsDateToken(string tokenName)
    {
        return tokenName.Equals("date", StringComparison.OrdinalIgnoreCase) ||
               tokenName.Equals("jdate", StringComparison.OrdinalIgnoreCase) ||
               tokenName.Equals("jalalidate", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMessageKey(MailMessageInfo message)
    {
        var source = !string.IsNullOrWhiteSpace(message.EntryId) ? message.EntryId : message.ScanId;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }
}
