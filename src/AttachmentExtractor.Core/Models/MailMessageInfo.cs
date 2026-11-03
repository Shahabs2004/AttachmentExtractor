namespace AttachmentExtractor.Core.Models;

public sealed class MailMessageInfo
{
    public string ScanId { get; set; } = Guid.NewGuid().ToString("N");

    public string EntryId { get; set; } = string.Empty;

    public string StoreId { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public string Cc { get; set; } = string.Empty;

    public DateTime? ReceivedTime { get; set; }

    public DateTime? SentOn { get; set; }

    public long SizeBytes { get; set; }

    public bool IsRead { get; set; }

    public int Importance { get; set; } = 1;

    public string Categories { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public List<AttachmentInfo> Attachments { get; set; } = new();

    public int AttachmentCount => Attachments.Count;

    public bool HasAttachments => Attachments.Count > 0;

    public string SenderDomain => ExtractDomain(SenderEmail);

    public static string ExtractDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1
            ? email[(at + 1)..].Trim().TrimEnd('.')
            : string.Empty;
    }
}
