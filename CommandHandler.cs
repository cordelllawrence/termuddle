using Microsoft.Extensions.AI;

namespace tuichat;

public static class CommandHandler
{
    /// <summary>
    /// Processes a slash command. Returns false if the application should exit.
    /// </summary>
    public static async Task<bool> HandleAsync(string input, ChatSession session)
    {
        var parts = input.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "/bye":
                ConsoleHelper.WriteSystem("Goodbye! Thanks for chatting.");
                return false;

            case "/info":
                int sent = session.History.Count(m => m.Role == ChatRole.User);
                int received = session.History.Count(m => m.Role == ChatRole.Assistant);
                ConsoleHelper.WriteInfo($"Host:               {session.OllamaHost}");
                ConsoleHelper.WriteInfo($"Model:              {session.ModelName}");
                ConsoleHelper.WriteInfo($"Messages sent:      {sent}");
                ConsoleHelper.WriteInfo($"Messages received:  {received}");
                break;

            case "/help":
                ConsoleHelper.WriteInfo("Available commands:");
                ConsoleHelper.WriteInfo("  /bye                              - Exit the application");
                ConsoleHelper.WriteInfo("  /info                             - Show session information");
                ConsoleHelper.WriteInfo("  /help                             - Show this help message");
                ConsoleHelper.WriteInfo("  /clear                            - Clear conversation history");
                ConsoleHelper.WriteInfo("  /models                           - List available Ollama models");
                ConsoleHelper.WriteInfo("  /switch <model>                   - Switch to a different model");
                ConsoleHelper.WriteInfo("  /preferences show                 - Show current preferences");
                ConsoleHelper.WriteInfo("  /preferences set <key>=<value>    - Update a preference");
                ConsoleHelper.WriteInfo("    Keys: ollama.host, ollama.model");
                break;

            case "/clear":
                session.History.Clear();
                ConsoleHelper.WriteSystem("Conversation history cleared.");
                break;

            case "/models":
                await HandleModelsAsync(session);
                break;

            case "/switch":
                await HandleSwitchAsync(parts, session);
                break;

            case "/preferences":
                HandlePreferences(parts, session);
                break;

            default:
                ConsoleHelper.WriteError($"Unknown command: {command}. Type /help for available commands.");
                break;
        }

        return true;
    }

    private static async Task HandleModelsAsync(ChatSession session)
    {
        try
        {
            var service = new OllamaService($"http://{session.OllamaHost}:11434");
            var models = await service.ListModelsAsync();

            if (models.Count == 0)
            {
                ConsoleHelper.WriteInfo("No models found. Pull a model with: ollama pull <model>");
                return;
            }

            ConsoleHelper.WriteInfo("Available models:");
            for (int i = 0; i < models.Count; i++)
            {
                var marker = models[i] == session.ModelName ? " (active)" : "";
                ConsoleHelper.WriteInfo($"  {i + 1}. {models[i]}{marker}");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Failed to list models: {ex.Message}");
        }
    }

    private static async Task HandleSwitchAsync(string[] parts, ChatSession session)
    {
        if (parts.Length < 2)
        {
            ConsoleHelper.WriteError("Usage: /switch <model>");
            return;
        }

        var newModel = parts[1];

        try
        {
            var service = new OllamaService($"http://{session.OllamaHost}:11434");
            var models = await service.ListModelsAsync();

            if (!models.Contains(newModel))
            {
                ConsoleHelper.WriteError($"Model '{newModel}' not found. Use /models to see available models.");
                return;
            }

            session.ModelName = newModel;
            session.Reconnect();
            ConsoleHelper.WriteSystem($"Switched to model: {newModel}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Failed to switch model: {ex.Message}");
        }
    }

    private static void HandlePreferences(string[] parts, ChatSession session)
    {
        if (parts.Length < 2)
        {
            ConsoleHelper.WriteError("Usage: /preferences show | /preferences set <key>=<value>");
            return;
        }

        var subCommand = parts[1].ToLowerInvariant();

        switch (subCommand)
        {
            case "show":
                ConsoleHelper.WriteInfo($"ollama.host  = {session.Preferences.OllamaHost}");
                ConsoleHelper.WriteInfo($"ollama.model = {session.Preferences.Model}");
                ConsoleHelper.WriteInfo($"File: {Preferences.DefaultPath}");
                break;

            case "set":
                if (parts.Length < 3 || !parts[2].Contains('='))
                {
                    ConsoleHelper.WriteError("Usage: /preferences set <key>=<value>");
                    ConsoleHelper.WriteError("  Keys: ollama.host, ollama.model");
                    return;
                }

                var eqIndex = parts[2].IndexOf('=');
                var key = parts[2][..eqIndex].Trim().ToLowerInvariant();
                var value = parts[2][(eqIndex + 1)..].Trim();

                switch (key)
                {
                    case "ollama.host":
                        session.Preferences.OllamaHost = value;
                        session.OllamaHost = value;
                        session.Reconnect();
                        session.Preferences.Save();
                        ConsoleHelper.WriteSystem($"Host updated to: {value} (reconnected)");
                        break;

                    case "ollama.model":
                        session.Preferences.Model = value;
                        session.ModelName = value;
                        session.Reconnect();
                        session.Preferences.Save();
                        ConsoleHelper.WriteSystem($"Default model updated to: {value} (switched)");
                        break;

                    default:
                        ConsoleHelper.WriteError($"Unknown preference key: {key}");
                        ConsoleHelper.WriteError("  Keys: ollama.host, ollama.model");
                        break;
                }
                break;

            default:
                ConsoleHelper.WriteError("Usage: /preferences show | /preferences set <key>=<value>");
                break;
        }
    }
}
