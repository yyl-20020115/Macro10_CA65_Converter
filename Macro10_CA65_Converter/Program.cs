using System.Text;

namespace Macro10_CA65_Converter;

internal class Program
{
    static readonly HashSet<string> directives =
    [
        "PAGE", "SUBTTL", "TITLE", "SEARCH", "SALL"
    ];

    static string ConvertNumberText(string text, int radix)
    {
        var output_hex = false;
        var done = false;
        int value = 0;
        switch (text[0])
        {
            case '^':
                switch (text[1])
                {
                    case '0':
                        done = int.TryParse(text[2..], out value);
                        break;
                    case 'H':
                        done = int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
                        output_hex = true;
                        break;
                    case 'O': //BIG 'o'
                        try
                        {
                            value = Convert.ToInt32(text[2..], 8);
                        }
                        catch (Exception e)
                        {
                            done = false;
                            output_hex = true;
                        }
                        break;
                }
                break;
            default:
                if (radix == 10)
                {
                    done = int.TryParse(text[2..], out value);
                }
                else
                {
                    try
                    {
                        value = Convert.ToInt32(text[2..], radix);
                        output_hex = true;
                        if (value < 10 && value < radix)
                        {
                            output_hex = false;
                        }
                        done = true;
                    }
                    catch (Exception e)
                    {
                        done = false;
                        output_hex = true;
                    }
                }
                break;
        }
        return output_hex ? $"${value:X04}" : value.ToString();
    }

    class Block
    {
        public string? Text;
        public string? Pre;
        public List<Block>? Children;
        public string? Post;
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Pre ?? "");
            if (Text != null)
            {
                builder.Append(Text);
            }
            else if (Children != null)
            {
                foreach (var child in Children)
                {
                    builder.Append(child.ToString());
                }
            }
            builder.Append(Post ?? "");
            return builder.ToString();
        }
    }
    static int radix = 10;
    static List<Block>? Blockize(TextReader reader, ref int ln, ref int col, Stack<(int, int)> stack, List<Block>? blocks)
    {
        var builder = new StringBuilder();
        uint line_end_count = 0;
        char tail0 = '\0';
        char tail1 = '\0';
        int v;
        int quoting = 0;
        int angles = 0;
        while (-1 != (v = reader.Peek()))
        {
            line_end_count %= 2;
            char c = (char)v;
            if (c == '"')
            {
                quoting ++;
            }
            switch (c)
            {
                case '\r': //\r\n
                case '\n':
                    tail1 = tail0;
                    tail0 = c;
                    ++line_end_count;
                    break;
                case '>':
                    --angles;
                    {
                        var _text = builder.ToString();
                        blocks?.Add(new() { Text = _text });
                        reader.Read();
                        if (stack.Count > 0)
                        {
                            stack.Pop();
                            return blocks;
                        }
                        break;
                    }
                case '<':
                    ++angles;
                    if (quoting > 0)
                    {
                        // inside quotes, treat as normal char
                        goto default;
                    }
                    else
                    {
                        blocks?.Add(new() { Text = builder.ToString() });
                        builder.Clear();
                        stack.Push((ln, col));
                        reader.Read();
                        blocks?.Add(new()
                        {
                            Pre = "<",
                            Children = Blockize(reader, ref ln, ref col, stack, []),
                            Post = ">"
                        });
                    }
                    continue;
                default:
                    builder.Append(c);
                    break;
            }
            ++col;
            if (line_end_count == 2)
            {
                angles = 0;
                quoting = 0;
                col = 1;
                ln++;
                line_end_count = 0;
                builder.Append(tail1);
                builder.Append(tail0);
                tail0 = '\0';
                tail1 = '\0';
                var line = builder.ToString();
                builder.Clear();

                var parts = line.Split(['\r', '\n', '\t', '\f', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && parts[0].Equals("COMMENT", StringComparison.OrdinalIgnoreCase))
                {
                    reader.Read();
                    string val = "";
                    var end = parts[1];
                    while (-1 != (v = reader.Read()))
                    {
                        val += (char)v;
                        if (val.EndsWith(end, StringComparison.Ordinal))
                        {
                            break;
                        }
                    }

                    blocks?.Add(new Block() { Text = $"{line}{val}" });
                    continue;
                }
                else if (parts.Length > 1 && parts[0].Equals("RADIX", StringComparison.OrdinalIgnoreCase))
                {
                    radix = int.TryParse(parts[1], out radix) ? radix : 10;
                    blocks?.Add(new() { Text = $";{line}" });
                }
                else if (parts.Length > 0 && (directives.Contains(parts[0].ToUpperInvariant()) || line.StartsWith('$')))
                {
                    blocks?.Add(new() { Text = $";{line}" });
                }
                else if (line.IndexOf(';') is int p && p >= 0)
                {
                    blocks?.Add(new() { Text = line[..p] });
                    blocks?.Add(new() { Text = line[p..] });
                }
                else
                {
                    blocks?.Add(new() { Text = $"{line}" });
                }

            }
            reader.Read();
        }
        if (builder.Length > 0)
        {
            blocks?.Add(new() { Text = builder.ToString() });
        }
        return blocks;
    }


    static int ConvertFile(string input, string output, params string[] includes)
    {
        int ln = 1, col = 1;
        Stack<(int, int)> stack = new();
        using var reader = new StreamReader(input);
        using var writer = new StreamWriter(output);

        var blocks = Blockize(reader, ref ln, ref col, stack, []);

        foreach (var include in includes)
        {
            writer.WriteLine($".INCLUDE \"{include}\"");
        }
        foreach (var block in blocks ?? [])
        {
            writer.Write(block);
        }

        return 0;
    }
    static int Main(string[] args)
    {
        string[] includes = ["macros.inc"];
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: Macro10_CA65_Converter <inputfile>");
            return 0;
        }
        if (!File.Exists(args[0]))
        {
            Console.WriteLine($"Error: File '{args[0]}' not found.");
            return 1;
        }
        return ConvertFile(args[0], Path.ChangeExtension(args[0], ".cvt.asm"), includes);
    }
}
