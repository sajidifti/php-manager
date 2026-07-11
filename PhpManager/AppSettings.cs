using System.Text.Json;

namespace PhpManager;

public sealed class AppSettings
{
    public string PhpRoot { get; set; } = @"C:\laragon\bin\php";
    public string? SelectedPhpPath { get; set; }
    public List<string> QuickSwitchPaths { get; set; } = [];
    public bool UseUserPath { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;

    public static string AppDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhpManager");

    public static string SettingsPath => Path.Combine(AppDirectory, "settings.json");
    public static string RuntimeDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhpManager");
    public static string CurrentDirectory => Path.Combine(RuntimeDirectory, "current");
    public static string LegacyShimDirectory => Path.Combine(AppDirectory, "shim");
    public static string SelectionPath => Path.Combine(AppDirectory, "selected-php.txt");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
                settings.PhpRoot ??= @"C:\laragon\bin\php";
                settings.QuickSwitchPaths ??= [];
                return settings;
            }
        }
        catch
        {
            // Broken config should not stop the tray app from opening.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDirectory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SettingsPath, overwrite: true);

        if (!string.IsNullOrWhiteSpace(SelectedPhpPath))
        {
            File.WriteAllText(SelectionPath, SelectedPhpPath);
        }
        else if (File.Exists(SelectionPath))
        {
            File.Delete(SelectionPath);
        }
    }
}
