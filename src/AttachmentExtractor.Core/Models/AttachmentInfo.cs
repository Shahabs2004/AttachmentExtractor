namespace AttachmentExtractor.Core.Models;

public sealed class AttachmentInfo
{
    public int Index { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public bool IsInline { get; set; }

    public bool IsHidden { get; set; }

    public int AttachmentType { get; set; }

    public string Extension { get; set; } = string.Empty;

    public string EffectiveName =>
        !string.IsNullOrWhiteSpace(FileName) ? FileName :
        !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : string.Empty;

    public string EffectiveExtension =>
        !string.IsNullOrWhiteSpace(Extension)
            ? Extension
            : Path.GetExtension(EffectiveName);
}
