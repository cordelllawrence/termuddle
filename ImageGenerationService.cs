using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace termuddle;

/// <summary>
/// Calls Ollama's native /api/generate endpoint for text-to-image models.
/// Uses raw HTTP instead of OllamaSharp so we can surface server-side error details.
/// </summary>
public class ImageGenerationService : IDisposable
{
    private readonly HttpClient _http;

    public ImageGenerationService(string baseUrl)
    {
        // Talk to the Ollama root (e.g. http://localhost:11434), not /v1
        var origin = new Uri(baseUrl).GetLeftPart(UriPartial.Authority);
        _http = new HttpClient { BaseAddress = new Uri(origin) };
        _http.Timeout = TimeSpan.FromMinutes(10); // image gen can be very slow
    }

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Generates an image from a text prompt with step-by-step progress.
    /// Returns the saved file path.
    /// </summary>
    public async Task<string> GenerateImageAsync(string prompt, string model, Action<long, long>? onProgress = null, string? outputDir = null)
    {
        var requestBody = JsonSerializer.Serialize(new { model, prompt, stream = true });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // For non-streaming errors (immediate 500s), read the body for details
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            var serverError = TryExtractErrorMessage(errorBody);
            throw new HttpRequestException(
                $"Image generation failed ({(int)response.StatusCode} {response.ReasonPhrase}): {serverError}");
        }

        // Stream the response — Ollama sends newline-delimited JSON chunks
        string? imageBase64 = null;
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            GenerateResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<GenerateResponse>(line);
            }
            catch (JsonException)
            {
                continue; // skip malformed chunks
            }

            if (chunk is null)
                continue;

            // Check for inline errors (Ollama can return {"error":"..."} mid-stream)
            if (chunk.Error is not null)
                throw new InvalidOperationException($"Server error during image generation: {chunk.Error}");

            if (chunk.CompletedSteps is > 0 && chunk.TotalSteps is > 0)
                onProgress?.Invoke(chunk.CompletedSteps.Value, chunk.TotalSteps.Value);

            if (chunk.Image is not null)
                imageBase64 = chunk.Image;
        }

        if (string.IsNullOrEmpty(imageBase64))
            throw new InvalidOperationException("No image data returned from the server. Check Ollama logs for details (journalctl -u ollama).");

        var imageBytes = Convert.FromBase64String(imageBase64);
        var extension = DetectImageFormat(imageBytes);
        var fileName = BuildFileName(prompt, extension);
        var saveDir = outputDir ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(saveDir);
        var filePath = Path.Combine(saveDir, fileName);

        await File.WriteAllBytesAsync(filePath, imageBytes);
        return filePath;
    }

    /// <summary>
    /// Tries to extract "error" field from an Ollama JSON error response,
    /// falling back to the raw body.
    /// </summary>
    private static string TryExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorProp))
                return errorProp.GetString() ?? body;
        }
        catch { /* not JSON, return raw */ }
        return string.IsNullOrWhiteSpace(body) ? "(no details in response)" : body;
    }

    private static string BuildFileName(string prompt, string extension)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        var slug = SanitizeForFileName(prompt);
        if (slug.Length > 0)
        {
            if (slug.Length > 40)
                slug = slug[..40].TrimEnd('-', '_');
            return $"{timestamp}_{slug}{extension}";
        }

        return $"{timestamp}_generated{extension}";
    }

    private static string SanitizeForFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (Array.IndexOf(invalidChars, c) >= 0 || c == ' ')
                sb.Append('_');
            else
                sb.Append(c);
        }

        var result = sb.ToString();
        while (result.Contains("__"))
            result = result.Replace("__", "_");
        return result.Trim('_');
    }

    private static string DetectImageFormat(byte[] data)
    {
        if (data.Length < 4) return ".bin";

        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return ".png";
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return ".jpg";
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return ".gif";
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return ".webp";
        if (data[0] == 0x42 && data[1] == 0x4D)
            return ".bmp";

        return ".png";
    }

    private record GenerateResponse
    {
        [JsonPropertyName("image")]
        public string? Image { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("completed")]
        public long? CompletedSteps { get; init; }

        [JsonPropertyName("total")]
        public long? TotalSteps { get; init; }

        [JsonPropertyName("done")]
        public bool Done { get; init; }
    }
}
