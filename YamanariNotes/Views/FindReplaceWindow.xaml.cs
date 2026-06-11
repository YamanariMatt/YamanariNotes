using System.Windows;

namespace YamanariNotes.Views;

public partial class FindReplaceWindow : Window
{
    public event EventHandler<FindReplaceEventArgs>? FindRequested;
    public event EventHandler<FindReplaceEventArgs>? ReplaceRequested;
    public event EventHandler<FindReplaceEventArgs>? ReplaceAllRequested;

    public FindReplaceWindow(bool replaceMode)
    {
        InitializeComponent();
        ReplaceTextBox.IsEnabled = replaceMode;
        Title = replaceMode ? "Substituir texto" : "Localizar texto";
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        FindRequested?.Invoke(this, CreateArgs());
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        ReplaceRequested?.Invoke(this, CreateArgs());
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        ReplaceAllRequested?.Invoke(this, CreateArgs());
    }

    private FindReplaceEventArgs CreateArgs()
    {
        return new FindReplaceEventArgs(FindTextBox.Text, ReplaceTextBox.Text, MatchCaseCheckBox.IsChecked == true);
    }
}

public sealed record FindReplaceEventArgs(string FindText, string ReplaceText, bool MatchCase);
