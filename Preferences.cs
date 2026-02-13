using System.Text.Json;

namespace tuichat;

public class Preferences
{
    public string OllamaHost { get; set; } = "localhost";
    public string Model { get; set; } = "";
    public bool StreamResponses { get; set; } = true;

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
}
