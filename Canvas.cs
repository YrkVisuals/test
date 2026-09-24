using System.Text;

namespace ConsoleApp1;

internal sealed class Canvas
{
    private static readonly string[] ForegroundCodes = BuildCodes(38);
    private static readonly string[] BackgroundCodes = BuildCodes(48);
    private readonly char[,] _characters;
    private readonly short[,] _foregrounds;
    private readonly short[,] _backgrounds;

    public Canvas(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _characters = new char[Width, Height];
        _foregrounds = new short[Width, Height];
        _backgrounds = new short[Width, Height];
        Clear();
    }

    public int Width { get; }

    public int Height { get; }

    public void Clear()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                _characters[x, y] = ' ';
                _foregrounds[x, y] = -1;
                _backgrounds[x, y] = -1;
            }
        }
    }

    public void Set(int x, int y, char character, int foreground = -1, int background = -1)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return;
        }

        _characters[x, y] = character;
        _foregrounds[x, y] = foreground < 0 || foreground > 255 ? (short)-1 : (short)foreground;
        _backgrounds[x, y] = background < 0 || background > 255 ? (short)-1 : (short)background;
    }

    public void Tint(int foreground, int background, int phase)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if ((x + y + phase) % 4 is 0 or 1)
                {
                    if (foreground is >= 0 and <= 255)
                    {
                        _foregrounds[x, y] = (short)foreground;
                    }

                    if (background is >= 0 and <= 255)
                    {
                        _backgrounds[x, y] = (short)background;
                    }
                }
            }
        }
    }

    public void FillRect(int x, int y, int width, int height, char character, int foreground = -1, int background = -1)
    {
        var left = Math.Max(0, x);
        var top = Math.Max(0, y);
        var right = Math.Min(Width, x + width);
        var bottom = Math.Min(Height, y + height);
        for (var py = top; py < bottom; py++)
        {
            for (var px = left; px < right; px++)
            {
                Set(px, py, character, foreground, background);
            }
        }
    }

    public void DrawText(int x, int y, string text, int foreground = -1, int background = -1)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var cursor = x;
        foreach (var character in text)
        {
            if (character is '\r' or '\n')
            {
                continue;
            }

            if (cursor >= Width)
            {
                break;
            }

            Set(cursor, y, character, foreground, background);
            cursor++;
        }
    }

    public void DrawTextCentered(int y, string text, int foreground = -1, int background = -1)
    {
        var x = Math.Max(0, (Width - text.Length) / 2);
        DrawText(x, y, text, foreground, background);
    }

    public void DrawHorizontalLine(int y, int left, int right, char character, int foreground = -1, int background = -1)
    {
        for (var x = Math.Max(0, left); x <= Math.Min(Width - 1, right); x++)
        {
            Set(x, y, character, foreground, background);
        }
    }

    public string Render()
    {
        var builder = new StringBuilder(Width * Height * 3);
        var currentForeground = -2;
        var currentBackground = -2;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var foreground = _foregrounds[x, y];
                var background = _backgrounds[x, y];
                if (foreground != currentForeground)
                {
                    if (foreground < 0)
                    {
                        builder.Append("\x1b[39m");
                    }
                    else
                    {
                        builder.Append(ForegroundCodes[foreground]);
                    }

                    currentForeground = foreground;
                }

                if (background != currentBackground)
                {
                    if (background < 0)
                    {
                        builder.Append("\x1b[49m");
                    }
                    else
                    {
                        builder.Append(BackgroundCodes[background]);
                    }

                    currentBackground = background;
                }

                builder.Append(_characters[x, y]);
            }

            builder.Append("\x1b[0m");
            currentForeground = -2;
            currentBackground = -2;
            if (y < Height - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string[] BuildCodes(int command)
    {
        var codes = new string[256];
        for (var i = 0; i < codes.Length; i++)
        {
            codes[i] = $"\x1b[{command};5;{i}m";
        }

        return codes;
    }
}
