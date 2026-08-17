// surface-ir.ts
// TypeScript port of Genesis.Surface.Abstractions — Block IR, SurfaceSpec,
// SurfaceMessage, and SurfaceExpression types.
// Must stay in sync with Contracts.cs and ExpressionContracts.cs.

// ── Block IR ─────────────────────────────────────────────────────────────────

export type EmitPrimitive = 'E' | 'M' | 'I' | 'T';
export type EmitBasin     = 'E_BASIN' | 'M_BASIN' | 'I_BASIN' | 'T_BASIN';
export type Altitude      = 'ground' | '1000ft' | '10000ft' | '50000ft';

export type BlockBase = {
  blockId:       string;
  emitPrimitive: EmitPrimitive;
  cssClass?:     string;
};

export type TextBlock = BlockBase & {
  blockType: 'text';
  label:     string;
  value:     string;
};

export type MetricBlock = BlockBase & {
  blockType: 'metric';
  label:     string;
  value:     number;
  unit?:     string;
  trend?:    'up' | 'down' | 'flat';
};

export type StatusBlock = BlockBase & {
  blockType: 'status';
  label:     string;
  state:     'healthy' | 'degraded' | 'down' | 'unknown' | 'action' | 'error';
  detail?:   string;
};

export type LogEntry = { ts: string; text: string; level: 'info' | 'warn' | 'error' };

export type LogBlock = BlockBase & {
  blockType: 'log';
  label?:    string;
  entries:   LogEntry[];
};

export type ContainerBlock = BlockBase & {
  blockType: 'container';
  title?:    string;
  layout:    'column' | 'row' | 'grid';
  children:  Block[];
};

export type Block =
  | TextBlock | MetricBlock | StatusBlock | LogBlock | ContainerBlock;

// ── SurfaceSpec ───────────────────────────────────────────────────────────────

export type SurfaceSpec = {
  specId:          string;
  targetSurfaceId: string;
  emitBasin:       EmitBasin;
  altitude:        Altitude;
  blocks:          Block[];
  title?:          string;
  generatedAt?:    string;
};

// ── SurfaceMessage (wire format) ──────────────────────────────────────────────

export type RenderedBlock = {
  blockId:     string;
  contentType: 'text/html' | 'application/json';
  content:     string;
};

export type SurfaceMessage = {
  op:        'patch' | 'replace' | 'clear';
  targetId:  string;
  blocks?:   RenderedBlock[];
  error?:    string;
  intentId?: string;
  revision?: number;
};

// ── SurfaceExpression ─────────────────────────────────────────────────────────

export type SurfaceNode =
  | { nodeType: 'container'; nodeId: string; role: string; label?: string; layout?: string; children?: SurfaceNode[] }
  | { nodeType: 'text';      nodeId: string; role: string; label?: string; text: string; binding?: string }
  | { nodeType: 'input';     nodeId: string; role: string; label?: string; binding: string; valueType: string; placeholder?: string; required?: boolean }
  | { nodeType: 'action';    nodeId: string; role: string; label: string;  actionId: string; style?: string };

export type SurfaceAction = {
  id:          string;
  label:       string;
  description: string;
  returnType:  string;
  mutatesData: boolean;
  parameters:  Array<{ name: string; type: string; description: string; required: boolean }>;
};

export type SurfaceExpression = {
  expressionId:         string;
  name:                 string;
  sourceKind:           string;
  root:                 SurfaceNode;
  actions:              SurfaceAction[];
  requiredCapabilities: Array<{ name: string; reason: string }>;
  version?:             string;
  createdAt?:           string;
};
