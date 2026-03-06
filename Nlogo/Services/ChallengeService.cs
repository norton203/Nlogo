using System.Text.RegularExpressions;
using Nlogo.Models;

namespace Nlogo.Services;

/// <summary>
/// Owns the challenge catalogue, progress tracking, validation, and persistence.
/// Registered as a singleton so state survives navigation.
/// </summary>
public sealed class ChallengeService
{
    private readonly ProgressPersistenceService _persistence;
    private readonly Dictionary<string, ChallengeProgress> _progress = new();
    private readonly Task _loadTask;
    private bool _loaded;

    public event Action? ProgressChanged;

    public ChallengeService(ProgressPersistenceService persistence)
    {
        _persistence = persistence;
        // Start loading immediately — callers await EnsureLoadedAsync() before reading progress.
        _loadTask = LoadProgressAsync();
    }

    // ── Bootstrap ──────────────────────────────────────────────────────────
    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _loadTask;
        _loaded = true;
    }

    private async Task LoadProgressAsync()
    {
        var saved = await _persistence.LoadAsync();
        foreach (var (k, v) in saved)
            _progress[k] = v;
        ProgressChanged?.Invoke();
    }

    // ── Catalogue ──────────────────────────────────────────────────────────
    public IReadOnlyList<ChallengeCategory> Catalog { get; } = BuildCatalog();

    // ── Aggregates ─────────────────────────────────────────────────────────
    public int TotalStars => _progress.Values.Sum(p => p.Stars);
    public int TotalPossible => Catalog.Sum(c => c.Challenges.Length * 3);
    public int CompletedCount => _progress.Values.Count(p => p.Stars > 0);

    // ── Progress queries ────────────────────────────────────────────────────
    public ChallengeProgress? GetProgress(string id)
        => _progress.TryGetValue(id, out var p) ? p : null;

    /// <summary>
    /// A challenge is unlocked if it is the first in the catalogue, or the
    /// immediately preceding challenge has been completed (≥ 1 star).
    /// </summary>
    public bool IsUnlocked(string id)
    {
        Challenge? prev = null;
        foreach (var cat in Catalog)
            foreach (var ch in cat.Challenges)
            {
                if (ch.Id == id)
                    return prev is null
                        || (_progress.TryGetValue(prev.Id, out var p) && p.Stars > 0);
                prev = ch;
            }
        return false;
    }

    // ── Validation ─────────────────────────────────────────────────────────
    public CheckResult Validate(
        Challenge challenge,
        string code,
        CanvasStats canvas,
        bool parsedOk)
    {
        var results = new bool[challenge.Checks.Length];

        for (int i = 0; i < challenge.Checks.Length; i++)
        {
            var c = challenge.Checks[i];
            results[i] = c.Type switch
            {
                CheckType.RunsWithoutError => parsedOk,
                CheckType.CanvasActivity => canvas.HasDrawing && canvas.PixelCount > 30,
                CheckType.CanvasSquareBounds => canvas.HasDrawing
                                               && canvas.AspectRatio is > 0.55 and < 1.8,
                CheckType.CodePattern => c.Pattern is not null
                                               && Regex.IsMatch(code, c.Pattern,
                                                  RegexOptions.IgnoreCase | RegexOptions.Multiline),
                CheckType.ManualCheck => true,
                _ => false
            };
        }

        int passed = results.Count(r => r);
        int total = results.Length;

        int stars = passed == total ? 3
                  : passed >= total - 1 ? 2
                  : passed >= 1 ? 1 : 0;

        string msg = stars switch
        {
            3 => "Perfect! Every check passed! 🎉",
            2 => "Great work — almost perfect!",
            1 => "Good start — check the hints and try again!",
            _ => "Not quite — read the hints and try again."
        };

        return new(results, stars, msg);
    }

    // ── Save progress (sync to caller; disk write is fire-and-forget) ──────
    public void SaveProgress(string id, int stars, TimeSpan time)
    {
        var existing = GetProgress(id);
        bool better = existing is null
                    || stars > existing.Stars
                    || (stars == existing.Stars && time < existing.BestTime);

        if (!better) return;

        _progress[id] = new(id, stars, time, DateTime.Now);

        // Write to disk in the background — non-blocking, non-fatal on failure.
        _ = _persistence.SaveAsync(_progress.Values);

        ProgressChanged?.Invoke();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Catalogue — 7 categories, 24 challenges total
    // ══════════════════════════════════════════════════════════════════════
    private static List<ChallengeCategory> BuildCatalog() =>
    [
        // ── 1. Basics ────────────────────────────────────────────────────
        new("Basics", "🐢",
        [
            new(
                "basics_move", "First Steps", "👣",
                "Move the turtle **forward** using the `FD` command!\n\n`FD 100` moves the turtle forward 100 steps, leaving a line behind it.",
                ["Try typing: `FD 100` then press Run ▶",
                 "FD is short for FORWARD — both work!",
                 "The number after FD is how many steps to take"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Used FD or FORWARD",
                        @"\b(FD|FORWARD)\s+[\d.]+"),
                ],
                "; Welcome to Logo IDE!\n; Type your code below and press Run ▶\n\n"
            ),
            new(
                "basics_turn", "Turn Around", "↩️",
                "Combine moving and turning to draw an **L-shape**.\n\nUse `RT` to turn right and `FD` to move forward. Try turning **90 degrees**!",
                ["RT 90 turns the turtle right by 90 degrees",
                 "Try: `FD 100` then `RT 90` then `FD 100`",
                 "LT turns left, RT turns right"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used a turn command (RT or LT)",
                        @"\b(RT|LT|RIGHT|LEFT)\s+[\d.]+"),
                    new(CheckType.CodePattern,      "Used FD or FORWARD",
                        @"\b(FD|FORWARD)\s+[\d.]+"),
                    new(CheckType.CanvasActivity,   "Turtle drew something"),
                ],
                "; Move forward, then turn!\n\nFD 100\n"
            ),
            new(
                "basics_square", "Draw a Square", "⬜",
                "Draw a **square** using `FD` and `RT`.\n\nA square has **4 equal sides** and **4 right-angle corners** (90°).",
                ["A square needs 4 sides — use FD 4 times",
                 "Turn 90° between each side with RT 90",
                 "Pattern: `FD 100  RT 90` — repeat 4 times"],
                [
                    new(CheckType.RunsWithoutError,   "Code runs without errors"),
                    new(CheckType.CodePattern,        "Turned 90 degrees",
                        @"\b(RT|LT|RIGHT|LEFT)\s+90\b"),
                    new(CheckType.CanvasSquareBounds, "Drawing has a square shape"),
                    new(CheckType.CanvasActivity,     "Something drawn on canvas"),
                ],
                "; Draw a square — 4 sides, 4 right-angle turns!\n\n"
            ),
            new(
                "basics_repeat", "Repeat Magic", "🔁",
                "Draw a square using **REPEAT** instead of writing the same commands 4 times!\n\n```\nREPEAT 4 [ FD 100  RT 90 ]\n```",
                ["REPEAT 4 [ ... ] runs the code inside exactly 4 times",
                 "Put `FD 100 RT 90` inside the square brackets",
                 "Full solution: `REPEAT 4 [ FD 100  RT 90 ]`"],
                [
                    new(CheckType.RunsWithoutError,   "Code runs without errors"),
                    new(CheckType.CodePattern,        "Used the REPEAT command", @"\bREPEAT\b"),
                    new(CheckType.CanvasSquareBounds, "A square shape was drawn"),
                    new(CheckType.CanvasActivity,     "Something drawn on canvas"),
                ],
                "; Use REPEAT to draw a square!\n\nREPEAT 4 [ FD 100  RT 90 ]\n"
            ),
        ]),

        // ── 2. Shapes ────────────────────────────────────────────────────
        new("Shapes", "🔷",
        [
            new(
                "shapes_triangle", "Triangle", "🔺",
                "Draw an **equilateral triangle**.\n\nA triangle has **3 sides**. Turn **120 degrees** between each side.",
                ["A triangle has 3 sides — use REPEAT 3",
                 "Exterior angle of an equilateral triangle = 120°",
                 "Try: `REPEAT 3 [ FD 100  RT 120 ]`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used REPEAT",        @"\bREPEAT\b"),
                    new(CheckType.CodePattern,      "Turned 120 degrees",
                        @"\b(RT|LT|RIGHT|LEFT)\s+120\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Draw a triangle!\n\n"
            ),
            new(
                "shapes_hexagon", "Hexagon", "⬡",
                "Draw a **hexagon** (6 sides).\n\nFor a regular hexagon, turn **60 degrees** each time.",
                ["A hexagon has 6 sides — use REPEAT 6",
                 "Turn 60° between each side",
                 "Try: `REPEAT 6 [ FD 80  RT 60 ]`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used REPEAT 6",
                        @"\bREPEAT\s+6\b"),
                    new(CheckType.CodePattern,      "Turned 60 degrees",
                        @"\b(RT|LT|RIGHT|LEFT)\s+60\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Draw a hexagon — 6 sides!\n\n"
            ),
            new(
                "shapes_circle", "Circle", "⭕",
                "Draw a **circle** by repeating small forward steps with tiny turns.\n\nUse `REPEAT 36` with **10-degree** turns for a smooth circle!",
                ["360 ÷ 36 = 10, so turn 10° thirty-six times",
                 "Try: `REPEAT 36 [ FD 10  RT 10 ]`",
                 "More repetitions = smoother circle!"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used REPEAT",  @"\bREPEAT\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                    new(CheckType.ManualCheck,      "The shape looks like a circle"),
                ],
                "; Draw a circle!\n; Hint: REPEAT 36 [ FD 10  RT 10 ]\n\n"
            ),
        ]),

        // ── 3. Colors ────────────────────────────────────────────────────
        new("Colors", "🎨",
        [
            new(
                "colors_basic", "Colorful Line", "🌈",
                "Change the pen color using `SETCOLOR`!\n\nTry: `SETCOLOR \"red` — then draw something.",
                ["SETCOLOR \"red sets the pen to red",
                 "Colors: \"red  \"blue  \"green  \"yellow  \"purple",
                 "Draw after setting color: `SETCOLOR \"red  FD 100`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used SETCOLOR command",
                        @"\b(SETCOLOR|SETPENCOLOR|SETPC)\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Change the pen color and draw!\n\n"
            ),
            new(
                "colors_rainbow", "Rainbow Square", "🌈",
                "Draw a square where **each side is a different color**!\n\nChange the color before drawing each side.",
                ["Use SETCOLOR before each FD command",
                 "Try: `SETCOLOR \"red  FD 100  RT 90`",
                 "Then: `SETCOLOR \"blue  FD 100  RT 90` — and so on"],
                [
                    new(CheckType.RunsWithoutError,   "Code runs without errors"),
                    new(CheckType.CodePattern,        "Used SETCOLOR at least twice",
                        @"(?s)(SETCOLOR|SETPC).*?(SETCOLOR|SETPC)"),
                    new(CheckType.CanvasSquareBounds, "A square shape was drawn"),
                    new(CheckType.CanvasActivity,     "Something drawn on canvas"),
                ],
                "; Draw a rainbow square!\n\n"
            ),
        ]),

        // ── 4. Variables ─────────────────────────────────────────────────
        new("Variables", "📦",
        [
            new(
                "vars_intro", "Store a Value", "🏷️",
                "Use `MAKE` to store a number in a **variable**, then use it with `FD`!\n\nVariables let you reuse values without typing them again.\n\n```\nMAKE \"size 100\nFD :size\n```",
                ["MAKE \"size 100 stores 100 in a variable called size",
                 "Use :size (with a colon) to read the value back",
                 "Try: `MAKE \"size 100` then `FD :size`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used MAKE to create a variable",
                        @"\bMAKE\s+""?\w+"),
                    new(CheckType.CodePattern,      "Used the variable with : syntax", @":\w+"),
                    new(CheckType.CanvasActivity,   "Turtle drew something"),
                ],
                "; Store a value and use it!\n\nMAKE \"size 100\nFD :size\n"
            ),
            new(
                "vars_square", "Variable Square", "📐",
                "Draw a square using a **variable for the side length**.\n\nThis way you can easily change the size by updating just one number!\n\n```\nMAKE \"side 80\nREPEAT 4 [ FD :side  RT 90 ]\n```",
                ["First: `MAKE \"side 80` to set the size",
                 "Then: `REPEAT 4 [ FD :side  RT 90 ]`",
                 "Try changing the value of :side and re-running!"],
                [
                    new(CheckType.RunsWithoutError,   "Code runs without errors"),
                    new(CheckType.CodePattern,        "Used MAKE",                @"\bMAKE\b"),
                    new(CheckType.CodePattern,        "Used REPEAT",              @"\bREPEAT\b"),
                    new(CheckType.CodePattern,        "Variable used inside FD",
                        @"\b(FD|FORWARD)\s+:\w+"),
                    new(CheckType.CanvasSquareBounds, "A square shape was drawn"),
                ],
                "; Variable square!\n\nMAKE \"side 80\nREPEAT 4 [ FD :side  RT 90 ]\n"
            ),
            new(
                "vars_two", "Two Variables", "🔢",
                "Use **two variables** to draw a rectangle — one for width, one for height.\n\n```\nMAKE \"w 120\nMAKE \"h 60\nREPEAT 2 [ FD :w  RT 90  FD :h  RT 90 ]\n```",
                ["Create two variables: `MAKE \"w 120` and `MAKE \"h 60`",
                 "A rectangle repeats two different side lengths twice each",
                 "Full: `REPEAT 2 [ FD :w  RT 90  FD :h  RT 90 ]`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Declared at least two variables",
                        @"(?s)\bMAKE\b.*\bMAKE\b"),
                    new(CheckType.CodePattern,      "Variables used in drawing commands",
                        @"\b(FD|FORWARD)\s+:\w+"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Two-variable rectangle!\n\nMAKE \"w 120\nMAKE \"h 60\nREPEAT 2 [ FD :w  RT 90  FD :h  RT 90 ]\n"
            ),
        ]),

        // ── 5. Loops & Conditionals ───────────────────────────────────────
        new("Loops & Conditionals", "🔀",
        [
            new(
                "loops_if", "Make a Decision", "🤔",
                "Use `IF` to **conditionally** run commands.\n\nHere we use a variable and IF to decide how far to move:\n\n```\nMAKE \"big 1\nIF :big = 1 [ FD 150 ] [ FD 50 ]\n```\n\nTry changing `\"big` to `0` and see what happens!",
                ["IF condition [ do this ] [ or this ] — the second branch is optional",
                 "MAKE \"big 1 then IF :big = 1 [ FD 150 ]",
                 "Change the value of :big to 0 to test the else branch"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used IF or IFELSE",         @"\b(IF|IFELSE)\b"),
                    new(CheckType.CodePattern,      "Used MAKE to set a variable", @"\bMAKE\b"),
                    new(CheckType.CanvasActivity,   "Turtle drew something"),
                ],
                "; Make a decision with IF!\n\nMAKE \"big 1\nIF :big = 1 [ FD 150 ] [ FD 50 ]\n"
            ),
            new(
                "loops_while", "Count Down", "⏱️",
                "Use `WHILE` and a variable to draw a **staircase** that shrinks!\n\n```\nMAKE \"n 5\nWHILE [ :n > 0 ] [\n  FD :n * 20\n  RT 90\n  MAKE \"n :n - 1\n]\n```",
                ["WHILE [ condition ] [ body ] — runs while condition is true",
                 "Use MAKE inside the loop to update the variable each time",
                 "`:n - 1` subtracts 1 from n so the loop eventually stops"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used WHILE",  @"\bWHILE\b"),
                    new(CheckType.CodePattern,      "Used MAKE inside loop to update variable",
                        @"(?s)\bWHILE\b.*\bMAKE\b"),
                    new(CheckType.CanvasActivity,   "Turtle drew something"),
                ],
                "; Count-down staircase!\n\nMAKE \"n 5\nWHILE [ :n > 0 ] [\n  FD :n * 20\n  RT 90\n  MAKE \"n :n - 1\n]\n"
            ),
            new(
                "loops_growing", "Growing Lines", "📈",
                "Draw **lines that get longer** each time using a WHILE loop!\n\nStart at length 10 and grow by 10 each step, stopping when length exceeds 100.\n\n```\nMAKE \"len 10\nWHILE [ :len <= 100 ] [\n  FD :len\n  RT 90\n  MAKE \"len :len + 10\n]\n```",
                ["Start with `MAKE \"len 10`",
                 "Each loop: move `FD :len`, turn `RT 90`, then increase: `MAKE \"len :len + 10`",
                 "Stop condition: `:len <= 100`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used WHILE",  @"\bWHILE\b"),
                    new(CheckType.CodePattern,      "Incremented a variable inside the loop",
                        @"(?s)\bWHILE\b.*MAKE\s+""\w+\s+:\w+\s*\+"),
                    new(CheckType.CanvasActivity,   "Turtle drew something"),
                ],
                "; Growing lines!\n\nMAKE \"len 10\nWHILE [ :len <= 100 ] [\n  FD :len\n  RT 90\n  MAKE \"len :len + 10\n]\n"
            ),
        ]),

        // ── 6. Patterns & Art ────────────────────────────────────────────
        new("Patterns & Art", "✨",
        [
            new(
                "art_star", "Five-Point Star", "⭐",
                "Draw a **five-point star** using REPEAT!\n\nThe secret: repeat 5 times, moving forward then turning **144 degrees**.\n\n```\nREPEAT 5 [ FD 120  RT 144 ]\n```",
                ["A five-point star turns 144° at each point",
                 "144 = 360 × 2 ÷ 5 — the turtle goes around twice",
                 "Try: `REPEAT 5 [ FD 120  RT 144 ]`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used REPEAT 5",
                        @"\bREPEAT\s+5\b"),
                    new(CheckType.CodePattern,      "Turned 144 degrees",
                        @"\b(RT|LT|RIGHT|LEFT)\s+144\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Draw a five-point star!\n\nREPEAT 5 [ FD 120  RT 144 ]\n"
            ),
            new(
                "art_flower", "Flower Power", "🌸",
                "Create a **flower** using nested REPEATs — a REPEAT inside a REPEAT!\n\nDraw a petal (a small circle), then rotate and repeat:\n\n```\nREPEAT 12 [\n  REPEAT 36 [ FD 5  RT 10 ]\n  RT 30\n]\n```",
                ["The inner REPEAT 36 draws one petal (a small circle)",
                 "The outer REPEAT 12 rotates 30° between each petal",
                 "12 petals × 30° = 360° — a full flower!"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used nested REPEATs",
                        @"(?s)\bREPEAT\b.*\bREPEAT\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                    new(CheckType.ManualCheck,      "The drawing looks like a flower pattern"),
                ],
                "; Flower with nested REPEATs!\n\nREPEAT 12 [\n  REPEAT 36 [ FD 5  RT 10 ]\n  RT 30\n]\n"
            ),
            new(
                "art_spiral", "Growing Spiral", "🌀",
                "Draw a **spiral** by increasing the forward distance each step!\n\nUse a variable that grows a little each loop iteration:\n\n```\nMAKE \"d 5\nREPEAT 60 [\n  FD :d\n  RT 91\n  MAKE \"d :d + 2\n]\n```\n\nThe **91°** turn (not 90°) stops it closing into a square!",
                ["Start: `MAKE \"d 5`",
                 "Each step: `FD :d`, then `RT 91`, then `MAKE \"d :d + 2`",
                 "Try changing 91 to other angles — what changes?"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used REPEAT with MAKE inside",
                        @"(?s)\bREPEAT\b.*\bMAKE\b"),
                    new(CheckType.CodePattern,      "Variable grows each iteration",
                        @"(?s)MAKE\s+""\w+\s+:\w+\s*\+"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Growing spiral!\n\nMAKE \"d 5\nREPEAT 60 [\n  FD :d\n  RT 91\n  MAKE \"d :d + 2\n]\n"
            ),
            new(
                "art_sunburst", "Sunburst", "🌟",
                "Create a **sunburst** — lines radiating out from the centre!\n\nDraw a line, return to centre, rotate, and repeat:\n\n```\nREPEAT 36 [\n  FD 100\n  BK 100\n  RT 10\n]\n```",
                ["FD 100 draws the ray outward",
                 "BK 100 returns the turtle to the centre",
                 "RT 10 rotates ready for the next ray — 36 × 10° = 360°"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used REPEAT",          @"\bREPEAT\b"),
                    new(CheckType.CodePattern,      "Used BK or BACKWARD",  @"\b(BK|BACKWARD)\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                    new(CheckType.ManualCheck,      "The drawing looks like rays from a centre"),
                ],
                "; Sunburst pattern!\n\nREPEAT 36 [\n  FD 100\n  BK 100\n  RT 10\n]\n"
            ),
        ]),

        // ── 7. Procedures ────────────────────────────────────────────────
        new("Procedures", "⚙️",
        [
            new(
                "procs_basic", "My First Procedure", "🔧",
                "Create your own **reusable procedure** using `TO` and `END`!\n\nDefine it once, then call it by name as many times as you like.",
                ["TO mysquare ... END defines a procedure called mysquare",
                 "Put your drawing code between TO and END",
                 "Then call it by name: `mysquare`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Defined a procedure with TO...END",
                        @"(?s)\bTO\b.+?\bEND\b"),
                    new(CheckType.CanvasActivity,   "Procedure was called and drew something"),
                ],
                "; Define your own procedure, then call it!\n\nTO mysquare\n  REPEAT 4 [ FD 100 RT 90 ]\nEND\n\nmysquare\n"
            ),
            new(
                "procs_params", "One Parameter", "🎛️",
                "Create a procedure that accepts a **parameter** — a value you pass in when calling it.\n\nMake a `square` procedure that takes a `:size` parameter:\n\n```\nTO square :size\n  REPEAT 4 [ FD :size  RT 90 ]\nEND\n\nsquare 100\nsquare 50\n```",
                ["Parameters use a colon: `TO square :size`",
                 "Use :size inside: `REPEAT 4 [ FD :size  RT 90 ]`",
                 "Call with: `square 100` or `square 50`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Procedure has a parameter",
                        @"\bTO\s+\w+\s+:\w+"),
                    new(CheckType.CodePattern,      "Parameter is used inside body", @":\w+"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; One-parameter procedure!\n\nTO square :size\n  REPEAT 4 [ FD :size RT 90 ]\nEND\n\nsquare 100\nsquare 50\n"
            ),
            new(
                "procs_two_params", "Two Parameters", "🎚️",
                "Take it further — write a `rect` procedure with **two parameters**: width and height.\n\n```\nTO rect :w :h\n  REPEAT 2 [ FD :w  RT 90  FD :h  RT 90 ]\nEND\n\nrect 120 60\nrect 80 40\n```",
                ["Two params: `TO rect :w :h`",
                 "A rectangle alternates :w and :h sides: `REPEAT 2 [ FD :w RT 90 FD :h RT 90 ]`",
                 "Call: `rect 120 60` for a 120×60 rectangle"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Procedure has two parameters",
                        @"\bTO\s+\w+\s+:\w+\s+:\w+"),
                    new(CheckType.CodePattern,      "Both parameters used in body",
                        @"(?s):\w+.*:\w+"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Two-parameter procedure!\n\nTO rect :w :h\n  REPEAT 2 [ FD :w  RT 90  FD :h  RT 90 ]\nEND\n\nrect 120 60\nrect 80 40\n"
            ),
            new(
                "procs_nested_calls", "Procedures Calling Procedures", "🏗️",
                "Write a `house` procedure that **calls your other procedures**!\n\nA house = a square body + a triangle roof:\n\n```\nTO square :s\n  REPEAT 4 [ FD :s  RT 90 ]\nEND\n\nTO triangle :s\n  REPEAT 3 [ FD :s  RT 120 ]\nEND\n\nTO house :s\n  square :s\n  FD :s\n  LT 30\n  triangle :s\nEND\n\nhouse 80\n```",
                ["Define `square :s` and `triangle :s` first",
                 "In `TO house :s` call `square :s` to draw the body",
                 "Then use FD and LT to position the turtle, then call `triangle :s` for the roof"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "At least two procedures defined",
                        @"(?s)\bTO\b.+?\bEND\b.+?\bTO\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                    new(CheckType.ManualCheck,      "The drawing looks like a house shape"),
                ],
                "; Procedures calling procedures!\n\nTO square :s\n  REPEAT 4 [ FD :s  RT 90 ]\nEND\n\nTO triangle :s\n  REPEAT 3 [ FD :s  RT 120 ]\nEND\n\nTO house :s\n  square :s\n  FD :s\n  LT 30\n  triangle :s\nEND\n\nhouse 80\n"
            ),
            new(
                "procs_star", "Configurable Star", "🌠",
                "Write a `star` procedure with **two parameters**: size and number of points.\n\nFor a star, the turn angle = `360 / :points * 2`.\n\n```\nTO star :size :points\n  REPEAT :points [ FD :size  RT 144 ]\nEND\n\nstar 80 5\n```",
                ["TO star :size :points — two parameters",
                 "Use `REPEAT :points` inside the body (parameter as loop count!)",
                 "A 5-point star uses 144°, try calling `star 60 5` then `star 40 6`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Procedure has two parameters",
                        @"\bTO\s+\w+\s+:\w+\s+:\w+"),
                    new(CheckType.CodePattern,      "REPEAT uses a parameter as count",
                        @"\bREPEAT\s+:\w+"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Configurable star!\n\nTO star :size :points\n  REPEAT :points [ FD :size  RT 144 ]\nEND\n\nstar 80 5\n"
            ),
        ]),
    ];
}