using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Services;

public static class OutputPathBuilder
{
    public static string BuildRelativeFolder(ExportOptions options, MailMessageInfo message)
    {
        var folder = options.Structure switch
        {
            OutputStructure.Flat => string.Empty,
            OutputStructure.BySender => FileNameSanitizer.SanitizeFolderSegment(
                !string.IsNullOrWhiteSpace(message.SenderEmail) ? message.SenderEmail : message.SenderName,
                "UnknownSender"),
            OutputStructure.ByDate => BuildDateFolder(message),
            OutputStructure.ByFolder => FileNameSanitizer.SanitizeRelativePath(message.FolderPath),
            OutputStructure.Custom => PathTemplateRenderer.RenderFolderTemplate(
                options.CustomFolderTemplate,
                message),
            _ => string.Empty
        };

        return folder.Trim('\\', '/');
    }

    public static string BuildFileName(
        ExportOptions options,
        MailMessageInfo message,
        AttachmentInfo attachment)
    {
        return PathTemplateRenderer.RenderFileNameTemplate(
            options.FileNameTemplate,
            message,
            attachment);
    }

    public static string BuildFullPath(
        ExportOptions options,
        MailMessageInfo message,
        AttachmentInfo attachment)
    {
        if (string.IsNullOrWhiteSpace(options.DestinationRoot))
        {
            throw new InvalidOperationException("Destination root is empty.");
        }

        var root = Path.GetFullPath(options.DestinationRoot);
        var relativeFolder = BuildRelativeFolder(options, message);
        var fileName = BuildFileName(options, message, attachment);

        var fullPath = string.IsNullOrWhiteSpace(relativeFolder)
            ? Path.Combine(root, fileName)
            : Path.Combine(root, relativeFolder, fileName);

        return Path.GetFullPath(fullPath);
    }

    private static string BuildDateFolder(MailMessageInfo message)
    {
        var date = message.ReceivedTime ?? message.SentOn ?? DateTime.Now;
        var year = JalaliDate.Format(date, "yyyy");
        var month = JalaliDate.Format(date, "MM");
        return Path.Combine(year, month);
    }
}
