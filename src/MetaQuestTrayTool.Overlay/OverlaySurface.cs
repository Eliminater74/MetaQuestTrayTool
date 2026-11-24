namespace MetaQuestTrayTool.Overlay;

public static class OverlaySurface
{
    public const int Width = 512;
    public const int Height = 256;

    public static byte[] Render(string text, OverlayDisplayMode mode)
    {
        var pixels = new byte[Width * Height * 4];
        if (mode == OverlayDisplayMode.Hidden)
        {
            return pixels;
        }

        var background = mode == OverlayDisplayMode.Compact ? (byte)28 : (byte)12;
        Fill(pixels, 0, 0, Width, Height, background, background, (byte)(background + 20), 255);
        Fill(pixels, 0, 192, 256, 64, 20, 80, 40, 255);
        Fill(pixels, 256, 192, 256, 64, 80, 30, 30, 255);
        DrawText(pixels, 16, 16, mode == OverlayDisplayMode.Compact ? "COMPACT" : "DETAIL");
        var body = mode == OverlayDisplayMode.Compact ? FirstLine(text) : text;
        DrawText(pixels, 16, 48, body);
        DrawText(pixels, 16, 208, "MODE");
        DrawText(pixels, 272, 208, "STOP BENCH");
        return pixels;
    }

    private static string FirstLine(string text)
    {
        var line = (text ?? string.Empty).Split('\n', '\r')[0];
        return line.Length <= 42 ? line : line[..42];
    }

    private static void Fill(byte[] pixels, int x, int y, int width, int height, byte r, byte g, byte b, byte a)
    {
        for (var row = y; row < y + height && row < Height; row++)
        {
            for (var col = x; col < x + width && col < Width; col++)
            {
                if (row < 0 || col < 0)
                {
                    continue;
                }

                var index = ((row * Width) + col) * 4;
                pixels[index] = r;
                pixels[index + 1] = g;
                pixels[index + 2] = b;
                pixels[index + 3] = a;
            }
        }
    }

    private static void DrawText(byte[] pixels, int x, int y, string text)
    {
        var cursor = x;
        foreach (var ch in (text ?? string.Empty).ToUpperInvariant())
        {
            if (ch == '\n' || ch == '\r')
            {
                continue;
            }

            DrawGlyph(pixels, cursor, y, ch);
            cursor += 8;
            if (cursor > Width - 8)
            {
                break;
            }
        }
    }

    private static void DrawGlyph(byte[] pixels, int x, int y, char ch)
    {
        if (!Glyphs.TryGetValue(ch, out var rows))
        {
            rows = Glyphs[' '];
        }

        for (var row = 0; row < rows.Length; row++)
        {
            var bits = rows[row];
            for (var col = 0; col < 5; col++)
            {
                if ((bits & (1 << (4 - col))) == 0)
                {
                    continue;
                }

                Fill(pixels, x + col, y + row, 1, 1, 230, 240, 245, 255);
            }
        }
    }

    private static readonly Dictionary<char, byte[]> Glyphs = Build();

    private static Dictionary<char, byte[]> Build()
    {
        var map = new Dictionary<char, byte[]>();
        void Add(char ch, string rows) => map[ch] = rows.Split('/').Select(row => Convert.ToByte(row, 2)).ToArray();
        Add(' ', "00000/00000/00000/00000/00000/00000/00000");
        Add('0', "01110/10001/10011/10101/11001/10001/01110");
        Add('1', "00100/01100/00100/00100/00100/00100/01110");
        Add('2', "01110/10001/00001/00010/00100/01000/11111");
        Add('3', "11110/00001/00001/01110/00001/00001/11110");
        Add('4', "00010/00110/01010/10010/11111/00010/00010");
        Add('5', "11111/10000/11110/00001/00001/10001/01110");
        Add('6', "00110/01000/10000/11110/10001/10001/01110");
        Add('7', "11111/00001/00010/00100/01000/01000/01000");
        Add('8', "01110/10001/10001/01110/10001/10001/01110");
        Add('9', "01110/10001/10001/01111/00001/00010/01100");
        Add('A', "01110/10001/10001/11111/10001/10001/10001");
        Add('B', "11110/10001/10001/11110/10001/10001/11110");
        Add('C', "01110/10001/10000/10000/10000/10001/01110");
        Add('D', "11110/10001/10001/10001/10001/10001/11110");
        Add('E', "11111/10000/10000/11110/10000/10000/11111");
        Add('F', "11111/10000/10000/11110/10000/10000/10000");
        Add('G', "01110/10001/10000/10111/10001/10001/01111");
        Add('H', "10001/10001/10001/11111/10001/10001/10001");
        Add('I', "01110/00100/00100/00100/00100/00100/01110");
        Add('L', "10000/10000/10000/10000/10000/10000/11111");
        Add('M', "10001/11011/10101/10101/10001/10001/10001");
        Add('N', "10001/11001/10101/10011/10001/10001/10001");
        Add('O', "01110/10001/10001/10001/10001/10001/01110");
        Add('P', "11110/10001/10001/11110/10000/10000/10000");
        Add('R', "11110/10001/10001/11110/10100/10010/10001");
        Add('S', "01111/10000/10000/01110/00001/00001/11110");
        Add('T', "11111/00100/00100/00100/00100/00100/00100");
        Add('U', "10001/10001/10001/10001/10001/10001/01110");
        Add('V', "10001/10001/10001/10001/10001/01010/00100");
        Add('Y', "10001/10001/01010/00100/00100/00100/00100");
        Add('%', "11001/11010/00010/00100/01000/01011/10011");
        Add('.', "00000/00000/00000/00000/00000/01100/01100");
        Add(':', "00000/01100/01100/00000/01100/01100/00000");
        Add('-', "00000/00000/00000/11111/00000/00000/00000");
        Add('/', "00001/00010/00010/00100/01000/01000/10000");
        return map;
    }
}
