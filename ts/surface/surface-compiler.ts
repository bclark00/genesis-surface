// surface-compiler.ts
// TypeScript port of Genesis.Surface.Abstractions.SurfaceExpressionCompiler.
// SurfaceExpression → SurfaceSpec.
//
// Must stay in sync with SurfaceExpressionCompiler.cs.

import { createHash }                                        from 'crypto';
import type {
  SurfaceExpression, SurfaceNode, SurfaceAction,
  SurfaceSpec, Block, EmitBasin, Altitude,
  TextBlock, StatusBlock, ContainerBlock, LogBlock, LogEntry,
} from './surface-ir';

// ── Public entry point ────────────────────────────────────────────────────────

export function compileSurface(
  expression:      SurfaceExpression,
  targetSurfaceId: string,
  altitude?:       Altitude,
): SurfaceSpec {
  const blocks     = compileNode(expression.root, expression.actions);
  if (expression.actions.length > 0)
    blocks.push(compileActionCatalogue(expression.actions));

  const emitBasin  = deriveBasin(blocks);
  const altBand    = altitude ?? deriveAltitude(expression);
  const specId     = contentHash(expression.expressionId, targetSurfaceId, blocks);

  return {
    specId,
    targetSurfaceId,
    emitBasin,
    altitude: altBand,
    blocks,
    title:       expression.name,
    generatedAt: expression.createdAt ?? new Date().toISOString(),
  };
}

// ── Compiler from raw AI text ─────────────────────────────────────────────────

/**
 * Compile raw AI assistant text into a SurfaceSpec directly —
 * skips the SurfaceExpression layer for streaming text responses.
 * Splits the text into paragraphs, classifies each by content.
 */
export function compileText(
  text:            string,
  targetSurfaceId: string,
  title?:          string,
): SurfaceSpec {
  const blocks: Block[] = text
    .split(/\n{2,}/)
    .filter(p => p.trim().length > 0)
    .map((para, i) => classifyParagraph(para.trim(), i));

  const specId = contentHash(text.slice(0, 64), targetSurfaceId, blocks);

  return {
    specId,
    targetSurfaceId,
    emitBasin:  deriveBasin(blocks),
    altitude:   'ground',
    blocks,
    title,
    generatedAt: new Date().toISOString(),
  };
}

// ── Node compilation ──────────────────────────────────────────────────────────

function compileNode(node: SurfaceNode, actions: SurfaceAction[]): Block[] {
  switch (node.nodeType) {
    case 'container': {
      const children = (node.children ?? []).flatMap(c => compileNode(c, actions));
      const block: ContainerBlock = {
        blockType:     'container',
        blockId:       node.nodeId,
        emitPrimitive: 'I',
        title:         node.label,
        layout:        (node.layout ?? 'column') as ContainerBlock['layout'],
        children,
      };
      return [block];
    }
    case 'text': {
      const block: TextBlock = {
        blockType:     'text',
        blockId:       node.nodeId,
        emitPrimitive: 'E',
        label:         node.label ?? node.nodeId,
        value:         node.text,
      };
      return [block];
    }
    case 'input': {
      const block: TextBlock = {
        blockType:     'text',
        blockId:       node.nodeId,
        emitPrimitive: 'E',
        label:         node.label ?? node.binding,
        value:         node.placeholder ?? `Enter ${node.valueType}`,
      };
      return [block];
    }
    case 'action': {
      const def   = actions.find(a => a.id === node.actionId);
      const block: StatusBlock = {
        blockType:     'status',
        blockId:       node.nodeId,
        emitPrimitive: 'T',
        label:         node.label,
        state:         styleToState(node.style),
        detail:        def?.description,
      };
      return [block];
    }
    default:
      return [];
  }
}

function compileActionCatalogue(actions: SurfaceAction[]): LogBlock {
  const entries: LogEntry[] = actions.map(a => ({
    ts:    new Date().toISOString(),
    text:  `${a.label} — ${a.description} [${a.returnType}]${a.mutatesData ? ' [mutating]' : ''}`,
    level: a.mutatesData ? 'warn' : 'info',
  }));
  return {
    blockType:     'log',
    blockId:       'action-catalogue',
    emitPrimitive: 'E',
    label:         'Actions',
    entries,
  };
}

// ── Paragraph classifier (for raw text → blocks) ──────────────────────────────

function classifyParagraph(text: string, index: number): Block {
  const blockId = `p${index}`;

  // Metric: "X: 42 ms" / "Latency: 3.2s"
  const metricMatch = text.match(/^([^:]+):\s*([\d.]+)\s*(\w+)?$/);
  if (metricMatch) {
    const value = parseFloat(metricMatch[2]);
    if (!isNaN(value)) {
      return {
        blockType: 'metric', blockId, emitPrimitive: 'M',
        label: metricMatch[1].trim(),
        value,
        unit:  metricMatch[3],
      };
    }
  }

  // Status: starts with ✓/✗/⚠ or "healthy/degraded/down/error"
  if (/^[✓✗⚠]|^(healthy|degraded|down|error|ok|fail)/i.test(text)) {
    const state = /fail|error|✗/i.test(text) ? 'error'
                : /warn|⚠/i.test(text)        ? 'degraded'
                :                               'healthy';
    return {
      blockType: 'status', blockId, emitPrimitive: 'T',
      label:  text.split('\n')[0].slice(0, 60),
      state,
      detail: text.length > 60 ? text : undefined,
    };
  }

  // Default: text block
  return {
    blockType: 'text', blockId, emitPrimitive: 'E',
    label: '',
    value: text,
  };
}

// ── Basin + altitude derivation ───────────────────────────────────────────────

function deriveBasin(blocks: Block[]): EmitBasin {
  const counts: Record<string, number> = { E: 0, M: 0, I: 0, T: 0 };
  countEmit(blocks, counts);
  const dominant = Object.entries(counts).reduce((a, b) => b[1] > a[1] ? b : a);
  return `${dominant[0]}_BASIN` as EmitBasin;
}

function countEmit(blocks: Block[], counts: Record<string, number>): void {
  for (const b of blocks) {
    counts[b.emitPrimitive] = (counts[b.emitPrimitive] ?? 0) + 1;
    if (b.blockType === 'container') countEmit(b.children, counts);
  }
}

function deriveAltitude(expr: SurfaceExpression): Altitude {
  const c = expr.actions.length + expr.requiredCapabilities.length;
  if (c >= 15) return '50000ft';
  if (c >= 8)  return '10000ft';
  if (c >= 3)  return '1000ft';
  return 'ground';
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function styleToState(style?: string): StatusBlock['state'] {
  switch (style) {
    case 'danger':    return 'error';
    case 'ghost':     return 'unknown';
    default:          return 'action';
  }
}

function contentHash(exprId: string, surfaceId: string, blocks: Block[]): string {
  const input = `${exprId}:${surfaceId}:${blocks.map(b => b.blockId).join(',')}`;
  return createHash('sha256').update(input).digest('hex').slice(0, 16);
}
