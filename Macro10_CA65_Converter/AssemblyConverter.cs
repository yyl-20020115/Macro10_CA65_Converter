using System.Text;

namespace Macro10_CA65_Converter;

public class AssemblyConverter
{
    public static readonly HashSet<string> DIRECTIVES =
    [
        "PAGE", "SUBTTL", "TITLE", "SEARCH", "SALL",".XCREF",".CREF","XLIST","LIST"
    ];
    public static readonly HashSet<string> MACROS = [
        "IFE", "IFN", "DEFINE","IF1","IF2"
    ];
    public static readonly HashSet<string> I_INSTRS = [
        "ADCI","ANDI","CMPI","CPXI","CPYI",
        "EORI","LDAI","LDXI","LDYI","ORAI","SBCI"
    ];
    public static string ConvertNumberText(string? text, int radix)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var output_hex = false;
        var done = false;
        uint value = 0;
        switch (text[0])
        {
            case '^':
                switch (char.ToUpper(text[1]))
                {
                    case 'D':
                        done = uint.TryParse(text[2..], out value);
                        break;
                    case 'H':
                        done = uint.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
                        output_hex = true;
                        break;
                    case 'O': //BIG 'o'
                        try
                        {
                            value = System.Convert.ToUInt32(text[2..], 8);
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
                    done = uint.TryParse(text, out value);
                }
                else
                {
                    try
                    {
                        value = System.Convert.ToUInt32(text, radix);
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
        return output_hex ?
            ((value >= 0x100) ? $"${value:X04}" : $"${value:X02}") : value.ToString();
    }
    public const string COMMENT_DIRECTIVE = "COMMENT";
    public const string RADIX_DIRECTIVE = "RADIX";
    public static int Radix = 8;

    public static Block TryCorrectNumber(Block block, Block? previous)
    {
        previous = previous?.SelfAndPreviousNonWhiteSpace;
        if (block != null && block.Text != null)
        {
            if (block.Type == BlockType.Identifier)
            {
                var prefix = false;
                if (previous != null && previous.Text == "^")
                {
                    previous.Text = "";
                    previous.Type = BlockType.WhiteSpace;
                    prefix = true;
                }
                if (char.ToUpper(block.Text[0]) == 'D')
                {
                    if (block.Text[1..].All(p => p >= '0' && p <= '9'))
                    {
                        block.Type = BlockType.Number;
                        block.Text = ConvertNumberText(prefix ? ("^" + block.Text) : block.Text, 10);
                    }
                }
                else if (char.ToUpper(block.Text[0]) == 'O')
                {
                    if (block.Text[1..].All(p => p >= '0' && p <= '7'))
                    {
                        block.Type = BlockType.Number;
                        block.Text = ConvertNumberText(prefix ? ("^" + block.Text) : block.Text, 8);
                    }
                }
                else if (char.ToUpper(block.Text[0]) == 'H')
                {
                    if (block.Text[1..].All(p => p >= '0' && p <= '9' || (p >= 'a' && p <= 'f') || (p >= 'A' && p <= 'F')))
                    {
                        block.Type = BlockType.Number;
                        block.Text = ConvertNumberText(prefix ? ("^" + block.Text) : block.Text, 16);
                    }
                }
            }
            else if (block.Type == BlockType.Number)
            {
                var prefix = false;
                if (previous != null && previous.Text == "^")
                {
                    previous.Text = "";
                    previous.Type = BlockType.WhiteSpace;
                    prefix = true;
                }
                block.Text = ConvertNumberText(prefix ? ("^" + block.Text) : block.Text, Radix);
            }
        }
        return block;
    }
    public static List<Block>? ParseLine(string line, List<Block>? local_list, int ln, int col)
    {
        var local_builder = new StringBuilder();
        var local_reader = new StringReader(line);
        int v;
        char c;
        char last_c = '\0';

        var stack = new Stack<Block>();
        Block? block = null, previous = null;
        while (-1 != (v = local_reader.Peek()))
        {
            var any = false;
            c = (char)v;
            string text;
            int start;
            if (c == ';')
            {
                local_builder.Clear();
                start = col;
                do
                {
                    c = (char)v;
                    local_builder.Append(c);
                    local_reader.Read();
                    col++;
                } while (-1 != (v = local_reader.Peek()));
                text = local_builder.ToString();
                if (previous != null && block != null)
                {
                    previous.Next = block;
                    block.Previous = previous;
                }
                previous = block;

                stack.Push(
                    block = new()
                    {
                        Text = text,
                        Type = BlockType.Comment,
                        LineNumber = ln,
                        ColumnNumber = start,

                    });
                //comment ends everything
                break;
            }
            if (c == '"')
            {
                var quoting_count = 0;
                start = col;
                any = true;
                local_builder.Clear();
                do
                {
                    c = (char)v;
                    local_builder.Append(c);
                    local_reader.Read();
                    if (c == '"' && quoting_count >= 2) break;
                    ++quoting_count;
                    col++;
                } while (-1 != (v = local_reader.Peek()));
                text = local_builder.ToString();
                if (previous != null && block != null)
                {
                    previous.Next = block;
                    block.Previous = previous;
                }
                previous = block;
                stack.Push(
                    block = new()
                    {
                        Text = text,
                        Type = BlockType.String,
                        LineNumber = ln,
                        ColumnNumber = start,
                    });
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                start = col;
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
                text = local_builder.ToString();
                if (previous != null && block != null)
                {
                    previous.Next = block;
                    block.Previous = previous;
                }
                previous = block;
                stack.Push(
                    block = new()
                    {
                        Text = text,
                        Type = BlockType.WhiteSpace,
                        LineNumber = ln,
                        ColumnNumber = start,

                    });
            }
            if (v == -1) break;
            if (char.IsDigit(c))
            {
                start = col;
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
                text = local_builder.ToString();
                if (previous != null && block != null)
                {
                    previous.Next = block;
                    block.Previous = previous;
                }
                previous = block;
                stack.Push(
                    TryCorrectNumber(block = new()
                    {
                        Text = text,
                        Type = BlockType.Number,
                        LineNumber = ln,
                        ColumnNumber = start,
                    }, previous));
            }
            if (char.IsLetter(c))
            {
                any = true;
                local_builder.Clear();
                start = col;
                do
                {
                    c = (char)v;
                    if (!char.IsLetterOrDigit(c)) break;
                    local_builder.Append(c);
                    local_reader.Read();
                    col++;
                } while (-1 != (v = local_reader.Peek()));
                text = local_builder.ToString();
                if (previous != null && block != null)
                {
                    previous.Next = block;
                    block.Previous = previous;
                }
                previous = block;
                stack.Push(
                    TryCorrectNumber(block = new()
                    {
                        Text = text,
                        Type = BlockType.Identifier,
                        LineNumber = ln,
                        ColumnNumber = start,
                    }, previous));
            }
            if (v == -1) break;
            if (char.IsPunctuation(c) || char.IsSymbol(c))
            {
                any = true;
                local_builder.Clear();
                start = col;
                do
                {
                    c = (char)v;
                    if (c == '"' || !char.IsPunctuation(c) && !char.IsSymbol(c)) break;
                    local_builder.Append(c);
                    local_reader.Read();
                    last_c = c;
                    col++;
                } while (-1 != (v = local_reader.Peek()));
                if (previous != null && block != null)
                {
                    previous.Next = block;
                    block.Previous = previous;
                }
                previous = block;
                text = local_builder.ToString();
                Block? last = null;
                col -= text.Length;
                foreach (var ct in text)
                {
                    if (last != null && last.Text == "=" && ct == '=')
                    {
                        last.Text = "==";
                        continue;
                    }
                    else
                    {
                        stack.Push(
                            block = last = new()
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

        local_list?.AddRange(stack.Reverse());
        return local_list;
    }
    public static List<Block>? Blockize(TextReader reader, ref int ln, ref int col, Stack<(int, int)> stack, List<Block>? blocks)
    {
        var builder = new StringBuilder();
        uint line_end_count = 0;
        char tail0 = '\0';
        char tail1 = '\0';
        int v;
        int quoting = 0;
        int angles = 0;
        string? text;
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
                        text = builder.ToString();
                        blocks?.AddRange(ParseLine(text, [], ln, col - text.Length) ?? []);
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
                        text = builder.ToString();

                        if (text.Trim().StartsWith(';'))
                        {
                            do
                            {
                                c = (char)v;
                                builder.Append(c);
                                if (builder.Length >= 2 && builder.ToString().EndsWith(Environment.NewLine))
                                {
                                    line_end_count += 2;
                                    tail1 = '\r';
                                    tail0 = '\n';
                                    builder.Remove(builder.Length - 2, 2);
                                    break;
                                }
                                reader.Read();
                                col++;
                            } while (-1 != (v = reader.Peek()));
                            break;
                        }
                        blocks?.AddRange(ParseLine(text, [], ln, col - text.Length) ?? []);
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
                    col = 1;

                    foreach (var vl in val.Split(Environment.NewLine))
                    {
                        blocks?.Add(new()
                        {
                            Text = $";{vl}",
                            Type = BlockType.Comment,
                            LineNumber = ln,
                            ColumnNumber = col,
                        });
                        blocks?.Add(new()
                        {
                            Text = Environment.NewLine,
                            Type = BlockType.Comment,
                            LineNumber = ln,
                            ColumnNumber = col + vl.Length + 1
                        });
                        ln++;
                    }

                    continue;
                }
                else if (parts.Length > 1 && parts[0].Equals(RADIX_DIRECTIVE, StringComparison.OrdinalIgnoreCase))
                {
                    Radix = int.TryParse(parts[1], out Radix) ? Radix : 10;
                    blocks?.Add(new()
                    {
                        Text = $";{line}",
                        Type = BlockType.Comment,
                        LineNumber = ln,
                        ColumnNumber = col,
                    });
                    line = null;
                }
                else if (parts.Length > 0 && (DIRECTIVES.Contains(parts[0].ToUpperInvariant()) || line.StartsWith('$')))
                {
                    blocks?.Add(new()
                    {
                        Text = $";{line}",
                        Type = BlockType.Comment,
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
                        blocks?.AddRange(ParseLine(line, [], ln, 1) ?? []);
                    }
                    else
                    {
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
                col = 1;
                ln++;

            }
            reader.Read();
        }
        if (builder.Length > 0)
        {
            blocks?.Add(new() { Text = builder.ToString() });
        }
        return blocks;
    }
    public static List<Block>? PreprocessBlocks(List<Block>? blocks, int depth)
    {
        var final_blocks = new List<Block>();
        Block? previous = null;
        Block? header = null;
        var post_comma = false;
        foreach (var block in blocks ?? [])
        {
            if (block == null) continue;
            if (header != null)
            {
                block.Header = header;
                header.Parts?.Add(block);
            }
            if (post_comma && block.Type == BlockType.Container)
            {
                header = null;
                post_comma = false;
            }
            if (!post_comma && block.Type == BlockType.Operator && block.Text == ",")
            {
                post_comma = true;
            }
            if (block.Type == BlockType.Identifier)
            {
                if (MACROS.Contains(block.Text?.ToUpperInvariant() ?? ""))
                {
                    header = block;
                    block.Parts = [];
                    post_comma = false;
                }
            }
            else if (block.Type == BlockType.Operator)
            {
                switch (block.Text)
                {
                    case "!":
                        block.Text = "|";
                        break;
                }
            }

            if (block.Children != null)
            {
                if (block.Pre != null)
                {
                    block.Pre = "(";
                }
                block.Children = PreprocessBlocks(block.Children, depth + 1);
                if (block.Post != null)
                {
                    block.Post = ")";
                }
                foreach (var child in block.Children ?? [])
                {
                    child.Parent = block;
                }
            }

            final_blocks.Add(block);
            if (previous != null)
                previous.Next = block;
            block.Previous = previous;
            previous = block;
        }

        return final_blocks;
    }
    public static bool TryBytePrefix(Block? block, List<Block> final_blocks)
    {
        if (block == null) return false;
        var start = block.LineStart;
        var first = start?.NextNonWhiteSpace;
        if (start == block ||
            start != null &&
            start.Type == BlockType.WhiteSpace
            && first == block ||
            block.PreviousNonWhiteSpace is Block p
            && p != null
            && p.Type == BlockType.Operator
            && p.Text == ":")
        {

            if (start != null && start.Parent != null && start.Parent.Pre == "(")
            {
                return false;
            }
            final_blocks.Add(new() { Text = ".BYTE", Type = BlockType.Identifier });
            final_blocks.Add(new() { Text = " ", Type = BlockType.WhiteSpace });
            return true;
        }

        return false;
    }
    public static List<Block>? ProcessBlocks(List<Block>? blocks, ref int ln)
    {
        if (blocks == null) return blocks;
        var final_blocks = new List<Block>();
        var local_blocks = new List<Block>();
        var enumerator = blocks.GetEnumerator();
        List<Block> line = [];
        while (enumerator.MoveNext())
        {
            var block = enumerator.Current;
            if (block == null) continue;
            line.Add(block);
            if (block.Type == BlockType.String)
            {
                if (block.Text != null && block.Text.Length == 3)
                {
                    block.Text = $"\'{block.Text[1..^1]}\'";
                }
                TryBytePrefix(block, final_blocks);
            }
            else if (block.Type == BlockType.Number)
            {
                TryBytePrefix(block, final_blocks);
            }
            else if (block.Type == BlockType.Operator)
            {
                switch (block.Text?.ToUpperInvariant())
                {
                    case "=":
                    case "==":
                        block.Text = " .SET ";
                        break;
                    case "!=":
                        block.Text = "<>";
                        break;
                }
                if (block.Text == "." && block.NextNonWhiteSpace is Block nwsp &&
                    nwsp != null && nwsp.Type == BlockType.Operator && nwsp.Text == "+")
                {
                    block.Text = "*";
                }
                if (block.Text == "%" && block.NextNonWhiteSpace is Block nwid &&
                    nwid != null)
                {
                    block.Text = "";
                    block.Type = BlockType.WhiteSpace;
                }

                //remove trailing comma
                if (block.Text == "," && block.NextNonWhiteSpace is Block nt
                    && nt != null
                    && nt.Text != null
                    && (nt.Text.StartsWith(';') || nt.Text == (Environment.NewLine)))
                {
                    block.Text = "";
                    block.Type = BlockType.WhiteSpace;
                }
            }
            else if (block.Type == BlockType.Identifier)
            {
                //fix .BYTE
                if (false && block.FindForwardingByLineNumber(":", BlockType.Operator) is Block fwd && fwd != null)
                {
                    if (fwd == block.PreviousNonWhiteSpace)
                    {
                        if (block.FindFollowingByLineNumber(Environment.NewLine, BlockType.WhiteSpace)
                            is Block ends && ends == block.NextNonWhiteSpace
                            || block.FindFollowingByLineNumber(BlockType.Comment) is Block cmt &&
                            cmt == block.NextNonWhiteSpace
                            )
                        {
                            final_blocks.Add(new() { Text = ".BYTE", Type = BlockType.Identifier });
                            final_blocks.Add(new() { Text = " ", Type = BlockType.WhiteSpace });
                            final_blocks.Add(block);
                            continue;
                        }
                    }
                }
                if (block.FindFollowingByLineNumber(",", BlockType.Operator) is Block op
                    && op != null && (op.Next == null || op.Next != null && op.Next.Text == Environment.NewLine))
                {
                    op.Text = "";
                    op.Type = BlockType.WhiteSpace;
                    final_blocks.Add(block);
                    continue;
                }
                if (block?.Text?.ToUpper() == "REPEAT"
                    && block.FindFollowingByHeader(",", BlockType.Operator) is Block comma2
                    && comma2 != null
                    && comma2.NextNonWhiteSpace is Block rep && rep != null &&
                    rep.Pre == "(" && rep.Post == ")")
                {
                    rep.Pre = "{";
                    rep.Post = "}";
                    final_blocks.Add(block);
                    continue;
                }
                if (block?.Text?.ToUpper() == "XWD")
                {
                    block.Text = ".BYTE";
                    final_blocks.Add(block);
                    continue;
                }
                if (I_INSTRS.Contains(block.Text?.ToUpperInvariant() ?? ""))
                {
                    block.Text = block?.Text?[..^1] ?? "";
                    final_blocks.Add(block);
                    final_blocks.Add(new() { Text = " ", Type = BlockType.WhiteSpace });
                    final_blocks.Add(new() { Text = "#", Type = BlockType.Operator });
                    var count = 0;
                    while (enumerator.MoveNext())
                    {
                        var next = enumerator.Current;
                        if (next == null) continue;
                        final_blocks.Add(next);
                        if (next.Type == BlockType.String)
                        {
                            next.Text = $"'{next.Text[1..^1]}'";
                        }
                        if (next.Next != null && next.Next.LineNumber > next.LineNumber) break;
                    }
                    continue;
                }
                var v = block?.Text?.ToUpperInvariant() switch
                {
                    "IFE" => 1,
                    "IFN" => 0,
                    "DEFINE" => 2,
                    "IF1" => 3,
                    "IF2" => 4,
                    "PRINTX" => 5,
                    _ => -1,
                };
                if (v >= 0)
                {
                    switch (v)
                    {
                        case 5:
                            block.Text = ".OUT";
                            if (enumerator.MoveNext())
                            {
                                var next = enumerator.Current;
                                local_blocks.Add(next);
                            }
                            var builder = new StringBuilder();
                            builder.Append('"');
                            while (enumerator.MoveNext())
                            {
                                var next = enumerator.Current;
                                builder.Append(next.Text);
                            }
                            builder.Append('"');
                            local_blocks.Add(new()
                            {
                                Text = builder.ToString(),
                                Type = BlockType.String
                            });
                            break;
                        case 3:
                        case 4:
                            block.Text = ".IF";
                            block.Parts[0].Text = $" {v - 2} ";
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
                                    if (next.Text == Environment.NewLine)
                                    {
                                        break;
                                    }
                                    if (next.Type == BlockType.Operator
                                        && next.Text == ",")
                                    {
                                        //local_blocks.Add(next);
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
                    block.Pre = "";
                    block.Post = "";
                    var pre_child = new Block { Text = Environment.NewLine, Type = BlockType.WhiteSpace };
                    var post_pre_child = new Block { Text = Environment.NewLine, Type = BlockType.WhiteSpace };
                    Block? post_child = null;
                    ++ln;
                    switch (block.Header.Text.ToUpperInvariant())
                    {
                        case ".IF":
                            post_child = new Block { Text = ".ENDIF", Type = BlockType.Identifier };
                            //block.Post = Environment.NewLine + ".ENDIF";
                            ++ln;
                            break;
                        case ".MACRO":
                            post_child = new Block { Text = ".ENDMACRO", Type = BlockType.Identifier };
                            //block.Post = Environment.NewLine + ".ENDMACRO";
                            ++ln;
                            break;
                    }

                    block.Children.Insert(0, pre_child);

                    block.Children = ProcessBlocks(block.Children, ref ln);

                    block.Children?.Add(post_pre_child);
                    if (post_child != null)
                    {
                        block.Children?.Add(post_child);
                    }
                }
                else
                {
                    block.Children = ProcessBlocks(block.Children, ref ln);
                }
            }
            final_blocks.Add(block);
            if (local_blocks.Count > 0)
            {
                final_blocks.AddRange(local_blocks);
                local_blocks.Clear();
            }
            if (block.Text != null && block.Text.EndsWith(Environment.NewLine))
            {
                ln++;
                line.Clear();
            }
        }
        return final_blocks;
    }
    public static int Convert(string input, string output, params string[] includes)
    {
        int ln = 1, col = 1, lnp = 1;
        Stack<(int, int)> stack = new();
        using var reader = new StreamReader(input);
        using var writer = new StreamWriter(output);

        var blocks
            = ProcessBlocks(
                PreprocessBlocks(
                    Blockize(reader, ref ln, ref col, stack, []), 0), ref lnp);

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
}
