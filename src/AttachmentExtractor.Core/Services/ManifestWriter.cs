using System.Text;
using System.Text.Json;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Services;

public static class ManifestWriter
{
    public static (string CsvPath, string JsonPath) Write(
        string destinationRoot,
        ExportOptions options,
        ExportResult result)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            throw new InvalidOperationException("مسیر مقصد خالی است.");
        }

        Directory.CreateDirectory(destinationRoot);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var csvPath = Path.Combine(destinationRoot, $"attachment-manifest-{stamp}.csv");
        var jsonPath = Path.Combine(destinationRoot, $"attachment-manifest-{stamp}.json");

        WriteCsv(csvPath, result.Entries);
        WriteJson(jsonPath, options, result);

        return (csvPath, jsonPath);
    }

    private static void WriteCsv(string path, IReadOnlyList<ExportManifestEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ردیف,وضعیت,تاریخ دریافت,فرستنده,ایمیل فرستنده,موضوع,مسیر پوشه,نام پیوست,اندازه (بایت),مسیر ذخیره,خطا");

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            builder.Append(i + 1).Append(',');
            builder.Append(EscapeCsv(entry.Status)).Append(',');
            builder.Append(EscapeCsv(JalaliDate.Format(entry.ReceivedTime, "yyyy/MM/dd HH:mm"))).Append(',');
            builder.Append(EscapeCsv(entry.SenderName)).Append(',');
            builder.Append(EscapeCsv(entry.SenderEmail)).Append(',');
            builder.Append(EscapeCsv(entry.Subject)).Append(',');
            builder.Append(EscapeCsv(entry.FolderPath)).Append(',');
            builder.Append(EscapeCsv(entry.AttachmentName)).Append(',');
            builder.Append(entry.SizeBytes).Append(',');
            builder.Append(EscapeCsv(entry.SavedPath)).Append(',');
            builder.Append(EscapeCsv(entry.Error));
            builder.AppendLine();
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
    }

    private static void WriteJson(string path, ExportOptions options, ExportResult result)
    {
        var payload = new
        {
            Summary = new
            {
                result.TotalAttachments,
                result.Saved,
                result.Skipped,
                result.Failed,
                result.BytesSaved,
                result.Canceled,
                result.StartedAt,
                result.FinishedAt,
                Destination = options.DestinationRoot,
                Structure = options.Structure.ToString(),
                options.FileNameTemplate,
                options.CollisionPolicy
            },
            Entries = result.Entries
        };

        var json = JsonSerializer.Serialize(payload, JsonFileStore.Options);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
    }
}
