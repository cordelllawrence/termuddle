namespace tuichat;

public static class ConsoleHelper
{
    private static readonly List<string> _history = new();
    private static int _historyIndex;
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

    public static string ReadInputWithHistory()
    {
        Console.ForegroundColor = ConsoleColor.Green;

        var buffer = new List<char>();
        var cursorPos = 0;
        _historyIndex = _history.Count; // past the end = "current" (empty)
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
                        RedrawLine(buffer, cursorPos);
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursorPos < buffer.Count)
                    {
                        buffer.RemoveAt(cursorPos);
                        RedrawLine(buffer, cursorPos);
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorPos > 0)
                    {
                        cursorPos--;
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorPos < buffer.Count)
                    {
                        cursorPos++;
                        Console.SetCursorPosition(Console.CursorLeft + 1, Console.CursorTop);
                    }
                    break;

                case ConsoleKey.Home:
                    cursorPos = 0;
                    Console.SetCursorPosition(Console.CursorLeft - cursorPos, Console.CursorTop);
                    break;

                case ConsoleKey.End:
                    Console.SetCursorPosition(Console.CursorLeft + (buffer.Count - cursorPos), Console.CursorTop);
                    cursorPos = buffer.Count;
                    break;

                case ConsoleKey.UpArrow:
                    if (_historyIndex > 0)
                    {
                        if (_historyIndex == _history.Count)
                            savedCurrent = new string(buffer.ToArray());
                        _historyIndex--;
                        SetBuffer(buffer, _history[_historyIndex], out cursorPos);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (_historyIndex < _history.Count)
                    {
                        _historyIndex++;
                        var text = _historyIndex == _history.Count
                            ? savedCurrent ?? ""
                            : _history[_historyIndex];
                        SetBuffer(buffer, text, out cursorPos);
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
                            RedrawLine(buffer, cursorPos);
                        }
                    }
                    break;
            }
        }
    }

    private static void SetBuffer(List<char> buffer, string text, out int cursorPos)
    {
        var promptLeft = Console.CursorLeft - buffer.Count;
        buffer.Clear();
        buffer.AddRange(text);
        cursorPos = buffer.Count;

        // Clear old text and write new
        Console.SetCursorPosition(promptLeft, Console.CursorTop);
        Console.Write(text);
        // Clear any leftover characters from a longer previous line
        var clearCount = Console.BufferWidth - Console.CursorLeft - 1;
        if (clearCount > 0)
            Console.Write(new string(' ', clearCount));
        Console.SetCursorPosition(promptLeft + cursorPos, Console.CursorTop);
    }

    private static void RedrawLine(List<char> buffer, int cursorPos)
    {
        var promptLeft = Console.CursorLeft - cursorPos;
        Console.SetCursorPosition(promptLeft, Console.CursorTop);
        var text = new string(buffer.ToArray());
        Console.Write(text + " "); // extra space clears trailing char on delete
        Console.SetCursorPosition(promptLeft + cursorPos, Console.CursorTop);
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
