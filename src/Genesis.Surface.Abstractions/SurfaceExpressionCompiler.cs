// Genesis.Surface.Abstractions — SurfaceExpressionCompiler.cs
// The missing adapter: SurfaceExpression (renderer-neutral UI IR produced by
// SchemaBackedGenerator) → SurfaceSpec (Block IR consumed by all projectors).
//
// Pipeline position:
//   SurfaceExpression  (schema-backed, node tree + actions + bindings)
//       ↓ this file
//   SurfaceSpec        (block tree, EMIT-annotated, content-addressed SpecId)
//       ↓ WebProjector / CehProjector
//   SurfaceIntent → SurfaceMessage → surface-channel-client.js DOM patch
//
// Mapping rules (SurfaceNode → BlockBase):
//   SurfaceContainerNode  → ContainerBlock    (I: integration, layout wrapper)
//   SurfaceTextNode       → TextBlock         (E: observation, labeled value)
//   SurfaceInputNode      → TextBlock         (E: observation, data entry slot)
//   SurfaceActionNode     → StatusBlock       (T: transition, action trigger)
//   SurfaceAction (list)  → LogBlock          (E: observed action catalogue)
//
// EmitBasin is derived from node type frequency (dominant character wins).
// Altitude is derived from expression complexity (action count + capability count).
// SpecId is SHA-256 content-addressed over expressionId + targetSurfaceId + blockIds.
//
// (c) 2026 Brandon Clark / Genesis Systems. All Rights Reserved.

using System.Security.Cryptography;
using System.Text;

namespace Genesis.Surface.Abstractions;

/// <summary>
/// Compiles a <see cref="SurfaceExpression"/> to a <see cref="SurfaceSpec"/>
/// suitable for projection by any <see cref="ISurfaceProjector"/>.
/// </summary>
public static class SurfaceExpressionCompiler
{
    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Compile a <see cref="SurfaceExpression"/> to a <see cref="SurfaceSpec"/>.
    /// </summary>
    /// <param name="expression">The renderer-neutral UI IR to compile.</param>
    /// <param name="targetSurfaceId">
    ///     The surface this spec will be projected onto.
    ///     Used as a merge key for <see cref="SurfaceSpec.SpecId"/>.
    /// </param>
    /// <param name="altitude">
    ///     Optional override for EMIT altitude band.
    ///     When null, altitude is inferred from expression complexity.
    /// </param>
    public static SurfaceSpec Compile(
        SurfaceExpression expression,
        string targetSurfaceId,
        string? altitude = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSurfaceId);

        // 1. Compile node tree → block list
        var blocks = CompileNode(expression.Root, expression.Actions).ToList();

        // 2. Append action catalogue as a LogBlock when actions exist
        if (expression.Actions.Count > 0)
            blocks.Add(CompileActionCatalogue(expression.Actions));

        // 3. Derive EMIT basin from compiled blocks
        var basin    = DeriveBasin(blocks);

        // 4. Derive altitude from expression complexity
        var altBand  = altitude ?? DeriveAltitude(expression);

        // 5. Content-address the spec id
        var specId   = ContentHash(expression.ExpressionId, targetSurfaceId, blocks);

        return new SurfaceSpec(
            SpecId:          specId,
            TargetSurfaceId: targetSurfaceId,
            EmitBasin:       basin,
            Altitude:        altBand,
            Blocks:          blocks,
            Title:           expression.Name,
            GeneratedAt:     expression.CreatedAt ?? DateTimeOffset.UtcNow);
    }

    // ── Node compilation ─────────────────────────────────────────────────────

    private static IEnumerable<BlockBase> CompileNode(
        SurfaceNode node,
        IReadOnlyList<SurfaceAction> actions)
    {
        switch (node)
        {
            case SurfaceContainerNode container:
                // I-dominant: integration / layout wrapper
                var children = (container.Children ?? [])
                    .SelectMany(c => CompileNode(c, actions))
                    .ToList();

                yield return new ContainerBlock(
                    BlockId:       container.NodeId,
                    Title:         container.Label,
                    Children:      children,
                    Layout:        container.Layout,
                    EmitPrimitive: "I");
                break;

            case SurfaceTextNode text:
                // E-dominant: observation — something is named or displays a value
                yield return new TextBlock(
                    BlockId:       text.NodeId,
                    Label:         text.Label ?? text.NodeId,
                    Value:         text.Text,
                    EmitPrimitive: "E");
                break;

            case SurfaceInputNode input:
                // E-dominant: data entry slot — an observation point waiting to be filled
                yield return new TextBlock(
                    BlockId:       input.NodeId,
                    Label:         input.Label ?? input.Binding ?? input.NodeId,
                    Value:         input.Placeholder ?? $"Enter {input.ValueType}",
                    EmitPrimitive: "E");
                break;

            case SurfaceActionNode action:
                // T-dominant: transition — a governed state change trigger
                var def = actions.FirstOrDefault(a => a.Id == action.ActionId);
                yield return new StatusBlock(
                    BlockId:       action.NodeId,
                    Label:         action.Label ?? action.ActionId ?? action.NodeId,
                    State:         ActionStyleToState(action.Style),
                    Detail:        def?.Description,
                    EmitPrimitive: "T");
                break;

            default:
                // Unknown node type — emit as opaque text (safe fallback)
                yield return new TextBlock(
                    BlockId:       node.NodeId,
                    Label:         node.Label ?? node.NodeId,
                    Value:         node.Role,
                    EmitPrimitive: "E");
                break;
        }
    }

    /// <summary>
    /// Compile the action list as a single LogBlock (E-dominant, observable catalogue).
    /// </summary>
    private static LogBlock CompileActionCatalogue(IReadOnlyList<SurfaceAction> actions)
    {
        var entries = actions.Select(a => new LogEntry(
            Ts:    DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Text:  $"{a.Label} — {a.Description} [{a.ReturnType}]" +
                   (a.MutatesData ? " [mutating]" : ""),
            Level: a.MutatesData ? "warn" : "info")).ToList();

        return new LogBlock(
            BlockId:       "action-catalogue",
            Label:         "Actions",
            Entries:       entries,
            EmitPrimitive: "E");
    }

    // ── Emit basin derivation ─────────────────────────────────────────────────

    private static string DeriveBasin(IReadOnlyList<BlockBase> blocks)
    {
        var counts = new Dictionary<string, int>
        {
            ["E"] = 0, ["M"] = 0, ["I"] = 0, ["T"] = 0,
        };

        CountEmit(blocks, counts);

        // Prefer I_BASIN when containers dominate (layout-driven expression)
        // Prefer T_BASIN when transitions dominate (action-driven expression)
        var dominant = counts.MaxBy(kv => kv.Value);
        return $"{dominant.Key}_BASIN";
    }

    private static void CountEmit(IEnumerable<BlockBase> blocks, Dictionary<string, int> counts)
    {
        foreach (var block in blocks)
        {
            if (counts.ContainsKey(block.EmitPrimitive))
                counts[block.EmitPrimitive]++;

            if (block is ContainerBlock c)
                CountEmit(c.Children, counts);
        }
    }

    // ── Altitude derivation ───────────────────────────────────────────────────

    private static string DeriveAltitude(SurfaceExpression expression)
    {
        // Altitude = complexity of the expression.
        // More actions + capabilities = higher abstraction = higher altitude band.
        var complexity = expression.Actions.Count + expression.RequiredCapabilities.Count;
        return complexity switch
        {
            >= 15 => "50000ft",
            >= 8  => "10000ft",
            >= 3  => "1000ft",
            _     => "ground",
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Map SurfaceActionNode.Style to a StatusBlock State value.
    /// </summary>
    private static string ActionStyleToState(string style) => style switch
    {
        "primary"   => "action",
        "danger"    => "error",
        "secondary" => "action",
        "ghost"     => "unknown",
        _           => "action",
    };

    /// <summary>
    /// Content-addressed SpecId: SHA-256 of expressionId + targetSurfaceId + blockId list.
    /// Ensures the same expression projected to the same surface always yields the same SpecId —
    /// idempotent projection is a SurfaceRuntime invariant.
    /// </summary>
    private static string ContentHash(
        string expressionId,
        string targetSurfaceId,
        IReadOnlyList<BlockBase> blocks)
    {
        var input = $"{expressionId}:{targetSurfaceId}:{string.Join(",", blocks.Select(b => b.BlockId))}";
        var hash  = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
