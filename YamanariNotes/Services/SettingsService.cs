using System.IO;
using System.Text.Json;
using YamanariNotes.Models;

namespace YamanariNotes.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public SettingsService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YamanariNotes");

        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions) ?? new AppSettings();
            return Normalize(settings);
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, Normalize(settings), JsonOptions);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.Theme = settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        settings.FontFamily = string.IsNullOrWhiteSpace(settings.FontFamily) ? "Consolas" : settings.FontFamily;
        settings.FontSize = Math.Clamp(settings.FontSize, 8, 48);
        settings.Zoom = Math.Clamp(settings.Zoom, 0.5, 3);
        settings.WindowWidth = Math.Clamp(settings.WindowWidth, 720, 3840);
        settings.WindowHeight = Math.Clamp(settings.WindowHeight, 480, 2160);
        settings.RecentFiles = settings.RecentFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        return settings;
    }
}
