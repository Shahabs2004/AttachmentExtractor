using System.Windows;

namespace AttachmentExtractor.App.Views;

public partial class InputDialog : Window
{
    public InputDialog(string title, string message, string defaultValue)
    {
        InitializeComponent();

        Title = title;
        HeaderText.Text = message;
        InputBox.Text = defaultValue;

        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string Value => InputBox.Text.Trim();

    private void InputBox_TextChanged(object sender, RoutedEventArgs e)
    {
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(InputBox.Text);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
        {
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
