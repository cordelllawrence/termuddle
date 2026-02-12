namespace tuichat;

public static class ConsoleHelper
{
    public static void WriteSystem(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WritePrompt(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(prompt);
        Console.ResetColor();
    }

    public static void WriteResponse(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static string ReadInput()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        var input = Console.ReadLine();
        Console.ResetColor();
        return input ?? string.Empty;
    }

    public static string ReadInputDefault(string prompt, string defaultValue)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{prompt} [{defaultValue}]: ");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Green;
        var input = Console.ReadLine()?.Trim();
        Console.ResetColor();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }
}
