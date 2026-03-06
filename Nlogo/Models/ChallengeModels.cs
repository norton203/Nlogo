using System.Text.Json.Serialization;

namespace Nlogo.Models;

// ── Check types ────────────────────────────────────────────────────────────
public enum CheckType
{
    RunsWithoutError,     // lexer + parser report no errors
    CanvasActivity,       // any pixels drawn on canvas
    CanvasSquareBounds,   // bounding-box aspect ratio ≈ 1
    CodePattern,          // regex match on source text
    ManualCheck           // student self-asserts ("does it look right?")
}

// ── A single requirement inside a challenge ────────────────────────────────
public sealed record ChallengeCheck(
    CheckType Type,
    string Requirement,   // shown to student
    string? Pattern = null // regex for CodePattern
);

// ── A challenge ────────────────────────────────────────────────────────────
public sealed record Challenge(
    string Id,
    string Title,
    string Emoji,
    string Description,   // supports **bold**, `code`, ```block```, \n\n paragraphs
    string[] Hints,
    ChallengeCheck[] Checks,
    string? StarterCode = null
);

// ── A category grouping challenges ─────────────────────────────────────────
public sealed record ChallengeCategory(
    string Name,
    string Emoji,
    Challenge[] Challenges
);

// ── Canvas stats returned from JS ─────────────────────────────────────────
public sealed class CanvasStats
{
    [JsonPropertyName("hasDrawing")] public bool HasDrawing { get; set; }
    [JsonPropertyName("pixelCount")] public int PixelCount { get; set; }
    [JsonPropertyName("aspectRatio")] public double AspectRatio { get; set; }
}

// ── Validation result ──────────────────────────────────────────────────────
public sealed record CheckResult(
    bool[] PassedChecks,
    int Stars,
    string Message
);

// ── Persisted progress per challenge ──────────────────────────────────────
public sealed record ChallengeProgress(
    string ChallengeId,
    int Stars,
    TimeSpan BestTime,
    DateTime CompletedAt
);