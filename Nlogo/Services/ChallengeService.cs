using System.Text.RegularExpressions;
using Nlogo.Models;

namespace Nlogo.Services;

/// <summary>
/// Owns the challenge catalogue, progress tracking, and validation.
/// Registered as a singleton so state survives navigation.
/// </summary>
public sealed class ChallengeService
{
    // ── Progress store ─────────────────────────────────────────────────────
    private readonly Dictionary<string, ChallengeProgress> _progress = new();

    public event Action? ProgressChanged;

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
            3 => "Perfect! Every check passed!",
            2 => "Great work — almost perfect!",
            1 => "Good start — check the hints and try again!",
            _ => "Not quite — read the hints and try again."
        };

        return new(results, stars, msg);
    }

    // ── Save progress ──────────────────────────────────────────────────────
    public void SaveProgress(string id, int stars, TimeSpan time)
    {
        var existing = GetProgress(id);
        bool better = existing is null
                    || stars > existing.Stars
                    || (stars == existing.Stars && time < existing.BestTime);
        if (better)
        {
            _progress[id] = new(id, stars, time, DateTime.Now);
            ProgressChanged?.Invoke();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Catalogue
    // ══════════════════════════════════════════════════════════════════════
    private static List<ChallengeCategory> BuildCatalog() =>
    [
        // ── Basics ──────────────────────────────────────────────────────
        new("Basics", "🐢",
        [
            new(
                "basics_move",
                "First Steps", "👣",
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
                "basics_turn",
                "Turn Around", "↩️",
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
                "basics_square",
                "Draw a Square", "⬜",
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
                "basics_repeat",
                "Repeat Magic", "🔁",
                "Draw a square using **REPEAT** instead of writing the same commands 4 times!\n\n```\nREPEAT 4 [ FD 100  RT 90 ]\n```",
                ["REPEAT 4 [ ... ] runs the code inside exactly 4 times",
                 "Put `FD 100 RT 90` inside the square brackets",
                 "Full solution: `REPEAT 4 [ FD 100  RT 90 ]`"],
                [
                    new(CheckType.RunsWithoutError,   "Code runs without errors"),
                    new(CheckType.CodePattern,        "Used the REPEAT command",
                        @"\bREPEAT\b"),
                    new(CheckType.CanvasSquareBounds, "A square shape was drawn"),
                    new(CheckType.CanvasActivity,     "Something drawn on canvas"),
                ],
                "; Use REPEAT to draw a square!\n\nREPEAT 4 [ FD 100  RT 90 ]\n"
            ),
        ]),

        // ── Shapes ──────────────────────────────────────────────────────
        new("Shapes", "🔷",
        [
            new(
                "shapes_triangle",
                "Triangle", "🔺",
                "Draw an **equilateral triangle**.\n\nA triangle has **3 sides**. Turn **120 degrees** between each side.",
                ["A triangle has 3 sides — use REPEAT 3",
                 "Exterior angle of an equilateral triangle = 120°",
                 "Try: `REPEAT 3 [ FD 100  RT 120 ]`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used REPEAT",       @"\bREPEAT\b"),
                    new(CheckType.CodePattern,      "Turned 120 degrees",
                        @"\b(RT|LT|RIGHT|LEFT)\s+120\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Draw a triangle!\n\n"
            ),

            new(
                "shapes_hexagon",
                "Hexagon", "⬡",
                "Draw a **hexagon** (6 sides).\n\nFor a regular hexagon, turn **60 degrees** each time.",
                ["A hexagon has 6 sides — use REPEAT 6",
                 "Turn 60° between each side",
                 "Try: `REPEAT 6 [ FD 80  RT 60 ]`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used REPEAT 6",    @"\bREPEAT\s+6\b"),
                    new(CheckType.CodePattern,      "Turned 60 degrees",
                        @"\b(RT|LT|RIGHT|LEFT)\s+60\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Draw a hexagon — 6 sides!\n\n"
            ),

            new(
                "shapes_circle",
                "Circle", "⭕",
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

        // ── Colors ──────────────────────────────────────────────────────
        new("Colors", "🎨",
        [
            new(
                "colors_basic",
                "Colorful Line", "🌈",
                "Change the pen color using `SETCOLOR`!\n\nTry: `SETCOLOR \"red` — then draw something.",
                ["SETCOLOR \"red sets the pen to red",
                 "Colors: \"red  \"blue  \"green  \"yellow  \"purple",
                 "Draw after: `SETCOLOR \"red  FD 100`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Used SETCOLOR command",
                        @"\b(SETCOLOR|SETPENCOLOR|SETPC)\b"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Change the pen color and draw!\n\n"
            ),

            new(
                "colors_rainbow",
                "Rainbow Square", "🌈🟥",
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

        // ── Procedures ──────────────────────────────────────────────────
        new("Procedures", "⚙️",
        [
            new(
                "procs_basic",
                "My First Procedure", "🔧",
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
                "procs_params",
                "Procedure with Parameters", "🎛️",
                "Create a procedure that accepts a **parameter** — a value you pass in when calling it.\n\nMake a `square` procedure that takes a `:size` parameter so you can draw squares of any size!",
                ["Parameters use a colon: `TO square :size`",
                 "Use :size inside: `REPEAT 4 [ FD :size  RT 90 ]`",
                 "Call with: `square 100` or `square 50`"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CodePattern,      "Procedure has a parameter",
                        @"\bTO\s+\w+\s+:\w+"),
                    new(CheckType.CodePattern,      "Parameter is referenced inside body",
                        @":\w+"),
                    new(CheckType.CanvasActivity,   "Something drawn on canvas"),
                ],
                "; Create a parameterised procedure!\n\nTO square :size\n  REPEAT 4 [ FD :size RT 90 ]\nEND\n\nsquare 100\nsquare 50\n"
            ),
        ]),
    ];
}