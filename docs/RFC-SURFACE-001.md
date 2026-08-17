# RFC-SURFACE-001: Surface Semantic Model

| Field | Value |
|---|---|
| Status | NORMATIVE DRAFT v0.1.0 |
| Date | 2026-07-29 |
| Author | Brandon Clark / Genesis Systems |
| Parents | RFC-REPRESENTATION-001 |
| Children | RFC-SURFACE-PROJECTION-001 |
| Implements | Canonical semantic model for UI expressions |

---

## 0. Position in the Translation Architecture

Genesis implements a platform-wide translation pattern that appears consistently across subsystems:

```
Canonical semantic object
        ↓
Translator
        ↓
Target-specific IR
        ↓
Materializer
        ↓
Runtime artifact
```

Instances of this pattern across Genesis:

| Semantic Object | Translator | Target IR | Runtime Artifact |
|---|---|---|---|
| Genome (codons) | Ribosome | PhenotypeSpec | Executable code |
| Context | Hyperbase planner | RetrievalPlan | Hydrated context |
| Surface expression | SurfaceExpressionCompiler | LiveSurfaceSpec / DocumentSpec | UI / document |

RFC-SURFACE-001 defines the **semantic object layer** for UI expressions. It knows nothing about HTML, WPF, WebView2, channels, or projection formats. It is the AST.

The dependency chain:

```
RFC-EXEC-001
      ↓
RFC-RIBOSOME-001   ← universal semantic → executable translator
      ↓
RFC-SURFACE-001    ← UI semantic model (this RFC)
      ↓
RFC-SURFACE-PROJECTION-001
    ┌───┴───┐
    ↓       ↓
RFC-LIVE-SURFACE-001   RFC-DOCUMENT-001
    └───┬───┘
        ↓
RFC-SURFACE-CHANNEL-001
```

---

## 1. Surface Semantic Model

A **SurfaceExpression** is the canonical representation of a UI intent before any projection decision has been made. It is renderer-neutral, transport-neutral, and format-neutral.

### 1.1 SurfaceExpression

```
SurfaceExpression
  ExpressionId : string          — content-addressed, stable across compilations
  Name         : string          — human-readable identifier
  SourceKind   : string          — "schema_backed" | "generated" | "authored"
  Root         : SurfaceNode     — the expression tree root
  Actions      : SurfaceAction[] — declared executable surface actions
  Bindings     : SurfaceBinding[]— declared data bindings
  RequiredCapabilities : SurfaceCapability[]
  Version      : string          — semver
  CreatedAt    : DateTimeOffset?
```

**Invariant**: ExpressionId is content-addressed over `(Name, SourceKind, Root, Actions, Bindings, Version)`. Same inputs always produce the same ExpressionId. Mutation of any field produces a new expression.

### 1.2 SurfaceNode

The node tree is a discriminated union:

```
SurfaceNode (abstract)
  NodeId   : string           — stable within the expression
  Role     : string           — semantic role, not a render hint
  Label    : string?
  Binding  : string?          — path into data binding scope
  ActionId : string?          — reference to SurfaceAction
  Children : SurfaceNode[]?

  SurfaceContainerNode   role="container"  Layout : string
  SurfaceTextNode        role="text"        Text : string
  SurfaceInputNode       role="input"       ValueType, Binding, Required
  SurfaceActionNode      role="action"      ActionId : string (required), Style
```

**Invariant**: NodeId is stable within an expression but not globally. The (ExpressionId, NodeId) pair uniquely identifies a node across the system.

**Role semantics**: Role describes *what the node is* semantically, not how it renders. `"input"` means "a place where the user provides a value." The projection layer decides whether that becomes an `<input>`, a XAML `TextBox`, or a spoken prompt.

### 1.3 SurfaceAction

Declares an executable operation that can be triggered from the surface:

```
SurfaceAction
  Id          : string
  Label       : string
  Description : string
  ReturnType  : string
  Parameters  : SurfaceParameter[]
  Capability  : string?   — capability required to invoke, or null = no gate
  MutatesData : bool      — true = action has side effects on data bindings
```

### 1.4 SurfaceParameter

```
SurfaceParameter
  Name         : string
  Type         : string     — primitive or qualified type name
  Description  : string
  Required     : bool
  DefaultValue : string?
```

### 1.5 SurfaceBinding

Declares a data path the expression reads from or writes to:

```
SurfaceBinding
  Path   : string         — dot-path into data scope (e.g. "session.uptime")
  Mode   : string         — "twoWay" | "oneWay" | "oneWayToSource"
  Format : string?        — format hint for rendering (e.g. "hh:mm:ss", "N2")
```

### 1.6 SurfaceCapability

Declares a host capability the expression requires:

```
SurfaceCapability
  Name             : string
  Reason           : string
  RequiresApproval : bool
```

### 1.7 SurfaceRole, SurfaceIdentity (reserved)

`SurfaceRole` and `SurfaceIdentity` are reserved for future normative definition. They will govern multi-user surface scenarios where the same expression is projected differently based on the viewer's role or identity.

---

## 2. What RFC-SURFACE-001 Does Not Define

The following are explicitly **not** defined here and belong to downstream RFCs:

| Concept | Belongs in |
|---|---|
| Block IR (TextBlock, MetricBlock, etc.) | RFC-LIVE-SURFACE-001 |
| SurfaceSpec / LiveSurfaceSpec | RFC-LIVE-SURFACE-001 |
| DocumentSpec, Paragraph, Heading, etc. | RFC-DOCUMENT-001 |
| ISurfaceProjector, ProjectionReceipt | RFC-SURFACE-PROJECTION-001 |
| ISurfaceChannel, SurfaceMessage | RFC-SURFACE-CHANNEL-001 |
| WebProjector, CehProjector, WpfProjector | RFC-SURFACE-CHANNEL-001 |
| SurfaceExpressionCompiler | RFC-SURFACE-PROJECTION-001 |

---

## 3. Governing Invariants

**INV-SURF-01 (Content addressing)**: ExpressionId is always content-addressed. Two SurfaceExpressions with identical semantic content are the same expression.

**INV-SURF-02 (Renderer neutrality)**: No field in the SurfaceNode tree carries a rendering hint. `Layout="column"` on `SurfaceContainerNode` is a *structural* hint (items are ordered), not a render hint (not "use CSS flexbox column"). The projection layer maps structural layout to render-specific implementations.

**INV-SURF-03 (Action declaration)**: Actions are declared, not executed. A SurfaceExpression does not invoke anything. Invocation is the responsibility of the surface runtime after the expression has been projected and the action triggered by the user.

**INV-SURF-04 (Binding declaration)**: Bindings are paths, not values. The semantic model records *where* data flows, not *what* the data is at projection time. Data injection is the responsibility of the projection compiler (RFC-SURFACE-PROJECTION-001).

---

## 4. Implementation Reference

**Canonical C# implementation**: `Genesis.Surface.Abstractions/ExpressionContracts.cs`

The implementation is complete and conformance-verified as of 2026-07-19. All types named in §1 are present with the stated fields.

**Package**: `Genesis.Surface.Abstractions` (genesis-monorepo `cs/src/Genesis.Surface.Abstractions/`)

---

## 5. EMIT Alignment

SurfaceExpression maps to EMIT dimensions as follows:

| SurfaceNode role | Dominant EMIT | Reason |
|---|---|---|
| `text` | E (Observe) | Surfaces an observation — something exists or is named |
| `input` | E (Observe) | A data-entry slot observes user intent |
| `action` | T (Transition) | Triggers a state transition |
| `container` | I (Integrate) | Integrates child nodes into a composition |

The dominant EMIT character of an expression is derived from its node frequency distribution by the projection compiler (§ RFC-SURFACE-PROJECTION-001).

---

*Brandon Clark / Genesis Systems 2026*
