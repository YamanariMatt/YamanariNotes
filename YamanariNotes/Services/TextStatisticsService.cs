using System.Text.RegularExpressions;
using System.Windows.Controls;
using YamanariNotes.Models;

namespace YamanariNotes.Services;

public sealed partial class TextStatisticsService
{
    public TextStatistics Calculate(TextBox textBox)
    {
        var text = textBox.Text;
        var characterCount = text.Length;
        var wordCount = WordRegex().Matches(text).Count;
        var line = textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex) + 1;
        var column = textBox.CaretIndex - textBox.GetCharacterIndexFromLineIndex(line - 1) + 1;

        return new TextStatistics(characterCount, wordCount, line, column);
    }

    [GeneratedRegex(@"\b[\p{L}\p{N}'-]+\b")]
    private static partial Regex WordRegex();
}
