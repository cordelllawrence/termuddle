using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace termuddle;

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

public static partial class WebFetchTool
{
    private const int MaxContentLength = 20_000;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(30);

    private static readonly Regex ScriptStyleRegex =
        new(@"<(script|style)[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    [GeneratedRegex(@"<(br|p|div|h[1-6]|li|tr)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockElementRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessNewlinesRegex();

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
            using var cts = new CancellationTokenSource(FetchTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("termuddle/1.0");
            request.Headers.Accept.ParseAdd("text/html, text/plain, application/json");

            using var response = await WebSearchTool.SharedHttp.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cts.Token);
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
        html = ScriptStyleRegex.Replace(html, "");
        html = BlockElementRegex().Replace(html, "\n");
        html = HtmlTagRegex().Replace(html, "");
        html = System.Net.WebUtility.HtmlDecode(html);
        html = WhitespaceRegex().Replace(html, " ");
        html = ExcessNewlinesRegex().Replace(html, "\n\n");
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
