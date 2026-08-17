# RFC-SURFACE-CHANNEL-001: Surface Channel Transport

| Field | Value |
|---|---|
| Status | NORMATIVE DRAFT v0.1.0 |
| Date | 2026-07-29 |
| Author | Brandon Clark / Genesis Systems |
| Parents | RFC-LIVE-SURFACE-001 |
| Implements | Transport and synchronization for live surface projections |

---

## 0. Purpose

RFC-SURFACE-CHANNEL-001 defines how `LiveSurfaceSpec` projections are **transported** to a surface runtime and **synchronized** over time. It is the final layer in the surface stack and the only layer that knows about WebView2, seed queues, XAML mutations, or any other runtime mechanism.

Transport is separated from semantics. A `SurfaceMessage` carries rendered Block IR — it does not carry `SurfaceExpression` nodes. The semantic objects have been fully compiled and projected before reaching this layer.

---

## 1. Channel Abstraction

### 1.1 ISurfaceChannel

```
ISurfaceChannel
  SurfaceId : string

  Task SendAsync(SurfaceMessage message, CancellationToken ct)
  event EventHandler<SurfaceMessage>? MessageReceived
```

`SurfaceId` identifies the runtime surface this channel connects to. A single host process may hold multiple channels to different surface instances simultaneously (e.g. one for the embedded ribosome window, one for the tiger overlay).

### 1.2 Registered Implementations

| TargetKind | Class | Transport |
|---|---|---|
| `"web"` | `SurfaceChannel` | WebView2 `PostWebMessageAsJson` / `WebMessageReceived` |
| `"ceh"` | `CehSeedChannel` | genesis-seed surface queue (SSE / polling) |
| `"mock"` | `MockSurfaceChannel` | In-memory, for tests |
| `"wpf"` | Future | XAML visual tree mutation |

---

## 2. SurfaceMessage Wire Format

```
SurfaceMessage
  Op      : string                — "patch" | "replace" | "clear"
  TargetId: string                — DOM element id (web) or surface region id (other)
  Blocks  : RenderedBlock[]?      — present for "patch" and "replace"
  Error   : string?               — present when the projector signals an error

RenderedBlock
  BlockId     : string
  ContentType : string            — "text/html" | "application/xaml+xml"
  Content     : string            — rendered content in ContentType format
```

### 2.1 Op Semantics

**`replace`**: Replace the entire content of `TargetId` with all `Blocks[].Content` joined in order. Use for initial projection and full resets.

**`patch`**: Merge `Blocks[]` into the existing surface by `BlockId`. If a block with the given `BlockId` exists, update its content in place. If it does not exist, append it. Order among new blocks is preserved. Use for incremental updates.

**`clear`**: Remove all children of `TargetId`. No `Blocks[]`.

### 2.2 Invariants

**INV-CHAN-01 (BlockId stability)**: A `RenderedBlock.BlockId` in a `patch` message must be the same `BlockId` used in the initial `replace` for the same logical block. BlockId mutation between projections causes visual duplication.

**INV-CHAN-02 (ContentType uniformity)**: All `RenderedBlock` objects in a single `SurfaceMessage` must have the same `ContentType`. Mixed content types within a message are invalid.

**INV-CHAN-03 (Op idempotency)**: A `replace` message applied twice to the same target produces the same result as applying it once. A `patch` message with a block already at current content is a no-op for that block.

---

## 3. Incremental Patching Protocol

The patching protocol minimizes data transferred on updates:

1. **Initial projection**: Send `Op="replace"` with all blocks.
2. **State update**: Compute which blocks changed (by content hash). Send `Op="patch"` with only the changed blocks.
3. **Full reset**: Send `Op="clear"` followed by `Op="replace"`.

The projector is responsible for computing the diff. The channel transmits what it is given; it does not diff.

---

## 4. JS Counterpart Protocol

The `surface-channel-client.js` in genesis-homebase implements the receiver side for the `"web"` transport:

```javascript
// WebView2 primary path
window.chrome.webview.addEventListener('message', handler)

// CEH/WWAHost polling fallback
setInterval(() => fetch('/api/surface/{surfaceId}'), pollIntervalMs)
```

Message handler semantics match the wire format in §2 exactly. The JS client:
1. Receives `SurfaceMessage` via the transport
2. Locates `TargetId` in the DOM
3. Applies the `Op` (replace / patch by BlockId / clear)
4. Optionally sends acknowledgement back via `window.chrome.webview.postMessage`

**Sync requirement**: `SurfaceMessage` JSON schema in C# (`Contracts.cs`) and JS (`surface-channel-client.js`) must be kept in sync. Any field addition in one requires a corresponding update in the other.

---

## 5. Capability Negotiation (reserved)

Capability negotiation — where the surface runtime advertises its supported `ContentType`s and `Op`s to the host — is reserved for a future normative revision. Current implementations assume a fixed capability set per `TargetKind`.

---

## 6. Heartbeat and Versioning (reserved)

Heartbeat (periodic keepalive from host → runtime) and protocol versioning (runtime rejects messages from incompatible host version) are reserved for future normative revision.

---

## 7. Implementation Reference

**Canonical C# implementations**:
- `Genesis.Windows.Ribosome/SurfaceChannel.cs` — WebView2 bridge
- `Genesis.Surface.Projectors/CehProjector.cs` — CEH/WWAHost seed path
- `Genesis.Surface.Projectors/WebProjector.cs` — Block→HTML renderer

**JS implementation**: `genesis-homebase/webapps/genesis/surface-channel-client.js`

**Package**: `Genesis.Surface.Projectors`, `Genesis.Windows.Ribosome`

Planned migration: channel abstractions (`ISurfaceChannel`, `SurfaceMessage`, `RenderedBlock`) will move to a new `Genesis.Surface.Channel` package. Currently co-located in `Genesis.Surface.Abstractions/Contracts.cs`.

---

*Brandon Clark / Genesis Systems 2026*
