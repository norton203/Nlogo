namespace Nlogo.Compiler;

public sealed record Token(
    TokenType Type,
    string Lexeme,   // the raw text as written
    object? Literal,  // parsed value for Number / String / Boolean
    int Line,
    int Col
)
{
    public override string ToString() =>
        $"[{Type,-14} | {Lexeme,-16} | L{Line}:C{Col}]";
}