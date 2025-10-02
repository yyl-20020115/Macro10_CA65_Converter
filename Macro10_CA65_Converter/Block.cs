using System.Text;

namespace Macro10_CA65_Converter;

public class Block
{
    public string? Text;
    public string? Pre;
    public Block? Parent;
    public List<Block>? Children;
    public string? Post;
    public BlockType? Type;
    public Block? Header;
    public List<Block>? Parts;
    public int? LineNumber;
    public int? ColumnNumber;
    public Block? Previous;
    public Block? Next;
    public Block? LineStart
    {
        get
        {
            if (this.Previous == null) return this;
            var previous = this;
            while (previous != null)
            {
                if (previous.LineNumber != this.LineNumber)
                    return previous.Next;
                previous = previous.Previous;
            }
            return null;
        }
    }
    public string LineText => this.LineStart is Block start
        && start != null ? start.ToEndOfLineText : "";
    public string ToEndOfLineText
    {
        get
        {
            var builder = new StringBuilder();
            var next = this;
            while (next != null)
            {
                var text = next.ToString();
                var index = text.IndexOf(Environment.NewLine);
                if (index >= 0)
                {
                    text = text[..index];
                }
                builder.Append(text);
                if (index >= 0) break;
                next = next.Next;
            }
            return builder.ToString();
        }
    }
    public Block? SelfAndPreviousNonWhiteSpace
    {
        get
        {
            var previous = this;
            while (previous != null)
            {
                if (previous.Type != BlockType.WhiteSpace)
                    return previous;
                previous = previous.Previous;
            }
            return previous;
        }
    }
    public Block? SelfAndNextNonWhiteSpace
    {
        get
        {
            var next = this;
            while (next != null)
            {
                if (next.Type != BlockType.WhiteSpace)
                    return next;
                next = next.Next;
            }
            return next;
        }
    }

    public Block? PreviousNonWhiteSpace
    {
        get
        {
            var previous = Previous;
            while (previous != null)
            {
                if (previous.Type != BlockType.WhiteSpace)
                    return previous;
                previous = previous.Previous;
            }
            return previous;
        }
    }
    public Block? NextNonWhiteSpace
    {
        get
        {
            var next = Next;
            while (next != null)
            {
                if (next.Type != BlockType.WhiteSpace)
                    return next;
                next = next.Next;
            }
            return next;
        }
    }
    public Block? FollowingNonSpace(int n)
    {
        var nsp = this;
        for (int i = 0; i < n; i++)
        {
            if (nsp == null) return null;
            nsp = nsp.NextNonWhiteSpace;
        }
        return nsp;
    }
    public Block? ForwardingNonSpace(int n)
    {
        var nsp = this;
        for (int i = 0; i < n; i++)
        {
            if (nsp == null) return null;
            nsp = nsp.PreviousNonWhiteSpace;
        }
        return nsp;
    }
    public Block? FindFollowingByHeader(string text, BlockType type)
    {
        var next = this.Next;
        while (next != null && next.Header == this.Header)
        {
            if (next.Type == type && next.Text == text)
                return next;
            next = next.Next;
        }
        return null;
    }

    public Block? FindFollowingByLineNumber(BlockType type)
    {
        var next = this.Next;
        while (next != null && next.LineNumber == this.LineNumber)
        {
            if (next.Type == type)
                return next;
            next = next.Next;
        }
        return null;
    }

    public Block? FindFollowingByLineNumber(string text, BlockType type)
    {
        var next = this.Next;
        while (next != null && next.LineNumber == this.LineNumber)
        {
            if (next.Type == type && next.Text == text)
                return next;
            next = next.Next;
        }
        return null;
    }

    public Block? FindForwardingByLineNumber(BlockType type)
    {
        var previous = this.Previous;
        while (previous != null && previous.LineNumber == this.LineNumber)
        {
            if (previous.Type == type)
                return previous;
            previous = previous.Previous;
        }
        return null;
    }

    public Block? FindForwardingByLineNumber(string text, BlockType type)
    {
        var previous = this.Previous;
        while (previous != null && previous.LineNumber == this.LineNumber)
        {
            if (previous.Type == type && previous.Text == text)
                return previous;
            previous = previous.Previous;
        }
        return null;
    }

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

