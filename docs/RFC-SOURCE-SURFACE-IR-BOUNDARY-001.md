# RFC-SOURCE-SURFACE-IR-BOUNDARY-001: Source IR / Surface IR Layer Boundary

**Status:** NORMATIVE DRAFT  
**Version:** 0.1.0  
**Date:** 2026-08-02  
**Authority:** Brandon Clark / Genesis Systems  

---

## Abstract

This RFC establishes the exact boundary between Source IR and Surface IR,
resolves the naming overlap between structural and presentation `ProjectionReceipt`
types, and states the invariant that Quartz is downstream of structural deltas,
not Surface IR deltas.

---

## Two orthogonal pipelines

### Source IR pipeline

Extracts and indexes canonical structure from source code:

```
SourceStructureBundle
    ↓
SourceIrCanonicalizer
    ↓
CanonicalBundle (identifier-neutral)
    ↓
SNOD / NIDX (canonical byte stream, GraphHash)
    ↓
TreeRePair / Terms (grammar compression)
    ↓
Canonical Object Graph (COG) + GraphHash
    ↓
node-store generation (CAS, BTF B-tree)
    ↓
Quartz (QSDL structural delta)
```

**Output authority:** `GraphHash`, `StructuralProjectionId`, `ProjectionCommitId`,
`node_generation_t`, structural delta (`nr_occurrence_t` removed/added arrays).

**Quartz receives:** binary QSDL containing `CompletionReceiptId`, `MutationId`,
`root_neuron_id`, `StructuralDeltaId`, `installed_generation`, and occurrence arrays.
Quartz MUST NOT be activated from Surface IR events.

### Surface IR pipeline

Projects semantic data onto live UI surfaces:

```
SurfaceExpression (schema-backed, ExpressionId content-addressed)
    ↓
SurfaceSpec / Block IR (TextBlock, MetricBlock, StatusBlock, ContainerBlock, LogBlock)
    ↓
ISurfaceProjector
    ├── WebProjector  → HTML fragments → WebView2 DOM (PostWebMessageAsJson)
    ├── CehProjector  → seed surface queue → homebase polling client
    └── WpfProjector  → XAML fragment → WPF visual tree (future)
    ↓
ISurfaceChannel.SendAsync(SurfaceMessage)
    ↓
ProjectionReceipt (surface audit — SpecId, ProjectorKind, SurfaceId, timestamp)
```

**Output authority:** `SurfaceMessage` (patch/replace/clear operations on DOM element IDs),
`ProjectionReceipt` (surface audit receipt).

---

## Naming collision

Both pipelines use the word "projection" and "receipt":

| Term | Source IR context | Surface IR context |
|------|-------------------|--------------------|
| `ProjectionHash` | `SHA256(domain || GraphHash || version || profile || node_refs)` in Gate D — corpus-level projection of a canonical object graph into a machine-readable format | — |
| `StructuralProjectionId` | Deterministic consequence of a source mutation on the node index | — |
| `ProjectionCommitId` | Particular admitted installation of a structural projection | — |
| `sr_completion_receipt_t` / `SrCompletionReceipt` | Binary SCRC: binds MutationId, ReceiptId, source/projection WAL hashes, structural delta | — |
| `ProjectionReceipt` | — | Surface audit: SpecId, ProjectorKind, SurfaceId, BlockCount, timestamp |
| `SurfaceIntent` | — | Governed operation moving a spec or delta across the channel |

These are distinct types at different layers. No cross-assignment is valid.

---

## Gate D projection boundary

`cog_project_json()` (Gate D) produces:

```
SourceGraphHash → ProjectionHash → ArtifactHash
```

This is a **corpus-level** projection: it takes a `canonical_object_graph_t` and
produces a machine-readable JSON serialization for corpus indexing, Hyperbase
ingest, and retrieval. It is NOT a Surface IR operation.

The JSON produced by `cog_project_json()` is consumed by corpus readers,
not by UI surface projectors.

---

## Identity anchoring (open obligation)

`SurfaceSpec.SpecId` is documented as content-addressed ("same blocks +
targetSurfaceId = same spec id") but the provided source contains no SHA-256
derivation at the construction site — callers are responsible.

`SurfaceExpression.ExpressionId` IS content-addressed:
`"surface:{name}:{SHA256(name|sourceKind|action_signatures)[..16]}"`.

The open obligation: `SurfaceSpec.SpecId` should be derived as:

```
SpecId = SHA256("surface.spec.v1\0" || targetSurfaceId || canonical_block_content)
```

This would allow tracing which surface presentation reflects which semantic state.

**Future binding (not required for this release):** If a `SurfaceSpec` is anchored to
a specific `GraphHash`, a surface presentation can declare which version of the
canonical source graph it reflects. This binding is optional and belongs in the
caller, not in `Genesis.Surface.Abstractions`.

---

## What Quartz does and does not receive

| Event | Quartz receives? |
|-------|-----------------|
| Source mutation committed to node-store | YES — binary QSDL with structural delta |
| Surface spec projected to WebView2 | NO |
| Surface spec projected via CEH | NO |
| Gate D JSON projection produced | NO |
| Hyperbase node created | NO (Quartz reads Hyperbase asynchronously via its own path) |

The invariant: **Quartz consumes only committed structural occurrence deltas.**
It does not activate on presentation events.

---

## Live Surface and presentation projection scope

Surface IR (`Genesis.Surface.*`) is a separate concern from structural replication.
It is not part of this structural replication release. Surface IR belongs in the
Morrigan UI rewrite delivery arc (V2.2.0-RENDERER-REWRITE-001).

The packages that DO belong in the structural replication release:

```
Source IR          ExeRay.Core.SourceIR           INCLUDED
Structural Codons  RFC-SOURCE-STRUCTURAL-CODON-001 INCLUDED (spec)
Canonical Graph    canonical_graph.h/c/cs          INCLUDED
node-store gen     node_store.h/c                  INCLUDED
Structural repl    structural_replication.h/c/cs   INCLUDED
Quartz handoff     QSDL v1                         INCLUDED
```

The packages that do NOT belong in the structural replication release:

```
Surface IR         Genesis.Surface.*               OUT OF SCOPE
SGIR               Genesis.Sgir.*                  OUT OF SCOPE
Document substrate Genesis.Documents.*             OUT OF SCOPE (spec committed)
```

---

*Brandon Clark / Genesis Systems 2026*

---

## Document IR

`Genesis.Documents.Core` is a third, distinct presentation layer — orthogonal to both Source IR and Surface IR.

### Position

```
Corpus query result / semantic analysis output
    ↓
DocumentBuilder → DocumentNode (semantic, identity-stable)
    ↓
IDocumentEmitter
    ├── HtmlEmitter  → HTML (durable, paginated)
    ├── JsonDocumentEmitter → JSON
    └── PdfEmitter   → PDF (archivable)
```

Document IR sits **downstream of corpus retrieval**, not upstream. It renders the results of Hyperbase queries, activation field outputs, and analysis into durable, archivable, paginated forms. It has no dependency on Source IR, Surface IR, or the structural replication chain.

### Distinction from Surface IR

| | Document IR | Surface IR |
|-|------------|------------|
| Character | Durable, linear, paginated | Live, mutable, real-time |
| Delivery | PDF / DOCX / HTML file | DOM patch via WebView2 / CEH |
| Identity | `DocumentIdentity.Derive(kind + metadata)` — stable envelope | `SurfaceSpec.SpecId` (caller convention, not yet enforced) |
| Audit | — | `ProjectionReceipt` (surface audit) |
| Revision | Semantic date (`DocumentMetadata.SemanticDate`) | Long revision counter, ordering enforced by `SurfaceRuntime` |

### Identity derivation — known gap

`DocumentIdentity.Derive()` hashes over `kind + title + subject + language + semanticDate + childCount`.

This establishes the document's **semantic envelope** identity — not its body content. Two documents with identical metadata but different body text receive the same identity. This is either intentional (identity is the semantic claim, not the instantiation) or a gap.

The RFC-DOCUMENT-001 principle — "a document is not its PDF; a document is a semantic object" — supports the envelope interpretation. However the derivation inputs and their justification should be made explicit in RFC-DOCUMENT-001 §12 (identity domains).

`DocumentBuilder.Build()` now correctly calls `Derive()` rather than `Guid.NewGuid()` (fixed). `NodeId.New()` (Guid) is retained for the structural node `Id` — it is an occurrence coordinate, not a semantic identity.

### Scope

Document IR (`Genesis.Documents.*`) is fully committed and referenced by RFC-DOCUMENT-001. It is out of scope for the structural replication release in the same way Surface IR is — it is a downstream consumer of corpus output, not a component of the structural indexing chain.
