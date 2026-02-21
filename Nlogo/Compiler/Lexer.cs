using System.Text;

namespace Nlogo.Compiler;

/// <summary>
/// Converts a Logo source string into a flat list of Tokens.
/// Single-pass, cursor-based. Stateless between calls — create
/// a new Lexer instance for each Tokenise() call.
/// </summary>
public sealed class Lexer
{
    // ── Input ──────────────────────────────────────────────────────
    private readonly string _src;
    private int _pos;
    private int _line;
    private int _col;

    // ── Output ────────────────────────────────────────────────────
    private readonly List<Token> _tokens = new();
    private readonly List<string> _errors = new();

    public IReadOnlyList<Token> Tokens => _tokens;
    public IReadOnlyList<string> Errors => _errors;
    public bool HasErrors => _errors.Count > 0;

    // ── Constructor ───────────────────────────────────────────────
    public Lexer(string source)
    {
        _src  = source ?? string.Empty;
        _pos  = 0;
        _line = 1;
        _col  = 1;
    }

    // ── Public entry point ────────────────────────────────────────
    public void Tokenise()
    {
        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            char c = Peek();

            if (c == '\n') { ConsumeNewline(); continue; }
            if (c == '\r') { SkipCarriageReturn(); continue; }
            if (char.IsDigit(c)) { ReadNumber(); continue; }
            if (c == '-' && IsNegativeNum()) { ReadNumber(); continue; }
            if (c == '"') { ReadLogoString(); continue; }
            if (c == ':') { ReadDeref(); continue; }
            if (char.IsLetter(c) || c == '_') { ReadWord(); continue; }

            ReadSymbol();
        }

        AddToken(TokenType.EOF, string.Empty, null);
    }

    // ── Whitespace & comments ─────────────────────────────────────
    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            char c = Peek();

            // horizontal whitespace
            if (c == ' ' || c == '\t')
            {
                Advance();
                continue;
            }

            // Logo comment — semicolon to end of line
            if (c == ';')
            {
                while (!IsAtEnd() && Peek() != '\n')
                    Advance();
                continue;
            }

            break;
        }
    }

    private void ConsumeNewline()
    {
        int line = _line;
        int col = _col;
        Advance(); // consume \n

        // Only emit a Newline token if the last token isn't already one
        if (_tokens.Count > 0 && _tokens[^1].Type != TokenType.Newline)
            _tokens.Add(new Token(TokenType.Newline, "\\n", null, line, col));

        _line++;
        _col = 1;
    }

    private void SkipCarriageReturn() => Advance(); // \r\n — \n handled next iteration

    // ── Numbers ───────────────────────────────────────────────────
    private void ReadNumber()
    {
        int start = _pos;
        int col = _col;
        bool isNeg = Peek() == '-';

        if (isNeg) Advance();

        while (!IsAtEnd() && char.IsDigit(Peek()))
            Advance();

        // decimal
        if (!IsAtEnd() && Peek() == '.' && IsDigit(PeekNext()))
        {
            Advance(); // consume '.'
            while (!IsAtEnd() && char.IsDigit(Peek()))
                Advance();
        }

        string lexeme = _src[start.._pos];

        if (double.TryParse(lexeme, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double value))
        {
            AddToken(TokenType.Number, lexeme, value, col);
        }
        else
        {
            AddError($"Invalid number '{lexeme}'", col);
        }
    }

    private bool IsNegativeNum()
    {
        // '-' only starts a number if followed immediately by a digit
        int next = _pos + 1;
        return next < _src.Length && char.IsDigit(_src[next]);
    }

    // ── Logo strings  ("word — no closing quote in Logo) ─────────
    private void ReadLogoString()
    {
        int col = _col;
        Advance(); // skip the opening "

        int start = _pos;
        while (!IsAtEnd() && !char.IsWhiteSpace(Peek())
               && Peek() != '[' && Peek() != ']'
               && Peek() != '(' && Peek() != ')')
        {
            Advance();
        }

        string value = _src[start.._pos];
        AddToken(TokenType.String, $"\"{value}", value, col);
    }

    // ── Deref  :varname ───────────────────────────────────────────
    private void ReadDeref()
    {
        int col = _col;
        Advance(); // skip ':'

        int start = _pos;
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
            Advance();

        string name = _src[start.._pos];
        AddToken(TokenType.Deref, $":{name}", name, col);
    }

    // ── Keywords & identifiers ────────────────────────────────────
    private void ReadWord()
    {
        int start = _pos;
        int col = _col;

        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
            Advance();

        string lexeme = _src[start.._pos];
        string upper = lexeme.ToUpperInvariant();
        TokenType type = upper switch
        {
            // movement
            "FORWARD"      or "FD" => TokenType.Forward,
            "BACKWARD"     or "BK" => TokenType.Backward,
            "RIGHT"        or "RT" => TokenType.Right,
            "LEFT"         or "LT" => TokenType.Left,

            // pen
            "PENUP"        or "PU" => TokenType.PenUp,
            "PENDOWN"      or "PD" => TokenType.PenDown,
            "SETCOLOR"     or
            "SETPENCOLOR"  or "SETPC" => TokenType.SetColor,
            "SETWIDTH"     or
            "SETPENSIZE" => TokenType.SetWidth,
            "SETPOS" => TokenType.SetPos,

            // screen
            "HOME" => TokenType.Home,
            "CLEARSCREEN"  or "CS" => TokenType.ClearScreen,
            "SHOWTURTLE"   or "ST" => TokenType.ShowTurtle,
            "HIDETURTLE"   or "HT" => TokenType.HideTurtle,

            // control flow
            "REPEAT" => TokenType.Repeat,
            "FOREVER" => TokenType.Forever,
            "IF" => TokenType.If,
            "IFELSE" => TokenType.IfElse,
            "WHILE" => TokenType.While,
            "STOP" => TokenType.Stop,
            "OUTPUT"       or "OP" => TokenType.Output,

            // procedures
            "TO" => TokenType.To,
            "END" => TokenType.End,

            // variables
            "MAKE" => TokenType.Make,
            "LOCAL" => TokenType.Local,
            "THING" => TokenType.Thing,

            // logic / boolean
            "TRUE" => TokenType.Boolean,
            "FALSE" => TokenType.Boolean,
            "AND" => TokenType.And,
            "OR" => TokenType.Or,
            "NOT" => TokenType.Not,

            _ => TokenType.Identifier
        };

        object? literal = type == TokenType.Boolean
            ? upper == "TRUE"
            : null;

        AddToken(type, lexeme, literal, col);
    }

    // ── Symbols ───────────────────────────────────────────────────
    private void ReadSymbol()
    {
        int col = _col;
        char c = Advance();

        TokenType type = c switch
        {
            '+' => TokenType.Plus,
            '*' => TokenType.Star,
            '/' => TokenType.Slash,
            '%' => TokenType.Modulo,
            '^' => TokenType.Caret,
            '[' => TokenType.LBracket,
            ']' => TokenType.RBracket,
            '(' => TokenType.LParen,
            ')' => TokenType.RParen,
            '=' => TokenType.Equal,

            '-' => TokenType.Minus,

            '<' => Peek() switch
            {
                '>' => Advance(TokenType.NotEqual),
                '=' => Advance(TokenType.LessEqual),
                _ => TokenType.Less
            },

            '>' => Peek() == '='
                ? Advance(TokenType.GreaterEqual)
                : TokenType.Greater,

            _ => TokenType.Unknown
        };

        if (type == TokenType.Unknown)
            AddError($"Unexpected character '{c}'", col);

        string lexeme = type switch
        {
            TokenType.NotEqual => "<>",
            TokenType.LessEqual => "<=",
            TokenType.GreaterEqual => ">=",
            _ => c.ToString()
        };

        if (type != TokenType.Unknown)
            AddToken(type, lexeme, null, col);
    }

    // ── Cursor helpers ────────────────────────────────────────────
    private char Advance()
    {
        char c = _src[_pos++];
        _col++;
        return c;
    }

    /// Advance and immediately return a fixed token type (for two-char symbols)
    private TokenType Advance(TokenType returnType)
    {
        Advance();
        return returnType;
    }

    private char Peek() => _pos     < _src.Length ? _src[_pos] : '\0';
    private char PeekNext() => _pos + 1 < _src.Length ? _src[_pos + 1] : '\0';
    private bool IsAtEnd() => _pos >= _src.Length;
    private bool IsDigit(char c) => char.IsDigit(c);

    // ── Token/error factories ─────────────────────────────────────
    private void AddToken(TokenType type, string lexeme, object? literal,
                          int col = 0)
        => _tokens.Add(new Token(type, lexeme, literal, _line,
                                 col == 0 ? _col : col));

    private void AddError(string message, int col)
        => _errors.Add($"Line {_line}:{col} — {message}");
}