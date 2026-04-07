using Cocona;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console;
using termuddle;

Console.OutputEncoding = System.Text.Encoding.UTF8;

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
    try { TerminalLayout.ResetLayout(); } catch { /* best-effort */ }
    Console.ResetColor();
    Console.Error.WriteLine($"Fatal error: {(e.ExceptionObject as Exception)?.Message ?? "Unknown error"}");
    Console.Error.WriteLine("Details have been written to the log file.");
    Log.CloseAndFlush();
};

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.Error(e.Exception, "Unobserved task exception");
    e.SetObserved();
};

var coconaBuilder = CoconaApp.CreateBuilder();
var coconaApp = coconaBuilder.Build();
coconaApp.AddCommand(async (
    [Option("base-url", Description = "Base URL for the API provider (e.g. http://localhost:11434/v1)")] string? baseUrl,
    [Option("api-key", Description = "API key for authentication")] string? apiKey,
    [Option("model", Description = "Model name to use")] string? model,
    [Option("stream", Description = "Enable streaming responses")] bool? stream,
    [Option("tps", Description = "Show tokens-per-second stats")] bool? tps,
    [Option("ask", Description = "Ask a single question and exit")] string? ask,
    [Option("attach", Description = "Attach file(s) to the prompt (use with --ask)")] string[]? attach,
    [Option("no-tools", Description = "Disable tool use (web search, fetch, etc.)")] bool? noTools,
    [Option("generate-image", Description = "Use the image generation endpoint instead of chat (use with --ask)")] bool? generateImage,
    [Option("use-ollama-api", Description = "Force using Ollama native API (skip auto-detection)")] bool? useOllamaApi,
    [Option("use-openai-api", Description = "Force using OpenAI-compatible API (skip auto-detection)")] bool? useOpenAiApi
) =>
{
    logger.LogInformation("termuddle starting");
    var cli = new CliOptions(baseUrl, apiKey, model, stream, tps, ask, attach, noTools, generateImage, useOllamaApi, useOpenAiApi);
    var prefs = await StartupHelper.ResolveConfigAsync(cli);

    if (prefs is null)
        return 1;

    // --- Detect API provider ---
    var provider = await StartupHelper.ResolveProviderAsync(cli, prefs.BaseUrl);

    // --- Build Session ---
    var session = new ChatSession { Preferences = prefs, Provider = provider };
    session.Reconnect();

    // --- Validate --attach requires --ask ---
    if (cli.Attach is { Length: > 0 } && cli.Ask is null)
    {
        Console.Error.WriteLine("Error: --attach requires --ask to specify a prompt.");
        return 1;
    }

    // --- Validate --generate-image requires --ask ---
    if (cli.GenerateImage == true && cli.Ask is null)
    {
        Console.Error.WriteLine("Error: --generate-image requires --ask to specify a prompt.");
        return 1;
    }

    // --- Image Generation Mode ---
    if (cli.GenerateImage == true && cli.Ask is not null)
    {
        try
        {
            var imageService = new ImageGenerationService(session.BaseUrl);
            ConsoleHelper.WriteSystem($"Generating image with {session.ModelName}...");
            var savedPath = await imageService.GenerateImageAsync(
                cli.Ask, session.ModelName,
                onProgress: (completed, total) =>
                {
                    Console.Write($"\rStep {completed}/{total}");
                    if (completed >= total) Console.WriteLine();
                });
            ConsoleHelper.WriteInfo($"Image saved: {savedPath}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during image generation");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        return 0;
    }

    // --- Quick Ask Mode ---
    if (cli.Ask is not null)
    {
        var askOptions = cli.NoTools == true ? new ChatOptions() : session.ChatOptions;

        try
        {
            if (cli.Attach is { Length: > 0 })
                session.History.Add(ChatSession.CreateMessageWithAttachments(cli.Ask, cli.Attach));
            else
                session.History.Add(new ChatMessage(ChatRole.User, cli.Ask));
            var allContents = new List<AIContent>();
            if (session.StreamResponses)
            {
                await foreach (var update in session.ChatClient.GetStreamingResponseAsync(session.History, askOptions))
                {
                    var chunk = update.Text ?? string.Empty;
                    if (chunk.Length > 0)
                        Console.Write(chunk);
                    if (update.Contents is not null)
                        allContents.AddRange(update.Contents);
                }
                Console.WriteLine();
            }
            else
            {
                var response = await session.ChatClient.GetResponseAsync(session.History, askOptions);
                Console.WriteLine(response.Text ?? string.Empty);
                foreach (var msg in response.Messages)
                    allContents.AddRange(msg.Contents);
            }

            var savedFiles = ChatSession.SaveResponseAttachments(allContents);
            foreach (var file in savedFiles)
                ConsoleHelper.WriteInfo($"Saved: {file}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during quick ask");
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (cli.NoTools != true && ex.Message.Contains("tool", StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine("Hint: This model may not support tool use. Try adding --no-tools to disable built-in tools.");
            if (ex.Message.Contains("does not support", StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine("Hint: This model may not support chat completions. If it's an image generation model, try --generate-image instead.");
            return 1;
        }
        return 0;
    }

    // --- Ctrl+C Handling ---
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        TerminalLayout.ResetLayout();
        ConsoleHelper.WriteSystem($"\n{ConsoleHelper.GetRandomGoodbye()}");
        loggerFactory.Dispose();
        Log.CloseAndFlush();
        Environment.Exit(0);
    };

    // --- Startup Banner ---
    AnsiConsole.Write(
        new Panel($"[yellow]API:[/] {Markup.Escape(session.BaseUrl)} | [yellow]Model:[/] {Markup.Escape(session.ModelName)}\nType /help for commands, /bye to exit.")
            .Header("[yellow bold]termuddle[/]")
            .BorderColor(Color.Yellow)
            .Padding(1, 0));
    AnsiConsole.WriteLine();

    // --- Initialize Terminal Layout ---
    TerminalLayout.Initialize();

    // --- REPL Loop ---
    while (true)
    {
        string input;
        try
        {
            ConsoleHelper.EnsureCursorVisible();
            ConsoleHelper.WritePrompt("> ");

            // Read input (with multi-line support via trailing \)
            input = ReadMultiLineInput();
            TerminalLayout.ClearInputArea();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading input");
            // Attempt to recover the terminal layout
            try { TerminalLayout.HandleResize(); } catch { /* best-effort */ }
            continue;
        }

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

            var allContents = new List<AIContent>();
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
                    if (update.Contents is not null)
                        allContents.AddRange(update.Contents);
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
                foreach (var msg in response.Messages)
                    allContents.AddRange(msg.Contents);
            }

            var savedFiles = ChatSession.SaveResponseAttachments(allContents);
            foreach (var file in savedFiles)
                ConsoleHelper.WriteInfo($"Saved: {file}");

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
            if (ex.Message.Contains("tool", StringComparison.OrdinalIgnoreCase))
                ConsoleHelper.WriteError("Hint: This model may not support tool use. Restart with --no-tools to disable built-in tools.");
            if (ex.Message.Contains("does not support", StringComparison.OrdinalIgnoreCase))
                ConsoleHelper.WriteError("Hint: This model may not support chat completions. If it's an image generation model, try --generate-image instead.");
        }
    }
}).WithDescription("A tool that allows you to quickly connect to and interact with LLM servers that support the OpenAI v1 API.");
coconaApp.Run();

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
