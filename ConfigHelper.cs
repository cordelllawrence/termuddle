using System.Text.Json;

namespace termuddle;

public class ConfigHelper
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
        Path.Combine(AppContext.BaseDirectory, "termuddle-config.json");

    public static ConfigHelper? Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ConfigHelper>(json);
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
        var fileName = $"termuddle-config_backup_{timestamp}.json";
        var backupPath = Path.Combine(AppContext.BaseDirectory, fileName);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(backupPath, json);
        return fileName;
    }

    public static List<(string FileName, ConfigHelper Config)> ListBackups()
    {
        var dir = AppContext.BaseDirectory;
        var files = Directory.GetFiles(dir, "termuddle-config_backup_*.json")
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        var result = new List<(string, ConfigHelper)>();
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var config = JsonSerializer.Deserialize<ConfigHelper>(json);
            if (config is not null)
                result.Add((Path.GetFileName(file), config));
        }
        return result;
    }

    public static ConfigHelper? LoadBackup(int index)
    {
        var backups = ListBackups();
        if (index < 1 || index > backups.Count)
            return null;
        return backups[index - 1].Config;
    }

    public static bool RemoveBackup(int index)
    {
        var dir = AppContext.BaseDirectory;
        var files = Directory.GetFiles(dir, "termuddle-config_backup_*.json")
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        if (index < 1 || index > files.Count)
            return false;

        File.Delete(files[index - 1]);
        return true;
    }
}
