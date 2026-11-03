using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using AttachmentExtractor.App.Services;
using AttachmentExtractor.App.ViewModels;
using AttachmentExtractor.Core.Services;
using AttachmentExtractor.Outlook;

namespace AttachmentExtractor.App;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureCulture();
        ConfigureExceptionHandling();
        base.OnStartup(e);

        var dialogs = new DialogService();
        _viewModel = new MainViewModel(
            new OutlookMailSource(),
            new SettingsStore(),
            new ProfileStore(),
            dialogs);

        var window = new MainWindow
        {
            DataContext = _viewModel
        };

        MainWindow = window;
        window.Show();

        _ = _viewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("fa-IR").Clone();
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    private void ConfigureExceptionHandling()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteErrorLog(e.Exception);
        _viewModel?.ReportUnexpectedError(e.Exception);

        MessageBox.Show(
            MainWindow,
            "خطای پیش‌بینی‌نشده‌ای رخ داد. برنامه به کار خود ادامه می‌دهد.\n\n" + e.Exception.Message,
            "خطای برنامه",
            MessageBoxButton.OK,
            MessageBoxImage.Error,
            MessageBoxResult.OK,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var message = e.ExceptionObject is Exception exception
            ? exception.Message
            : "خطای ناشناخته";

        if (e.ExceptionObject is Exception ex)
        {
            WriteErrorLog(ex);
        }

        MessageBox.Show(
            "خطای جدی رخ داد و برنامه باید بسته شود.\n\n" + message,
            "خطای برنامه",
            MessageBoxButton.OK,
            MessageBoxImage.Error,
            MessageBoxResult.OK,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    private static void WriteErrorLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AttachmentExtractor");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "error.log"),
                $"[{DateTime.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
