using System.ComponentModel;
using System.Windows;
using AttachmentExtractor.App.ViewModels;

namespace AttachmentExtractor.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.IsBusy)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "عملیاتی در حال اجراست. آیا می‌خواهید برنامه را ببندید؟",
            "خروج از برنامه",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }
    }
}
