namespace termuddle;

public record CliOptions(string? BaseUrl, string? ApiKey, string? Model, bool? Stream, bool? Tps, string? Ask, string[]? Attach, bool? NoTools);

public static class StartupHelper
{
    /// <summary>
    /// Resolves config from saved file, CLI overrides, or the setup wizard.
    /// Returns null if the wizard fails (connection error, no models, invalid selection).
    /// </summary>
    public static async Task<ConfigHelper?> ResolveConfigAsync(CliOptions cli)
    {
        var config = ConfigHelper.Load();

        if (config is not null)
        {
            config = ApplyCliOverrides(config, cli);
            if (!await ValidateServerAsync(config))
                return null;
            return config;
        }

        if (cli.BaseUrl is not null && cli.Model is not null)
        {
            config = FromCliOptions(cli);
            if (!await ValidateServerAsync(config))
                return null;
            return config;
        }

        return await RunSetupWizardAsync();
    }

    /// <summary>
    /// Validates that the configured server is reachable and the model is available.
    /// If the model is not found, lists available models and prompts the user to choose one.
    /// Returns true if validation succeeded, false if the server is unreachable or user cancels.
    /// </summary>
    public static async Task<bool> ValidateServerAsync(ConfigHelper config)
    {
        var origin = new Uri(config.BaseUrl).GetLeftPart(UriPartial.Authority);
        using var service = new ModelService(origin, config.ApiKey);

        ConsoleHelper.WriteSystem($"Connecting to {config.BaseUrl}...");

        List<string> models;
        try
        {
            models = await service.ListModelsAsync();
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Cannot reach server at {config.BaseUrl}: {ex.Message}");
            ConsoleHelper.WriteError("Check the URL and ensure the server is running.");
            return false;
        }

        if (models.Count == 0)
        {
            ConsoleHelper.WriteError("Server is reachable but no models are available.");
            return false;
        }

        if (models.Contains(config.Model))
        {
            ConsoleHelper.WriteSystem($"Ready! Server is reachable and model '{config.Model}' is available.");
            return true;
        }

        ConsoleHelper.WriteError($"Model '{config.Model}' is not available on this server.");
        ConsoleHelper.WriteInfo("Available models:");
        for (int i = 0; i < models.Count; i++)
            ConsoleHelper.WriteInfo($"  {i + 1}. {models[i]}");

        Console.WriteLine();
        var choice = ConsoleHelper.ReadInputDefault("Select a model number (or press Enter to cancel)", "");
        if (string.IsNullOrWhiteSpace(choice))
            return false;

        if (!int.TryParse(choice, out var index) || index < 1 || index > models.Count)
        {
            ConsoleHelper.WriteError("Invalid selection.");
            return false;
        }

        config.Model = models[index - 1];
        config.Save();
        ConsoleHelper.WriteSystem($"Model updated to '{config.Model}'. Ready!");
        return true;
    }

    private static ConfigHelper ApplyCliOverrides(ConfigHelper config, CliOptions cli)
    {
        if (cli.BaseUrl is not null) config.BaseUrl = cli.BaseUrl;
        if (cli.ApiKey is not null) config.ApiKey = cli.ApiKey;
        if (cli.Model is not null) config.Model = cli.Model;
        if (cli.Stream is not null) config.StreamResponses = cli.Stream.Value;
        if (cli.Tps is not null) config.ShowTps = cli.Tps.Value;

        ConsoleHelper.WriteSystem($"Loaded config: {config.BaseUrl}, model={config.Model}");
        return config;
    }

    private static ConfigHelper FromCliOptions(CliOptions cli)
    {
        var config = new ConfigHelper
        {
            BaseUrl = cli.BaseUrl!,
            ApiKey = cli.ApiKey ?? string.Empty,
            Model = cli.Model!,
            StreamResponses = cli.Stream ?? false,
            ShowTps = cli.Tps ?? false
        };
        ConsoleHelper.WriteSystem($"Using CLI args: {config.BaseUrl}, model={config.Model}");
        return config;
    }

    private static async Task<ConfigHelper?> RunSetupWizardAsync()
    {
        ConsoleHelper.WriteSystem("Welcome to termuddle! Let's set things up.");
        Console.WriteLine();

        var defaultUrl = "http://localhost:11434/v1";
        ConsoleHelper.WriteInfo("Enter the base URL for your API provider.");
        ConsoleHelper.WriteInfo("Examples:");
        ConsoleHelper.WriteInfo("  Ollama:    http://localhost:11434/v1");
        ConsoleHelper.WriteInfo("  OpenAI:    https://api.openai.com/v1");
        ConsoleHelper.WriteInfo("  LM Studio: http://localhost:1234/v1");
        var baseUrl = ConsoleHelper.ReadInputDefault("Base URL", defaultUrl);

        ConsoleHelper.WriteInfo("Enter your API key (leave blank if not required, e.g. Ollama).");
        var apiKey = ConsoleHelper.ReadInputMasked("API key");

        using var service = new ModelService(new Uri(baseUrl).GetLeftPart(UriPartial.Authority), apiKey);

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
            return null;
        }

        if (models.Count == 0)
        {
            ConsoleHelper.WriteError("No models found on this server.");
            return null;
        }

        ConsoleHelper.WriteInfo("Available models:");
        for (int i = 0; i < models.Count; i++)
            ConsoleHelper.WriteInfo($"  {i + 1}. {models[i]}");

        Console.WriteLine();
        var choice = ConsoleHelper.ReadInputDefault("Select model number", "1");
        if (!int.TryParse(choice, out var index) || index < 1 || index > models.Count)
        {
            ConsoleHelper.WriteError("Invalid selection.");
            return null;
        }

        var config = new ConfigHelper
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Model = models[index - 1]
        };
        config.Save();
        ConsoleHelper.WriteSystem($"Config saved to {ConfigHelper.DefaultPath}");
        Console.WriteLine();

        return config;
    }
}
