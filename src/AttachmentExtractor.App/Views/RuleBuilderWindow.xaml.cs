using System.Windows;
using AttachmentExtractor.App.ViewModels;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.Views;

public partial class RuleBuilderWindow : Window
{
    public RuleBuilderWindow()
    {
        InitializeComponent();
    }

    public FilterNode? Result { get; private set; }

    private void RuleTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is RuleBuilderViewModel viewModel)
        {
            viewModel.SelectedNode = e.NewValue as RuleNodeViewModel;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RuleBuilderViewModel viewModel)
        {
            return;
        }

        if (viewModel.TryBuild(out var root, out var error, out var invalidNode))
        {
            Result = root;
            DialogResult = true;
            return;
        }

        if (invalidNode is not null)
        {
            viewModel.SelectedNode = invalidNode;
        }

        MessageBox.Show(
            this,
            error,
            "فیلتر نامعتبر",
            MessageBoxButton.OK,
            MessageBoxImage.Warning,
            MessageBoxResult.OK,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
