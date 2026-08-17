# genesis-surface

Genesis Surface / Presentation UX IR — block-based presentation layer.

## Architecture

Surface IR is the output contract between the reasoning layer (Agent, Recon, Security,
HiveMind) and any presentation host (WPF Ribosome, Web, claude-desktop, future UIs).

```
Producer                  Surface IR              Host
─────────────────         ──────────────────      ──────────────────
Genesis.Agent       ─┐
Genesis.Recon       ─┤   SurfaceSpec              Genesis.Windows.Ribosome (WPF)
Genesis.Security    ─┼──► [Block, Block, ...]  ──► WebProjector (SSE/HTTP)
Genesis.HiveMind    ─┤    ↑ compiled by           CehProjector (CEH seed)
Platform EventBridge ─┘   SurfaceExpressionCompiler
```

## Projects

`src/Genesis.Surface.Abstractions`
  Block IR types (Text, Metric, Status, Log, Container), SurfaceSpec, SurfaceExpression,
  SurfaceActions, AdaptiveSurface (attention regions, activation, composition),
  ObservationContracts, ControlPlaneSurfaceFactory.
  SurfaceExpressionCompiler — compiles expression trees to SurfaceSpec.

`src/Genesis.Surface.Runtime`
  SurfaceRuntime — session management, mutations, projections, receipts.
  SurfaceActionDispatcher — handler registry and dispatch.
  SurfaceActionRouter — routes actions to registered handlers.
  SurfaceAttentionComposer — composes attention surface from activation snapshot.
  QuartzSurfaceFeedbackInterpreter — interprets Quartz coactivation as surface feedback.

`src/Genesis.Surface.Projectors`
  CehProjector — projects SurfaceSpec to CEH seed channel.
  WebProjector — projects via SSE/HTTP to web clients.

`src/Genesis.Windows.Ribosome`
  WPF presentation host. RibosomeWindow, WpfSurfaceChannel, WpfSurfaceProjector,
  ControlPlaneSurfaceWindow, TigerOverlay.

`tests/Genesis.Surface.Tests`
  SurfaceActionDispatcherTests, SurfaceObservationRouterTests, SurfacePresenceTests.

## TypeScript

`ts/surface/` — TypeScript port of Block IR and SurfaceExpression contracts.
  Must stay in sync with Genesis.Surface.Abstractions.
  Used by claude-desktop-exponential and future web hosts.

`ts/hooks/` — useSurfaceChannel React hook.

## RFCs

`docs/` — 8 normative draft RFCs (July–August 2026):
  RFC-SURFACE-001               Semantic model
  RFC-PRESENTATION-SUBSTRATE-001  Composition, materialization, emission
  RFC-LIVE-SURFACE-001          Live projection family
  RFC-SURFACE-CHANNEL-001       Transport contracts
  RFC-SURFACE-PROJECTION-001    Projection contracts
  RFC-REPRESENTATION-001        Representation substrate
  RFC-REPRESENTATION-TRANSFORM-001  Transform pipeline
  RFC-SOURCE-SURFACE-IR-BOUNDARY-001  Source IR / Surface IR layer boundary

(c) 2026 Brandon Clark / Genesis Systems.
