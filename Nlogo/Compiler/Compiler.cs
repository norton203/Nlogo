using Microsoft.JSInterop;


namespace Nlogo.Compiler;

/// <summary>
/// Walks the AST produced by the Parser and executes each node.
/// Draw commands are forwarded to the JS canvas via IJSObjectReference.
/// </summary>
public sealed class Interpreter
{
    // ── Infrastructure ─────────────────────────────────────────────
    private readonly IJSObjectReference _js;
    private readonly Action<string> _log;
    private readonly Action<string> _logError;
    private readonly CancellationToken _ct;

    // ── Runtime state ──────────────────────────────────────────────
    private readonly LogoEnvironment _global = new();
    private readonly Dictionary<string, ProcDefNode> _procs = new();
    private int _speed = 5;   // 1–10

    // ── Control-flow sentinels (thrown, not returned) ──────────────
    private sealed class StopSignal : Exception { }
    private sealed class OutputSignal : Exception
    {
        public object? Value { get; }
        public OutputSignal(object? value) => Value = value;
    }

    public Interpreter(
        IJSObjectReference js,
        Action<string> log,
        Action<string> logError,
        CancellationToken ct,
        int speed = 5)
    {
        _js       = js;
        _log      = log;
        _logError = logError;
        _ct       = ct;
        _speed    = speed;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Entry point
    // ═══════════════════════════════════════════════════════════════
    public async Task RunAsync(ProgramNode program)
    {
        // First pass — register all procedure definitions
        foreach (var node in program.Statements)
            if (node is ProcDefNode def)
                _procs[def.Name.ToUpperInvariant()] = def;

        // Second pass — execute top-level statements
        foreach (var node in program.Statements)
        {
            _ct.ThrowIfCancellationRequested();

            if (node is ProcDefNode) continue; // already registered

            await ExecuteAsync(node, _global);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Statement executor
    // ═══════════════════════════════════════════════════════════════
    private async Task ExecuteAsync(Node node, LogoEnvironment env)
    {
        _ct.ThrowIfCancellationRequested();

        switch (node)
        {
            // ── Movement ──────────────────────────────────────────
            case ForwardNode f: await DrawAsync("forward", Num(f.Distance, env)); break;
            case BackwardNode b: await DrawAsync("backward", Num(b.Distance, env)); break;
            case RightNode r: await DrawAsync("right", Num(r.Degrees, env)); break;
            case LeftNode l: await DrawAsync("left", Num(l.Degrees, env)); break;
            case HomeNode _: await DrawAsync("home"); break;

            case SetPosNode s:
                await _js.InvokeVoidAsync("goTo",
                    Num(s.X, env), Num(s.Y, env));
                break;

            // ── Pen ───────────────────────────────────────────────
            case PenUpNode _: await DrawAsync("penUp"); break;
            case PenDownNode _: await DrawAsync("penDown"); break;
            case SetColorNode c: await _js.InvokeVoidAsync("setColor", Str(c.Color, env)); break;
            case SetWidthNode w: await _js.InvokeVoidAsync("setWidth", Num(w.Width, env)); break;

            // ── Screen ────────────────────────────────────────────
            case ClearScreenNode _: await _js.InvokeVoidAsync("clearCanvas"); break;
            case ShowTurtleNode _: await _js.InvokeVoidAsync("showTurtle"); break;
            case HideTurtleNode _: await _js.InvokeVoidAsync("hideTurtle"); break;

            // ── Console output ────────────────────────────────────
            case PrintNode p:
                _log(Str(p.Value, env));
                break;

            // ── Canvas text ───────────────────────────────────────
            case LabelNode lb:
                await _js.InvokeVoidAsync("drawLabel", Str(lb.Text, env));
                break;

            // ── Timing ────────────────────────────────────────────
            case WaitNode w:
                int waitMs = (int)(Num(w.Duration, env) * 1000);
                await Task.Delay(Math.Max(0, waitMs), _ct);
                break;

            // ── Variables ─────────────────────────────────────────
            case MakeNode m:
                env.Set(m.Name, Evaluate(m.Value, env));
                break;

            case LocalNode l:
                env.DefineLocal(l.Name);
                break;

            // ── Control flow ──────────────────────────────────────
            case RepeatNode rep:
                int count = (int)Num(rep.Count, env);
                for (int i = 0; i < count; i++)
                {
                    _ct.ThrowIfCancellationRequested();
                    await ExecuteBlockAsync(rep.Body, env);
                }
                break;

            case ForeverNode fv:
                while (true)
                {
                    _ct.ThrowIfCancellationRequested();
                    await ExecuteBlockAsync(fv.Body, env);
                }

            case WhileNode wh:
                while (IsTruthy(Evaluate(wh.Condition, env)))
                {
                    _ct.ThrowIfCancellationRequested();
                    await ExecuteBlockAsync(wh.Body, env);
                }
                break;

            case IfNode ifn:
                if (IsTruthy(Evaluate(ifn.Condition, env)))
                    await ExecuteBlockAsync(ifn.Then, env);
                break;

            case IfElseNode ife:
                if (IsTruthy(Evaluate(ife.Condition, env)))
                    await ExecuteBlockAsync(ife.Then, env);
                else
                    await ExecuteBlockAsync(ife.Else, env);
                break;

            case StopNode _: throw new StopSignal();
            case OutputNode o: throw new OutputSignal(Evaluate(o.Value, env));

            // ── Procedure call ────────────────────────────────────
            case ProcCallNode call:
                await CallProcAsync(call.Name, call.Args, env);
                break;

            default:
                _logError($"Unknown node type: {node.GetType().Name}");
                break;
        }
    }

    private async Task ExecuteBlockAsync(List<Node> body, LogoEnvironment env)
    {
        foreach (var node in body)
        {
            _ct.ThrowIfCancellationRequested();
            await ExecuteAsync(node, env);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Procedure calls
    // ═══════════════════════════════════════════════════════════════
    private async Task<object?> CallProcAsync(
        string name, List<Node> argNodes, LogoEnvironment callerEnv)
    {
        string key = name.ToUpperInvariant();

        if (!_procs.TryGetValue(key, out var def))
            throw new RuntimeException($"Unknown procedure '{name}'");

        if (argNodes.Count != def.Params.Count)
            throw new RuntimeException(
                $"'{name}' expects {def.Params.Count} argument(s), got {argNodes.Count}");

        // Build a new local scope for this call
        var local = new LogoEnvironment(_global);
        for (int i = 0; i < def.Params.Count; i++)
            local.DefineLocal(def.Params[i], Evaluate(argNodes[i], callerEnv));

        try
        {
            await ExecuteBlockAsync(def.Body, local);
            return null;
        }
        catch (StopSignal)
        {
            return null; // STOP just exits the procedure
        }
        catch (OutputSignal os)
        {
            return os.Value; // OUTPUT returns a value
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Expression evaluator
    // ═══════════════════════════════════════════════════════════════
    private object? Evaluate(Node node, LogoEnvironment env) => node switch
    {
        NumberNode n => (object?)n.Value,
        StringNode s => s.Value,
        BooleanNode b => b.Value,

        DerefNode d => env.Get(d.Name),

        UnaryOpNode u => u.Op switch
        {
            "-" => -(double)Evaluate(u.Operand, env)!,
            "NOT" => !IsTruthy(Evaluate(u.Operand, env)),
            _ => throw new RuntimeException($"Unknown unary op '{u.Op}'")
        },

        BinaryOpNode b => EvaluateBinary(b, env),

        // ── Math functions ─────────────────────────────────────────
        MathFuncNode mf => EvaluateMathFunc(mf, env),
        PowerFuncNode pf => Math.Pow(Num(pf.Base, env), Num(pf.Exponent, env)),
        RandomNode rn => (double)Random.Shared.Next(Math.Max(1, (int)Num(rn.Max, env))),

        ProcCallNode call =>
            CallProcAsync(call.Name, call.Args, env).GetAwaiter().GetResult(),

        _ => throw new RuntimeException($"Cannot evaluate node '{node.GetType().Name}'")
    };

    // ═══════════════════════════════════════════════════════════════
    //  Math function evaluator
    // ═══════════════════════════════════════════════════════════════
    private object? EvaluateMathFunc(MathFuncNode mf, LogoEnvironment env)
    {
        double arg = Num(mf.Arg, env);

        // Logo uses degrees for trig; convert to/from radians internally
        const double DegToRad = Math.PI / 180.0;
        const double RadToDeg = 180.0 / Math.PI;

        return mf.FuncName switch
        {
            "SIN" => Math.Sin(arg * DegToRad),
            "COS" => Math.Cos(arg * DegToRad),
            "TAN" => Math.Tan(arg * DegToRad),
            "ARCTAN" => Math.Atan(arg) * RadToDeg,
            "SQRT" => arg < 0
                            ? throw new RuntimeException("SQRT of a negative number")
                            : Math.Sqrt(arg),
            "ABS" => Math.Abs(arg),
            "ROUND" => (double)Math.Round(arg, MidpointRounding.AwayFromZero),
            "INT" => Math.Truncate(arg),
            "LOG" => arg <= 0
                            ? throw new RuntimeException("LOG requires a positive number")
                            : Math.Log(arg),
            "EXP" => Math.Exp(arg),
            _ => throw new RuntimeException($"Unknown math function '{mf.FuncName}'")
        };
    }

    private object? EvaluateBinary(BinaryOpNode b, LogoEnvironment env)
    {
        // Logical operators short-circuit
        if (b.Op == "AND")
            return IsTruthy(Evaluate(b.Left, env)) && IsTruthy(Evaluate(b.Right, env));
        if (b.Op == "OR")
            return IsTruthy(Evaluate(b.Left, env)) || IsTruthy(Evaluate(b.Right, env));

        var left = Evaluate(b.Left, env);
        var right = Evaluate(b.Right, env);

        // String concatenation with +
        if (b.Op == "+" && (left is string || right is string))
            return $"{left}{right}";

        double l = ToDouble(left, b.Op);
        double r = ToDouble(right, b.Op);

        return b.Op switch
        {
            "+" => l + r,
            "-" => l - r,
            "*" => l * r,
            "/" => r == 0 ? throw new RuntimeException("Division by zero") : l / r,
            "%" => l % r,
            "^" => Math.Pow(l, r),
            "=" => l == r,
            "<>" => l != r,
            "<" => l <  r,
            ">" => l >  r,
            "<=" => l <= r,
            ">=" => l >= r,
            _ => throw new RuntimeException($"Unknown operator '{b.Op}'")
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  Canvas helpers
    // ═══════════════════════════════════════════════════════════════

    /// Invoke a JS draw function then pause briefly based on speed setting.
    private async Task DrawAsync(string func, params object?[] args)
    {
        await _js.InvokeVoidAsync(func, args);
        await PaceAsync();
    }

    /// Speed 10 = no delay, Speed 1 = 500ms per step
    private Task PaceAsync()
    {
        int ms = _speed >= 10 ? 0 : (10 - _speed) * 50;
        return ms > 0 ? Task.Delay(ms, _ct) : Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Type helpers
    // ═══════════════════════════════════════════════════════════════
    private double Num(Node node, LogoEnvironment env)
    {
        var val = Evaluate(node, env);
        return ToDouble(val, node.GetType().Name);
    }

    private string Str(Node node, LogoEnvironment env)
        => Evaluate(node, env)?.ToString() ?? string.Empty;

    private static double ToDouble(object? val, string context) => val switch
    {
        double d => d,
        int i => i,
        bool b => b ? 1 : 0,
        string s when double.TryParse(s,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double d) => d,
        _ => throw new RuntimeException($"Expected a number near '{context}', got '{val}'")
    };

    private static bool IsTruthy(object? val) => val switch
    {
        bool b => b,
        double d => d != 0,
        string s => s.Length > 0,
        null => false,
        _ => true
    };
}