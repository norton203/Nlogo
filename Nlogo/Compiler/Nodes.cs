using System;
using System.Collections.Generic;
using System.Text;


namespace Nlogo.Compiler;

// ── Base ──────────────────────────────────────────────────────────────
public abstract record Node(int Line, int Col);

// ── Program root ──────────────────────────────────────────────────────
public sealed record ProgramNode(
    List<Node> Statements,
    int Line, int Col
) : Node(Line, Col);

// ── Literals ──────────────────────────────────────────────────────────
public sealed record NumberNode(
    double Value,
    int Line, int Col
) : Node(Line, Col);

public sealed record StringNode(
    string Value,
    int Line, int Col
) : Node(Line, Col);

public sealed record BooleanNode(
    bool Value,
    int Line, int Col
) : Node(Line, Col);

// ── Variable access / assignment ──────────────────────────────────────

/// :varname  or  THING "varname
public sealed record DerefNode(
    string Name,
    int Line, int Col
) : Node(Line, Col);

/// MAKE "varname <expr>
public sealed record MakeNode(
    string Name,
    Node Value,
    int Line, int Col
) : Node(Line, Col);

/// LOCAL "varname
public sealed record LocalNode(
    string Name,
    int Line, int Col
) : Node(Line, Col);

// ── Arithmetic / logic expressions ────────────────────────────────────
public sealed record BinaryOpNode(
    string Op,       // "+", "-", "*", "/", "%", "^", "=", "<>", "<", ">", "<=", ">="
    Node Left,
    Node Right,
    int Line, int Col
) : Node(Line, Col);

public sealed record UnaryOpNode(
    string Op,       // "-", "NOT"
    Node Operand,
    int Line, int Col
) : Node(Line, Col);

// ── Movement commands ─────────────────────────────────────────────────
public sealed record ForwardNode(Node Distance, int Line, int Col) : Node(Line, Col);
public sealed record BackwardNode(Node Distance, int Line, int Col) : Node(Line, Col);
public sealed record RightNode(Node Degrees, int Line, int Col) : Node(Line, Col);
public sealed record LeftNode(Node Degrees, int Line, int Col) : Node(Line, Col);
public sealed record HomeNode(int Line, int Col) : Node(Line, Col);

public sealed record SetPosNode(
    Node X, Node Y,
    int Line, int Col
) : Node(Line, Col);

// ── Pen commands ──────────────────────────────────────────────────────
public sealed record PenUpNode(int Line, int Col) : Node(Line, Col);
public sealed record PenDownNode(int Line, int Col) : Node(Line, Col);
public sealed record SetColorNode(Node Color, int Line, int Col) : Node(Line, Col);
public sealed record SetWidthNode(Node Width, int Line, int Col) : Node(Line, Col);

// ── Screen commands ───────────────────────────────────────────────────
public sealed record ClearScreenNode(int Line, int Col) : Node(Line, Col);
public sealed record ShowTurtleNode(int Line, int Col) : Node(Line, Col);
public sealed record HideTurtleNode(int Line, int Col) : Node(Line, Col);

// ── Control flow ──────────────────────────────────────────────────────

/// REPEAT <count> [ <body> ]
public sealed record RepeatNode(
    Node Count,
    List<Node> Body,
    int Line, int Col
) : Node(Line, Col);

/// FOREVER [ <body> ]
public sealed record ForeverNode(
    List<Node> Body,
    int Line, int Col
) : Node(Line, Col);

/// IF <condition> [ <then> ]
public sealed record IfNode(
    Node Condition,
    List<Node> Then,
    int Line, int Col
) : Node(Line, Col);

/// IFELSE <condition> [ <then> ] [ <else> ]
public sealed record IfElseNode(
    Node Condition,
    List<Node> Then,
    List<Node> Else,
    int Line, int Col
) : Node(Line, Col);

/// WHILE <condition> [ <body> ]
public sealed record WhileNode(
    Node Condition,
    List<Node> Body,
    int Line, int Col
) : Node(Line, Col);

/// STOP  (exit procedure early)
public sealed record StopNode(int Line, int Col) : Node(Line, Col);

/// OUTPUT <expr>  (return a value from a procedure)
public sealed record OutputNode(Node Value, int Line, int Col) : Node(Line, Col);

// ── Procedure definition & call ───────────────────────────────────────

/// TO procname :param1 :param2 ... \n <body> END
public sealed record ProcDefNode(
    string Name,
    List<string> Params,
    List<Node> Body,
    int Line, int Col
) : Node(Line, Col);

/// procname arg1 arg2 ...
public sealed record ProcCallNode(
    string Name,
    List<Node> Args,
    int Line, int Col
) : Node(Line, Col);