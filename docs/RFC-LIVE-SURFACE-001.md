# RFC-LIVE-SURFACE-001: Live Surface Projection Family

| Field | Value |
|---|---|
| Status | NORMATIVE DRAFT v0.2.0 |
| Date | 2026-07-29; revised 2026-07-31 |
| Author | Brandon Clark / Genesis Systems |
| Parents | RFC-SURFACE-PROJECTION-001 |
| Children | RFC-SURFACE-CHANNEL-001 |
| Implements | Block IR and LiveSurfaceSpec for real-time surface projections |

---

## 0. Purpose

RFC-LIVE-SURFACE-001 defines the **live surface projection family** — the intermediate representation for dynamic, real-time UI projections that update as system state changes.

This RFC defines what Block IR nodes look like and how they compose into a `SurfaceSpec` (herein called `LiveSurfaceSpec` for clarity). It does not define how that spec reaches the runtime — that is RFC-SURFACE-CHANNEL-001.

---

## 1. Block IR

Block IR is the projection artifact for the live surface family. It is **not** a semantic object (RFC-SURFACE-001 semantics have already been discarded by this layer). It is a rendering primitive annotated with EMIT character.

### 1.1 BlockBase

```
BlockBase (abstract)
  BlockId       : string   — stable merge key; same block updated in place across projections
  EmitPrimitive : string   — "E" | "M" | "I" | "T"
  CssClass      : string?  — render hint, optional

  TextBlock      blockType="text"       Label, Value
  MetricBlock    blockType="metric"     Label, Value (double), Unit, Trend
  StatusBlock    blockType="status"     Label, State, Detail
  ContainerBlock blockType="container"  Title, Children[], Layout
  LogBlock       blockType="log"        Label, Entries[], ChainHash
  LogEntry {                            — receipt chain node
      Ts              : DateTimeOffset  — wall-clock (observational, receipt plane)
      Text            : string          — observed content
      Level           : string          — "info" | "warn" | "error" | "debug"
      entry_hash      : bytes[32]       — sha256(canonicalJSON(Ts, Text, Level, prev_entry_hash))
      prev_entry_hash : bytes[32]?      — null for chain head; prior entry_hash otherwise
  }
  LogBlock.ChainHash = Entries.Last?.entry_hash ?? null
                       — commits to the full entry sequence
```

**EMIT alignment**:

| Block type | EMIT primitive | Semantic role |
|---|---|---|
| TextBlock | E | Observation — something exists or is named |
| MetricBlock | M | Measure — quantity, magnitude, comparison |
| StatusBlock | T | Transition — state, lifecycle, governance |
| ContainerBlock | I | Integration — composition of child nodes |
| LogBlock | E | **Receipt plane** — hash-linked chain of observed events. Dual to the content blocks (TextBlock/MetricBlock/StatusBlock/ContainerBlock). See §1.3. |

**BlockId invariant**: BlockId is the merge key for incremental patching (RFC-SURFACE-CHANNEL-001 §3). It must be stable across projection calls for the same logical block. It is derived from the source node's NodeId and the projection context, not generated randomly.

### 1.3 LogBlock as Receipt Plane

The Block IR implements the RFC-014 Dual-Plane Law at the surface layer:

```
Content plane:   TextBlock, MetricBlock, StatusBlock, ContainerBlock
Receipt plane:   LogBlock
```

The four content blocks carry what IS — content-addressed, F-sector identities
that are stable across re-projection of the same semantic state. LogBlock carries
what HAPPENED — a hash-linked, append-only receipt chain of observed events.

**Receipt chain invariants (INV-LOG-RECEIPT):**

```
INV-LOG-CHAIN-ORDER:
    LogEntry.entry_hash = sha256(canonicalJSON({
        ts:               entry.Ts.ToString("O"),
        text:             entry.Text,
        level:            entry.Level,
        prev_entry_hash:  prior.entry_hash ?? "null"
    }))
    Chain is append-only. Entries are never removed or reordered.

INV-LOG-CHAIN-IMMUTABLE:
    A committed LogEntry is immutable. Its entry_hash is permanent.
    Modifying a committed entry requires creating a new entry with
    new content and a new hash.

INV-LOG-CHAIN-HEAD:
    LogBlock.ChainHash = Entries.Last?.entry_hash ?? null
    ChainHash commits to the full entry sequence: any change to any
    prior entry invalidates all subsequent entry_hashes and the ChainHash.
```

**Relationship to CMP-001:** LogBlock materializes the EMIT evidence stream
of RFC-CMP-001 §3 as a governed receipt chain. Each LogEntry is an EMIT.E
observation record. The chain ordering corresponds to the pipeline's causal
ordering. ChainHash is the content-addressed behavioral fingerprint of what
the pipeline observed.

**Behavioral identity:** The `BehavioralSpecId` (§1.2) derived from a surface
spec containing LogBlocks is the behavioral canonical form — the surface analog
of `gene_hash` at the binary layer and `SpecId` at the structural layer.
Same computation over same inputs → same chain → same ChainHash → same
BehavioralSpecId.

### 1.2 LiveSurfaceSpec (SurfaceSpec)

```
SurfaceSpec (LiveSurfaceSpec)
  SpecId          : string         — content-addressed
  TargetSurfaceId : string         — which surface this spec targets
  EmitBasin       : string         — "E_BASIN" | "M_BASIN" | "I_BASIN" | "T_BASIN"
  Altitude        : string         — "ground" | "1000ft" | "10000ft" | "50000ft"
  Blocks          : BlockBase[]    — top-level block list
  Title           : string?
  GeneratedAt     : DateTimeOffset?
```

**SpecId invariant**: `SHA-256(TargetSurfaceId + ":" + sorted(BlockId[]))` hex-encoded. Identical block trees projected to the same surface always produce the same SpecId.

**BehavioralSpecId invariant**: When a `LiveSurfaceSpec` contains one or more LogBlocks, a stronger identity is available:

```
BehavioralSpecId = SHA-256(
    TargetSurfaceId + ":" +
    sorted(BlockId[] + LogBlock.ChainHash[])
)
```

BehavioralSpecId commits to both the block structure AND the full receipt chain of every LogBlock. Two surface projections with the same BehavioralSpecId produced identical observable events in identical order. This is the behavioral identity of the computation — the `CompositionId` for the surface layer.

`SpecId` and `BehavioralSpecId` are related but distinct. SpecId answers "are these the same blocks?" BehavioralSpecId answers "did these blocks record the same history?"

**Note on naming**: The C# type is currently called `SurfaceSpec`. This RFC clarifies its role as the live-surface-specific IR (a `LiveSurfaceSpec`). The type will be renamed in the refactor implied by this RFC family.

---

## 2. Render Modes

A live surface can render in one of three modes. The render mode is a property of the projection context (host configuration), not of the `LiveSurfaceSpec` itself:

| Mode | Description | Implementation |
|---|---|---|
| `Embedded` | Surface renders inside a host window | `RibosomeWindow` (WPF + WebView2) |
| `Overlay` | Surface renders as floating glass over the host | `TigerOverlayWindow` |
| `Fullscreen` | Surface takes over the entire display | Future |

TigerOverlay is a render mode variant of the live surface family, not a separate projection family. The same `LiveSurfaceSpec` can be rendered in any mode.

---

## 3. SurfaceRenderMode

The render mode governs how a live surface is materialized by the host. It is a property of the projection context, not of `LiveSurfaceSpec`.

```csharp
public enum SurfaceRenderMode
{
    Embedded,    // Surface renders inside a host window (RibosomeWindow)
    Windowed,    // Surface renders in its own top-level window
    Overlay,     // Surface renders as floating glass over the host (TigerOverlayWindow)
    Fullscreen,  // Surface takes over the entire display
    Headless     // No visible output — for testing, remote projection, snapshots,
                 // accessibility extraction, or channels that materialize state
                 // without a visible host.
}
```

**Invariant**: `Headless` mode accepts the same `LiveSurfaceSpec` and produces the same `SurfaceMessage` stream as any other mode. The difference is the materializer — no WebView2 window, no XAML tree. The live surface pipeline is otherwise identical.


## 4. Block IR Serialization

Block IR serializes to JSON using the `blockType` discriminator property:

```json
{
  "blockType": "metric",
  "blockId":   "uptime-metric",
  "emitPrimitive": "M",
  "label":     "Uptime",
  "value":     3600.0,
  "unit":      "s",
  "trend":     "up"
}
```

The JSON discriminator is `blockType`. The serialization context (`SurfaceJsonContext`) registers all derived types. Consumers must use the registered context for polymorphic deserialization.

---

## 4. Implementation Reference

**Canonical C# implementation**: `Genesis.Surface.Abstractions/Contracts.cs`

Planned migration target: `Genesis.Surface.Live/` (new package, RFC-implied refactor).

**Registered block types**: `TextBlock`, `MetricBlock`, `StatusBlock`, `ContainerBlock`, `LogBlock`.

**JS counterpart**: `surface-channel-client.js` in genesis-homebase — handles deserializing Block IR from `SurfaceMessage.blocks[]` and applying DOM patches.

---

---

## Changelog

| Version | Date | Change |
|---------|------|--------|
| 0.2.0 | 2026-07-31 | LogBlock: add `entry_hash` + `prev_entry_hash` to LogEntry (receipt chain); add `ChainHash` to LogBlock; add `BehavioralSpecId` invariant; add §1.3 LogBlock as Receipt Plane. Grounds LogBlock in RFC-014 Dual-Plane Law and RFC-CMP-001 EMIT evidence semantics. |
| 0.1.0 | 2026-07-29 | Initial normative draft — Block IR types, LiveSurfaceSpec, SpecId invariant, render modes, serialization. |

---

*Brandon Clark / Genesis Systems 2026*
