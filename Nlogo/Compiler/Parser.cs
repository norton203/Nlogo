using Nlogo.Compiler;

namespace Nlogo.Compiler;

/// <summary>
/// Recursive-descent parser.
/// Consumes the token list produced by the Lexer and builds a ProgramNode AST.
/// </summary>
public sealed class Parser
{
    // ── Input ──────────────────────────────────────────────────────
    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;

    // ── Output ────────────────────────────────────────────────────
    private readonly List<string> _errors = new();
    public IReadOnlyList<string> Errors => _errors;
    public bool HasErrors => _errors.Count > 0;

    public Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    // ═════════════════════════════════════════════════════════════
    //  Entry point
    // ═════════════════════════════════════════════════════════════
    public ProgramNode Parse()
    {
        var statements = new List<Node>();
        int line = Current.Line, col = Current.Col;

        SkipNewlines();

        while (!IsAtEnd())
        {
            try
            {
                var stmt = ParseStatement();
                if (stmt is not null)
                    statements.Add(stmt);
            }
            catch (ParseException ex)
            {
                _errors.Add(ex.Message);
                Synchronise(); // skip to next safe point and keep parsing
            }

            SkipNewlines();
        }

        return new ProgramNode(statements, line, col);
    }

    // ═════════════════════════════════════════════════════════════
    //  Statement dispatch
    // ═════════════════════════════════════════════════════════════
    private Node? ParseStatement() => Current.Type switch
    {
        TokenType.Forward => ParseForward(),
        TokenType.Backward => ParseBackward(),
        TokenType.Right => ParseRight(),
        TokenType.Left => ParseLeft(),
        TokenType.Home => ParseHome(),
        TokenType.SetPos => ParseSetPos(),

        TokenType.PenUp => ParsePenUp(),
        TokenType.PenDown => ParsePenDown(),
        TokenType.SetColor => ParseSetColor(),
        TokenType.SetWidth => ParseSetWidth(),

        TokenType.ClearScreen => ParseClearScreen(),
        TokenType.ShowTurtle => ParseShowTurtle(),
        TokenType.HideTurtle => ParseHideTurtle(),

        TokenType.Repeat => ParseRepeat(),
        TokenType.Forever => ParseForever(),
        TokenType.If => ParseIf(),
        TokenType.IfElse => ParseIfElse(),
        TokenType.While => ParseWhile(),
        TokenType.Stop => ParseStop(),
        TokenType.Output => ParseOutput(),

        TokenType.To => ParseProcDef(),
        TokenType.Make => ParseMake(),
        TokenType.Local => ParseLocal(),

        TokenType.Identifier => ParseProcCall(),

        TokenType.Newline => null, // consumed by SkipNewlines but guard here
        TokenType.EOF => null,

        _ => throw Error($"Unexpected token '{Current.Lexeme}'")
    };

    // ═════════════════════════════════════════════════════════════
    //  Movement
    // ═════════════════════════════════════════════════════════════
    private ForwardNode ParseForward() { var t = Consume(); return new(ParseExpr(), t.Line, t.Col); }
    private BackwardNode ParseBackward() { var t = Consume(); return new(ParseExpr(), t.Line, t.Col); }
    private RightNode ParseRight() { var t = Consume(); return new(ParseExpr(), t.Line, t.Col); }
    private LeftNode ParseLeft() { var t = Consume(); return new(ParseExpr(), t.Line, t.Col); }

    private HomeNode ParseHome()
    {
        var t = Consume();
        return new(t.Line, t.Col);
    }

    private SetPosNode ParseSetPos()
    {
        var t = Consume();
        var x = ParseExpr();
        var y = ParseExpr();
        return new(x, y, t.Line, t.Col);
    }

    // ═════════════════════════════════════════════════════════════
    //  Pen
    // ═════════════════════════════════════════════════════════════
    private PenUpNode ParsePenUp() { var t = Consume(); return new(t.Line, t.Col); }
    private PenDownNode ParsePenDown() { var t = Consume(); return new(t.Line, t.Col); }
    private SetColorNode ParseSetColor() { var t = Consume(); return new(ParseExpr(), t.Line, t.Col); }
    private SetWidthNode ParseSetWidth() { var t = Consume(); return new(ParseExpr(), t.Line, t.Col); }

    // ═════════════════════════════════════════════════════════════
    //  Screen
    // ═════════════════════════════════════════════════════════════
    private ClearScreenNode ParseClearScreen() { var t = Consume(); return new(t.Line, t.Col); }
    private ShowTurtleNode ParseShowTurtle() { var t = Consume(); return new(t.Line, t.Col); }
    private HideTurtleNode ParseHideTurtle() { var t = Consume(); return new(t.Line, t.Col); }

    // ═════════════════════════════════════════════════════════════
    //  Control flow
    // ═════════════════════════════════════════════════════════════
    private RepeatNode ParseRepeat()
    {
        var t = Consume(); // REPEAT
        var count = ParseExpr();
        var body = ParseBlock();
        return new(count, body, t.Line, t.Col);
    }

    private ForeverNode ParseForever()
    {
        var t = Consume(); // FOREVER
        var body = ParseBlock();
        return new(body, t.Line, t.Col);
    }

    private IfNode ParseIf()
    {
        var t = Consume(); // IF
        var condition = ParseExpr();
        var then = ParseBlock();
        return new(condition, then, t.Line, t.Col);
    }

    private IfElseNode ParseIfElse()
    {
        var t = Consume(); // IFELSE
        var condition = ParseExpr();
        var then = ParseBlock();
        var els = ParseBlock();
        return new(condition, then, els, t.Line, t.Col);
    }

    private WhileNode ParseWhile()
    {
        var t = Consume(); // WHILE
        var condition = ParseExpr();
        var body = ParseBlock();
        return new(condition, body, t.Line, t.Col);
    }

    private StopNode ParseStop() { var t = Consume(); return new(t.Line, t.Col); }
    private OutputNode ParseOutput() { var t = Consume(); return new(ParseExpr(), t.Line, t.Col); }

    // ═════════════════════════════════════════════════════════════
    //  Procedure definition  TO name :p1 :p2 ... body END
    // ═════════════════════════════════════════════════════════════
    private ProcDefNode ParseProcDef()
    {
        var t = Consume(TokenType.To);
        var name = Expect(TokenType.Identifier, "Expected procedure name after TO").Lexeme;

        var parms = new List<string>();
        while (Check(TokenType.Deref))
            parms.Add((string)Consume().Literal!);

        SkipNewlines();

        var body = new List<Node>();
        while (!Check(TokenType.End) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(TokenType.End)) break;

            var stmt = ParseStatement();
            if (stmt is not null)
                body.Add(stmt);

            SkipNewlines();
        }

        Expect(TokenType.End, "Expected END to close procedure definition");
        return new(name, parms, body, t.Line, t.Col);
    }

    // ═════════════════════════════════════════════════════════════
    //  Variables
    // ═════════════════════════════════════════════════════════════
    private MakeNode ParseMake()
    {
        var t = Consume(TokenType.Make);
        var name = Expect(TokenType.String, "Expected quoted name after MAKE e.g. MAKE \"x 10").Literal as string
                   ?? throw Error("Invalid variable name in MAKE");
        var val = ParseExpr();
        return new(name, val, t.Line, t.Col);
    }

    private LocalNode ParseLocal()
    {
        var t = Consume(TokenType.Local);
        var name = Expect(TokenType.String, "Expected quoted name after LOCAL e.g. LOCAL \"x").Literal as string
                   ?? throw Error("Invalid variable name in LOCAL");
        return new(name, t.Line, t.Col);
    }

    // ═════════════════════════════════════════════════════════════
    //  Procedure call   name arg1 arg2 ...
    // ═════════════════════════════════════════════════════════════
    private ProcCallNode ParseProcCall()
    {
        var t = Consume(); // identifier
        var args = new List<Node>();

        // Consume arguments until end of line, EOF, or a closing bracket
        while (!IsAtEnd()
               && !Check(TokenType.Newline)
               && !Check(TokenType.RBracket)
               && !Check(TokenType.EOF))
        {
            args.Add(ParseExpr());
        }

        return new(t.Lexeme.ToUpperInvariant(), args, t.Line, t.Col);
    }

    // ═════════════════════════════════════════════════════════════
    //  Block  [ statement* ]
    // ═════════════════════════════════════════════════════════════
    private List<Node> ParseBlock()
    {
        Expect(TokenType.LBracket, "Expected '['");
        SkipNewlines();

        var nodes = new List<Node>();
        while (!Check(TokenType.RBracket) && !IsAtEnd())
        {
            SkipNewlines();
            if (Check(TokenType.RBracket)) break;

            var stmt = ParseStatement();
            if (stmt is not null)
                nodes.Add(stmt);

            SkipNewlines();
        }

        Expect(TokenType.RBracket, "Expected ']'");
        return nodes;
    }

    // ═════════════════════════════════════════════════════════════
    //  Expression parsing  (Pratt / precedence climbing)
    // ═════════════════════════════════════════════════════════════
    private Node ParseExpr() => ParseOr();

    private Node ParseOr()
    {
        var left = ParseAnd();
        while (Check(TokenType.Or))
        {
            var t = Consume();
            left  = new BinaryOpNode("OR", left, ParseAnd(), t.Line, t.Col);
        }
        return left;
    }

    private Node ParseAnd()
    {
        var left = ParseNot();
        while (Check(TokenType.And))
        {
            var t = Consume();
            left  = new BinaryOpNode("AND", left, ParseNot(), t.Line, t.Col);
        }
        return left;
    }

    private Node ParseNot()
    {
        if (Check(TokenType.Not))
        {
            var t = Consume();
            return new UnaryOpNode("NOT", ParseNot(), t.Line, t.Col);
        }
        return ParseComparison();
    }

    private Node ParseComparison()
    {
        var left = ParseAddSub();
        while (Current.Type is TokenType.Equal or TokenType.NotEqual
                             or TokenType.Less  or TokenType.Greater
                             or TokenType.LessEqual or TokenType.GreaterEqual)
        {
            var t = Consume();
            left  = new BinaryOpNode(t.Lexeme, left, ParseAddSub(), t.Line, t.Col);
        }
        return left;
    }

    private Node ParseAddSub()
    {
        var left = ParseMulDiv();
        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var t = Consume();
            left  = new BinaryOpNode(t.Lexeme, left, ParseMulDiv(), t.Line, t.Col);
        }
        return left;
    }

    private Node ParseMulDiv()
    {
        var left = ParsePower();
        while (Current.Type is TokenType.Star or TokenType.Slash or TokenType.Modulo)
        {
            var t = Consume();
            left  = new BinaryOpNode(t.Lexeme, left, ParsePower(), t.Line, t.Col);
        }
        return left;
    }

    private Node ParsePower()
    {
        var left = ParseUnary();
        if (Check(TokenType.Caret))
        {
            var t = Consume();
            // right-associative
            return new BinaryOpNode("^", left, ParsePower(), t.Line, t.Col);
        }
        return left;
    }

    private Node ParseUnary()
    {
        if (Check(TokenType.Minus))
        {
            var t = Consume();
            return new UnaryOpNode("-", ParseUnary(), t.Line, t.Col);
        }
        return ParsePrimary();
    }

    private Node ParsePrimary()
    {
        var t = Current;

        // Parenthesised expression
        if (Check(TokenType.LParen))
        {
            Consume();
            var inner = ParseExpr();
            Expect(TokenType.RParen, "Expected ')'");
            return inner;
        }

        // Literals
        if (Check(TokenType.Number))
        {
            Consume();
            return new NumberNode((double)t.Literal!, t.Line, t.Col);
        }

        if (Check(TokenType.String))
        {
            Consume();
            return new StringNode((string)t.Literal!, t.Line, t.Col);
        }

        if (Check(TokenType.Boolean))
        {
            Consume();
            return new BooleanNode((bool)t.Literal!, t.Line, t.Col);
        }

        // Variable dereference  :varname
        if (Check(TokenType.Deref))
        {
            Consume();
            return new DerefNode((string)t.Literal!, t.Line, t.Col);
        }

        // THING "varname (long form of :varname)
        if (Check(TokenType.Thing))
        {
            Consume();
            var name = Expect(TokenType.String, "Expected quoted name after THING").Literal as string
                       ?? throw Error("Invalid variable name in THING");
            return new DerefNode(name, t.Line, t.Col);
        }

        // User-defined proc call used as expression  e.g.  FORWARD MYPROC
        if (Check(TokenType.Identifier))
        {
            Consume();
            return new ProcCallNode(t.Lexeme.ToUpperInvariant(), new(), t.Line, t.Col);
        }

        throw Error($"Expected a value but found '{t.Lexeme}'");
    }

    // ═════════════════════════════════════════════════════════════
    //  Cursor helpers
    // ═════════════════════════════════════════════════════════════
    private Token Current => _tokens[_pos];
    private bool IsAtEnd() => Current.Type == TokenType.EOF;

    private bool Check(TokenType type) => Current.Type == type;

    private Token Consume()
    {
        var t = Current;
        if (!IsAtEnd()) _pos++;
        return t;
    }

    private Token Consume(TokenType expected)
    {
        if (!Check(expected))
            throw Error($"Expected {expected} but found '{Current.Lexeme}'");
        return Consume();
    }

    private Token Expect(TokenType type, string message)
    {
        if (!Check(type)) throw Error(message);
        return Consume();
    }

    private void SkipNewlines()
    {
        while (Check(TokenType.Newline) && !IsAtEnd())
            _pos++;
    }

    // ── Error recovery — skip forward to next newline or EOF ──────
    private void Synchronise()
    {
        while (!IsAtEnd() && !Check(TokenType.Newline))
            _pos++;
        SkipNewlines();
    }

    private ParseException Error(string message)
        => new($"Line {Current.Line}:{Current.Col} — {message}");

    private sealed class ParseException(string message) : Exception(message);
}