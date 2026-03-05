using Microsoft.Extensions.AI;
using OpenAI;

namespace tuichat;

public class ChatSession
{
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "";
    public bool StreamResponses { get; set; } = true;
    public bool ShowTps { get; set; } = false;
    public IChatClient ChatClient { get; set; } = null!;
    public List<ChatMessage> History { get; } = new();
    public Preferences Preferences { get; set; } = new();

    public void Reconnect()
    {
        ChatClient = CreateChatClient(BaseUrl, ApiKey, ModelName);
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
