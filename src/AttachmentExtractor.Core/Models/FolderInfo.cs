namespace AttachmentExtractor.Core.Models;

public sealed class FolderInfo
{
    public string Name { get; set; } = string.Empty;

    public string EntryId { get; set; } = string.Empty;

    public string StoreId { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public int Depth { get; set; }

    public List<FolderInfo> Children { get; set; } = new();
}
