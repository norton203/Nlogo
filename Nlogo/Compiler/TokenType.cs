namespace Nlogo.Compiler;

public enum TokenType
{
    // ── Literals ──────────────────────────────
    Number,         // 100, 3.14, -45
    String,         // "hello
    Boolean,        // TRUE, FALSE
    Identifier,     // variable or procedure name

    // ── Built-in Commands ─────────────────────
    Forward,        // FORWARD / FD
    Backward,       // BACKWARD / BK
    Right,          // RIGHT / RT
    Left,           // LEFT / LT
    PenUp,          // PENUP / PU
    PenDown,        // PENDOWN / PD
    SetColor,       // SETCOLOR / SETPC
    SetWidth,       // SETWIDTH
    SetPos,         // SETPOS
    Home,           // HOME
    ClearScreen,    // CLEARSCREEN / CS
    ShowTurtle,     // SHOWTURTLE / ST
    HideTurtle,     // HIDETURTLE / HT

    // ── Console Output ─────────────────────────
    Print,          // PRINT / SHOW / TYPE  — prints a value to the console

    // ── Math Functions (expression) ────────────
    Sin,            // SIN <degrees>
    Cos,            // COS <degrees>
    Tan,            // TAN <degrees>
    ArcTan,         // ARCTAN <value>  → degrees
    Sqrt,           // SQRT <value>
    Abs,            // ABS <value>
    Round,          // ROUND <value>
    IntFunc,        // INT <value>  (truncate toward zero)
    Power,          // POWER <base> <exponent>
    Log,            // LOG <value>  (natural log)
    Exp,            // EXP <value>  (e^value)
    Random,         // RANDOM <max>  → integer 0..(max-1)

    // ── Timing ─────────────────────────────────
    Wait,           // WAIT <seconds>

    // ── Canvas Text ────────────────────────────
    Label,          // LABEL <text>  — draws text at turtle position

    // ── Control Flow ──────────────────────────
    Repeat,         // REPEAT
    Forever,        // FOREVER
    If,             // IF
    IfElse,         // IFELSE
    While,          // WHILE
    Stop,           // STOP  (exit a procedure)
    Output,         // OUTPUT (return a value)

    // ── Procedure Definition ──────────────────
    To,             // TO   (start of proc def)
    End,            // END  (end of proc def)

    // ── Variables ─────────────────────────────
    Make,           // MAKE "varname value
    Local,          // LOCAL "varname
    Thing,          // THING "varname  (:varname shorthand too)
    Deref,          // :varname  (de-reference shorthand)

    // ── Arithmetic Operators ──────────────────
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    Modulo,         // %
    Caret,          // ^ (power)

    // ── Comparison Operators ──────────────────
    Equal,          // =
    NotEqual,       // <>
    Less,           // <
    Greater,        // >
    LessEqual,      // <=
    GreaterEqual,   // >=

    // ── Logic Operators ───────────────────────
    And,            // AND
    Or,             // OR
    Not,            // NOT

    // ── Delimiters ────────────────────────────
    LBracket,       // [
    RBracket,       // ]
    LParen,         // (
    RParen,         // )

    // ── Special ───────────────────────────────
    Newline,        // end of a logical line
    EOF,            // end of input
    Unknown         // anything we don't recognise (surfaces as an error)
}