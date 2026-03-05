using Cocona;
using Microsoft.Extensions.AI;
using tuichat;

CoconaApp.Run(async (
    [Option("base-url", Description = "Base URL for the API provider (e.g. http://localhost:11434/v1)")] string? baseUrl,
    [Option("api-key", Description = "API key for authentication")] string? apiKey,
    [Option("model", Description = "Model name to use")] string? model,
    [Option("stream", Description = "Enable streaming responses")] bool? stream,
    [Option("tps", Description = "Show tokens-per-second stats")] bool? tps
) =>
{
    var cli = new CliOptions(baseUrl, apiKey, model, stream, tps);
    var prefs = await StartupHelper.ResolvePreferencesAsync(cli);
    
    if (prefs is null)
        return 1;

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
                await foreach (var update in session.ChatClient.GetStreamingResponseAsync(session.History, session.ChatOptions))
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

                var response = await session.ChatClient.GetResponseAsync(session.History, session.ChatOptions);
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
                var tpsValue = elapsed > 0 ? tokenCount / elapsed : 0;
                ConsoleHelper.WriteInfo($"[{tokenCount} tokens in {elapsed:F1}s — {tpsValue:F1} tokens/sec]");
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
});

static string ReadMultiLineInput()
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
