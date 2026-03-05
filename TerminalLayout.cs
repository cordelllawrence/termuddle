namespace tuichat;

public static class TerminalLayout
{
    private static bool _initialized;
    private static int _lastWindowWidth;
    private static int _lastWindowHeight;

    public static bool IsInitialized => _initialized;

    public static int OutputAreaHeight => Console.WindowHeight - 2;

    public static int SeparatorRow => Console.WindowHeight - 2;

    public static int InputRow => Console.WindowHeight - 1;

    public static void Initialize()
    {
        _lastWindowWidth = Console.WindowWidth;
        _lastWindowHeight = Console.WindowHeight;

        // Set scrolling region to rows 1 through (height - 2)
        // ANSI scrolling regions are 1-based
        Console.Write($"\e[1;{OutputAreaHeight}r");

        // Draw separator
        RedrawSeparator();

        // Position cursor at input row
        Console.SetCursorPosition(0, InputRow);

        _initialized = true;
    }

    public static void RedrawSeparator()
    {
        Console.SetCursorPosition(0, SeparatorRow);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(new string('─', Console.WindowWidth));
        Console.ResetColor();
    }

    public static void WriteToOutputArea(Action writeAction)
    {
        if (!_initialized)
        {
            writeAction();
            return;
        }

        // Save cursor position
        Console.Write("\e7");

        // Move cursor to the bottom of the output scrolling region
        Console.SetCursorPosition(0, OutputAreaHeight - 1);

        // Write a newline to scroll the region if needed, then the content
        Console.WriteLine();
        writeAction();

        // Restore cursor position
        Console.Write("\e8");
    }

    public static void WriteLineToOutputArea(string text)
    {
        if (!_initialized)
        {
            Console.WriteLine(text);
            return;
        }

        Console.Write("\e7");
        Console.SetCursorPosition(0, OutputAreaHeight - 1);
        Console.WriteLine();
        Console.Write(text);
        Console.Write("\e8");
    }

    public static void BeginStreamOutput()
    {
        if (!_initialized) return;

        // Save cursor and move to output area for streaming
        Console.Write("\e7");
        Console.SetCursorPosition(0, OutputAreaHeight - 1);
        Console.WriteLine();
    }

    public static void EndStreamOutput()
    {
        if (!_initialized) return;

        // Restore cursor to input line
        Console.Write("\e8");
    }

    public static void ClearInputLine()
    {
        Console.SetCursorPosition(0, InputRow);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, InputRow);
    }

    public static void HandleResize()
    {
        if (!_initialized) return;

        if (Console.WindowWidth != _lastWindowWidth || Console.WindowHeight != _lastWindowHeight)
        {
            _lastWindowWidth = Console.WindowWidth;
            _lastWindowHeight = Console.WindowHeight;

            // Re-establish scrolling region
            Console.Write($"\e[1;{OutputAreaHeight}r");
            RedrawSeparator();
            Console.SetCursorPosition(0, InputRow);
        }
    }

    public static void ResetLayout()
    {
        if (!_initialized) return;

        // Reset scrolling region to full terminal
        Console.Write("\e[r");

        // Move cursor to bottom
        Console.SetCursorPosition(0, Console.WindowHeight - 1);
        Console.WriteLine();

        _initialized = false;
    }
}
