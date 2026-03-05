using Cocona;
using Microsoft.Extensions.AI;
using Spectre.Console;
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
        TerminalLayout.ResetLayout();
        ConsoleHelper.WriteSystem("\nGoodbye! Thanks for chatting.");
        Environment.Exit(0);
    };

    // --- Startup Banner ---
    AnsiConsole.Write(
        new Panel($"[yellow]API:[/] {Markup.Escape(session.BaseUrl)} | [yellow]Model:[/] {Markup.Escape(session.ModelName)}\nType /help for commands, /bye to exit.\nUse \\ at end of line for multi-line input.")
            .Header("[yellow bold]tuichat[/]")
            .BorderColor(Color.Yellow)
            .Padding(1, 0));
    AnsiConsole.WriteLine();

    // --- Initialize Terminal Layout ---
    TerminalLayout.Initialize();

    // --- REPL Loop ---
    while (true)
    {
        ConsoleHelper.EnsureCursorVisible();
        ConsoleHelper.WritePrompt("> ");

        // Read input (with multi-line support via trailing \)
        var input = ReadMultiLineInput();

        if (string.IsNullOrWhiteSpace(input))
            continue;

        // Slash command handling
        if (input.StartsWith('/'))
        {
            if (!await CommandHandler.HandleAsync(input, session))
            {
                TerminalLayout.ResetLayout();
                return 0;
            }
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
                var fullResponse = new System.Text.StringBuilder();
                TerminalLayout.BeginStreamOutput();
                await foreach (var update in session.ChatClient.GetStreamingResponseAsync(session.History, session.ChatOptions))
                {
                    var chunk = update.Text ?? string.Empty;
                    ConsoleHelper.WriteStreamChunk(chunk);
                    fullResponse.Append(chunk);
                    tokenCount++;
                }
                Console.WriteLine();
                TerminalLayout.EndStreamOutput();
                session.History.Add(new ChatMessage(ChatRole.Assistant, fullResponse.ToString()));
            }
            else
            {
                ChatResponse? response = null;
                // For non-streaming, show a simple waiting message
                ConsoleHelper.WriteSystem("Thinking...");
                response = await session.ChatClient.GetResponseAsync(session.History, session.ChatOptions);

                var responseText = response!.Text ?? string.Empty;
                tokenCount = responseText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

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
            ConsoleHelper.WriteError($"Error: {ex.Message}");
        }
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
