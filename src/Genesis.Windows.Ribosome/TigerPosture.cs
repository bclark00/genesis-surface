using System.Text.Json;
using System.IO;

namespace Genesis.Windows.Ribosome;

/// <summary>Read-only posture projection from RFC-PROPRIOCEPTION-001 JSONL.</summary>
public sealed record TigerPosture(
    string Basin,
    string Altitude,
    long Clauses,
    double SpectralDistance,
    string Governs,
    string FrontierIntent,
    double MeanConvergence);

public static class TigerProprioceptionBridge
{
    public static bool TryReadLatest(string path, out TigerPosture posture)
    {
        posture = default!;
        if (!File.Exists(path)) return false;
        string? last = null;
        foreach (var line in File.ReadLines(path))
            if (!string.IsNullOrWhiteSpace(line)) last = line;
        if (last is null) return false;

        try
        {
            using var doc = JsonDocument.Parse(last);
            var root = doc.RootElement;
            var frontier = root.TryGetProperty("frontier", out var f) && f.ValueKind == JsonValueKind.Object
                ? f : default;
            posture = new TigerPosture(
                String(root, "basin", "UNKNOWN"),
                String(root, "altitude", "unknown"),
                NumberInt(root, "clauses"),
                Math.Sqrt(Math.Pow(NumberDouble(root, "spec1"), 2) +
                          Math.Pow(NumberDouble(root, "spec2"), 2) +
                          Math.Pow(NumberDouble(root, "spec3"), 2)),
                String(root, "governs", "none"),
                frontier.ValueKind == JsonValueKind.Object ? String(frontier, "intents", "unknown") : "unknown",
                frontier.ValueKind == JsonValueKind.Object ? NumberDouble(frontier, "mean_convergence") : 0);
            return true;
        }
        catch (JsonException) { return false; }
    }

    public static bool TryHydrate(
        TigerStateHub hub,
        string? stateDirectory = null,
        string? proposedIntent = null,
        double confidence = 0)
    {
        var dir = stateDirectory ?? Environment.GetEnvironmentVariable("GENESIS_STATE_DIR")
            ?? "C:\\genesis\\seed\\state";
        var path = Environment.GetEnvironmentVariable("PROPRIOCEPTION_LOG")
            ?? Path.Combine(dir, "proprioception.jsonl");
        if (!TryReadLatest(path, out var posture)) return false;
        hub.Publish(posture: posture, proposedIntent: proposedIntent ?? posture.FrontierIntent,
            intentConfidence: confidence);
        return true;
    }

    private static string String(JsonElement obj, string name, string fallback)
    {
        if (obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? fallback;
        if (name == "intents" && obj.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().FirstOrDefault().GetString() ?? fallback;
        return fallback;
    }

    private static long NumberInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : 0;

    private static double NumberDouble(JsonElement obj, string name, double fallback = 0)
        => obj.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number : fallback;
}
