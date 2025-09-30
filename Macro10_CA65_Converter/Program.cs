using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Macro10_CA65_Converter;

internal class Program
{
    static readonly HashSet<string> ifs = ["IFE", "IFN", "DEFINE"];
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
    static List<Block>? Blockize(string text, ref int i, ref int col, ref int ln, Stack<(int, int, int)> stack)
    {
        var blocks = new List<Block>();
        var builder = new StringBuilder();
        uint line_end_count = 0;

        for (; i < text.Length; )
        {
            line_end_count %= 2;
            char c = text[i];
            switch (c)
            {
                case '\r': //\r\n
                case '\n':
                    ++line_end_count;
                    break;
                case '>':
                    {
                        var _text = builder.ToString();
                        blocks.Add(new() { Text = _text });
                        ++i;
                        if (stack.Count > 0)
                        {
                            stack.Pop();
                            return blocks;
                        }
                        else
                        {

                        }
                        break;
                    }
                case '<':
                    {
                        blocks.Add(new() { Text = builder.ToString() });
                        builder.Clear();
                        stack.Push((i, col, ln));
                        ++i;
                        blocks.Add(new()
                        {
                            Pre = "<",
                            Children = Blockize(text, ref i, ref col, ref ln, stack),
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
                if (text[i - 1] != '\r' && text[i - 1] != '\n')
                {
                    line_end_count = 0;
                }
                else
                {
                    col = 1;
                    ln++;
                    line_end_count = 0;
                    builder.Append(text[i - 1]);
                    builder.Append(text[i - 0]);
                    var line = builder.ToString();
                    builder.Clear();

                    var sp = i - line.Length + 1;
                    var parts = line.Split(['\r', '\n', '\t', '\f', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && parts[0].Equals("COMMENT", StringComparison.OrdinalIgnoreCase))
                    {
                        var end = parts[1];
                        var ep = text.IndexOf(end, i + 1);

                        if (ep >= 0)
                        {
                            ep += end.Length;

                            blocks.Add(new Block() { Text = $"{text[sp..ep]}" });
                            i = ep;
                            continue;
                        }

                    }
                    else if (parts.Length > 1 && parts[0].Equals("RADIX", StringComparison.OrdinalIgnoreCase))
                    {
                        radix = int.TryParse(parts[1], out radix) ? radix : 10;
                        blocks.Add(new() { Text = $";{line}" });
                    }
                    else if (parts.Length > 0 && (directives.Contains(parts[0].ToUpperInvariant()) || line.StartsWith('$')))
                    {
                        blocks.Add(new() { Text = $";{line}" });
                    }
                    else
                    {
                        blocks.Add(new() { Text = $"{line}" });
                    }
                }
            }
            ++i;
        }

        return blocks;

    }
    static int ConvertFile(string input, string output, params string[] includes)
    {
        var text = File.ReadAllText(input);
        int i = 0, col=1,ln = 1;
        Stack<(int, int, int)> stack = new();

        var blocks = Blockize(text, ref i,ref col, ref ln, stack);

        using var reader = new StreamReader(input);
        using var writer = new StreamWriter(output);
        foreach (var include in includes)
        {
            writer.WriteLine($".INCLUDE \"{include}\"");
        }
        foreach(var block in blocks ?? [])
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
