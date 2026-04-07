using System.IO;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace termuddle;

public class ChatSession
{
    public ApiProvider Provider { get; set; } = ApiProvider.Auto;
    public ConfigHelper Preferences { get; set; } = new();

    public string BaseUrl
    {
        get => Preferences.BaseUrl;
        set => Preferences.BaseUrl = value;
    }

    public string ApiKey
    {
        get => Preferences.ApiKey;
        set => Preferences.ApiKey = value;
    }

    public string ModelName
    {
        get => Preferences.Model;
        set => Preferences.Model = value;
    }

    public bool StreamResponses
    {
        get => Preferences.StreamResponses;
        set => Preferences.StreamResponses = value;
    }

    public bool ShowTps
    {
        get => Preferences.ShowTps;
        set => Preferences.ShowTps = value;
    }

    public IChatClient ChatClient { get; set; } = null!;
    public List<ChatMessage> History { get; } = new();
    public ChatOptions ChatOptions { get; } = new()
    {
        Tools = [..WebSearchTool.CreateTools(), ..WebFetchTool.CreateTools(), ..DateTimeTool.CreateTools()]
    };

    public void Reconnect()
    {
        var rawClient = CreateChatClient(Provider, BaseUrl, ApiKey, ModelName);
        ChatClient = new ChatClientBuilder(rawClient)
            .UseFunctionInvocation()
            .Build();
    }

    public static IChatClient CreateChatClient(ApiProvider provider, string baseUrl, string apiKey, string model)
    {
        if (provider == ApiProvider.Ollama)
        {
            var origin = new Uri(baseUrl).GetLeftPart(UriPartial.Authority);
            return new OllamaApiClient(new Uri(origin), model);
        }

        var endpoint = new Uri(baseUrl);
        var credential = new System.ClientModel.ApiKeyCredential(
            string.IsNullOrEmpty(apiKey) ? "no-key" : apiKey);
        var client = new OpenAIClient(credential, new OpenAI.OpenAIClientOptions
        {
            Endpoint = endpoint
        });
        return client.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// Creates a ChatMessage with text and optional file attachments.
    /// Images are sent as DataContent; other files are inlined as text.
    /// </summary>
    public static ChatMessage CreateMessageWithAttachments(string text, string[] filePaths)
    {
        var contents = new List<AIContent>();

        foreach (var filePath in filePaths)
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Attachment not found: {filePath}");

            var mediaType = GetMediaType(fullPath);
            if (mediaType.StartsWith("image/"))
            {
                var data = File.ReadAllBytes(fullPath);
                contents.Add(new DataContent(data, mediaType));
            }
            else
            {
                // For non-image files, include as text with a filename header
                var fileText = File.ReadAllText(fullPath);
                contents.Add(new TextContent($"[File: {Path.GetFileName(fullPath)}]\n{fileText}"));
            }
        }

        contents.Add(new TextContent(text));

        return new ChatMessage(ChatRole.User, contents);
    }

    /// <summary>
    /// Scans response contents for non-text data (images, files) and saves them to temp files.
    /// Returns a list of saved file paths.
    /// </summary>
    public static List<string> SaveResponseAttachments(IEnumerable<AIContent> contents)
    {
        var savedFiles = new List<string>();
        var outputDir = Path.Combine(Path.GetTempPath(), "termuddle");
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        foreach (var content in contents)
        {
            if (content is DataContent data && data.Data is { } bytes && bytes.Length > 0)
            {
                Directory.CreateDirectory(outputDir);
                var ext = GetExtensionForMediaType(data.MediaType);
                var fileName = $"{timestamp}_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(outputDir, fileName);
                File.WriteAllBytes(filePath, bytes.ToArray());
                savedFiles.Add(filePath);
            }
        }

        return savedFiles;
    }

    private static string GetExtensionForMediaType(string? mediaType)
    {
        return mediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/svg+xml" => ".svg",
            "application/pdf" => ".pdf",
            "application/json" => ".json",
            "application/xml" => ".xml",
            "text/csv" => ".csv",
            "text/markdown" => ".md",
            "text/html" => ".html",
            "text/plain" => ".txt",
            "audio/mpeg" => ".mp3",
            "audio/wav" => ".wav",
            "video/mp4" => ".mp4",
            _ => ".bin",
        };
    }

    private static string GetMediaType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".csv" => "text/csv",
            ".md" => "text/markdown",
            ".html" or ".htm" => "text/html",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };
    }
}
