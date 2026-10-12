using System.IO;
using System.Text.Json;
using XWhat.Core;

namespace XWhat.App.Services;

public sealed class SettingsStore
{
    public string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XWhat");
    private string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public string LoadWarning { get; private set; } = "";

    public MonitorSettings Load()
    {
        try
        {
            return File.Exists(FilePath) ? (JsonSerializer.Deserialize<MonitorSettings>(File.ReadAllText(FilePath)) ?? new()).Normalize() : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LoadWarning = "Не удалось прочитать настройки; применены стандартные.";
            return new();
        }
    }

    public void Save(MonitorSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalize(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, overwrite: true);
    }

    public void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, "diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
