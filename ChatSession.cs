using Microsoft.Extensions.AI;
using OpenAI;

namespace tuichat;

public class ChatSession
{
    public string OllamaHost { get; set; } = "localhost";
    public string ModelName { get; set; } = "";
    public bool StreamResponses { get; set; } = true;
    public IChatClient ChatClient { get; set; } = null!;
    public List<ChatMessage> History { get; } = new();
    public Preferences Preferences { get; set; } = new();

    public void Reconnect()
    {
        ChatClient = CreateChatClient(OllamaHost, ModelName);
    }

    public static IChatClient CreateChatClient(string host, string model)
    {
        var endpoint = new Uri($"http://{host}:11434/v1");
        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential("ollama"), new OpenAI.OpenAIClientOptions
        {
            Endpoint = endpoint
        });
        return client.GetChatClient(model).AsIChatClient();
    }
}
