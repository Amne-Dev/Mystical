using System.Text;

namespace Mystical.WinUI.Services.Detection.Internal;

internal sealed class VdfObject : Dictionary<string, object>
{
    public VdfObject() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public string GetString(string key)
    {
        return TryGetValue(key, out var value) ? value as string ?? string.Empty : string.Empty;
    }

    public VdfObject? GetObject(string key)
    {
        return TryGetValue(key, out var value) ? value as VdfObject : null;
    }
}

internal static class VdfParser
{
    public static VdfObject ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new VdfObject();
        }

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return Parse(content);
    }

    public static VdfObject Parse(string content)
    {
        var parser = new Parser(content);
        return parser.ParseRoot();
    }

    private sealed class Parser
    {
        private readonly string _content;
        private int _position;

        public Parser(string content)
        {
            _content = content;
        }

        public VdfObject ParseRoot()
        {
            var root = new VdfObject();

            while (TryReadQuotedString(out var key))
            {
                SkipWhitespace();
                if (Peek() == '{')
                {
                    Consume();
                    root[key] = ParseObject();
                }
                else if (TryReadQuotedString(out var value))
                {
                    root[key] = value;
                }
            }

            return root;
        }

        private VdfObject ParseObject()
        {
            var obj = new VdfObject();

            while (true)
            {
                SkipWhitespace();

                if (_position >= _content.Length)
                {
                    break;
                }

                if (Peek() == '}')
                {
                    Consume();
                    break;
                }

                if (!TryReadQuotedString(out var key))
                {
                    Consume();
                    continue;
                }

                SkipWhitespace();

                if (Peek() == '{')
                {
                    Consume();
                    obj[key] = ParseObject();
                }
                else if (TryReadQuotedString(out var value))
                {
                    obj[key] = value;
                }
            }

            return obj;
        }

        private bool TryReadQuotedString(out string result)
        {
            result = string.Empty;
            SkipWhitespace();

            if (Peek() != '"')
            {
                return false;
            }

            Consume();
            var builder = new StringBuilder();

            while (_position < _content.Length)
            {
                var c = Consume();
                if (c == '\\' && _position < _content.Length)
                {
                    builder.Append(Consume());
                    continue;
                }

                if (c == '"')
                {
                    result = builder.ToString();
                    return true;
                }

                builder.Append(c);
            }

            return false;
        }

        private void SkipWhitespace()
        {
            while (_position < _content.Length && char.IsWhiteSpace(_content[_position]))
            {
                _position++;
            }
        }

        private char Peek()
        {
            return _position < _content.Length ? _content[_position] : '\0';
        }

        private char Consume()
        {
            return _position < _content.Length ? _content[_position++] : '\0';
        }
    }
}
