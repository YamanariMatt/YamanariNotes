using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace YamanariNotes.Services;

public sealed class PrintService
{
    public void PrintText(string content, string documentName, FontFamily fontFamily, double fontSize)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var document = new FlowDocument(new Paragraph(new Run(content)))
        {
            FontFamily = fontFamily,
            FontSize = fontSize,
            PagePadding = new Thickness(48),
            ColumnWidth = dialog.PrintableAreaWidth
        };

        document.PageHeight = dialog.PrintableAreaHeight;
        document.PageWidth = dialog.PrintableAreaWidth;

        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, documentName);
    }
}
