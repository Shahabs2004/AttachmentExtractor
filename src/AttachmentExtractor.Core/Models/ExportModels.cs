namespace AttachmentExtractor.Core.Models;

public sealed class ExportItem
{
    public MailMessageInfo Message { get; set; } = new();

    public List<int> AttachmentIndexes { get; set; } = new();
}

public sealed class ExportProgress
{
    public int TotalAttachments { get; set; }

    public int ProcessedAttachments { get; set; }

    public int SavedAttachments { get; set; }

    public int SkippedAttachments { get; set; }

    public int FailedAttachments { get; set; }

    public long BytesSaved { get; set; }

    public string CurrentFile { get; set; } = string.Empty;
}

public sealed class ExportManifestEntry
{
    public string Status { get; set; } = string.Empty;

    public DateTime? ReceivedTime { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;

    public string AttachmentName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string SavedPath { get; set; } = string.Empty;

    public string Error { get; set; } = string.Empty;
}

public sealed class ExportResult
{
    public int TotalAttachments { get; set; }

    public int Saved { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }

    public long BytesSaved { get; set; }

    public bool Canceled { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime FinishedAt { get; set; }

    public string? ManifestCsvPath { get; set; }

    public string? ManifestJsonPath { get; set; }

    public List<ExportManifestEntry> Entries { get; set; } = new();
}
