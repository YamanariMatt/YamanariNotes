namespace YamanariNotes.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Light";
    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 16;
    public bool WordWrap { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool AutoSave { get; set; }
    public bool IsReadOnly { get; set; }
    public double Zoom { get; set; } = 1;
    public double WindowWidth { get; set; } = 1080;
    public double WindowHeight { get; set; } = 720;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool IsWindowMaximized { get; set; }
    public List<string> RecentFiles { get; set; } = [];
}
