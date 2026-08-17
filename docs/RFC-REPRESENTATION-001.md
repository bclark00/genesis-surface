# RFC-REPRESENTATION-001: Canonical Representation Substrate

| Field | Value |
|---|---|
| Status | NORMATIVE DRAFT v0.1.0 |
| Date | 2026-07-29 |
| Author | Brandon Clark / Genesis Systems |
| Parents | RFC-RIBOSOME-001, RFC-EXEC-001 |
| Children | RFC-REPRESENTATION-PLANNING-001, RFC-REPRESENTATION-LAYOUT-001, RFC-REPRESENTATION-PARTITION-001, RFC-REPRESENTATION-MATERIALIZATION-001, RFC-SURFACE-001, RFC-DOCUMENT-001 |
| Supersedes | RFC-PRESENTATION-001 |
| Implements | Canonical substrate for all governed transformations from semantic objects to observable artifacts |

---

## 0. The Law

> **A representation is not its realization.**

A semantic object may admit many valid representations.
A representation may admit many valid materializations.
A materialization may admit many valid encodings.
An encoding is merely one artifact.

This one statement provides the stable foundation for why so many apparently different subsystems in Genesis share the same pipeline: they are all governed transformations from semantics to observable artifacts, differing only in the representation domain they traverse.

---

## 1. The Rediscovered Pattern

The same five-step law appears independently across every Genesis subsystem:

| Subsystem | Step 1 | Step 2 | Step 3 | Step 4 | Step 5 |
|---|---|---|---|---|---|
| Hyperbase | Knowledge | Logical Object | Physical Plan | Materialization | Storage Artifact |
| Surface | Intent | SurfaceExpression | Live Projection | Materialization | Runtime Surface |
| Documents | Intent | Presentation Tree | Layout | Partition | PDF |
| SGIR | Semantic Object | Receipt | CMP | EMIT | — |
| Ribosome | Codon | Expression | Executable | — | — |

They are all structurally identical. RFC-REPRESENTATION-001 names that structure as a first-class platform law.

---

## 2. The Representation Substrate

### 2.1 The Universal Pipeline

```
Intent / Semantic Object
          ↓
RepresentationExpression    — what is expressed
          ↓
RepresentationPlan          — how it will be represented, which domain
          ↓
RepresentationSpec          — domain-specific structural IR
          ↓
[Layout Resolution]         — geometric domains only (RFC-REPRESENTATION-LAYOUT-001)
          ↓
[Spatial Partition]         — geometric domains only (RFC-REPRESENTATION-PARTITION-001)
          ↓
MaterializedRepresentation  — ready to cross CMP
          ↓
CMP Crossing                — the governed transition: planned → realized
          ↓
EMIT
          ↓
Artifact / Runtime Instance
```

Layout Resolution and Spatial Partition are conditional stages: they apply to geometric domains (presentation, spatial) but not to all domains (execution, serialization).

### 2.2 Representation Domains

The platform currently defines five representation domains. Each is an instantiation of the universal pipeline, not a separate architecture:

| Domain | What it produces | Existing subsystem |
|---|---|---|
| **Presentation** | Human-observable output | RFC-SURFACE-001, RFC-DOCUMENT-001, RFC-TERMINAL-001 |
| **Execution** | Machine-executable artifacts | RFC-RIBOSOME-001, RFC-EXEC-001 |
| **Storage** | Persistent data records | Hyperbase, BTF, VAP |
| **Serialization** | Transport-ready byte sequences | Wire formats, JSON, protobuf |
| **Communication** | Event streams and receipts | SGIR, channels |

Each domain defines its own `RepresentationSpec` subtype and materialization path. No domain has architectural priority over any other.

### 2.3 Hyperbase / Representation Duality

These are the two platform halves:

```
Hyperbase              — answers: What exists?
Representation         — answers: How does it intentionally become observable?
```

Together, they form the complete path from knowledge to intentional communication:

```
Intent
   ↓
Context Engine (selects from Hyperbase)
   ↓
Canonical Knowledge
   ↓
Representation Planner
   ↓
Canonical Representation
   ↓
CMP / EMIT
   ↓
Artifact
```

---

## 3. Core Types

### 3.1 RepresentationExpression

The universal semantic object. Independent of any representation domain. Does not know whether it will become HTML, a PDF, a compiled binary, a JSON payload, or a storage record.

```
RepresentationExpression
  ExpressionId  : string          — content-addressed
  Name          : string
  SourceKind    : string
  Nodes         : RepresentationNode[]
  Actions       : RepresentationAction[]
  Bindings      : RepresentationBinding[]
  Capabilities  : RepresentationCapability[]
  Version       : string
  CreatedAt     : DateTimeOffset?
```

**Primitive node types** — semantic, domain-independent:

| Node type | Meaning |
|---|---|
| `identity` | What the subject is |
| `title` | Name or heading of the thing |
| `status` | Lifecycle or health state |
| `metric` | A measured quantity |
| `instruction` | Directs an agent to do something |
| `action` | A triggerable operation |
| `table` | Structured relational content |
| `relationship` | A connection between things |
| `warning` | A condition requiring attention |
| `evidence` | Supporting material |
| `schema` | Structural definition of a type |
| `procedure` | A sequence of steps |

**Invariant**: `ExpressionId` is content-addressed over `(Name, SourceKind, sorted(Nodes), sorted(Actions))`. Same semantic intent produces the same ExpressionId.

### 3.2 RepresentationPlan

Decides the representation strategy: which domain, which family within that domain, which policy, which adaptation.

```
RepresentationPlan
  PlanId             : string
  ExpressionId       : string
  TargetDomain       : string    — "presentation" | "execution" | "storage" | "serialization" | "communication"
  TargetFamily       : string    — domain-specific family discriminant
  PolicyId           : string?
  AdaptationHints    : string[]
  CapabilityContext  : string[]
  PlannedAt          : DateTimeOffset
```

Planning splits into two sequential stages (RFC-REPRESENTATION-PLANNING-001):

- **Stage 1 (Representation Planning)**: What information survives? Which domain and family? Which policies?
- **Stage 2 (Presentation Planning)**: How should the chosen family be structured (table vs chart, compact vs expanded)? No geometry yet.

The planner makes choices including:

- What information survives vs. is suppressed
- Which representation family (table, chart, terminal, JSON, binary, PDF, interactive surface)
- Density (detailed vs. compact vs. summary)
- Applicable policy
- Adaptation (accessibility, mobile, dark mode, print)
- Capability level available at the target

### 3.3 RepresentationSpec

A domain-specific structural intermediate representation. Base type only — each domain defines concrete subtypes:

```
RepresentationSpec (base)
  SpecId        : string
  TargetDomain  : string
  TargetFamily  : string
  PlanId        : string
  GeneratedAt   : DateTimeOffset?
```

Domain subtypes:

| Domain | Concrete spec | Defined in |
|---|---|---|
| Presentation / Surface | `LiveSurfaceSpec` | RFC-LIVE-SURFACE-001 |
| Presentation / Document | `DocumentSpec` | RFC-DOCUMENT-001 |
| Presentation / Terminal | `TerminalSpec` | RFC-TERMINAL-001 |
| Execution | `ExecutableSpec` | RFC-RIBOSOME-001 |
| Storage | `StorageSpec` | Hyperbase / BTF |
| Serialization | `SerializationSpec` | (per-format) |
| Communication | `CommunicationSpec` | SGIR |

### 3.4 MaterializedRepresentation

Contains complete physical realization instructions. Ready to cross CMP. This is the object that crosses the CMP boundary — everything before it is planned, everything after is artifact.

```
MaterializedRepresentation
  MaterializationId    : string
  SpecId               : string
  Domain               : string
  ResourceBindings     : ResourceBinding[]
  PhysicalInstructions : PhysicalInstruction[]
  ReadyAt              : DateTimeOffset
```

---

## 4. CMP as the Governed Transition

CMP is the **governed transition from planned representation into realized representation**.

```
MaterializedRepresentation
        ↓
CMP admission (RFC-CORRIDOR-001)
        ↓
EMIT event
        ↓
Artifact or runtime realization
```

This definition applies equally to:
- PDFs
- Web UI patches
- Compiled code artifacts
- Persisted storage records
- Rendered XR scenes
- Emitted SGIR receipts

The abstraction is domain-independent.

---

## 5. Universal Emitter Contract

```csharp
public interface IMaterializationEmitter<
    in TMaterialized,
    TArtifact>
{
    string EmitterId { get; }

    ValueTask<EmissionResult<TArtifact>> EmitAsync(
        TMaterialized   materialized,
        EmissionContext context,
        CancellationToken cancellationToken = default);
}
```

The `in` variance annotation (contravariant in `TMaterialized`) means an emitter of a more general materialized type is substitutable where a more specific one is expected.

**Example emitter instances across all domains**:

| Emitter | Input | Output | Domain |
|---|---|---|---|
| `PdfEmitter` | `MaterializedPages` | PDF artifact | Presentation |
| `WebSurfaceEmitter` | `MaterializedSurface` | `SurfaceMessage` stream | Presentation |
| `AnsiEmitter` | `MaterializedTerminal` | ANSI byte stream | Presentation |
| `XrSceneEmitter` | `MaterializedScene` | XR scene graph | Presentation |
| `AssemblyEmitter` | `MaterializedExecutable` | Binary artifact | Execution |
| `BtfEmitter` | `MaterializedRecord` | BTF storage block | Storage |
| `JsonEmitter` | `MaterializedSchema` | JSON bytes | Serialization |
| `SgirEmitter` | `MaterializedReceipt` | SGIR receipt | Communication |

---

## 6. Resources as Governed Objects

Resources referenced in any representation are canonical object identities, not raw paths:

```
Representation Resource Reference
        ↓
Canonical Object Identity
        ↓
Publication / Version Selection
        ↓
Materialization Bytes
```

A `RepresentationReceipt` records both the canonical object identity and the exact byte/publication identity. This provides semantic governance and byte-level reproducibility across all domains.

The canonical resource reference type:

```csharp
public sealed record RepresentationResourceRef(
    ObjectId         ObjectId,        // canonical identity — not a path
    RepresentationId Representation,  // which representation of the object
    ResourceRole     Role);           // logo | font | template | style | watermark | ...
```

A URI such as `resource://brand/logo-primary` may serve as an alias, but is not the identity authority. The `ObjectId` is the identity authority. This permits lifecycle governance, dependency closure, archival reproduction, revocation, provenance, and emission evidence across all representation domains.

---

## 7. Representation Policy

Policy governs how semantic roles map to domain-specific representations. The stack applies to all domains, not just visual rendering:

```
Semantic Role
      ↓
Representation Policy
      ↓
Domain Token
      ↓
Resolved Representation
      ↓
Material Primitive
```

For presentation: SemanticRole `CriticalWarning` → policy → style token → color, weight, border.
For storage: SemanticRole `AuthoritativeRecord` → policy → retention class, index strategy, replication.
For serialization: SemanticRole `SensitiveField` → policy → encryption, masking, omission.

The same policy abstraction governs all representation decisions.

---

## 8. Identity Domains

Eight identity domains cover the full transformation chain:

| Domain | Question |
|---|---|
| Semantic Identity | What does the underlying object mean? |
| Representation Identity | What was intentionally selected for representation? |
| Plan Identity | Which representational decisions were made? |
| Layout Identity | Which layout rules and geometry policies apply? *(geometric domains only)* |
| Partition Identity | How is the resolved representation divided? *(geometric domains only)* |
| Materialization Identity | What is the complete realized form? |
| Emission Identity | What encoding and emitter configuration was used? |
| Artifact Identity | What are the resulting bytes or runtime instance? |

All applicable domains must be recorded in a `RepresentationReceipt` at CMP crossing. This makes reproducibility, governance, and audit mechanically provable.

A single semantic claim may legitimately yield:

```
1 Semantic Identity
N Representation Identities   (member-facing vs. auditor-facing)
M Plan Identities             (table, chart, accessible summary)
K Emission Identities         (PDF/A, HTML, DOCX, JSON)
```

None should be conflated.

---

## 9. Governing Invariants

**INV-REP-01 (Expression independence)**: `RepresentationExpression` contains no domain-specific types. It does not know whether it will become HTML, a compiled binary, a storage record, or a PDF.

**INV-REP-02 (Plan authority)**: The representation domain and family are selected in the `RepresentationPlan`, not in the `RepresentationExpression`.

**INV-REP-03 (The Law)**: An artifact is not the representation. `MaterializedRepresentation` is the representation; its encoding is one form of one artifact.

**INV-REP-04 (Identity chain completeness)**: All applicable identity domains must appear in a `RepresentationReceipt`. No domain may be conflated with another.

**INV-REP-05 (Resource governance)**: No resource may be referenced by raw path in a `MaterializedRepresentation`. All resources are resolved through canonical object identity.

**INV-REP-06 (Policy indirection)**: No semantic role maps directly to a domain primitive. All representation decisions go through the policy stack.

**INV-REP-07 (CMP boundary)**: `MaterializedRepresentation` is produced at the CMP boundary. Nothing before CMP is realized. Nothing after CMP is still planned.

---

## 10. RFC Family

```
RFC-REPRESENTATION-001 (this RFC)
         ↓
RFC-REPRESENTATION-PLANNING-001    Planning, policies, adaptation, RepresentationPlan
RFC-REPRESENTATION-LAYOUT-001      Geometry, measurement (geometric domains)
RFC-REPRESENTATION-PARTITION-001   Spatial partition into bounded regions
RFC-REPRESENTATION-MATERIALIZATION-001   CMP crossing, receipts, emitter contract
         ↓
         ├── RFC-SURFACE-001              Presentation / interactive domain
         ├── RFC-DOCUMENT-001             Presentation / durable linear domain
         ├── RFC-TERMINAL-001             Presentation / character-cell domain
         ├── RFC-XR-001                   Presentation / spatial domain
         └── [execution / storage / serialization / communication domain RFCs]
```

## 11. Initial Implementation Boundary

The first conformance implementation should remain deliberately narrow. The EOB (Explanation of Benefits) is the canonical proof case because it exercises the broadest range of representation concerns:

- Semantic tables with anchored content
- Repeating headers at partition boundaries
- Continuation policies (widow/orphan, carry totals)
- Currency formatting and numeric precision
- Fixed and measured pagination (PagePartitioner)
- Multiple emitters from a single MaterializedRepresentation (PDF, HTML)
- Deterministic identity and byte-level reproducibility
- CMP/EMIT adapter integration

**Initial boundary**:
- Semantic representation tree
- Representation and presentation planning
- Document presentation profile
- Fixed and measured page partitioners
- Materialized page primitives
- JSON inspection emitter (for testing)
- MigraDoc-backed PDF emitter
- HTML emitter
- Identity and conformance suite
- Corridor/EMIT adapter

Everything beyond this boundary is valid in future iterations but must not be required to reach v1.0.0 conformance.

---

*Brandon Clark / Genesis Systems 2026*
