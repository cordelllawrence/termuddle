using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace tuichat;

public static class WebSearchTool
{
    private const string DuckDuckGoEndpoint = "https://api.duckduckgo.com/";
    private static readonly HttpClient _http = new();

    [Description("Searches the web using DuckDuckGo and returns the top results.")]
    public static async Task<string> SearchAsync(
        [Description("The search query")] string query,
        [Description("Maximum number of results to return")] int topN = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Error: query must not be empty.";

        var url = $"{DuckDuckGoEndpoint}?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";

        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<DuckDuckGoResponse>();

        var results = new List<SearchResult>();
        foreach (var topic in raw?.RelatedTopics ?? [])
        {
            if (topic.Topics is { Count: > 0 })
            {
                foreach (var sub in topic.Topics)
                    results.Add(ConvertTopic(sub));
            }
            else
            {
                results.Add(ConvertTopic(topic));
            }

            if (results.Count >= topN) break;
        }

        if (results.Count == 0)
            return "No results found.";

        var sb = new System.Text.StringBuilder();
        foreach (var r in results)
        {
            sb.AppendLine($"- {r.Title}");
            if (!string.IsNullOrWhiteSpace(r.Url))
                sb.AppendLine($"  {r.Url}");
            if (!string.IsNullOrWhiteSpace(r.Snippet))
                sb.AppendLine($"  {r.Snippet}");
        }
        return sb.ToString();
    }

    public static IList<AITool> CreateTools()
    {
        return [AIFunctionFactory.Create(SearchAsync)];
    }

    internal static HttpClient SharedHttp => _http;

    private static SearchResult ConvertTopic(DuckDuckGoTopic topic) => new()
    {
        Title = topic.Text?.Split(" - ")[0] ?? string.Empty,
        Url = topic.FirstUrl ?? string.Empty,
        Snippet = topic.Text ?? string.Empty,
    };

    private class SearchResult
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
    }

    private class DuckDuckGoResponse
    {
        [JsonPropertyName("RelatedTopics")]
        public List<DuckDuckGoTopic> RelatedTopics { get; set; } = new();
    }

    private class DuckDuckGoTopic
    {
        [JsonPropertyName("FirstURL")]
        public string? FirstUrl { get; set; }

        [JsonPropertyName("Text")]
        public string? Text { get; set; }

        [JsonPropertyName("Topics")]
        public List<DuckDuckGoTopic>? Topics { get; set; }
    }
}

public static class WebFetchTool
{
    private const int MaxContentLength = 20_000;

    [Description("Fetches the content of a web page at the given URL and returns it as plain text. Useful for reading articles, documentation, or any web page.")]
    public static async Task<string> FetchAsync(
        [Description("The URL to fetch")] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "Error: URL must not be empty.";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return "Error: Invalid URL. Must be an absolute HTTP or HTTPS URL.";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("tuichat/1.0");
            request.Headers.Accept.ParseAdd("text/html, text/plain, application/json");

            using var response = await WebSearchTool.SharedHttp.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

            if (contentType.Contains("html"))
                content = StripHtmlTags(content);

            if (content.Length > MaxContentLength)
                content = content[..MaxContentLength] + "\n\n[Content truncated]";

            return content;
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching URL: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "Error: Request timed out.";
        }
    }

    public static IList<AITool> CreateTools()
    {
        return [AIFunctionFactory.Create(FetchAsync)];
    }

    private static string StripHtmlTags(string html)
    {
        // Remove script and style blocks entirely
        html = System.Text.RegularExpressions.Regex.Replace(
            html, @"<(script|style)[^>]*>[\s\S]*?</\1>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Replace common block elements with newlines
        html = System.Text.RegularExpressions.Regex.Replace(
            html, @"<(br|p|div|h[1-6]|li|tr)[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Strip remaining tags
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", "");

        // Decode common HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Collapse whitespace
        html = System.Text.RegularExpressions.Regex.Replace(html, @"[ \t]+", " ");
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\n{3,}", "\n\n");

        return html.Trim();
    }
}

public static class DateTimeTool
{
    [Description("Returns the current date and time in the user's local timezone. Use this when you need to know the current time or date.")]
    public static string GetCurrentDateTime()
    {
        var now = DateTimeOffset.Now;
        return $"{now:dddd, MMMM d, yyyy h:mm:ss tt} ({now:zzz} UTC)";
    }

    public static IList<AITool> CreateTools()
    {
        return [AIFunctionFactory.Create(GetCurrentDateTime)];
    }
}
