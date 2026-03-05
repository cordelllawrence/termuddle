using Spectre.Console;

namespace tuichat;

public static class ConsoleHelper
{
    private static readonly List<string> _history = new();
    private static int _historyIndex;

    public static void WriteSystem(string message)
    {
        TerminalLayout.WriteToOutputArea(() =>
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]"));
    }

    public static void WriteError(string message)
    {
        TerminalLayout.WriteToOutputArea(() =>
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]"));
    }

    public static void WritePrompt(string prompt)
    {
        // Prompt is written directly to the input line, not the output area
        if (TerminalLayout.IsInitialized)
        {
            TerminalLayout.ClearInputLine();
            AnsiConsole.Markup($"[green]{Markup.Escape(prompt)}[/]");
        }
        else
        {
            AnsiConsole.Markup($"[green]{Markup.Escape(prompt)}[/]");
        }
    }

    public static void WriteResponse(string message)
    {
        TerminalLayout.WriteToOutputArea(() =>
            AnsiConsole.MarkupLine($"[cyan]{Markup.Escape(message)}[/]"));
    }

    public static void WriteInfo(string message)
    {
        TerminalLayout.WriteToOutputArea(() =>
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]"));
    }

    public static void WriteStreamChunk(string chunk)
    {
        // During streaming, cursor is already in the output area (via BeginStreamOutput)
        AnsiConsole.Markup($"[cyan]{Markup.Escape(chunk)}[/]");
    }

    public static void EnsureCursorVisible()
    {
        Console.CursorVisible = true;
    }

    public static string ReadInput()
    {
        EnsureCursorVisible();
        Console.ForegroundColor = ConsoleColor.Green;

        if (TerminalLayout.IsInitialized)
        {
            // Simple line read constrained to input row
            var buffer = new List<char>();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.ResetColor();
                    return new string(buffer.ToArray());
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Count > 0)
                    {
                        buffer.RemoveAt(buffer.Count - 1);
                        Console.Write("\b \b");
                    }
                }
                else if (key.KeyChar >= ' ')
                {
                    buffer.Add(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
        }
        else
        {
            var input = Console.ReadLine();
            Console.ResetColor();
            return input ?? string.Empty;
        }
    }

    public static string ReadInputWithHistory()
    {
        EnsureCursorVisible();
        Console.ForegroundColor = ConsoleColor.Green;

        if (!TerminalLayout.IsInitialized)
            return ReadInputWithHistoryClassic();

        return ReadInputWithHistoryFixed();
    }

    private static string ReadInputWithHistoryFixed()
    {
        var promptLeft = Console.CursorLeft;
        var inputRow = TerminalLayout.InputRow;
        var buffer = new List<char>();
        var cursorPos = 0;
        var viewOffset = 0;
        _historyIndex = _history.Count;
        string? savedCurrent = null;

        int AvailableWidth() => Math.Max(1, Console.WindowWidth - promptLeft);

        void Redraw()
        {
            var availWidth = AvailableWidth();

            // Adjust viewOffset to keep cursor visible
            if (cursorPos < viewOffset)
                viewOffset = cursorPos;
            else if (cursorPos >= viewOffset + availWidth)
                viewOffset = cursorPos - availWidth + 1;

            // Ensure viewOffset is valid
            if (viewOffset < 0) viewOffset = 0;

            Console.SetCursorPosition(promptLeft, inputRow);

            var visibleLen = Math.Min(availWidth, buffer.Count - viewOffset);
            if (visibleLen < 0) visibleLen = 0;

            var visible = new string(buffer.Skip(viewOffset).Take(visibleLen).ToArray());

            // Draw indicators for overflow
            var display = visible;
            if (viewOffset > 0 && display.Length > 0)
                display = "◀" + display[1..];
            if (viewOffset + availWidth < buffer.Count && display.Length > 0)
                display = display[..^1] + "▶";

            Console.Write(display);

            // Clear remaining space
            var clearLen = availWidth - display.Length;
            if (clearLen > 0)
                Console.Write(new string(' ', clearLen));

            // Position cursor
            var cursorCol = promptLeft + (cursorPos - viewOffset);
            if (cursorCol >= Console.WindowWidth)
                cursorCol = Console.WindowWidth - 1;
            Console.SetCursorPosition(cursorCol, inputRow);
        }

        void SetBufferContent(string text)
        {
            buffer.Clear();
            buffer.AddRange(text);
            cursorPos = buffer.Count;
            viewOffset = 0;
            Redraw();
        }

        while (true)
        {
            TerminalLayout.HandleResize();
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.ResetColor();
                    var result = new string(buffer.ToArray());
                    if (!string.IsNullOrWhiteSpace(result))
                        _history.Add(result);
                    // Echo the input to the output area
                    TerminalLayout.WriteToOutputArea(() =>
                    {
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt");
                        AnsiConsole.MarkupLine($"[green]{Markup.Escape($"[{timestamp}] You> ")}{Markup.Escape(result)}[/]");
                    });
                    return result;

                case ConsoleKey.Backspace:
                    if (cursorPos > 0)
                    {
                        buffer.RemoveAt(cursorPos - 1);
                        cursorPos--;
                        Redraw();
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursorPos < buffer.Count)
                    {
                        buffer.RemoveAt(cursorPos);
                        Redraw();
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorPos > 0)
                    {
                        cursorPos--;
                        Redraw();
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorPos < buffer.Count)
                    {
                        cursorPos++;
                        Redraw();
                    }
                    break;

                case ConsoleKey.Home:
                    cursorPos = 0;
                    Redraw();
                    break;

                case ConsoleKey.End:
                    cursorPos = buffer.Count;
                    Redraw();
                    break;

                case ConsoleKey.UpArrow:
                    if (_historyIndex > 0)
                    {
                        if (_historyIndex == _history.Count)
                            savedCurrent = new string(buffer.ToArray());
                        _historyIndex--;
                        SetBufferContent(_history[_historyIndex]);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (_historyIndex < _history.Count)
                    {
                        _historyIndex++;
                        var text = _historyIndex == _history.Count
                            ? savedCurrent ?? ""
                            : _history[_historyIndex];
                        SetBufferContent(text);
                    }
                    break;

                default:
                    if (key.KeyChar >= ' ')
                    {
                        buffer.Insert(cursorPos, key.KeyChar);
                        cursorPos++;
                        Redraw();
                    }
                    break;
            }
        }
    }

    private static string ReadInputWithHistoryClassic()
    {
        var promptLeft = Console.CursorLeft;
        var promptTop = Console.CursorTop;
        var buffer = new List<char>();
        var cursorPos = 0;
        _historyIndex = _history.Count;
        string? savedCurrent = null;

        while (true)
        {
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.ResetColor();
                    Console.WriteLine();
                    var result = new string(buffer.ToArray());
                    if (!string.IsNullOrWhiteSpace(result))
                        _history.Add(result);
                    return result;

                case ConsoleKey.Backspace:
                    if (cursorPos > 0)
                    {
                        buffer.RemoveAt(cursorPos - 1);
                        cursorPos--;
                        RedrawLine(promptLeft, promptTop, buffer, cursorPos);
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursorPos < buffer.Count)
                    {
                        buffer.RemoveAt(cursorPos);
                        RedrawLine(promptLeft, promptTop, buffer, cursorPos);
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorPos > 0)
                    {
                        cursorPos--;
                        SetCursorFromOffset(promptLeft, promptTop, cursorPos);
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorPos < buffer.Count)
                    {
                        cursorPos++;
                        SetCursorFromOffset(promptLeft, promptTop, cursorPos);
                    }
                    break;

                case ConsoleKey.Home:
                    cursorPos = 0;
                    SetCursorFromOffset(promptLeft, promptTop, cursorPos);
                    break;

                case ConsoleKey.End:
                    cursorPos = buffer.Count;
                    SetCursorFromOffset(promptLeft, promptTop, cursorPos);
                    break;

                case ConsoleKey.UpArrow:
                    if (_historyIndex > 0)
                    {
                        if (_historyIndex == _history.Count)
                            savedCurrent = new string(buffer.ToArray());
                        _historyIndex--;
                        SetBuffer(promptLeft, promptTop, buffer, _history[_historyIndex], out cursorPos);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (_historyIndex < _history.Count)
                    {
                        _historyIndex++;
                        var text = _historyIndex == _history.Count
                            ? savedCurrent ?? ""
                            : _history[_historyIndex];
                        SetBuffer(promptLeft, promptTop, buffer, text, out cursorPos);
                    }
                    break;

                default:
                    if (key.KeyChar >= ' ')
                    {
                        buffer.Insert(cursorPos, key.KeyChar);
                        cursorPos++;
                        if (cursorPos == buffer.Count)
                        {
                            Console.Write(key.KeyChar);
                        }
                        else
                        {
                            RedrawLine(promptLeft, promptTop, buffer, cursorPos);
                        }
                    }
                    break;
            }
        }
    }

    private static (int col, int row) GetCursorPosition(int promptLeft, int promptTop, int offset)
    {
        var width = Console.WindowWidth;
        var absolutePos = promptLeft + offset;
        var rowOffset = absolutePos / width;
        var col = absolutePos % width;
        return (col, promptTop + rowOffset);
    }

    private static void SetCursorFromOffset(int promptLeft, int promptTop, int offset)
    {
        var (col, row) = GetCursorPosition(promptLeft, promptTop, offset);
        Console.SetCursorPosition(col, row);
    }

    private static void SetBuffer(int promptLeft, int promptTop, List<char> buffer, string text, out int cursorPos)
    {
        var oldLen = buffer.Count;
        buffer.Clear();
        buffer.AddRange(text);
        cursorPos = buffer.Count;

        Console.SetCursorPosition(promptLeft, promptTop);
        Console.Write(text);
        var clearCount = oldLen - text.Length;
        if (clearCount > 0)
            Console.Write(new string(' ', clearCount));
        SetCursorFromOffset(promptLeft, promptTop, cursorPos);
    }

    private static void RedrawLine(int promptLeft, int promptTop, List<char> buffer, int cursorPos)
    {
        Console.SetCursorPosition(promptLeft, promptTop);
        var text = new string(buffer.ToArray());
        Console.Write(text + " ");
        SetCursorFromOffset(promptLeft, promptTop, cursorPos);
    }

    public static string ReadInputDefault(string prompt, string defaultValue)
    {
        EnsureCursorVisible();
        AnsiConsole.Markup($"[yellow]{Markup.Escape(prompt)} [{Markup.Escape(defaultValue)}]: [/]");
        Console.ForegroundColor = ConsoleColor.Green;
        var input = Console.ReadLine()?.Trim();
        Console.ResetColor();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }

    public static string ReadInputMasked(string prompt)
    {
        EnsureCursorVisible();
        AnsiConsole.Markup($"[yellow]{Markup.Escape(prompt)}: [/]");
        Console.ForegroundColor = ConsoleColor.Green;

        var buffer = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.ResetColor();
                Console.WriteLine();
                return new string(buffer.ToArray());
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Count > 0)
                {
                    buffer.RemoveAt(buffer.Count - 1);
                    Console.Write("\b \b");
                }
            }
            else if (key.KeyChar >= ' ')
            {
                buffer.Add(key.KeyChar);
                Console.Write('*');
            }
        }
    }
}
