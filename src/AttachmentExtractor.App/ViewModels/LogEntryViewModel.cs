using AttachmentExtractor.App.Services;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.ViewModels;

public sealed class LogEntryViewModel
{
    public LogEntryViewModel(LogLevel level, string message)
    {
        Level = level;
        Message = message;
    }

    public LogLevel Level { get; }

    public string Message { get; }

    public DateTime Timestamp { get; } = DateTime.Now;

    public string TimeText => Formatters.FormatClock(Timestamp);

    public string LevelText => Level switch
    {
        LogLevel.Success => "موفق",
        LogLevel.Warning => "هشدار",
        LogLevel.Error => "خطا",
        _ => "اطلاع"
    };
}
