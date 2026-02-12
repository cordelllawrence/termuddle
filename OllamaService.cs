using System.Text.Json;

namespace tuichat;

public class OllamaService
{
    private readonly HttpClient _http;

    public OllamaService(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<List<string>> ListModelsAsync()
    {
        var response = await _http.GetAsync("/api/tags");
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var models = new List<string>();

        if (doc.RootElement.TryGetProperty("models", out var modelsArray))
        {
            foreach (var model in modelsArray.EnumerateArray())
            {
                if (model.TryGetProperty("name", out var name))
                    models.Add(name.GetString()!);
            }
        }

        return models;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            await ListModelsAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
