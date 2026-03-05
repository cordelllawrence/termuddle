using System.Text.Json;

namespace termuddle;

public class Preferences
{
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public bool StreamResponses { get; set; } = true;
    public bool ShowTps { get; set; } = false;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static readonly string DefaultPath =
        Path.Combine(AppContext.BaseDirectory, "preferences.json");

    public static Preferences? Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Preferences>(json);
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    public string Backup()
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var fileName = $"preferences_backup_{timestamp}.json";
        var backupPath = Path.Combine(AppContext.BaseDirectory, fileName);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(backupPath, json);
        return fileName;
    }

    public static List<(string FileName, Preferences Prefs)> ListBackups()
    {
        var dir = AppContext.BaseDirectory;
        var files = Directory.GetFiles(dir, "preferences_backup_*.json")
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        var result = new List<(string, Preferences)>();
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var prefs = JsonSerializer.Deserialize<Preferences>(json);
            if (prefs is not null)
                result.Add((Path.GetFileName(file), prefs));
        }
        return result;
    }

    public static Preferences? LoadBackup(int index)
    {
        var backups = ListBackups();
        if (index < 1 || index > backups.Count)
            return null;
        return backups[index - 1].Prefs;
    }

    public static bool RemoveBackup(int index)
    {
        var dir = AppContext.BaseDirectory;
        var files = Directory.GetFiles(dir, "preferences_backup_*.json")
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        if (index < 1 || index > files.Count)
            return false;

        File.Delete(files[index - 1]);
        return true;
    }
}
