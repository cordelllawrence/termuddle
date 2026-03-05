using Microsoft.Extensions.AI;

namespace termuddle;

public static class CommandHandler
{
    public static readonly string[] Commands = [
        "/bye", "/info", "/help", "/clear", "/stream", "/tps",
        "/model", "/config", "/backup"
    ];

    public static readonly Dictionary<string, string[]> Subcommands = new()
    {
        ["/model"] = ["list", "use"],
        ["/config"] = ["show", "set", "reset"],
        ["/backup"] = ["list", "load", "remove"]
    };

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
                HandleInfo(session);
                break;

            case "/help":
            case "/?":
                HandleHelp();
                break;

            case "/stream":
                session.StreamResponses = !session.StreamResponses;
                session.Preferences.Save();
                ConsoleHelper.WriteSystem($"Streaming is now {(session.StreamResponses ? "on" : "off")}.");
                break;

            case "/tps":
                session.ShowTps = !session.ShowTps;
                session.Preferences.Save();
                ConsoleHelper.WriteSystem($"Tokens per second display is now {(session.ShowTps ? "on" : "off")}.");
                break;

            case "/clear":
                session.History.Clear();
                ConsoleHelper.WriteSystem("Conversation history cleared.");
                break;

            case "/model":
                await HandleModelAsync(parts, session);
                break;

            case "/config":
                HandleConfig(parts, session);
                break;

            case "/backup":
                HandleBackup(parts, session);
                break;

            default:
                ConsoleHelper.WriteError($"Unknown command: {command}. Type /help for available commands.");
                break;
        }

        return true;
    }

    private static void HandleInfo(ChatSession session)
    {
        int sent = session.History.Count(m => m.Role == ChatRole.User);
        int received = session.History.Count(m => m.Role == ChatRole.Assistant);
        ConsoleHelper.WriteInfo($"Base URL:           {session.BaseUrl}");
        ConsoleHelper.WriteInfo($"API Key:            {MaskApiKey(session.ApiKey)}");
        ConsoleHelper.WriteInfo($"Model:              {session.ModelName}");
        ConsoleHelper.WriteInfo($"Streaming:          {(session.StreamResponses ? "on" : "off")}");
        ConsoleHelper.WriteInfo($"Show TPS:           {(session.ShowTps ? "on" : "off")}");
        ConsoleHelper.WriteInfo($"Messages sent:      {sent}");
        ConsoleHelper.WriteInfo($"Messages received:  {received}");
    }

    private static void HandleHelp()
    {
        ConsoleHelper.WriteInfo("Available commands:");
        ConsoleHelper.WriteInfo("  /help                          - Show this help message");
        ConsoleHelper.WriteInfo("  /info                          - Show session information");
        ConsoleHelper.WriteInfo("  /clear                         - Clear conversation history");
        ConsoleHelper.WriteInfo("  /stream                        - Toggle streaming on/off");
        ConsoleHelper.WriteInfo("  /tps                           - Toggle tokens/sec display on/off");
        ConsoleHelper.WriteInfo("  /model list                    - List available models");
        ConsoleHelper.WriteInfo("  /model use <name|N>            - Switch to a model (name, number, or partial match)");
        ConsoleHelper.WriteInfo("  /config show                   - Show current config");
        ConsoleHelper.WriteInfo("  /config set <key>=<value>      - Update a config value");
        ConsoleHelper.WriteInfo("  /config reset                  - Reload config from disk");
        ConsoleHelper.WriteInfo("  /backup                        - Create a config backup");
        ConsoleHelper.WriteInfo("  /backup list                   - List config backups");
        ConsoleHelper.WriteInfo("  /backup load <N>               - Restore Nth backup");
        ConsoleHelper.WriteInfo("  /backup remove <N>             - Remove Nth backup");
        ConsoleHelper.WriteInfo("  /bye                           - Exit the application");
        ConsoleHelper.WriteInfo("    Config keys: base_url, api_key, model, stream, tps");
    }

    // --- /model ---

    private static async Task HandleModelAsync(string[] parts, ChatSession session)
    {
        var sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "list";

        switch (sub)
        {
            case "list":
            case "ls":
                await HandleModelListAsync(session);
                break;
            case "use":
            case "switch":
                await HandleModelUseAsync(parts, session);
                break;
            default:
                ConsoleHelper.WriteError("Usage: /model list | use <name|N>");
                break;
        }
    }

    private static async Task HandleModelListAsync(ChatSession session)
    {
        try
        {
            var origin = new Uri(session.BaseUrl).GetLeftPart(UriPartial.Authority);
            using var service = new ModelService(origin, session.ApiKey);
            var models = await service.ListModelsAsync();

            if (models.Count == 0)
            {
                ConsoleHelper.WriteInfo("No models found on this server.");
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

    private static async Task HandleModelUseAsync(string[] parts, ChatSession session)
    {
        if (parts.Length < 3)
        {
            ConsoleHelper.WriteError("Usage: /model use <name|N>");
            return;
        }

        var input = parts[2];

        try
        {
            var origin = new Uri(session.BaseUrl).GetLeftPart(UriPartial.Authority);
            using var service = new ModelService(origin, session.ApiKey);
            var models = await service.ListModelsAsync();

            string? resolved = null;

            if (int.TryParse(input, out var index) && index >= 1 && index <= models.Count)
            {
                resolved = models[index - 1];
            }
            else if (models.Contains(input))
            {
                resolved = input;
            }
            else
            {
                var matches = models
                    .Where(m => m.Contains(input, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 1)
                {
                    resolved = matches[0];
                }
                else if (matches.Count > 1)
                {
                    ConsoleHelper.WriteError($"'{input}' matches multiple models:");
                    foreach (var m in matches)
                        ConsoleHelper.WriteError($"  - {m}");
                    ConsoleHelper.WriteError("Be more specific, or use the model number from /model list.");
                    return;
                }
            }

            if (resolved is null)
            {
                ConsoleHelper.WriteError($"Model '{input}' not found. Use /model list to see available models.");
                return;
            }

            session.ModelName = resolved;
            session.Reconnect();
            ConsoleHelper.WriteSystem($"Switched to model: {resolved}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Failed to switch model: {ex.Message}");
        }
    }

    // --- /config ---

    private static void HandleConfig(string[] parts, ChatSession session)
    {
        var sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "show";

        switch (sub)
        {
            case "show":
                HandleConfigShow(session);
                break;
            case "set":
                HandleConfigSet(parts, session);
                break;
            case "reset":
                HandleConfigReset(session);
                break;
            default:
                ConsoleHelper.WriteError("Usage: /config show | set <key>=<value> | reset");
                break;
        }
    }

    private static void HandleConfigShow(ChatSession session)
    {
        ConsoleHelper.WriteInfo($"base_url  = {session.Preferences.BaseUrl}");
        ConsoleHelper.WriteInfo($"api_key   = {MaskApiKey(session.Preferences.ApiKey)}");
        ConsoleHelper.WriteInfo($"model     = {session.Preferences.Model}");
        ConsoleHelper.WriteInfo($"stream    = {session.Preferences.StreamResponses.ToString().ToLowerInvariant()}");
        ConsoleHelper.WriteInfo($"tps       = {session.Preferences.ShowTps.ToString().ToLowerInvariant()}");
        ConsoleHelper.WriteInfo($"File: {ConfigHelper.DefaultPath}");
    }

    private static void HandleConfigSet(string[] parts, ChatSession session)
    {
        if (parts.Length < 3 || !parts[2].Contains('='))
        {
            ConsoleHelper.WriteError("Usage: /config set <key>=<value>");
            ConsoleHelper.WriteError("  Keys: base_url, api_key, model, stream, tps");
            return;
        }

        var eqIndex = parts[2].IndexOf('=');
        var key = parts[2][..eqIndex].Trim().ToLowerInvariant();
        var value = parts[2][(eqIndex + 1)..].Trim();

        switch (key)
        {
            case "base_url":
                try
                {
                    session.BaseUrl = value;
                    session.Reconnect();
                    session.Preferences.Save();
                    ConsoleHelper.WriteSystem($"Base URL updated to: {value} (reconnected)");
                }
                catch (UriFormatException)
                {
                    ConsoleHelper.WriteError($"Invalid URL: {value}");
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteError($"Failed to set base URL: {ex.Message}");
                }
                break;

            case "api_key":
                try
                {
                    session.ApiKey = value;
                    session.Reconnect();
                    session.Preferences.Save();
                    ConsoleHelper.WriteSystem("API key updated (reconnected)");
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteError($"Failed to set API key: {ex.Message}");
                }
                break;

            case "model":
                try
                {
                    session.ModelName = value;
                    session.Reconnect();
                    session.Preferences.Save();
                    ConsoleHelper.WriteSystem($"Default model updated to: {value} (switched)");
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteError($"Failed to set model: {ex.Message}");
                }
                break;

            case "stream":
                if (bool.TryParse(value, out var streamVal))
                {
                    session.StreamResponses = streamVal;
                    session.Preferences.Save();
                    ConsoleHelper.WriteSystem($"Streaming updated to: {(streamVal ? "on" : "off")}");
                }
                else
                {
                    ConsoleHelper.WriteError("Invalid value. Use true or false.");
                }
                break;

            case "tps":
                if (bool.TryParse(value, out var tpsVal))
                {
                    session.ShowTps = tpsVal;
                    session.Preferences.Save();
                    ConsoleHelper.WriteSystem($"Show TPS updated to: {(tpsVal ? "on" : "off")}");
                }
                else
                {
                    ConsoleHelper.WriteError("Invalid value. Use true or false.");
                }
                break;

            default:
                ConsoleHelper.WriteError($"Unknown config key: {key}");
                ConsoleHelper.WriteError("  Keys: base_url, api_key, model, stream, tps");
                break;
        }
    }

    private static void HandleConfigReset(ChatSession session)
    {
        var loaded = ConfigHelper.Load();
        if (loaded is null)
        {
            ConsoleHelper.WriteError($"No config file found at {ConfigHelper.DefaultPath}");
            return;
        }

        session.Preferences = loaded;
        session.Reconnect();
        ConsoleHelper.WriteSystem("Config reloaded from disk and reconnected.");
    }

    // --- /backup ---

    private static void HandleBackup(string[] parts, ChatSession session)
    {
        var sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : null;

        // bare /backup with no subcommand creates a backup
        if (sub is null)
        {
            var backupFile = session.Preferences.Backup();
            ConsoleHelper.WriteSystem($"Config backed up to: {backupFile}");
            return;
        }

        switch (sub)
        {
            case "list":
            case "ls":
                HandleBackupList();
                break;
            case "load":
            case "restore":
                HandleBackupLoad(parts, session);
                break;
            case "remove":
            case "delete":
            case "rm":
                HandleBackupRemove(parts);
                break;
            default:
                ConsoleHelper.WriteError("Usage: /backup | /backup list | load <N> | remove <N>");
                break;
        }
    }

    private static void HandleBackupList()
    {
        var backups = ConfigHelper.ListBackups();
        if (backups.Count == 0)
        {
            ConsoleHelper.WriteInfo("No backups found.");
            return;
        }

        ConsoleHelper.WriteInfo("Config backups:");
        for (int i = 0; i < backups.Count; i++)
        {
            var (fileName, prefs) = backups[i];
            ConsoleHelper.WriteInfo($"  {i + 1}. {fileName}");
            ConsoleHelper.WriteInfo($"       base_url={prefs.BaseUrl}  model={prefs.Model}  stream={prefs.StreamResponses.ToString().ToLowerInvariant()}  tps={prefs.ShowTps.ToString().ToLowerInvariant()}");
        }
    }

    private static void HandleBackupLoad(string[] parts, ChatSession session)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out var loadIndex))
        {
            ConsoleHelper.WriteError("Usage: /backup load <N> (use /backup list to see backups)");
            return;
        }

        var loaded = ConfigHelper.LoadBackup(loadIndex);
        if (loaded is null)
        {
            ConsoleHelper.WriteError($"Backup #{loadIndex} not found. Use /backup list to see available backups.");
            return;
        }

        try
        {
            session.BaseUrl = loaded.BaseUrl;
            session.ApiKey = loaded.ApiKey;
            session.ModelName = loaded.Model;
            session.StreamResponses = loaded.StreamResponses;
            session.ShowTps = loaded.ShowTps;
            session.Reconnect();
            session.Preferences.Save();
            ConsoleHelper.WriteSystem($"Restored backup #{loadIndex} and reconnected.");
        }
        catch (UriFormatException)
        {
            ConsoleHelper.WriteError($"Backup contains an invalid base URL: {loaded.BaseUrl}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Failed to restore backup: {ex.Message}");
        }
    }

    private static void HandleBackupRemove(string[] parts)
    {
        if (parts.Length < 3 || !int.TryParse(parts[2], out var removeIndex))
        {
            ConsoleHelper.WriteError("Usage: /backup remove <N> (use /backup list to see backups)");
            return;
        }

        var allBackups = ConfigHelper.ListBackups();
        if (removeIndex < 1 || removeIndex > allBackups.Count)
        {
            ConsoleHelper.WriteError($"Backup #{removeIndex} not found. Use /backup list to see available backups.");
            return;
        }

        var (backupName, _) = allBackups[removeIndex - 1];
        ConsoleHelper.WriteSystem($"Remove backup #{removeIndex} ({backupName})? [y/N] ");
        var confirm = ConsoleHelper.ReadInput().Trim().ToLowerInvariant();
        if (confirm is "y" or "yes")
        {
            ConfigHelper.RemoveBackup(removeIndex);
            ConsoleHelper.WriteSystem($"Backup #{removeIndex} removed.");
        }
        else
        {
            ConsoleHelper.WriteSystem("Cancelled.");
        }
    }

    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return "(not set)";
        if (apiKey.Length <= 8)
            return new string('*', apiKey.Length);
        return apiKey[..4] + new string('*', apiKey.Length - 8) + apiKey[^4..];
    }
}
