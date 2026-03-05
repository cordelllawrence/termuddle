using Microsoft.Extensions.AI;
using tuichat;

// --- Load or Create Preferences ---
var prefs = Preferences.Load();

if (prefs is not null)
{
    ConsoleHelper.WriteSystem($"Loaded preferences: {prefs.BaseUrl}, model={prefs.Model}");
}
else
{
    ConsoleHelper.WriteSystem("Welcome to tuichat! Let's set things up.");
    Console.WriteLine();

    // Ask for base URL
    var defaultUrl = "http://localhost:11434/v1";
    ConsoleHelper.WriteInfo("Enter the base URL for your API provider.");
    ConsoleHelper.WriteInfo("Examples:");
    ConsoleHelper.WriteInfo("  Ollama:    http://localhost:11434/v1");
    ConsoleHelper.WriteInfo("  OpenAI:    https://api.openai.com/v1");
    ConsoleHelper.WriteInfo("  LM Studio: http://localhost:1234/v1");
    var baseUrl = ConsoleHelper.ReadInputDefault("Base URL", defaultUrl);

    // Ask for API key (optional)
    ConsoleHelper.WriteInfo("Enter your API key (leave blank if not required, e.g. Ollama).");
    var apiKey = ConsoleHelper.ReadInputMasked("API key");

    // Connect and list models
    // Extract the origin (scheme + host + port) from the base URL for the model service
    var service = new ModelService(new Uri(baseUrl).GetLeftPart(UriPartial.Authority), apiKey);

    ConsoleHelper.WriteSystem($"Connecting to {baseUrl}...");
    List<string> models;
    try
    {
        models = await service.ListModelsAsync();
    }
    catch (Exception ex)
    {
        ConsoleHelper.WriteError($"Failed to connect to {baseUrl}: {ex.Message}");
        ConsoleHelper.WriteError("Check the URL and ensure the server is running.");
        return 1;
    }

    if (models.Count == 0)
    {
        ConsoleHelper.WriteError("No models found on this server.");
        return 1;
    }

    // Display model list and ask user to pick
    ConsoleHelper.WriteInfo("Available models:");
    for (int i = 0; i < models.Count; i++)
        ConsoleHelper.WriteInfo($"  {i + 1}. {models[i]}");

    Console.WriteLine();
    var choice = ConsoleHelper.ReadInputDefault("Select model number", "1");
    if (!int.TryParse(choice, out var index) || index < 1 || index > models.Count)
    {
        ConsoleHelper.WriteError("Invalid selection.");
        return 1;
    }

    var selectedModel = models[index - 1];

    prefs = new Preferences
    {
        BaseUrl = baseUrl,
        ApiKey = apiKey,
        Model = selectedModel
    };
    prefs.Save();
    ConsoleHelper.WriteSystem($"Preferences saved to {Preferences.DefaultPath}");
    Console.WriteLine();
}

// --- Build Session ---
var session = new ChatSession
{
    BaseUrl = prefs.BaseUrl,
    ApiKey = prefs.ApiKey,
    ModelName = prefs.Model,
    StreamResponses = prefs.StreamResponses,
    ShowTps = prefs.ShowTps,
    Preferences = prefs
};
session.Reconnect();

// --- Ctrl+C Handling ---
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    ConsoleHelper.WriteSystem("\nGoodbye! Thanks for chatting.");
    Environment.Exit(0);
};

// --- Startup Banner ---
ConsoleHelper.WriteSystem("╔══════════════════════════════════════╗");
ConsoleHelper.WriteSystem("║            tuichat                   ║");
ConsoleHelper.WriteSystem("╚══════════════════════════════════════╝");
ConsoleHelper.WriteSystem($"API: {session.BaseUrl} | Model: {session.ModelName}");
ConsoleHelper.WriteSystem("Type /help for commands, /bye to exit.");
ConsoleHelper.WriteSystem("Use \\ at end of line for multi-line input.");
Console.WriteLine();

// --- REPL Loop ---
while (true)
{
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt");
    ConsoleHelper.WritePrompt($"[{timestamp}] You> ");

    // Read input (with multi-line support via trailing \)
    var input = ReadMultiLineInput();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    // Slash command handling
    if (input.StartsWith('/'))
    {
        if (!await CommandHandler.HandleAsync(input, session))
            return 0;
        Console.WriteLine();
        continue;
    }

    // Send to LLM
    session.History.Add(new ChatMessage(ChatRole.User, input));

    try
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int tokenCount = 0;

        if (session.StreamResponses)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            var fullResponse = new System.Text.StringBuilder();
            await foreach (var update in session.ChatClient.GetStreamingResponseAsync(session.History))
            {
                var chunk = update.Text ?? string.Empty;
                Console.Write(chunk);
                fullResponse.Append(chunk);
                tokenCount++;
            }
            Console.ResetColor();
            Console.WriteLine();
            session.History.Add(new ChatMessage(ChatRole.Assistant, fullResponse.ToString()));
        }
        else
        {
            // Thinking indicator
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Thinking...");

            var response = await session.ChatClient.GetResponseAsync(session.History);
            var responseText = response.Text ?? string.Empty;
            tokenCount = responseText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            // Clear the "Thinking..." text
            Console.Write("\r            \r");
            Console.ResetColor();

            ConsoleHelper.WriteResponse(responseText);
            session.History.Add(new ChatMessage(ChatRole.Assistant, responseText));
        }

        stopwatch.Stop();
        if (session.ShowTps && tokenCount > 0)
        {
            var elapsed = stopwatch.Elapsed.TotalSeconds;
            var tps = elapsed > 0 ? tokenCount / elapsed : 0;
            ConsoleHelper.WriteInfo($"[{tokenCount} tokens in {elapsed:F1}s — {tps:F1} tokens/sec]");
        }
    }
    catch (Exception ex)
    {
        Console.Write("\r            \r");
        Console.ResetColor();
        ConsoleHelper.WriteError($"Error: {ex.Message}");
    }

    Console.WriteLine();
}

string ReadMultiLineInput()
{
    var firstLine = ConsoleHelper.ReadInputWithHistory();
    if (!firstLine.EndsWith('\\'))
        return firstLine;

    var lines = new List<string> { firstLine[..^1] };

    while (true)
    {
        ConsoleHelper.WritePrompt("  ... ");
        var line = ConsoleHelper.ReadInput();
        if (line.EndsWith('\\'))
        {
            lines.Add(line[..^1]);
        }
        else
        {
            lines.Add(line);
            break;
        }
    }

    return string.Join(Environment.NewLine, lines);
}
