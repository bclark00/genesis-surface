# RFC-PRESENTATION-SUBSTRATE-001

## Canonical Presentation Composition, Materialization, and Emission

**Status:** NORMATIVE DRAFT v0.2.0
**Version:** 0.2.0
**Date:** 2026-07-29
**Author:** Brandon Clark / Genesis Systems
**Category:** Presentation Substrate
**Parents:** RFC-REPRESENTATION-001
**Depends on:** RFC-CMP-001, RFC-CORRIDOR-001, RFC-OBJECT-LIFECYCLE-001
**Supersedes:** RFC-DOCUMENT-COMPOSITION-001 v0.1.0, RFC-PRESENTATION-001 (SUPERSEDED)
**Initial Profile:** Canonical Document Composition
**Reference Implementation:** Legacy EOB document generator, circa 2016

---

## Abstract

This RFC defines the canonical presentation substrate — the Presentation domain specialization of RFC-REPRESENTATION-001 — for constructing semantic presentations independently of their final physical representation or output encoding.

A presentation is an immutable tree or graph of semantic nodes. Presentation policy, constraint resolution, geometry resolution, partitioning, materialization, and format-specific emission are separate deterministic stages. Documents are the first normative profile. PDF, HTML, DOCX, Markdown, plain text, SVG, and terminal output are emitters over a shared semantic model, not independent definitions of the presented content.

The governing principle:

> **A presentation is not its encoded output.**
> An encoded output is one materialization of one semantic presentation under declared policies, resources, geometry, and emitter semantics.

The platform-wide generalization (RFC-REPRESENTATION-001):

> **A representation is not its realization.**

---

# 1. Status and Scope

## 1.1 Purpose

This RFC establishes the constitutional boundary between:

```
meaning
presentation policy
resolved geometry
materialized representation
encoded artifact
```

It is the Presentation domain profile of RFC-REPRESENTATION-001. It defines the stable model beneath document composition, surface projection, terminal output, and future presentation families.

This RFC does not freeze a particular UI framework, renderer, document library, page-description language, graphics API, or binary format.

## 1.2 Normative Language

**MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**, **SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **MAY**, and **OPTIONAL** are normative.

## 1.3 Constitutional Statement

```
Semantic composition owns presented meaning.
Presentation policy owns declarative presentation intent.
Constraint resolution owns satisfaction of presentation requirements.
Geometry resolution owns concrete spatial realization.
Partitioning owns division into pages, viewports, frames, or regions.
Materialization owns the complete emitter-neutral realized artifact.
Emitters own target encoding.
Publication owns visibility and distribution.
No later stage may silently redefine the semantic meaning established by an earlier stage.
```

## 1.4 Initial Profile and v1 Scope

The first normative profile is **Canonical Document Composition** — static, snapshot-bound presentations with deterministic layout, pagination, and multi-format emission.

The following are **non-normative future extensions** and MUST NOT be required for v1.0.0 conformance:

```
Dynamic UI surfaces            (requires temporal presentation model)
Interactive dashboards         (requires interaction policy + state)
XR / spatial scenes            (requires 3D geometry substrate)
Animation and transitions      (requires frame/epoch model)
Real-time streaming surfaces   (requires incremental patch protocol)
```

These extension targets are acknowledged and their eventual profiles will slot beneath this RFC as children, but they impose no v1 obligations.

---

# 2. Motivation

Traditional report generators conflate: business data; semantic content; layout construction; page breaking; drawing; serialization; storage. This produces systems difficult to test, reproduce, inspect, or render into alternate formats.

The reference EOB implementation already implicitly separates composition from emission (`CreateDocument → DefineStyles → FillContent → RenderDocument → Save`). This RFC formalizes that implicit architecture and generalizes it into a reusable platform substrate.

---

# 3. Architectural Model

## 3.1 Canonical Pipeline

```
Source Snapshot
    ↓
Semantic Composition
    ↓
Presentation Intent
    ↓
Presentation Planning
    ↓
Presentation Plan
    ↓
Constraint Resolution
    ↓
Resolved Constraints
    ↓
Geometry Resolution
    ↓
Geometric Presentation
    ↓
Spatial Partitioning
    ↓
Partitioned Presentation
    ↓
CMP.[T]
    ↓
Materialized Presentation
    ↓
Emission
    ↓
Encoded Artifact
    ↓
Publication
```

Each stage produces an artifact in a distinct identity domain (§14).

## 3.2 Stage Authority

```
Source Snapshot          owns observed application-domain state
Semantic Composition     owns presented meaning and authored structure
Presentation Intent      owns the purposive framing of the composition
Presentation Planning    owns representation strategy selection
Constraint Resolution    owns satisfaction of presentation requirements
Geometry Resolution      owns concrete dimensions and positions
Partitioning             owns division into pages, frames, viewports
Materialization          owns the complete emitter-neutral realization
Emission                 owns target-format encoding
Publication              owns channel-scoped visibility
```

## 3.3 Prohibited Stage Escalation

A later stage MUST NOT silently assume authority belonging to an earlier stage.

Examples:
- An emitter must not calculate business totals
- A paginator must not remove semantically required content
- A geometry resolver must not reinterpret a semantic role
- A planner must not invent semantic content

---

# 4. Source Snapshot

## 4.1 Source Objects Are Not Presentation Nodes

Source objects (employee, claim, invoice, policy, audit event) are not presentation nodes. A composer consumes a bounded source snapshot and produces a semantic presentation.

## 4.2 Snapshot Requirement

Composition MUST operate against a bounded, immutable source snapshot. Composition MUST NOT depend on undeclared mutable application state.

## 4.3 Derived Values

Business calculations MUST be completed before emission. Currency calculations MUST NOT require binary floating-point conversion.

---

# 5. Semantic Composition

## 5.1 Semantic Presentation

Semantic composition describes what is being presented, not where it appears.

```
Presentation(role: ExplanationOfBenefits)
├── Header
│   ├── Image(role: OrganizationLogo)
│   └── Heading(role: DocumentTitle)
├── Address(role: RecipientAddress)
├── Table(role: PrescriptionClaims)
│   ├── TableHeader
│   └── TableBody
│       └── TableRow(role: Claim)
└── Footer(role: LegalDisclaimer)
```

The semantic presentation MUST NOT contain concrete physical coordinates except where an explicit anchored-placement semantic is required by the profile.

## 5.2 Semantic Node

```csharp
public abstract record PresentationNode
{
    public required NodeId   Id       { get; init; }
    public required NodeKind Kind     { get; init; }
    public string?           Role     { get; init; }
    public StyleRef?         Style    { get; init; }
    public IReadOnlyDictionary<string, ScalarValue> Attributes { get; init; } = new Dictionary<string, ScalarValue>();
    public IReadOnlyList<PresentationNode> Children { get; init; } = Array.Empty<PresentationNode>();
}
```

## 5.3 Core Node Classes

`Presentation`, `Section`, `Container`, `Paragraph`, `TextRun`, `Heading`, `Address`, `Image`, `Table`, `TableHeader`, `TableBody`, `TableRow`, `TableCell`, `List`, `ListItem`, `Note`, `Header`, `Footer`, `Link`, `Reference`, `Conditional`, `Repeater`, `Anchor`, `BreakHint`, `AccessibilityAnnotation`.

Domain-specific meaning SHOULD be expressed through semantic roles before introducing new physical primitives.

## 5.4 Authored vs Derived Nodes

Every node MUST declare whether it is **Authored** (from semantic composition) or **Derived** (introduced by a later stage). Derived nodes (repeated table header, continuation notice, page number) MUST retain lineage to the authored node or policy that caused them.

## 5.5 Text

Text MUST be Unicode scalar content. Semantic text MUST NOT be reconstructed from rendered glyph positions.

## 5.6 Tables

```csharp
public sealed record TableNode : PresentationNode
{
    public required IReadOnlyList<ColumnDefinition> Columns { get; init; }
    public required IReadOnlyList<TableRowNode>     Rows    { get; init; }
    public bool RepeatHeader { get; init; }
}
```

Merged cells MUST be explicit through row-span and column-span values.

---

# 6. Presentation Policy and Planning

## 6.1 Policy and Meaning Are Separate

Semantic role expresses meaning. Presentation policy expresses how that meaning should be presented. Changing a presentation policy MUST NOT alter semantic presentation identity unless the identity scheme explicitly incorporates presentation intent.

## 6.2 IPresentationPlanner

The planner receives an already-composed `SemanticPresentation`, not a raw source object. This preserves the snapshot boundary and enforces the authority split: the composer determines semantic membership; the planner determines representation strategy.

```csharp
public interface IPresentationPlanner
{
    PresentationPlan Plan(
        SemanticPresentation   presentation,    // NOT SemanticObject — composition is already done
        PresentationIntent     intent,
        PresentationPolicySet  policies,
        PresentationEnvironment environment);
}
```

Authority split:

```
Composer   — determines semantic membership and authored structure
Planner    — selects representation strategy and presentation structure
Layout     — resolves constraints and geometry
Partitioner— divides resolved presentation space
Materializer — produces the complete realized object
Emitter    — encodes it
```

The planner owns choices such as: render as chart or table; compact vs expanded; suppress empty sections; mobile vs print structure; repeat headers; accessibility structure selection.

The planner MUST NOT resolve coordinates or encode output bytes.

## 6.3 Presentation Profile

```csharp
public sealed record PresentationProfile
{
    public required PresentationProfileId Id          { get; init; }
    public required LayoutProfile         Layout      { get; init; }
    public required PartitioningPolicy    Partitioning{ get; init; }
    public required AccessibilityPolicy   Accessibility{ get; init; }
    public required LocalizationPolicy    Localization { get; init; }
    public required ResourcePolicy        Resources    { get; init; }
}
```

Examples: `US-Letter-Portrait`, `A4-Portrait`, `HTML-Responsive`, `Terminal-120x40`, `Thermal-Receipt-80mm`.

---

# 7. Styles

## 7.1 Style Resolution Stack

```
Semantic Role
    ↓
Presentation Policy
    ↓
Style Token
    ↓
Resolved Style
    ↓
Physical Primitive (glyph runs, lines, fills, coordinates)
```

Example:

```
Semantic Role:    TotalsRow
Policy:           Emphasize financial summary; keep with table; preserve accessibility grouping
Style Token:      SummaryTableTotal
Resolved Style:   bold, top border, currency alignment, spacing
Primitive:        glyph runs, line segments, fills
```

## 7.2 Style Definition

```csharp
public sealed record StyleDefinition(
    StyleId   Id,
    StyleId?  BasedOn,
    TextStyle Text,
    ParagraphStyle Paragraph,
    BorderStyle Border,
    FillStyle Fill,
    AccessibilityStyle Accessibility);
```

Style inheritance MUST be fully resolved before geometry resolution. The effective style set MUST be explicitly identified and versioned.

---

# 8. Constraint Resolution

Layout is a system of declarative constraints, not initially concrete coordinates.

```csharp
ResolvedPresentation Resolve(
    SemanticPresentation presentation,
    PresentationProfile  profile,
    ResolvedStyleSheet   styles,
    ResourceMetrics      resources)
```

Constraint resolution MUST NOT query mutable source-domain state. Unsatisfiable required constraints MUST produce a resolution failure, explicit degradation, alternate-profile selection, or overflow artifact — not silent violation.

---

# 9. Geometry Resolution

Geometry transforms declarative constraints into concrete spatial realization: resolved width, height, position, baseline, line boxes, cell geometry, drawing primitives, clipping regions, z-order.

Geometry resolution MUST NOT change semantic values, calculate business data, remove required content, or invent undeclared semantic nodes. Geometry identity MUST bind the metric sources that affect it, including font metric identity and shaping-engine version.

---

# 10. Partitioning

## 10.1 General Partitioning Model

Partitioning divides resolved geometry into presentation units (pages, viewports, slides, columns, frames, screen regions, terminal segments, print sheets). Pagination is the document-profile specialization of partitioning.

```csharp
PartitionedPresentation Partition(
    GeometricPresentation presentation,
    PartitioningPolicy    policy)
```

The partitioner MUST NOT query mutable application state.

## 10.2 Fixed vs Measured Partitioning

**Fixed** — explicit capacity rules (15 claims on first page, 20 on continuation). Valid and useful for regulated forms.

**Measured** — derives boundaries from resolved geometry. Depends on deterministic metrics.

## 10.3 Continuation Semantics

```csharp
public sealed record ContinuationPolicy
{
    public string? EndOfPartitionContent { get; init; }
    public bool    RepeatTableHeader     { get; init; }
    public bool    CarrySubtotal         { get; init; }
    public bool    PreserveSemanticReadingOrder { get; init; } = true;
}
```

---

# 11. Resources

## 11.1 Resource Identity

Resources (images, fonts, templates, style sheets, icons) MUST be addressed through stable resource identity, not environment-relative paths.

```csharp
public sealed record PresentationResourceRef(
    ObjectId         ObjectId,          // canonical identity — not a path
    RepresentationId RepresentationId,  // which representation of the object
    ResourceRole     Role,
    ResourceRequirement Requirement);   // required | optional | replaceable | metric-bearing | embeddable
```

`resource://brand/ww-wood-products/logo-primary` may serve as an alias; `ObjectId` is the identity authority. A missing logo and a missing required font do not have equivalent consequences — `ResourceRequirement` makes this explicit.

## 11.2 Resolver Boundary

```csharp
public interface IResourceResolver
{
    ValueTask<ResolvedResource> ResolveAsync(
        ResourceId              id,
        ResourceResolutionContext context,
        CancellationToken       cancellationToken = default);
}
```

---

# 12. Materialization

A materialized presentation contains all emitter-neutral information for deterministic target emission: partition boundaries, resolved geometry, resolved styles, text runs, image references, table geometry, derived nodes, drawing primitives, accessibility structure, resource commitments, degradation records.

**CMP.[T]** is the materialization crossing — the point at which the planned presentation becomes a realized presentation-domain object.

**MaterializedPresentation** is the realized object produced by that crossing.

**EMIT event** is the observation that an artifact was emitted.

**EMIT.T** is durable evidence of that emission event when required for audit.

These are three types of one event, not three names for one thing. One `MaterializedPresentation` may be emitted repeatedly (PDF, HTML, DOCX, print) without changing. Each emission produces a distinct `EmissionIdentity`.

---

# 13. Emitters

## 13.1 Constitutional Emitter Contract

```csharp
public interface IPresentationEmitter
{
    EmitterDescriptor Descriptor { get; }

    ValueTask<EmissionResult> EmitAsync(
        MaterializedPresentation presentation,
        Stream                   destination,
        EmissionOptions          options,
        CancellationToken        cancellationToken = default);
}
```

All emitters consume the same `MaterializedPresentation` contract. Profiles may extend `MaterializedPresentation`, but the constitutional interface remains non-generic. This prevents PDF, HTML, and terminal emitters from each requiring differently interpreted materialized inputs — which would recreate the coupling this RFC eliminates.

The `in TMaterialized` generic variant (`IMaterializationEmitter<TMaterialized, TArtifact>` from RFC-REPRESENTATION-MATERIALIZATION-001) is valid at the representation-substrate level for cross-domain emitters, but within the presentation substrate the constitutional type is `IPresentationEmitter`.

## 13.2 Emitter Responsibilities

Emitters own: target-format encoding; target metadata; resource embedding; compression; target-specific accessibility structures; format validation; deterministic byte production where supported.

## 13.3 Emitter Prohibitions

Emitters MUST NOT: query source databases; calculate business totals; select business records; mutate semantic values; introduce undeclared content; silently drop unsupported content.

Unsupported features MUST produce an error, recorded warning, or explicitly configured degradation.

## 13.4 Registered Emitters

`PdfEmitter`, `HtmlEmitter`, `DocxEmitter`, `MarkdownEmitter`, `PlainTextEmitter`, `SvgEmitter`, `TerminalEmitter`, `EmailEmitter`, `JsonInspectionEmitter`.

The `JsonInspectionEmitter` SHOULD be implemented first for conformance, debugging, and golden-vector generation.

---

# 14. Identity Domains

Nine distinct identity domains:

| Domain | What it identifies |
|---|---|
| `SourceSnapshotIdentity` | The bounded source state used for composition |
| `SemanticPresentationIdentity` | Semantic node kinds, values, canonical ordering, roles |
| `PresentationIntentIdentity` | The purposive framing and audience selection |
| `PresentationPlanIdentity` | Representation strategy, selected policies, adaptation choices |
| `ResolvedGeometryIdentity` | Concrete dimensions and positions under declared metrics |
| `PartitionIdentity` | Page/viewport/frame boundaries and derived-node commitments |
| `MaterializationIdentity` | Complete emitter-neutral realization including resource commitments |
| `EmissionIdentity` | Emitter identity, options, embedded resource identities |
| `PublicationIdentity` | Channel-scoped visibility (NOT the same as emission) |

Identical semantic presentation + different page size → different `ResolvedGeometryIdentity`, same `SemanticPresentationIdentity`.

Identical materialization + different emitter → different `EmissionIdentity`.

Identical emission + different publication channel → different `PublicationIdentity`.

`SemanticPresentationIdentity` excludes: output path; generation timestamp (unless semantically declared); renderer process ID; temporary filenames; UI state.

---

# 15. Equivalence

The substrate distinguishes:

```
semantic equivalence
presentation-policy equivalence
geometric equivalence
partition equivalence
visual equivalence
structural equivalence
byte equivalence
```

These MUST NOT be treated as interchangeable. Every conformance claim MUST identify the equivalence class being asserted.

---

# 16. Validation

Validation occurs at each stage:

**Semantic**: required fields, table structure, footnote references, totals match rows.

**Policy**: style references resolve, required policies exist, profile supports node kinds.

**Constraint**: internally consistent, required bounds satisfiable, keep-together rules legal.

**Geometry**: frames within declared regions, required content not clipped, dimensions finite.

**Partition**: repeated headers fit, continuation rules satisfied, no required node lost.

**Materialization**: every font/image resolves, no unresolved styles, resource hashes match.

**Emission**: output parses, metadata present, stream complete, partition count matches.

---

# 17. Provenance and Lineage

```
SourceSnapshot       →composed_from→  SemanticPresentation
SemanticPresentation →policy_applied→ ResolvedPresentation
ResolvedPresentation →geometry_from→  GeometricPresentation
GeometricPresentation→partitioned_from→PartitionedPresentation
PartitionedPresentation→materialized_from→ MaterializedPresentation
MaterializedPresentation→emitted_from→ EncodedArtifact
EncodedArtifact      →published_as→   Publication
```

A materialization receipt SHOULD commit to: input artifact identities; semantic algorithm versions; policy identities; resource checkpoint; materializer identity; emitter identity; result commitment; degradation commitment; lineage root.

---

# 18. Lifecycle

```
Potential → Composed → Validated → PolicyResolved → ConstraintResolved
→ GeometryResolved → Partitioned → Materialized → Emitted → Published → Archived
```

Exceptional states: `Rejected`, `Suspended`, `Superseded`, `Revoked`, `ResolutionFailed`, `MaterializationFailed`, `EmissionFailed`.

An emitted artifact does not replace the canonical semantic presentation — it is evidence of one materialization and encoding under declared inputs.

---

# 19. Document Profile

The document profile specializes the substrate for linear, paginated, durable output.

```csharp
public sealed record DocumentPresentation(
    SemanticPresentationIdentity Identity,
    DocumentMetadata             Metadata,
    IReadOnlyList<PresentationNode> Children);
```

Document partitioning produces ordered pages. Page profiles: `US-Letter-Portrait`, `A4-Portrait`, `Thermal-Receipt-80mm`. Initial emitters: `JsonInspectionEmitter`, `PdfEmitter`, `HtmlEmitter`.

---

# 20. Composition API

```csharp
SemanticPresentation presentation =
    PresentationBuilder
        .Create("ExplanationOfBenefits")
        .WithMetadata(metadata)
        .Add(HeaderComponents.CreateBrandHeader(brand))
        .Add(EobComponents.CreateClaimTable(member.Claims))
        .Add(FooterComponents.CreateLegalFooter())
        .Build();

public interface IPresentationComposer<in TSource>
{
    SemanticPresentation Compose(TSource source, CompositionContext context);
}
```

A composer owns only domain-to-semantic mapping. It MUST NOT emit target formats.

---

# 21. Mapping from Legacy EOB

| Legacy method | Formal responsibility |
|---|---|
| `CreateDocument` | Semantic composition coordinator |
| `DefineStyles` | Style sheet + presentation-policy construction |
| `CreateFirstPage` | Initial partition policy |
| `FillContent` | Repeater expansion + row composition |
| `PdfDocumentRenderer.RenderDocument` | Constraint, geometry, partition, materialization, emission |
| `pdf.Save` | Artifact persistence |

---

# 22. Reference EOB Shape

```
Presentation(role: ExplanationOfBenefits)
├── Header
│   ├── Image(role: OrganizationLogo)
│   └── Heading(role: DocumentTitle)
├── Address(role: RecipientAddress)
├── Table(role: PrescriptionClaims)
│   ├── TableHeader
│   ├── TableBody(rows: Claim×N, Totals)
│   └── ContinuationNotice
└── Footer(role: LegalDisclaimer)
```

PDF uses anchored layout. HTML uses responsive grid. Text uses canonical reading order. The semantic presentation is identical across all three.

---

# 23. Security and Governance

```
A rendered artifact is not authority.
An emitted file is not proof of semantic truth.
A valid encoding is not proof of authorized publication.
A resource hash proves content commitment, not trust.
A receipt records production; it does not grant permission.
A preview must not silently become a publication.
```

Untrusted inputs MUST be bounded before resource resolution, font loading, image decoding, template expansion, HTML emission, and attachment embedding.

---

# 24. Conformance Profiles

| Profile | Requires |
|---|---|
| PRES-CORE | Semantic model, canonical ordering, semantic identity, style/role separation, resource identity, JSON inspection |
| PRES-LAYOUT | Presentation profiles, constraint resolution, geometry, deterministic metrics |
| PRES-PARTITION | Fixed partitioning, measured partitioning, derived-node lineage, continuation policy |
| PRES-EMISSION | Materialization, ≥2 emitters, degradation records, output validation, emission identity |
| PRES-GOVERNED | Receipts, artifact lineage, publication separation, corridor integration, archive/revocation |
| PRES-FULL | All profiles |

---

# 25. Conformance Requirements

A conforming PRES-FULL implementation MUST demonstrate:

1. Semantic composition without invoking an emitter
2. Canonical serialization of the semantic presentation (JSON)
3. Stable semantic identity from identical source snapshots
4. Separation of semantic roles from styles
5. Explicit resource identities (ObjectId, not path)
6. Deterministic child ordering
7. Immutable stage artifacts
8. Independent presentation-plan identity
9. Deterministic constraint resolution
10. Deterministic geometry under identical resource metrics
11. Deterministic fixed partitioning
12. Deterministic measured partitioning
13. Repeated table headers on continuation pages
14. Continuation notices generated by policy
15. Derived-node lineage
16. Currency calculation without binary floating-point conversion
17. Validation of table and span structure
18. Materialization independent of target encoding
19. Emission to at least two target formats
20. Preservation of semantic text across formats
21. Recorded degradation for unsupported features
22. Distinct semantic, plan, geometry, partition, materialization, emission, and publication identities
23. Output validation after emission
24. Reproduction from archived semantic presentation + committed resources
25. Explicit distinction among semantic, visual, structural, and byte equivalence
26. Receipt generation for governed materialization
27. Publication lifecycle independent of emission lifecycle

---

# 26. Package Structure

```
packages/presentation-substrate/
├── spec/
│   ├── RFC-PRESENTATION-SUBSTRATE-001.md   (this RFC)
│   ├── PROFILE-DOCUMENT-001.md             (RFC-DOCUMENT-001 ref)
│   └── CONFORMANCE.md
├── src/
│   ├── Genesis.Presentation.Core/
│   │   ├── Model/       PresentationNode hierarchy
│   │   ├── Identity/    Nine identity domains
│   │   ├── Composition/ IPresentationComposer, PresentationBuilder
│   │   ├── Styles/      StyleDefinition, resolution
│   │   ├── Resources/   PresentationResourceRef, IResourceResolver
│   │   ├── Validation/
│   │   └── Lineage/     Receipt, provenance
│   ├── Genesis.Presentation.Layout/
│   │   ├── Constraints/ IPresentationPlanner, constraint model
│   │   ├── Resolution/  ResolvedPresentation
│   │   ├── Measurement/
│   │   └── Geometry/    GeometricPresentation
│   ├── Genesis.Presentation.Partitioning/
│   │   ├── Pagination/  PagePartitioner, continuation policy
│   │   └── Continuation/
│   ├── Genesis.Presentation.Materialization/
│   ├── Genesis.Presentation.Emit.Json/   JsonInspectionEmitter (first)
│   ├── Genesis.Presentation.Emit.Pdf/   MigraDoc-backed
│   ├── Genesis.Presentation.Emit.Html/
│   └── Genesis.Presentation.Emit.Docx/
└── tests/
    ├── Composition/
    ├── Identity/
    ├── Constraints/
    ├── Geometry/
    ├── Pagination/
    ├── Materialization/
    ├── Emitters/
    ├── Golden/     EOB reference outputs
    └── Conformance/
```

---

# 27. Implementation Phases

| Phase | Deliverable |
|---|---|
| A | Legacy EOB extraction — composer, style sheet, page profile, claim table, pagination policy, MigraDoc adapter |
| B | Canonical core — immutable nodes, semantic roles, JSON, semantic identity, resource identity, structural validation |
| C | Policy and constraint resolution — presentation profiles, style resolution, constraint model, degradation reporting |
| D | Geometry and partitioning — resource metrics, geometry resolution, fixed + measured pagination, partition identity |
| E | JSON inspection emitter — semantic presentation, constraints, geometry, partition tree, materialization |
| F | PDF compatibility — MigraDoc-backed PDF emitter reproducing legacy EOB layout |
| G | HTML emitter — proves semantic model independence from MigraDoc |
| H | Governance integration — lifecycle, corridor, EMIT evidence, receipts, publication, archive |

---

# 28. Constitutional Invariants

**P1 — Semantic Independence**: Semantic presentation meaning MUST be representable without invoking layout, geometry, partitioning, materialization, or emission.

**P2 — Stage Ownership**: Each stage owns only the semantics assigned to it in §3.2.

**P3 — No Silent Reinterpretation**: No downstream stage may silently reinterpret, remove, or invent semantic content.

**P4 — Identity Separation**: All nine identity domains are distinct (§14).

**P5 — Deterministic Boundaries**: Identical canonical inputs and algorithm versions MUST produce equivalent stage artifacts.

**P6 — Explicit Degradation**: Unsupported features MUST fail or produce an explicit recorded degradation.

**P7 — Derived-Node Lineage**: Every derived node MUST be traceable to its authored source or governing policy.

**P8 — Resource Commitment**: Materialization MUST bind the identities of resources and metrics that affect the realized presentation.

**P9 — Emission Non-Authority**: Successful emission does not establish semantic truth, authorization, or publication validity.

**P10 — Publication Separation**: Visibility and distribution belong to publication, not emission.

---

# 29. Governing Principle

> A presentation is a semantic object.
> Presentation policy declares how that object should be expressed.
> Constraint resolution determines a satisfiable presentation.
> Geometry resolution realizes that presentation spatially.
> Partitioning divides the realization into consumable units.
> Materialization commits the complete emitter-neutral result.
> An encoded file is one emission of that materialization.
> Publication makes that emission visible under governed authority.

For the document profile:

> A document is not its PDF.
> A document is a semantic presentation.
> A layout is a policy-constrained realization of that presentation.
> A page set is one partitioning of that realization.
> A PDF is one encoded emission of that materialization.

---

*Brandon Clark / Genesis Systems 2026*
