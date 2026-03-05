using Microsoft.Extensions.AI;
using OpenAI;

namespace termuddle;

public class ChatSession
{
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
        var rawClient = CreateChatClient(BaseUrl, ApiKey, ModelName);
        ChatClient = new ChatClientBuilder(rawClient)
            .UseFunctionInvocation()
            .Build();
    }

    public static IChatClient CreateChatClient(string baseUrl, string apiKey, string model)
    {
        var endpoint = new Uri(baseUrl);
        var credential = new System.ClientModel.ApiKeyCredential(
            string.IsNullOrEmpty(apiKey) ? "no-key" : apiKey);
        var client = new OpenAIClient(credential, new OpenAI.OpenAIClientOptions
        {
            Endpoint = endpoint
        });
        return client.GetChatClient(model).AsIChatClient();
    }
}
