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
        // Prompt is written directly to the input area, not the output area
        if (TerminalLayout.IsInitialized)
        {
            TerminalLayout.ClearInputArea();
            AnsiConsole.Markup($"[white]{Markup.Escape(prompt)}[/]");
        }
        else
        {
            AnsiConsole.Markup($"[white]{Markup.Escape(prompt)}[/]");
        }
    }

    public static void WriteResponse(string message)
    {
        TerminalLayout.WriteToOutputArea(() =>
            AnsiConsole.MarkupLine($"[blue]{Markup.Escape(message)}[/]"));
    }

    public static void WriteInfo(string message)
    {
        TerminalLayout.WriteToOutputArea(() =>
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]"));
    }

    public static void WriteStreamChunk(string chunk)
    {
        // During streaming, cursor is already in the output area (via BeginStreamOutput)
        AnsiConsole.Markup($"[blue]{Markup.Escape(chunk)}[/]");
    }

    public static void EnsureCursorVisible()
    {
        Console.CursorVisible = true;
    }

    public static string ReadInput()
    {
        EnsureCursorVisible();
        Console.ForegroundColor = ConsoleColor.White;

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
        Console.ForegroundColor = ConsoleColor.White;

        if (!TerminalLayout.IsInitialized)
            return ReadInputWithHistoryClassic();

        return ReadInputWithHistoryFixed();
    }

    private static string ReadInputWithHistoryFixed()
    {
        var promptLen = Console.CursorLeft;
        var buffer = new List<char>();
        var cursorPos = 0;
        _historyIndex = _history.Count;
        string? savedCurrent = null;

        int width = Console.WindowWidth;
        int firstLineCap = Math.Max(1, width - promptLen);

        int GetLineCount()
        {
            if (buffer.Count <= firstLineCap) return 1;
            return 1 + (int)Math.Ceiling((double)(buffer.Count - firstLineCap) / width);
        }

        (int col, int row) CursorToPosition(int pos)
        {
            var inputStart = TerminalLayout.InputStartRow;
            if (pos <= firstLineCap)
                return (promptLen + pos, inputStart);

            var adjusted = pos - firstLineCap;
            return (adjusted % width, inputStart + 1 + adjusted / width);
        }

        int PositionToCursorPos(int col, int row)
        {
            var inputStart = TerminalLayout.InputStartRow;
            if (row == inputStart)
                return Math.Min(col - promptLen, firstLineCap);

            var rowOffset = row - inputStart - 1;
            return firstLineCap + rowOffset * width + col;
        }

        void Redraw()
        {
            width = Console.WindowWidth;
            firstLineCap = Math.Max(1, width - promptLen);

            var lineCount = GetLineCount();
            TerminalLayout.UpdateInputHeight(lineCount);

            var inputStart = TerminalLayout.InputStartRow;

            // Draw first line (after prompt)
            Console.SetCursorPosition(promptLen, inputStart);
            var firstChunk = Math.Min(firstLineCap, buffer.Count);
            if (firstChunk > 0)
                Console.Write(new string(buffer.GetRange(0, firstChunk).ToArray()));
            // Clear rest of first line
            var clearFirst = firstLineCap - firstChunk;
            if (clearFirst > 0)
                Console.Write(new string(' ', clearFirst));

            // Draw subsequent lines
            var written = firstChunk;
            for (int line = 1; line < lineCount; line++)
            {
                Console.SetCursorPosition(0, inputStart + line);
                var chunk = Math.Min(width, buffer.Count - written);
                if (chunk > 0)
                    Console.Write(new string(buffer.GetRange(written, chunk).ToArray()));
                var clearRest = width - chunk;
                if (clearRest > 0)
                    Console.Write(new string(' ', clearRest));
                written += chunk;
            }

            // Clear any leftover rows from previous longer input
            for (int row = inputStart + lineCount; row < Console.WindowHeight; row++)
            {
                Console.SetCursorPosition(0, row);
                Console.Write(new string(' ', width));
            }

            // Position cursor
            var (curCol, curRow) = CursorToPosition(cursorPos);
            Console.SetCursorPosition(curCol, curRow);
        }

        void SetBufferContent(string text)
        {
            buffer.Clear();
            buffer.AddRange(text);
            cursorPos = buffer.Count;
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
                    // Reset input area to single line before echoing
                    TerminalLayout.UpdateInputHeight(1);
                    // Echo the input to the output area
                    TerminalLayout.WriteToOutputArea(() =>
                    {
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt");
                        AnsiConsole.MarkupLine($"[dim grey]{Markup.Escape($"[{timestamp}]")}[/] [white]{Markup.Escape("You> ")}{Markup.Escape(result)}[/]");
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
                        var (lc, lr) = CursorToPosition(cursorPos);
                        Console.SetCursorPosition(lc, lr);
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorPos < buffer.Count)
                    {
                        cursorPos++;
                        var (rc, rr) = CursorToPosition(cursorPos);
                        Console.SetCursorPosition(rc, rr);
                    }
                    break;

                case ConsoleKey.Home:
                    cursorPos = 0;
                    Console.SetCursorPosition(promptLen, TerminalLayout.InputStartRow);
                    break;

                case ConsoleKey.End:
                    cursorPos = buffer.Count;
                    var (ec, er) = CursorToPosition(cursorPos);
                    Console.SetCursorPosition(ec, er);
                    break;

                case ConsoleKey.UpArrow:
                {
                    var (_, curRow) = CursorToPosition(cursorPos);
                    if (curRow > TerminalLayout.InputStartRow)
                    {
                        // Move up one visual row within multiline input
                        var (curCol, _) = CursorToPosition(cursorPos);
                        var targetPos = PositionToCursorPos(curCol, curRow - 1);
                        cursorPos = Math.Clamp(targetPos, 0, buffer.Count);
                        var (nc, nr) = CursorToPosition(cursorPos);
                        Console.SetCursorPosition(nc, nr);
                    }
                    else if (_historyIndex > 0)
                    {
                        if (_historyIndex == _history.Count)
                            savedCurrent = new string(buffer.ToArray());
                        _historyIndex--;
                        SetBufferContent(_history[_historyIndex]);
                    }
                    break;
                }

                case ConsoleKey.DownArrow:
                {
                    var (_, curRow) = CursorToPosition(cursorPos);
                    var lastRow = TerminalLayout.InputStartRow + GetLineCount() - 1;
                    if (curRow < lastRow)
                    {
                        // Move down one visual row within multiline input
                        var (curCol, _) = CursorToPosition(cursorPos);
                        var targetPos = PositionToCursorPos(curCol, curRow + 1);
                        cursorPos = Math.Clamp(targetPos, 0, buffer.Count);
                        var (nc, nr) = CursorToPosition(cursorPos);
                        Console.SetCursorPosition(nc, nr);
                    }
                    else if (_historyIndex < _history.Count)
                    {
                        _historyIndex++;
                        var text = _historyIndex == _history.Count
                            ? savedCurrent ?? ""
                            : _history[_historyIndex];
                        SetBufferContent(text);
                    }
                    break;
                }

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
        Console.ForegroundColor = ConsoleColor.White;
        var input = Console.ReadLine()?.Trim();
        Console.ResetColor();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }

    public static string ReadInputMasked(string prompt)
    {
        EnsureCursorVisible();
        AnsiConsole.Markup($"[yellow]{Markup.Escape(prompt)}: [/]");
        Console.ForegroundColor = ConsoleColor.White;

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
