using System.Windows;

namespace YamanariNotes.Views;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptTextBlock.Text = prompt;
        ValueTextBox.Text = initialValue;
        ValueTextBox.SelectAll();
        ValueTextBox.Focus();
    }

    public string Value => ValueTextBox.Text;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
