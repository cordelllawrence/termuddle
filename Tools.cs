/*Below is a **self‑contained C# example** that shows how you could wrap a generic “web‑search” capability inside a Microsoft Bot Framework (v4) bot.  
The method `SearchAsync` is the **tool** that the bot can call when it needs to fetch the latest news (or any other query).  

```csharp*/
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// ------------------------------------------------------------
// 1️⃣  POCOs that model the JSON response from the search API
// ------------------------------------------------------------
public class SearchResult
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("snippet")]
    public string Snippet { get; set; } = string.Empty;

    [JsonPropertyName("published")]
    public DateTime? Published { get; set; }
}

// The outer envelope that many search services return
public class SearchResponse
{
    [JsonPropertyName("results")]
    public List<SearchResult> Results { get; set; } = new();
}

// ------------------------------------------------------------
// 2️⃣  The “tool” – an async method that can be called from a
//     Bot Framework dialog, activity handler, or a
//     AdaptiveDialog custom action.
// ------------------------------------------------------------
public static class WebSearchTool
{
    // You can replace this with any public search endpoint
    // (Bing Web Search API, Google Custom Search, DuckDuckGo
    // Instant Answer API, etc.).  The example uses the free
    // DuckDuckGo Instant Answer API because it does not require
    // an API key for simple queries.
    private const string DuckDuckGoEndpoint = "https://api.duckduckgo.com/";

    private static readonly HttpClient _http = new HttpClient();

    /// <summary>
    /// Performs a web‑search and returns the top N results.
    /// </summary>
    /// <param name="query">What to search for.</param>
    /// <param name="topN">Maximum number of items to return.</param>
    /// <param name="recencyDays">
    /// Optional filter – only return items newer than this many days.
    /// (The DuckDuckGo endpoint does not support it; you would need a
    /// paid API for that feature.)
    /// </param>
    /// <returns>A list of <see cref="SearchResult"/> objects.</returns>
    public static async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int topN = 5,
        int? recencyDays = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query must be provided.", nameof(query));

        // Build the request URL.
        // For DuckDuckGo we ask for JSON output and a “no_html” response.
        var url = $"{DuckDuckGoEndpoint}?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";

        // Call the endpoint.
        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        // DuckDuckGo returns a different shape than the POCOs above,
        // so we map the fields we care about.
        var raw = await response.Content.ReadFromJsonAsync<DuckDuckGoResponse>();

        // Transform into our canonical SearchResult list.
        var results = new List<SearchResult>();

        // The “RelatedTopics” array contains the most useful hits.
        foreach (var topic in (raw?.RelatedTopics ?? []))
        {
            // Some topics are nested groups; flatten them.
            if (topic.Topics != null && topic.Topics.Count > 0)
            {
                foreach (var sub in topic.Topics)
                {
                    results.Add(ConvertTopic(sub));
                }
            }
            else
            {
                results.Add(ConvertTopic(topic));
            }

            if (results.Count >= topN) break;
        }

        // Optional recency filter – only works when the source supplies a date.
        if (recencyDays.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-recencyDays.Value);
            results = results.FindAll(r => r.Published.HasValue && r.Published.Value >= cutoff);
        }

        return results.AsReadOnly();
    }

    // ------------------------------------------------------------
    // 3️⃣  Helper: map the DuckDuckGo JSON model to our POCO.
    // ------------------------------------------------------------
    private static SearchResult ConvertTopic(DuckDuckGoTopic topic) => new()
    {
        Title   = topic.Text?.Split(" - ")[0] ?? string.Empty,
        Url     = topic.FirstUrl ?? string.Empty,
        Snippet = topic.Text ?? string.Empty,
        // DuckDuckGo does not expose a publish date; leave null.
        Published = null
    };

    // ------------------------------------------------------------
    // 4️⃣  Minimal POCOs that match DuckDuckGo’s JSON format.
    // ------------------------------------------------------------
    private class DuckDuckGoResponse
    {
        [JsonPropertyName("RelatedTopics")]
        public List<DuckDuckGoTopic> RelatedTopics { get; set; } = new();
    }

    private class DuckDuckGoTopic
    {
        // When the entry is a leaf node:
        [JsonPropertyName("FirstURL")]
        public string? FirstUrl { get; set; }

        [JsonPropertyName("Text")]
        public string? Text { get; set; }

        // When the entry is a “category” that contains nested topics:
        [JsonPropertyName("Topics")]
        public List<DuckDuckGoTopic>? Topics { get; set; }
    }
}
/*```

### How to call it from a Bot Framework activity handler

```csharp*/
public class NewsBot : ActivityHandler
{
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext,
                                                          CancellationToken cancellationToken)
    {
        var userInput = turnContext.Activity.Text?.Trim() ?? string.Empty;

        // Very simple intent detection – if the user asks for “news”, we invoke the tool.
        if (userInput.Contains("news", StringComparison.OrdinalIgnoreCase) ||
            userInput.Contains("today", StringComparison.OrdinalIgnoreCase))
        {
            // Example query: “latest news Trinidad Express”
            var results = await WebSearchTool.SearchAsync("Trinidad Express latest news", topN: 5);

            if (results.Count == 0)
            {
                await turnContext.SendActivityAsync("I couldn't find any recent stories.", cancellationToken: cancellationToken);
                return;
            }

            var reply = "Here are the 5 most recent stories I could locate:\n\n";
            foreach (var r in results)
            {
                reply += $"- **{r.Title}**\n  {r.Url}\n";
                if (!string.IsNullOrWhiteSpace(r.Snippet))
                    reply += $"  {r.Snippet}\n";
                reply += "\n";
            }

            await turnContext.SendActivityAsync(reply, cancellationToken: cancellationToken);
        }
        else
        {
            await turnContext.SendActivityAsync("I’m not sure how to help with that. Try asking for today’s news.", cancellationToken: cancellationToken);
        }
    }
}
