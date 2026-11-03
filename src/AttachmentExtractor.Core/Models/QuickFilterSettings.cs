namespace AttachmentExtractor.Core.Models;

public sealed class QuickFilterSettings
{
    public string DateFrom { get; set; } = string.Empty;

    public string DateTo { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string SenderDomain { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string MinSizeMb { get; set; } = string.Empty;

    public string MaxSizeMb { get; set; } = string.Empty;

    public string MinAttachmentCount { get; set; } = string.Empty;

    public bool OnlyWithAttachments { get; set; } = true;

    public bool UnreadOnly { get; set; }

    public string Importance { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}
