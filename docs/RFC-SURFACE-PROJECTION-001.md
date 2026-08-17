# RFC-SURFACE-PROJECTION-001: Projection Contracts

| Field | Value |
|---|---|
| Status | NORMATIVE DRAFT v0.1.0 |
| Date | 2026-07-29 |
| Author | Brandon Clark / Genesis Systems |
| Parents | RFC-SURFACE-001 |
| Children | RFC-LIVE-SURFACE-001, RFC-DOCUMENT-001 |
| Implements | Compilation of surface semantics to projection-family IRs |

---

## 0. Purpose

RFC-SURFACE-PROJECTION-001 defines the **compilation layer** between the renderer-neutral semantic model (RFC-SURFACE-001) and the family-specific IRs (RFC-LIVE-SURFACE-001, RFC-DOCUMENT-001).

A SurfaceExpression does not know it will become HTML, XAML, a PDF, or a stream of Block IR updates. Projection is the decision point that makes the family choice and produces a target-specific intermediate representation.

---

## 1. Core Contracts

### 1.1 ISurfaceProjector

```
ISurfaceProjector
  TargetKind : string         — "web" | "ceh" | "wpf" | "pdf" | ...

  Task<ProjectionReceipt> ProjectAsync(
      SurfaceSpec    spec,
      ISurfaceChannel channel,
      CancellationToken ct)
```

A projector takes a `SurfaceSpec` (the live-surface IR — see RFC-LIVE-SURFACE-001) and materializes it onto a surface via a channel. The projector does not interpret semantic-level objects; it receives a fully compiled spec.

**Invariant**: ProjectAsync is idempotent for identical (SurfaceSpec.SpecId, ISurfaceChannel.SurfaceId) pairs within a session. Duplicate calls produce the same visual outcome and return a `Duplicate=true` receipt.

### 1.2 ProjectionReceipt

Every projection call produces an audit receipt:

```
ProjectionReceipt
  ReceiptId     : string
  SpecId        : string         — from SurfaceSpec
  ProjectorKind : string         — TargetKind of the projector used
  SurfaceId     : string         — from ISurfaceChannel
  ProjectedAt   : DateTimeOffset
  Success       : bool
  BlockCount    : int            — number of blocks projected
  Error         : string?        — present when Success=false
```

Receipts are **append-only** and **never modified** after creation. The receipt log is the audit trail for all surface projections in a session.

### 1.3 LiveSurfaceCompiler (formerly SurfaceExpressionCompiler)

The projection compiler transforms a `SurfaceExpression` (RFC-SURFACE-001) into a `SurfaceSpec` (RFC-LIVE-SURFACE-001). It is the bridge between the semantic layer and the live-surface projection family.

**Mapping rules**:

| SurfaceNode type | Block IR type | EMIT primitive |
|---|---|---|
| SurfaceContainerNode | ContainerBlock | I |
| SurfaceTextNode | TextBlock | E |
| SurfaceInputNode | TextBlock (data entry slot) | E |
| SurfaceActionNode | StatusBlock (action trigger) | T |
| SurfaceAction[] (catalogue) | LogBlock | E |

**EMIT basin derivation**: Count nodes by dominant EMIT primitive. The majority character names the basin (`E_BASIN`, `M_BASIN`, `I_BASIN`, `T_BASIN`).

**Altitude derivation** from expression complexity:

| Complexity score | Altitude |
|---|---|
| ≤ 2 actions + 0 capabilities | `"ground"` |
| ≤ 5 actions + ≤ 2 capabilities | `"1000ft"` |
| > 5 actions or > 2 capabilities | `"10000ft"` |

**Naming**: The compiler is renamed `LiveSurfaceCompiler` (from `SurfaceExpressionCompiler`) to make explicit that it only lowers into the live projection family. The sibling compiler for documents will be `SurfaceToDocumentProjectionCompiler`.

**SpecId content addressing**: `SHA-256(expressionId + ":" + targetSurfaceId + ":" + sorted(blockIds))` hex-encoded.

### 1.4 ProjectionPolicy (reserved)

`ProjectionPolicy` is reserved for future normative definition. It will govern:

- Maximum block count per projection
- Capability gate enforcement
- Rate limiting and cost ceiling
- Privacy ceiling (which data paths may appear in projections)

### 1.5 ProjectionIdentity (reserved)

`ProjectionIdentity` is reserved for per-viewer projection variants under `SurfaceRole` / `SurfaceIdentity` (RFC-SURFACE-001 §1.7).

---

## 2. Projection Family Discriminant

The projection compiler produces one of two downstream IRs depending on the projection family:

```
SurfaceExpression
        ↓ ProjectionCompiler
        ├── LiveSurfaceSpec   (if family = "live")   → RFC-LIVE-SURFACE-001
        └── DocumentSpec      (if family = "document") → RFC-DOCUMENT-001
```

The family choice is made by the caller of `ProjectionCompiler.Compile()`, not by the compiler itself. The compiler is family-agnostic; the produced IR type is family-specific.

---

## 3. Implementation Reference

**Canonical C# implementation**:
- `Genesis.Surface.Abstractions/SurfaceExpressionCompiler.cs` — `LiveSurfaceCompiler` (rename pending)
- `Genesis.Surface.Abstractions/Contracts.cs` — `ISurfaceProjector`, `ProjectionReceipt`

**Package**: `Genesis.Surface.Abstractions` (genesis-monorepo `cs/src/Genesis.Surface.Abstractions/`)

**Note**: The current package mixes projection contracts with live-surface IR and channel abstractions. The refactor implied by this RFC family moves:
- Block IR and SurfaceSpec → `Genesis.Surface.Live` (new package)
- ISurfaceChannel, SurfaceMessage → `Genesis.Surface.Channel` (new package)
- ISurfaceProjector, ProjectionReceipt, SurfaceExpressionCompiler → retained in `Genesis.Surface.Abstractions`

---

*Brandon Clark / Genesis Systems 2026*
