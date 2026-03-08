using System.Text.RegularExpressions;
using Nlogo.Models;

namespace Nlogo.Services;

/// <summary>
/// Owns the challenge catalogue, progress tracking, and validation.
/// Registered as a singleton so state survives navigation.
/// </summary>
public sealed class ChallengeService
{
    // ── Dependencies ───────────────────────────────────────────────────────
    private readonly ProgressPersistenceService _persistence;
    private bool _loaded;

    // ── Progress store ─────────────────────────────────────────────────────
    private readonly Dictionary<string, ChallengeProgress> _progress = new();

    public event Action? ProgressChanged;

    // ── Catalogue ──────────────────────────────────────────────────────────
    public IReadOnlyList<ChallengeCategory> Catalog { get; } = BuildCatalog();

    // ── Constructor ────────────────────────────────────────────────────────
    public ChallengeService(ProgressPersistenceService persistence)
    {
        _persistence = persistence;
    }

    // ── Aggregates ─────────────────────────────────────────────────────────
    public int TotalStars => _progress.Values.Sum(p => p.Stars);
    public int TotalPossible => Catalog.Sum(c => c.Challenges.Length * 3);
    public int CompletedCount => _progress.Values.Count(p => p.Stars > 0);

    // ── Persistence bootstrap ──────────────────────────────────────────────

    /// <summary>
    /// Loads persisted progress from disk on first call. Idempotent — safe
    /// to call multiple times. Called from Home.razor OnAfterRenderAsync.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;

        var saved = await _persistence.LoadAsync();
        foreach (var (id, prog) in saved)
            _progress[id] = prog;

        // Notify UI so star counts and unlock states refresh
        ProgressChanged?.Invoke();
    }

    // ── Progress queries ───────────────────────────────────────────────────
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
    /// <summary>
    /// Persists challenge progress in memory and fires a background save.
    /// Only updates if the new result is strictly better than the existing one.
    /// </summary>
    public void SaveProgress(string id, int stars, TimeSpan time, string? code = null)
    {
        var existing = GetProgress(id);
        bool better = existing is null
                    || stars > existing.Stars
                    || (stars == existing.Stars && time < existing.BestTime);

        if (better)
        {
            _progress[id] = new ChallengeProgress(id, stars, time, DateTime.Now, code);
            ProgressChanged?.Invoke();

            // Fire-and-forget — non-fatal if it fails; progress is still in memory
            _ = _persistence.SaveAsync(_progress.Values);
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
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                ]
            ),

            new(
                "basics_square",
                "Draw a Square", "⬛",
                "Draw a **square** by repeating: move forward, then turn right.\n\nA square has 4 equal sides and 4 right angles (90°).",
                ["A square needs 4 sides",
                 "Each corner is a 90 degree turn",
                 "Try using REPEAT 4 [ FD 100 RT 90 ]"],
                [
                    new(CheckType.RunsWithoutError,   "Code runs without errors"),
                    new(CheckType.CanvasActivity,     "Turtle drew something on the canvas"),
                    new(CheckType.CanvasSquareBounds, "Drawing looks roughly square"),
                    new(CheckType.CodePattern,        "Used REPEAT",
                        @"\bREPEAT\b"),
                ]
            ),
        ]),

        // ── Shapes ──────────────────────────────────────────────────────
        new("Shapes", "🔷",
        [
            new(
                "shapes_triangle",
                "Triangle Time", "🔺",
                "Draw an **equilateral triangle**.\n\nA triangle has 3 sides. The outside turn angle is **120 degrees**.",
                ["REPEAT 3 repeats something 3 times",
                 "Each turn for a triangle is 120 degrees",
                 "Try: REPEAT 3 [ FD 100 RT 120 ]"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Used REPEAT 3",
                        @"\bREPEAT\s+3\b"),
                ]
            ),

            new(
                "shapes_circle",
                "Round and Round", "⭕",
                "Draw a **circle** (or close to one!) using REPEAT.\n\nHint: repeat many small steps with tiny turns adds up to a circle.",
                ["Try REPEAT 36 [ FD 10 RT 10 ]",
                 "More repetitions with smaller steps = smoother circle",
                 "360 degrees total makes a full circle"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Used a large REPEAT (36+)",
                        @"\bREPEAT\s+([3-9]\d|\d{3,})\b"),
                ]
            ),

            new(
                "shapes_star",
                "Reach for the Stars", "⭐",
                "Draw a **5-pointed star**!\n\nFor a 5-point star, each outer turn is **144 degrees**.",
                ["A star has 5 points",
                 "Try REPEAT 5 [ FD 150 RT 144 ]",
                 "144 = 720 / 5 — the total degrees in a star path"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Used REPEAT 5",
                        @"\bREPEAT\s+5\b"),
                ]
            ),
        ]),

        // ── Colour ──────────────────────────────────────────────────────
        new("Colour", "🎨",
        [
            new(
                "color_basic",
                "Paint it Red", "🔴",
                "Change the pen colour using `SETCOLOR`.\n\nTry `SETCOLOR \"red` then draw something!",
                ["SETCOLOR \"red sets the colour to red",
                 "Colours: red, blue, green, yellow, orange, purple, black, white",
                 "Put SETCOLOR before your drawing commands"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Used SETCOLOR",
                        @"\bSETCOLOR\b|\bSETPENCOLOR\b|\bSETPC\b"),
                ]
            ),

            new(
                "color_rainbow",
                "Rainbow Lines", "🌈",
                "Draw lines in **multiple different colours**!\n\nChange the colour between each line using SETCOLOR.",
                ["Use SETCOLOR before each line to change colour",
                 "Draw at least 3 differently-coloured lines",
                 "Try: SETCOLOR \"red FD 100 RT 120 SETCOLOR \"blue FD 100"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Used SETCOLOR at least 3 times",
                        @"(\bSETCOLOR\b|\bSETPENCOLOR\b|\bSETPC\b).*(\bSETCOLOR\b|\bSETPENCOLOR\b|\bSETPC\b).*(\bSETCOLOR\b|\bSETPENCOLOR\b|\bSETPC\b)"),
                ]
            ),
        ]),

        // ── Variables ─────────────────────────────────────────────────────
        new("Variables", "📦",
        [
            new(
                "vars_make",
                "Store a Value", "🏷️",
                "Use `MAKE` to store a number in a variable, then use it!\n\n```\nMAKE \"size 100\nFD :size\n```",
                ["MAKE \"name value stores a value",
                 ":name reads the stored value back",
                 "Try: MAKE \"side 80 then REPEAT 4 [ FD :side RT 90 ]"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Used MAKE to create a variable",
                        @"\bMAKE\s+""[A-Za-z]"),
                    new(CheckType.CodePattern,      "Used a variable with :",
                        @":[A-Za-z]"),
                ]
            ),
        ]),

        // ── Procedures ────────────────────────────────────────────────────
        new("Procedures", "⚙️",
        [
            new(
                "proc_basic",
                "Your First Procedure", "🔧",
                "Write your own **procedure** (a reusable block of commands) using `TO` and `END`.\n\n```\nTO SQUARE\n  REPEAT 4 [ FD 100 RT 90 ]\nEND\n\nSQUARE\n```",
                ["TO starts the definition, END finishes it",
                 "Give your procedure a name after TO",
                 "Call it by typing its name"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Defined a procedure with TO...END",
                        @"\bTO\s+[A-Za-z]"),
                ]
            ),

            new(
                "proc_param",
                "Procedures with Parameters", "🎛️",
                "Give your procedure a **parameter** so it can draw different sizes!\n\n```\nTO SQUARE :size\n  REPEAT 4 [ FD :size RT 90 ]\nEND\n\nSQUARE 50\nSQUARE 100\n```",
                ["Add :paramname after the procedure name",
                 "Use the parameter inside the procedure with :name",
                 "Call it with different values to draw different sizes"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something on the canvas"),
                    new(CheckType.CodePattern,      "Procedure has at least one parameter",
                        @"\bTO\s+[A-Za-z]\w*\s+:[A-Za-z]"),
                ]
            ),
        ]),
    ];
}