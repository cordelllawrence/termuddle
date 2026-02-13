using Microsoft.Extensions.AI;
using tuichat;

// --- Load or Create Preferences ---
var prefs = Preferences.Load();

if (prefs is not null)
{
    ConsoleHelper.WriteSystem($"Loaded preferences: host={prefs.OllamaHost}, model={prefs.Model}");
}
else
{
    ConsoleHelper.WriteSystem("Welcome to tuichat! Let's set things up.");
    Console.WriteLine();

    // Ask for Ollama host
    var envHost = Environment.GetEnvironmentVariable("OLLAMA_HOST");
    var defaultHost = envHost ?? "localhost";
    var host = ConsoleHelper.ReadInputDefault("Ollama host", defaultHost);

    // Connect and list models
    var service = new OllamaService($"http://{host}:11434");

    ConsoleHelper.WriteSystem($"Connecting to Ollama at {host}...");
    List<string> models;
    try
    {
        models = await service.ListModelsAsync();
    }
    catch (Exception ex)
    {
        ConsoleHelper.WriteError($"Failed to connect to Ollama at {host}: {ex.Message}");
        ConsoleHelper.WriteError("Make sure Ollama is running and accessible.");
        return 1;
    }

    if (models.Count == 0)
    {
        ConsoleHelper.WriteError("No models found. Pull a model first with: ollama pull <model>");
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
        OllamaHost = host,
        Model = selectedModel
    };
    prefs.Save();
    ConsoleHelper.WriteSystem($"Preferences saved to {Preferences.DefaultPath}");
    Console.WriteLine();
}

// --- OLLAMA_HOST env var overrides preferences ---
var ollamaHost = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? prefs.OllamaHost;

// --- Build Session ---
var session = new ChatSession
{
    OllamaHost = ollamaHost,
    ModelName = prefs.Model,
    StreamResponses = prefs.StreamResponses,
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
ConsoleHelper.WriteSystem("║  Terminal Chat powered by Ollama     ║");
ConsoleHelper.WriteSystem("╚══════════════════════════════════════╝");
ConsoleHelper.WriteSystem($"Host: {session.OllamaHost} | Model: {session.ModelName}");
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

    // Send to Ollama
    session.History.Add(new ChatMessage(ChatRole.User, input));

    try
    {
        if (session.StreamResponses)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            var fullResponse = new System.Text.StringBuilder();
            await foreach (var update in session.ChatClient.GetStreamingResponseAsync(session.History))
            {
                var chunk = update.Text ?? string.Empty;
                Console.Write(chunk);
                fullResponse.Append(chunk);
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

            // Clear the "Thinking..." text
            Console.Write("\r            \r");
            Console.ResetColor();

            ConsoleHelper.WriteResponse(responseText);
            session.History.Add(new ChatMessage(ChatRole.Assistant, responseText));
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
