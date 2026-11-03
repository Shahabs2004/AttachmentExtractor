namespace AttachmentExtractor.Core.Models;

public sealed class AppSettings
{
    public string LastDestination { get; set; } = string.Empty;

    public OutputStructure Structure { get; set; } = OutputStructure.Flat;

    public string CustomFolderTemplate { get; set; } = "{SenderEmail}\\{JalaliDate:yyyy-MM}";

    public string FileNameTemplate { get; set; } = "{OriginalName}";

    public CollisionPolicy CollisionPolicy { get; set; } = CollisionPolicy.Rename;

    public bool CreateManifest { get; set; } = true;

    public bool PreserveTimestamps { get; set; } = true;

    public bool OpenFolderAfterExport { get; set; } = true;

    public bool IncludeSubfolders { get; set; } = true;

    public bool OnlyWithAttachments { get; set; } = true;

    public string LastProfileName { get; set; } = string.Empty;
}
