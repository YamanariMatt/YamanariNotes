using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
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
    private readonly DispatcherTimer _autoSaveTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    private AppSettings _settings = new();
    private string? _currentFilePath;
    private bool _hasUnsavedChanges;
    private bool _isLoading;
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
        ApplySettings();
        UpdateRecentFilesMenu();
        UpdateStatus();
        UpdateTitle();
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        CaptureSettings();
        await _settingsService.SaveAsync(_settings);
    }

    private void ConfigureShortcuts()
    {
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => NewFile()), new KeyGesture(Key.N, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => OpenFile()), new KeyGesture(Key.O, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => SaveFile()), new KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => SaveFileAs()), new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ShowFindReplace(false)), new KeyGesture(Key.F, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(_ => ShowFindReplace(true)), new KeyGesture(Key.H, ModifierKeys.Control)));
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

    private void CaptureSettings()
    {
        _settings.FontFamily = EditorTextBox.FontFamily.Source;
        _settings.FontSize = Math.Round(EditorTextBox.FontSize / _settings.Zoom, 2);
        _settings.WordWrap = WordWrapMenuItem.IsChecked == true;
        _settings.ShowStatusBar = StatusBarMenuItem.IsChecked == true;
    }

    private async Task SaveSettingsAsync()
    {
        CaptureSettings();
        await _settingsService.SaveAsync(_settings);
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_hasUnsavedChanges)
        {
            return true;
        }

        var result = MessageBox.Show(
            "Existem alteracoes nao salvas. Deseja descarta-las?",
            "Alteracoes nao salvas",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
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
        FileStateStatusText.Text = _hasUnsavedChanges ? "Nao salvo" : "Salvo";
        ZoomStatusText.Text = $"Zoom: {_settings.Zoom:P0}";
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
    }

    private void AddRecentFile(string path)
    {
        _settings.RecentFiles = _recentFilesService.Add(path, _settings.RecentFiles);
        UpdateRecentFilesMenu();
    }

    private void NewFile()
    {
        if (!ConfirmDiscardChanges())
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
        if (!ConfirmDiscardChanges())
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

    private async void SaveFile()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            await SaveFileAsAsync();
            return;
        }

        await SaveFileAsync(_currentFilePath);
    }

    private async Task SaveFileAsync(string path)
    {
        try
        {
            await _fileService.SaveTextAsync(path, EditorTextBox.Text);
            MarkSaved(path);
            AddRecentFile(path);
            await SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Nao foi possivel salvar o arquivo.\n\n{ex.Message}", "Erro ao salvar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveFileAs()
    {
        await SaveFileAsAsync();
    }

    private async Task SaveFileAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = FileDialogFilter,
            DefaultExt = ".txt",
            FileName = string.IsNullOrWhiteSpace(_currentFilePath) ? "novo-arquivo.txt" : Path.GetFileName(_currentFilePath)
        };

        if (dialog.ShowDialog(this) == true)
        {
            await SaveFileAsync(dialog.FileName);
        }
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

    private void NewFile_Click(object sender, RoutedEventArgs e) => NewFile();
    private void OpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();
    private void SaveFile_Click(object sender, RoutedEventArgs e) => SaveFile();
    private void SaveFileAs_Click(object sender, RoutedEventArgs e) => SaveFileAs();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void Undo_Click(object sender, RoutedEventArgs e) => EditorTextBox.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => EditorTextBox.Redo();
    private void Cut_Click(object sender, RoutedEventArgs e) => EditorTextBox.Cut();
    private void Copy_Click(object sender, RoutedEventArgs e) => EditorTextBox.Copy();
    private void Paste_Click(object sender, RoutedEventArgs e) => EditorTextBox.Paste();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => EditorTextBox.SelectAll();
    private void Find_Click(object sender, RoutedEventArgs e) => ShowFindReplace(false);
    private void Replace_Click(object sender, RoutedEventArgs e) => ShowFindReplace(true);
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

    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }
}
