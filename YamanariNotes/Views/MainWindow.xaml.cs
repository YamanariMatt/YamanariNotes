using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using YamanariNotes.Helpers;
using YamanariNotes.Models;
using YamanariNotes.Services;
using Forms = System.Windows.Forms;

namespace YamanariNotes.Views;

public partial class MainWindow : Window
{
    private const string FileDialogFilter = "Text files (*.txt;*.md)|*.txt;*.md|All files (*.*)|*.*";

    private readonly FileService _fileService = new();
    private readonly SettingsService _settingsService = new();
    private readonly RecentFilesService _recentFilesService = new();
    private readonly TextStatisticsService _textStatisticsService = new();
    private readonly ThemeService _themeService = new();
    private readonly PrintService _printService = new();
    private readonly DispatcherTimer _autoSaveTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    private AppSettings _settings = new();
    private string? _currentFilePath;
    private bool _hasUnsavedChanges;
    private bool _isLoading;
    private bool _isClosingConfirmed;
    private FindReplaceWindow? _findReplaceWindow;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureShortcuts();
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();
        ApplyWindowSettings();
        ApplySettings();
        UpdateRecentFilesMenu();
        UpdateStatus();
        UpdateTitle();
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosingConfirmed)
        {
            CaptureSettings();
            await _settingsService.SaveAsync(_settings);
            return;
        }

        e.Cancel = true;

        if (await ConfirmContinueWithUnsavedChangesAsync())
        {
            _isClosingConfirmed = true;
            CaptureSettings();
            await _settingsService.SaveAsync(_settings);
            Close();
        }
    }

    private void ConfigureShortcuts()
    {
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => _ = NewFileAsync()), new KeyGesture(Key.N, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OpenFile()), new KeyGesture(Key.O, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => _ = SaveCurrentFileAsync()), new KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => SaveFileAs()), new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => PrintDocument()), new KeyGesture(Key.P, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ShowFindReplace(false)), new KeyGesture(Key.F, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => GoToLine()), new KeyGesture(Key.G, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ShowFindReplace(true)), new KeyGesture(Key.H, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => InsertDateTime()), new KeyGesture(Key.F5)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ResetZoom()), new KeyGesture(Key.D0, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ZoomIn()), new KeyGesture(Key.Add, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ZoomOut()), new KeyGesture(Key.Subtract, ModifierKeys.Control)));
    }

    private void ApplySettings()
    {
        EditorTextBox.FontFamily = new FontFamily(_settings.FontFamily);
        EditorTextBox.FontSize = _settings.FontSize * _settings.Zoom;
        EditorTextBox.TextWrapping = _settings.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        WordWrapMenuItem.IsChecked = _settings.WordWrap;
        AutoSaveMenuItem.IsChecked = _settings.AutoSave;
        StatusBarMenuItem.IsChecked = _settings.ShowStatusBar;
        EditorStatusBar.Visibility = _settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
        _themeService.Apply(this, EditorTextBox, EditorStatusBar, _settings.Theme);

        if (_settings.AutoSave)
        {
            _autoSaveTimer.Start();
        }
    }

    private void ApplyWindowSettings()
    {
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        if (_settings.WindowLeft.HasValue)
        {
            Left = _settings.WindowLeft.Value;
        }

        if (_settings.WindowTop.HasValue)
        {
            Top = _settings.WindowTop.Value;
        }

        if (_settings.IsWindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void CaptureSettings()
    {
        _settings.FontFamily = EditorTextBox.FontFamily.Source;
        _settings.FontSize = Math.Round(EditorTextBox.FontSize / _settings.Zoom, 2);
        _settings.WordWrap = WordWrapMenuItem.IsChecked == true;
        _settings.ShowStatusBar = StatusBarMenuItem.IsChecked == true;

        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
        }

        _settings.IsWindowMaximized = WindowState == WindowState.Maximized;
    }

    private async Task SaveSettingsAsync()
    {
        CaptureSettings();
        await _settingsService.SaveAsync(_settings);
    }

    private async Task<bool> ConfirmContinueWithUnsavedChangesAsync()
    {
        if (!_hasUnsavedChanges)
        {
            return true;
        }

        var result = MessageBox.Show(
            "Existem alteracoes nao salvas. Deseja salvar antes de continuar?",
            "Alteracoes nao salvas",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);

        return result switch
        {
            MessageBoxResult.Yes => await SaveCurrentFileAsync(),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private void MarkSaved(string? path = null)
    {
        _currentFilePath = path ?? _currentFilePath;
        _hasUnsavedChanges = false;
        UpdateTitle();
        UpdateStatus();
    }

    private void MarkUnsaved()
    {
        if (_isLoading)
        {
            return;
        }

        _hasUnsavedChanges = true;
        UpdateTitle();
        UpdateStatus();
    }

    private void UpdateTitle()
    {
        var fileName = string.IsNullOrWhiteSpace(_currentFilePath)
            ? "Sem titulo"
            : Path.GetFileName(_currentFilePath);

        Title = $"{(_hasUnsavedChanges ? "* " : string.Empty)}{fileName} - YamanariNotes";
    }

    private void UpdateStatus()
    {
        var stats = _textStatisticsService.Calculate(EditorTextBox);
        CharactersStatusText.Text = $"Caracteres: {stats.Characters}";
        WordsStatusText.Text = $"Palavras: {stats.Words}";
        LineColumnStatusText.Text = $"Linha {stats.Line}, Coluna {stats.Column}";
        SelectionStatusText.Text = EditorTextBox.SelectionLength > 0
            ? $"Selecionado: {EditorTextBox.SelectionLength}"
            : "Selecionado: 0";
        FileStateStatusText.Text = _hasUnsavedChanges ? "Nao salvo" : "Salvo";
        ZoomStatusText.Text = $"Zoom: {_settings.Zoom:P0}";
        FilePathStatusText.Text = string.IsNullOrWhiteSpace(_currentFilePath)
            ? "Arquivo: sem caminho"
            : $"Arquivo: {_currentFilePath}";
    }

    private void UpdateRecentFilesMenu()
    {
        RecentFilesMenuItem.Items.Clear();

        var existingFiles = _settings.RecentFiles.Where(File.Exists).ToList();
        _settings.RecentFiles = existingFiles;

        if (existingFiles.Count == 0)
        {
            RecentFilesMenuItem.Items.Add(new MenuItem { Header = "Nenhum arquivo recente", IsEnabled = false });
            return;
        }

        foreach (var file in existingFiles)
        {
            var item = new MenuItem { Header = file };
            item.Click += async (_, _) => await OpenFileAsync(file);
            RecentFilesMenuItem.Items.Add(item);
        }

        RecentFilesMenuItem.Items.Add(new Separator());
        var clearItem = new MenuItem { Header = "Limpar lista" };
        clearItem.Click += async (_, _) => await ClearRecentFilesAsync();
        RecentFilesMenuItem.Items.Add(clearItem);
    }

    private void AddRecentFile(string path)
    {
        _settings.RecentFiles = _recentFilesService.Add(path, _settings.RecentFiles);
        UpdateRecentFilesMenu();
    }

    private async Task ClearRecentFilesAsync()
    {
        _settings.RecentFiles.Clear();
        UpdateRecentFilesMenu();
        await SaveSettingsAsync();
    }

    private async Task NewFileAsync()
    {
        if (!await ConfirmContinueWithUnsavedChangesAsync())
        {
            return;
        }

        _isLoading = true;
        EditorTextBox.Clear();
        _currentFilePath = null;
        _isLoading = false;
        MarkSaved(null);
    }

    private async void OpenFile()
    {
        if (!await ConfirmContinueWithUnsavedChangesAsync())
        {
            return;
        }

        var dialog = new OpenFileDialog { Filter = FileDialogFilter };
        if (dialog.ShowDialog(this) == true)
        {
            await OpenFileAsync(dialog.FileName);
        }
    }

    private async Task OpenFileAsync(string path)
    {
        try
        {
            _isLoading = true;
            EditorTextBox.Text = await _fileService.ReadTextAsync(path);
            _isLoading = false;
            MarkSaved(path);
            AddRecentFile(path);
            await SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            _isLoading = false;
            MessageBox.Show($"Nao foi possivel abrir o arquivo.\n\n{ex.Message}", "Erro ao abrir", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> SaveCurrentFileAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return await SaveFileAsAsync();
        }

        return await SaveFileAsync(_currentFilePath);
    }

    private async Task<bool> SaveFileAsync(string path)
    {
        try
        {
            await _fileService.SaveTextAsync(path, EditorTextBox.Text);
            MarkSaved(path);
            AddRecentFile(path);
            await SaveSettingsAsync();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Nao foi possivel salvar o arquivo.\n\n{ex.Message}", "Erro ao salvar", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private async void SaveFileAs()
    {
        await SaveFileAsAsync();
    }

    private async Task<bool> SaveFileAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = FileDialogFilter,
            DefaultExt = ".txt",
            FileName = string.IsNullOrWhiteSpace(_currentFilePath) ? "novo-arquivo.txt" : Path.GetFileName(_currentFilePath)
        };

        if (dialog.ShowDialog(this) == true)
        {
            return await SaveFileAsync(dialog.FileName);
        }

        return false;
    }

    private void PrintDocument()
    {
        var documentName = string.IsNullOrWhiteSpace(_currentFilePath)
            ? "YamanariNotes"
            : Path.GetFileName(_currentFilePath);

        _printService.PrintText(EditorTextBox.Text, documentName, EditorTextBox.FontFamily, EditorTextBox.FontSize);
    }

    private bool TryEnsureSavedFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_currentFilePath) && File.Exists(_currentFilePath))
        {
            return true;
        }

        MessageBox.Show(
            "Salve o arquivo primeiro para usar esta acao.",
            "Arquivo sem caminho",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        return false;
    }

    private void CopyFilePath()
    {
        if (!TryEnsureSavedFilePath())
        {
            return;
        }

        Clipboard.SetText(_currentFilePath!);
    }

    private void OpenContainingFolder()
    {
        if (!TryEnsureSavedFilePath())
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{_currentFilePath}\"",
            UseShellExecute = true
        });
    }

    private void ShowFindReplace(bool replaceMode)
    {
        _findReplaceWindow?.Close();
        _findReplaceWindow = new FindReplaceWindow(replaceMode) { Owner = this };
        _findReplaceWindow.FindRequested += (_, args) => FindText(args);
        _findReplaceWindow.ReplaceRequested += (_, args) => ReplaceText(args);
        _findReplaceWindow.ReplaceAllRequested += (_, args) => ReplaceAllText(args);
        _findReplaceWindow.Closed += (_, _) => _findReplaceWindow = null;
        _findReplaceWindow.Show();
    }

    private void FindText(FindReplaceEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.FindText))
        {
            return;
        }

        var comparison = args.MatchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
        var startIndex = Math.Min(EditorTextBox.CaretIndex + EditorTextBox.SelectionLength, EditorTextBox.Text.Length);
        var index = EditorTextBox.Text.IndexOf(args.FindText, startIndex, comparison);

        if (index < 0 && startIndex > 0)
        {
            index = EditorTextBox.Text.IndexOf(args.FindText, 0, comparison);
        }

        if (index >= 0)
        {
            EditorTextBox.Focus();
            EditorTextBox.Select(index, args.FindText.Length);
        }
        else
        {
            MessageBox.Show("Texto nao encontrado.", "Localizar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ReplaceText(FindReplaceEventArgs args)
    {
        if (EditorTextBox.SelectionLength > 0)
        {
            var comparison = args.MatchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            var selectedText = EditorTextBox.SelectedText;

            if (string.Equals(selectedText, args.FindText, comparison))
            {
                EditorTextBox.SelectedText = args.ReplaceText;
            }
        }

        FindText(args);
    }

    private void ReplaceAllText(FindReplaceEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.FindText))
        {
            return;
        }

        var comparison = args.MatchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
        var source = EditorTextBox.Text;
        var index = source.IndexOf(args.FindText, comparison);

        while (index >= 0)
        {
            source = source.Remove(index, args.FindText.Length).Insert(index, args.ReplaceText);
            index = source.IndexOf(args.FindText, index + args.ReplaceText.Length, comparison);
        }

        EditorTextBox.Text = source;
    }

    private void InsertDateTime()
    {
        EditorTextBox.SelectedText = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        EditorTextBox.Focus();
    }

    private void GoToLine()
    {
        var currentLine = EditorTextBox.GetLineIndexFromCharacterIndex(EditorTextBox.CaretIndex) + 1;
        var dialog = new InputDialog("Ir para linha", "Digite o numero da linha:", currentLine.ToString())
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!int.TryParse(dialog.Value, out var requestedLine))
        {
            MessageBox.Show("Digite um numero de linha valido.", "Ir para linha", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var lineCount = Math.Max(1, EditorTextBox.LineCount);
        var targetLine = Math.Clamp(requestedLine, 1, lineCount);
        var characterIndex = EditorTextBox.GetCharacterIndexFromLineIndex(targetLine - 1);

        EditorTextBox.Focus();
        EditorTextBox.CaretIndex = characterIndex;
        EditorTextBox.ScrollToLine(targetLine - 1);
        UpdateStatus();
    }

    private void ChangeFont()
    {
        using var dialog = new Forms.FontDialog
        {
            Font = new System.Drawing.Font(EditorTextBox.FontFamily.Source, (float)Math.Max(8, EditorTextBox.FontSize / _settings.Zoom)),
            ShowEffects = false
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            EditorTextBox.FontFamily = new FontFamily(dialog.Font.FontFamily.Name);
            _settings.FontSize = dialog.Font.Size;
            EditorTextBox.FontSize = _settings.FontSize * _settings.Zoom;
        }
    }

    private void ChangeFontSize(double delta)
    {
        _settings.FontSize = Math.Clamp(_settings.FontSize + delta, 8, 48);
        EditorTextBox.FontSize = _settings.FontSize * _settings.Zoom;
        UpdateStatus();
    }

    private void ZoomIn()
    {
        _settings.Zoom = Math.Min(3, _settings.Zoom + 0.1);
        EditorTextBox.FontSize = _settings.FontSize * _settings.Zoom;
        UpdateStatus();
    }

    private void ZoomOut()
    {
        _settings.Zoom = Math.Max(0.5, _settings.Zoom - 0.1);
        EditorTextBox.FontSize = _settings.FontSize * _settings.Zoom;
        UpdateStatus();
    }

    private void ResetZoom()
    {
        _settings.Zoom = 1;
        EditorTextBox.FontSize = _settings.FontSize;
        UpdateStatus();
    }

    private async void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        if (_settings.AutoSave && _hasUnsavedChanges && !string.IsNullOrWhiteSpace(_currentFilePath))
        {
            await SaveFileAsync(_currentFilePath);
        }
    }

    private async void NewFile_Click(object sender, RoutedEventArgs e) => await NewFileAsync();
    private void OpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();
    private async void SaveFile_Click(object sender, RoutedEventArgs e) => await SaveCurrentFileAsync();
    private void SaveFileAs_Click(object sender, RoutedEventArgs e) => SaveFileAs();
    private void Print_Click(object sender, RoutedEventArgs e) => PrintDocument();
    private void CopyFilePath_Click(object sender, RoutedEventArgs e) => CopyFilePath();
    private void OpenContainingFolder_Click(object sender, RoutedEventArgs e) => OpenContainingFolder();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void Undo_Click(object sender, RoutedEventArgs e) => EditorTextBox.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => EditorTextBox.Redo();
    private void Cut_Click(object sender, RoutedEventArgs e) => EditorTextBox.Cut();
    private void Copy_Click(object sender, RoutedEventArgs e) => EditorTextBox.Copy();
    private void Paste_Click(object sender, RoutedEventArgs e) => EditorTextBox.Paste();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => EditorTextBox.SelectAll();
    private void InsertDateTime_Click(object sender, RoutedEventArgs e) => InsertDateTime();
    private void Find_Click(object sender, RoutedEventArgs e) => ShowFindReplace(false);
    private void Replace_Click(object sender, RoutedEventArgs e) => ShowFindReplace(true);
    private void GoToLine_Click(object sender, RoutedEventArgs e) => GoToLine();
    private void IncreaseFontSize_Click(object sender, RoutedEventArgs e) => ChangeFontSize(1);
    private void DecreaseFontSize_Click(object sender, RoutedEventArgs e) => ChangeFontSize(-1);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOut();
    private void ResetZoom_Click(object sender, RoutedEventArgs e) => ResetZoom();

    private async void WordWrap_Click(object sender, RoutedEventArgs e)
    {
        _settings.WordWrap = WordWrapMenuItem.IsChecked == true;
        EditorTextBox.TextWrapping = _settings.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        await SaveSettingsAsync();
    }

    private async void LightTheme_Click(object sender, RoutedEventArgs e)
    {
        _settings.Theme = "Light";
        _themeService.Apply(this, EditorTextBox, EditorStatusBar, _settings.Theme);
        await SaveSettingsAsync();
    }

    private async void DarkTheme_Click(object sender, RoutedEventArgs e)
    {
        _settings.Theme = "Dark";
        _themeService.Apply(this, EditorTextBox, EditorStatusBar, _settings.Theme);
        await SaveSettingsAsync();
    }

    private async void ToggleStatusBar_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowStatusBar = StatusBarMenuItem.IsChecked == true;
        EditorStatusBar.Visibility = _settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
        await SaveSettingsAsync();
    }

    private async void AutoSave_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoSave = AutoSaveMenuItem.IsChecked == true;

        if (_settings.AutoSave)
        {
            _autoSaveTimer.Start();
        }
        else
        {
            _autoSaveTimer.Stop();
        }

        await SaveSettingsAsync();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        MarkUnsaved();
        UpdateStatus();
    }

    private void EditorTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateStatus();
    }
}
