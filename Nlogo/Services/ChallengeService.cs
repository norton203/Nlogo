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
    /// <summary>
    /// Persists challenge progress. Stores the student's code so it can
    /// be included in offline email submissions.
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
                ]
            ),

            new(
                "basics_square",
                "Draw a Square", "⬛",
                "Draw a **square** using forward and turn commands.\n\nA square has 4 equal sides and 4 right angles (90°).",
                ["You need to move forward and turn — 4 times!",
                 "Each turn should be 90 degrees",
                 "Try using REPEAT 4 [ FD 100 RT 90 ]"],
                [
                    new(CheckType.RunsWithoutError,  "Code runs without errors"),
                    new(CheckType.CanvasActivity,    "Turtle drew something on the canvas"),
                    new(CheckType.CanvasSquareBounds,"Drawing looks roughly square"),
                ]
            ),
        ]),

        // ── Loops ───────────────────────────────────────────────────────
        new("Loops", "🔁",
        [
            new(
                "loops_repeat",
                "Repeat It!", "🔄",
                "Use `REPEAT` to draw a shape without repeating yourself!\n\n`REPEAT 4 [ FD 100 RT 90 ]` draws a square in one line.",
                ["REPEAT n [ ... ] runs the block n times",
                 "Try REPEAT 6 [ FD 100 RT 60 ] for a hexagon",
                 "The number after REPEAT controls how many times"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something"),
                    new(CheckType.CodePattern,      "Used REPEAT",
                        @"\bREPEAT\b"),
                ]
            ),

            new(
                "loops_spiral",
                "Spiral Out", "🌀",
                "Create a **spiral** by increasing the distance each time around.\n\nHint: use a variable that grows with each step!",
                ["Use MAKE to create a variable: MAKE \"dist 10",
                 "Inside your loop, increase dist each time",
                 "Try: REPEAT 36 [ FD :dist RT 10  MAKE \"dist :dist + 5 ]"],
                [
                    new(CheckType.RunsWithoutError, "Code runs without errors"),
                    new(CheckType.CanvasActivity,   "Turtle drew something"),
                    new(CheckType.CodePattern,      "Used a variable",
                        @"\bMAKE\b"),
                ]
            ),
        ]),
    ];
}