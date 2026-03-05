namespace tuichat;

public static class TerminalLayout
{
    private static bool _initialized;
    private static int _lastWindowWidth;
    private static int _lastWindowHeight;
    private static int _inputLineCount = 1;

    public static bool IsInitialized => _initialized;

    public static int InputLineCount => _inputLineCount;

    public static int SeparatorRow => Console.WindowHeight - 1 - _inputLineCount;

    public static int OutputAreaHeight => SeparatorRow;

    public static int InputStartRow => SeparatorRow + 1;

    public static void Initialize()
    {
        _lastWindowWidth = Console.WindowWidth;
        _lastWindowHeight = Console.WindowHeight;
        _inputLineCount = 1;

        // Set scrolling region to rows 1 through OutputAreaHeight
        // ANSI scrolling regions are 1-based
        Console.Write($"\e[1;{OutputAreaHeight}r");

        // Draw separator
        RedrawSeparator();

        // Position cursor at input area
        Console.SetCursorPosition(0, InputStartRow);

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

    public static void ClearInputArea()
    {
        for (int row = InputStartRow; row < Console.WindowHeight; row++)
        {
            Console.SetCursorPosition(0, row);
            Console.Write(new string(' ', Console.WindowWidth));
        }
        Console.SetCursorPosition(0, InputStartRow);
    }

    /// <summary>
    /// Update the input area height. If the line count changed, re-set scrolling region,
    /// redraw separator, and clear old input rows.
    /// </summary>
    public static void UpdateInputHeight(int lineCount)
    {
        if (lineCount < 1) lineCount = 1;

        // Cap to avoid consuming more than half the terminal
        var maxLines = Math.Max(1, Console.WindowHeight / 2);
        if (lineCount > maxLines) lineCount = maxLines;

        if (lineCount == _inputLineCount) return;

        _inputLineCount = lineCount;

        // Re-establish scrolling region
        Console.Write($"\e[1;{OutputAreaHeight}r");

        // Redraw separator at new position
        RedrawSeparator();

        // Clear entire input area (old content may be stale)
        ClearInputArea();
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
            Console.SetCursorPosition(0, InputStartRow);
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

        _inputLineCount = 1;
        _initialized = false;
    }
}
