using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YamanariNotes.Services;

public sealed class ThemeService
{
    public void Apply(Window window, TextBox editor, StatusBar statusBar, string theme)
    {
        var isDark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
        var background = isDark ? Color.FromRgb(30, 32, 36) : Colors.White;
        var foreground = isDark ? Color.FromRgb(232, 236, 240) : Color.FromRgb(28, 31, 35);
        var chrome = isDark ? Color.FromRgb(42, 45, 51) : Color.FromRgb(245, 247, 250);

        window.Background = new SolidColorBrush(chrome);
        editor.Background = new SolidColorBrush(background);
        editor.Foreground = new SolidColorBrush(foreground);
        editor.CaretBrush = new SolidColorBrush(foreground);
        statusBar.Background = new SolidColorBrush(chrome);
        statusBar.Foreground = new SolidColorBrush(foreground);
    }
}
