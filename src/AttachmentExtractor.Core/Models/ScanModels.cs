namespace AttachmentExtractor.Core.Models;

public sealed class ScanRequest
{
    public List<FolderInfo> Folders { get; set; } = new();

    public FilterNode Filter { get; set; } = FilterNode.NewGroup();

    public bool IncludeBody { get; set; }
}

public sealed class ScanProgress
{
    public int TotalItems { get; set; }

    public int ScannedItems { get; set; }

    public int MatchedItems { get; set; }

    public int FoldersDone { get; set; }

    public int FoldersTotal { get; set; }

    public string CurrentFolder { get; set; } = string.Empty;
}

public sealed class ScanResult
{
    public List<MailMessageInfo> Messages { get; set; } = new();

    public int ScannedItems { get; set; }

    public int FolderCount { get; set; }

    public bool Canceled { get; set; }

    public List<string> Errors { get; set; } = new();
}
