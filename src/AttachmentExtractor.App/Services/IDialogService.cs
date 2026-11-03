using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.Services;

public interface IDialogService
{
    string? PickFolder(string title, string? initialPath);

    string? PickOpenFile(string title, string filter, string? initialPath = null);

    string? PickSaveFile(string title, string filter, string defaultFileName, string? initialPath = null);

    void ShowInfo(string message, string title = "اطلاعات");

    void ShowWarning(string message, string title = "هشدار");

    void ShowError(string message, string title = "خطا");

    bool Confirm(string message, string title = "تأیید");

    string? Prompt(string title, string message, string defaultValue = "");

    FilterNode? EditRule(FilterNode current);

    void OpenFolder(string path);
}
