using System.Diagnostics;
using System.IO;
using System.Windows;
using AttachmentExtractor.App.ViewModels;
using AttachmentExtractor.App.Views;
using AttachmentExtractor.Core.Models;
using Microsoft.Win32;

namespace AttachmentExtractor.App.Services;

public sealed class DialogService : IDialogService
{
    private const MessageBoxOptions RtlOptions =
        MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    private static Window? Owner => Application.Current?.MainWindow;

    public string? PickFolder(string title, string? initialPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public string? PickOpenFile(string title, string filter, string? initialPath = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(
        string title,
        string filter,
        string defaultFileName,
        string? initialPath = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public void ShowInfo(string message, string title = "اطلاعات")
    {
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title = "هشدار")
    {
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string title = "خطا")
    {
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool Confirm(string message, string title = "تأیید")
    {
        return Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public string? Prompt(string title, string message, string defaultValue = "")
    {
        var dialog = new InputDialog(title, message, defaultValue)
        {
            Owner = Owner
        };

        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    public FilterNode? EditRule(FilterNode current)
    {
        var viewModel = new RuleBuilderViewModel(current);
        var window = new RuleBuilderWindow
        {
            DataContext = viewModel,
            Owner = Owner
        };

        return window.ShowDialog() == true ? window.Result : null;
    }

    public void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            ShowWarning("مسیر مقصد وجود ندارد. ابتدا یک مسیر معتبر انتخاب کنید.", "مسیر نامعتبر");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError($"باز کردن پوشه ممکن نشد: {ex.Message}", "خطا");
        }
    }

    private static MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton button,
        MessageBoxImage image,
        MessageBoxResult defaultResult = MessageBoxResult.OK)
    {
        return Owner is { } owner
            ? MessageBox.Show(owner, message, title, button, image, defaultResult, RtlOptions)
            : MessageBox.Show(message, title, button, image, defaultResult, RtlOptions);
    }
}
