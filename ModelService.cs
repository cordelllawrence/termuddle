using System.Net.Http.Headers;
using System.Text.Json;

namespace termuddle;

public class ModelService : IDisposable
{
    private readonly HttpClient _http;

    public ModelService(string baseUrl, string apiKey = "")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _http.Timeout = TimeSpan.FromSeconds(10);
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public void Dispose() => _http.Dispose();

    public async Task<List<string>> ListModelsAsync()
    {
        var response = await _http.GetAsync("/v1/models");
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var models = new List<string>();

        if (doc.RootElement.TryGetProperty("data", out var dataArray))
        {
            foreach (var model in dataArray.EnumerateArray())
            {
                if (model.TryGetProperty("id", out var id))
                    models.Add(id.GetString()!);
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
