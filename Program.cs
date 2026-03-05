using Cocona;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console;
using termuddle;

// --- Configure Serilog file logger ---
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "termuddle-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
var logger = loggerFactory.CreateLogger("termuddle");

// --- Global exception handlers ---
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
    Log.CloseAndFlush();
};

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.Error(e.Exception, "Unobserved task exception");
    e.SetObserved();
};

CoconaApp.Run(async (
    [Option("base-url", Description = "Base URL for the API provider (e.g. http://localhost:11434/v1)")] string? baseUrl,
    [Option("api-key", Description = "API key for authentication")] string? apiKey,
    [Option("model", Description = "Model name to use")] string? model,
    [Option("stream", Description = "Enable streaming responses")] bool? stream,
    [Option("tps", Description = "Show tokens-per-second stats")] bool? tps
) =>
{
    logger.LogInformation("termuddle starting");
    var cli = new CliOptions(baseUrl, apiKey, model, stream, tps);
    var prefs = await StartupHelper.ResolvePreferencesAsync(cli);

    if (prefs is null)
        return 1;

    // --- Build Session ---
    var session = new ChatSession { Preferences = prefs };
    session.Reconnect();

    // --- Ctrl+C Handling ---
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        TerminalLayout.ResetLayout();
        ConsoleHelper.WriteSystem("\nGoodbye! Thanks for chatting.");
        loggerFactory.Dispose();
        Log.CloseAndFlush();
        Environment.Exit(0);
    };

    // --- Startup Banner ---
    AnsiConsole.Write(
        new Panel($"[yellow]API:[/] {Markup.Escape(session.BaseUrl)} | [yellow]Model:[/] {Markup.Escape(session.ModelName)}\nType /help for commands, /bye to exit.\nUse \\ at end of line for multi-line input.")
            .Header("[yellow bold]termuddle[/]")
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
        TerminalLayout.ClearInputArea();

        if (string.IsNullOrWhiteSpace(input))
            continue;

        // Slash command handling
        if (input.StartsWith('/'))
        {
            if (!await CommandHandler.HandleAsync(input, session))
            {
                TerminalLayout.ResetLayout();
                Log.CloseAndFlush();
                return 0;
            }
            continue;
        }

        // Send to LLM
        session.History.Add(new ChatMessage(ChatRole.User, input));

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long? tokenCount = null;

            if (session.StreamResponses)
            {
                var fullResponse = new System.Text.StringBuilder();
                ConsoleHelper.StartThinkingAnimation();
                var firstChunk = true;
                ChatResponseUpdate? lastUpdate = null;
                await foreach (var update in session.ChatClient.GetStreamingResponseAsync(session.History, session.ChatOptions))
                {
                    var chunk = update.Text ?? string.Empty;
                    lastUpdate = update;
                    if (chunk.Length == 0)
                        continue;

                    if (firstChunk)
                    {
                        await ConsoleHelper.StopThinkingAnimationAsync();
                        TerminalLayout.BeginStreamOutput();
                        firstChunk = false;
                    }
                    ConsoleHelper.WriteStreamChunk(chunk);
                    fullResponse.Append(chunk);
                    ConsoleHelper.DrainTypeAhead();
                }
                if (firstChunk)
                {
                    await ConsoleHelper.StopThinkingAnimationAsync();
                }
                Console.WriteLine();
                TerminalLayout.EndStreamOutput();
                var responseText = fullResponse.ToString();
                session.History.Add(new ChatMessage(ChatRole.Assistant, responseText));
                tokenCount = lastUpdate?.Contents?.OfType<UsageContent>().FirstOrDefault()?.Details?.OutputTokenCount
                    ?? EstimateWordCount(responseText);
            }
            else
            {
                ConsoleHelper.StartThinkingAnimation();
                var response = await session.ChatClient.GetResponseAsync(session.History, session.ChatOptions);
                await ConsoleHelper.StopThinkingAnimationAsync();

                var responseText = response.Text ?? string.Empty;
                tokenCount = response.Usage?.OutputTokenCount
                    ?? EstimateWordCount(responseText);

                ConsoleHelper.WriteResponse(responseText);
                session.History.Add(new ChatMessage(ChatRole.Assistant, responseText));
            }

            stopwatch.Stop();
            if (session.ShowTps && tokenCount > 0)
            {
                var elapsed = stopwatch.Elapsed.TotalSeconds;
                var tpsValue = elapsed > 0 ? (double)tokenCount.Value / elapsed : 0;
                ConsoleHelper.WriteInfo($"[~{tokenCount} tokens in {elapsed:F1}s — {tpsValue:F1} tokens/sec]");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during chat interaction");
            ConsoleHelper.WriteError($"Error: {ex.Message}");
        }
    }
});

static int EstimateWordCount(string text)
    => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

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
