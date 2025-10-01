using System.Text;

namespace Macro10_CA65_Converter;

internal class Program
{
    static readonly HashSet<string> DIRECTIVES =
    [
        "PAGE", "SUBTTL", "TITLE", "SEARCH", "SALL"
    ];
    static readonly HashSet<string> MACROS = [
        "IFE", "IFN", "DEFINE","IF1"
        ];

    static string ConvertNumberText(string text, int radix)
    {
        var output_hex = false;
        var done = false;
        int value = 0;
        switch (text[0])
        {
            case '^':
                switch (char.ToUpper(text[1]))
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
                            output_hex = true;
                            done = true;
                        }
                        catch (Exception e)
                        {
                            done = false;
                        }
                        break;
                }
                break;
            default:
                if (radix == 10)
                {
                    done = int.TryParse(text, out value);
                }
                else
                {
                    try
                    {
                        value = Convert.ToInt32(text, radix);
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

    public const string COMMENT_DIRECTIVE = "COMMENT";
    public const string RADIX_DIRECTIVE = "RADIX";
    public enum BlockType : int
    {
        Unknown,
        Identifier,
        Number,
        WhiteSpace,
        Comment,
        Operator,
        Container,
    }
    public class Block
    {
        public string? Text;
        public string? Pre;
        public List<Block>? Children;
        public string? Post;
        public BlockType? Type;
        public Block? Header;
        public List<Block>? Parts;
        public int? LineNumber;
        public int? ColumnNumber;
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

    static List<Block>? ParseLine(string line, List<Block>? local_list, int ln, int col)
    {
        var local_builder = new StringBuilder();
        var local_reader = new StringReader(line);
        int v;
        char c;
        char last_c = '\0';
        while (-1 != (v = local_reader.Peek()))
        {
            var any = false;
            c = (char)v;
            if (char.IsWhiteSpace(c))
            {
                any = true;
                local_builder.Clear();
                do
                {
                    c = (char)v;
                    if (!char.IsWhiteSpace(c)) break;
                    local_builder.Append(c);
                    local_reader.Read();
                    col++;
                } while (-1 != (v = local_reader.Peek()));
                local_list.Add(
                    new()
                    {
                        Text = local_builder.ToString(),
                        Type = BlockType.WhiteSpace,
                        LineNumber = ln,
                        ColumnNumber = col,

                    });
            }
            if (v == -1) break;
            if (char.IsDigit(c))
            {
                any = true;
                local_builder.Clear();
                do
                {
                    c = (char)v;
                    if (!char.IsDigit(c)) break;
                    local_builder.Append(c);
                    local_reader.Read();
                    col++;
                } while (-1 != (v = local_reader.Peek()));
                local_list.Add(
                    new()
                    {
                        Text = local_builder.ToString(),
                        Type = BlockType.Number,
                        LineNumber = ln,
                        ColumnNumber = col,
                    });
            }
            if (char.IsLetter(c))
            {
                any = true;
                local_builder.Clear();
                do
                {
                    c = (char)v;
                    if (!char.IsLetterOrDigit(c)) break;
                    local_builder.Append(c);
                    local_reader.Read();
                    col++;
                } while (-1 != (v = local_reader.Peek()));
                local_list.Add(
                    new()
                    {
                        Text = local_builder.ToString(),
                        Type = BlockType.Identifier,
                        LineNumber = ln,
                        ColumnNumber = col,
                    });
            }
            if (v == -1) break;
            if (char.IsPunctuation(c) || char.IsSymbol(c))
            {
                any = true;
                local_builder.Clear();
                do
                {
                    c = (char)v;
                    if (!char.IsPunctuation(c) && !char.IsSymbol(c)) break;
                    local_builder.Append(c);
                    local_reader.Read();
                    last_c = c;
                    col++;
                } while (-1 != (v = local_reader.Peek()));
                var sp = local_builder.ToString();
                col -= sp.Length;
                Block? last = null;
                foreach (var ct in sp)
                {
                    if (last != null && last.Text == "=" && ct == '=')
                    {
                        last.Text = "==";
                        continue;
                    }
                    else
                    {
                        local_list.Add(
                            last = new()
                            {
                                Text = ct.ToString(),
                                Type = BlockType.Operator,
                                LineNumber = ln,
                                ColumnNumber = col,
                            });
                        col++;
                    }
                }
            }
            if (v == -1) break;
            if (!any)
                local_reader.Read();
            last_c = c;
        }
        return local_list;
    }
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
                quoting++;
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
                        blocks?.AddRange(ParseLine(builder.ToString(), [], ln, col) ?? []);
                        reader.Read();
                        if (stack.Count > 0)
                        {
                            stack.Pop();
                            return blocks;
                        }
                        break;
                    }
                case '<':
                    if (quoting > 0 && quoting % 2 == 1)
                    {
                        // inside quotes, treat as normal char
                        goto default;
                    }
                    else
                    {
                        ++angles;
                        blocks?.AddRange(ParseLine(builder.ToString(), [], ln, col) ?? []);
                        builder.Clear();
                        stack.Push((ln, col));
                        reader.Read();
                        blocks?.Add(new()
                        {
                            Pre = "<",
                            Children = Blockize(reader, ref ln, ref col, stack, []),
                            Post = ">",
                            Type = BlockType.Container,
                            LineNumber = ln,
                            ColumnNumber = col,
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

                string? line_comment = null;
                builder.Clear();
                var parts = line.Split(['\r', '\n', '\t', '\f', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && parts[0].Equals(COMMENT_DIRECTIVE, StringComparison.OrdinalIgnoreCase))
                {
                    reader.Read();
                    var val = "";
                    var end = parts[1];
                    while (-1 != (v = reader.Read()))
                    {
                        val += (char)v;
                        if (val.EndsWith(end, StringComparison.Ordinal))
                        {
                            break;
                        }
                    }
                    foreach (var vl in val.Split(Environment.NewLine))
                    {
                        blocks?.Add(new() { 
                            Text = $";{vl}{Environment.NewLine}",
                            Type = BlockType.Comment,
                            LineNumber = ln,
                            ColumnNumber = col,
                        });
                    }

                    continue;
                }
                else if (parts.Length > 1 && parts[0].Equals(RADIX_DIRECTIVE, StringComparison.OrdinalIgnoreCase))
                {
                    radix = int.TryParse(parts[1], out radix) ? radix : 10;
                    blocks?.Add(new() { Text = $";{line}", Type = BlockType.Comment,
                        LineNumber = ln,
                        ColumnNumber = col,
                    });
                    line = null;
                }
                else if (parts.Length > 0 && (DIRECTIVES.Contains(parts[0].ToUpperInvariant()) || line.StartsWith('$')))
                {
                    blocks?.Add(new() { Text = $";{line}", Type = BlockType.Comment,
                        LineNumber = ln,
                        ColumnNumber = col,
                    });
                    line = null;
                }
                else if (line.IndexOf(';') is int p && p >= 0)
                {
                    line_comment = line[p..];
                    line = line[..p];
                }
                if (line != null)
                {
                    //should be not comment
                    if (!line.Trim().StartsWith(';'))
                    {
                        blocks?.AddRange(ParseLine(line, [], ln, col) ?? []);
                    }
                    if (line_comment != null)
                    {
                        blocks?.Add(new()
                        {
                            Text = line_comment,
                            Type = BlockType.Comment,
                            LineNumber = ln,
                            ColumnNumber = col,
                        });
                    }
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

    static List<Block>? PreprocessBlocks(List<Block>? blocks, int depth)
    {
        var final_blocks = new List<Block>();
        Block? previous = null;
        Block? header = null;
        foreach (var block in blocks ?? [])
        {
            if (block == null) continue;
            if (header != null)
            {
                block.Header = header;
                header.Parts?.Add(block);
            }
            if (block.Type == BlockType.Container)
            {
                header = null;
            }
            if (block.Type == BlockType.Identifier
                && MACROS.Contains(block.Text?.ToUpperInvariant() ?? ""))
            {
                header = block;
                block.Parts = [];
            }
            //if (previous != null && block.Type != BlockType.Comment
            //    && block.Type != BlockType.WhiteSpace
            //    && previous.Type != BlockType.WhiteSpace)
            //{
            //    final_blocks.Add(new() { Text = " ", Type = BlockType.WhiteSpace });
            //}
            final_blocks.Add(block);
            previous = block;
        }
        for (int i = 0; i < final_blocks.Count; i++)
        {
            var block = final_blocks[i];
            if (block.Children != null)
            {
                block.Children = PreprocessBlocks(block.Children, depth + 1);
            }
        }
        return final_blocks;
    }

    static List<Block>? ProcessBlocks(List<Block>? blocks)
    {
        if (blocks == null) return blocks;
        var final_blocks = new List<Block>();
        var local_blocks = new List<Block>();
        var enumerator = blocks.GetEnumerator();
        var ln = 1;
        List<Block> line = [];
        while (enumerator.MoveNext())
        {
            var block = enumerator.Current;
            if (block == null) continue;
            line.Add(block);

            if (block.Type == BlockType.Operator && block.Text == "^")
            {
                block.Text = "";
                block.Type = BlockType.WhiteSpace;
                Block? next = null;
                while (enumerator.MoveNext())
                {
                    next = enumerator.Current;
                    if (next == null) continue;
                    final_blocks.Add(next);

                    if (next.Type == BlockType.Identifier &&
                        next.Text.StartsWith("O", StringComparison.InvariantCultureIgnoreCase)
                        || next.Type == BlockType.Number)
                    {
                        next.Text = ConvertNumberText("^" + next.Text, radix);
                        next.Type = BlockType.Number;
                        break;
                    }
                }
            }
            else if (block.Type == BlockType.Number && block.Text != null)
            {
                block.Text = ConvertNumberText(block.Text, radix);
                int p = line.IndexOf(block);
                if (p == 0)
                {

                }
                int q = p;
                for (; q >= 0; --q)
                {
                    var pb = line[q];
                    if (pb.Type == BlockType.WhiteSpace)
                    {
                        continue;
                    }
                    else if (pb != block && pb.Type == BlockType.Identifier)
                    {
                        break;
                    }
                    else if (pb.Type == BlockType.Operator && pb.Text == ":" || q == 0)
                    {
                        final_blocks.Add(new() { Text = ".BYTE", Type = BlockType.Identifier });
                        final_blocks.Add(new() { Text = " ", Type = BlockType.WhiteSpace });
                        break;
                    }
                    else
                    {
                        break;
                    }
                }

            }
            else if (block.Type == BlockType.Operator)
            {
                switch (block?.Text?.ToUpperInvariant())
                {
                    case "=":
                    case "==":
                        block.Text = " .SET ";
                        break;
                    case "!=":
                        block.Text = "<>";
                        break;
                }
            }
            else if (block.Type == BlockType.Identifier)
            {
                var v = block?.Text?.ToUpperInvariant() switch
                {
                    "IFE" => 1,
                    "IFN" => 0,
                    "DEFINE" => 2,
                    "IF1" => 3,
                    "PRINTX" => 4,
                    _ => -1,
                };
                if (v >= 0)
                {
                    switch (v)
                    {
                        case 4:
                            block.Text = ".out";
                            if (enumerator.MoveNext())
                            {
                                var next = enumerator.Current;
                                local_blocks.Add(next);
                            }
                            local_blocks.Add(new() { Text = "\"", Type = BlockType.Operator });
                            while (enumerator.MoveNext())
                            {
                                var next = enumerator.Current;
                                local_blocks.Add(next);
                            }
                            local_blocks.Add(new() { Text = "\"", Type = BlockType.Operator });
                            break;
                        case 3:
                            block.Text = ".IF";
                            block.Parts[0].Text = " 1 ";
                            block.Parts[0].Type = BlockType.WhiteSpace;
                            break;
                        case 2:
                            {
                                var comma = block.Parts.Find(pt => pt.Text == ",");
                                if (comma != null)
                                {
                                    comma.Text = "";
                                    comma.Type = BlockType.WhiteSpace;
                                }
                                var left = block.Parts.Find(pt => pt.Text == "(");
                                if (left != null)
                                {
                                    left.Text = " ";
                                    left.Type = BlockType.WhiteSpace;
                                }

                                var right = block.Parts.FindLast(pt => pt.Text == ")");
                                if (right != null)
                                {
                                    right.Text = " ";
                                    right.Type = BlockType.WhiteSpace;
                                }

                                break;
                            }

                        default:
                            {
                                block.Text = ".IF";

                                Block? next = null;
                                while (enumerator.MoveNext())
                                {
                                    next = enumerator.Current;
                                    if (next.Text == null || next.Text == Environment.NewLine)
                                        break;
                                    if (next.Type == BlockType.Operator
                                        && next.Text == ",")
                                    {
                                        local_blocks.Add(new() { Text = v == 1 ? " = " : " <> ", Type = BlockType.Operator });
                                        local_blocks.Add(new() { Text = "0", Type = BlockType.Identifier });
                                        break;
                                    }
                                    else
                                    {
                                        local_blocks.Add(next);
                                    }
                                }

                                break;
                            }
                    }

                }
                if (block?.Text?.ToUpperInvariant() == "DEFINE")
                {
                    block.Text = ".MACRO";
                }
            }
            if (block?.Children != null)
            {
                if (block.Header != null)
                {
                    block.Pre = Environment.NewLine;
                    switch (block.Header.Text.ToUpperInvariant())
                    {
                        case ".IF":
                            block.Post = Environment.NewLine + ".ENDIF";
                            break;
                        case ".MACRO":
                            block.Post = Environment.NewLine + ".ENDMACRO";
                            break;
                    }
                }
                block.Children = ProcessBlocks(block.Children);
            }
            final_blocks.Add(block);
            if (local_blocks.Count > 0)
            {
                final_blocks.AddRange(local_blocks);
                local_blocks.Clear();
            }
            if (block.Text != null && block.Text.EndsWith(Environment.NewLine))
            {
                line.Clear();
                ln++;
            }

        }
        return final_blocks;
    }
    static int ConvertFile(string input, string output, params string[] includes)
    {
        int ln = 1, col = 1;
        Stack<(int, int)> stack = new();
        using var reader = new StreamReader(input);
        using var writer = new StreamWriter(output);

        var blocks
            = ProcessBlocks(
                PreprocessBlocks(
                    Blockize(reader, ref ln, ref col, stack, []), 0));

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
