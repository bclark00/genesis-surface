// surface-projector.ts
// TypeScript port of Genesis.Surface.Projectors.WebProjector.
// SurfaceSpec → SurfaceMessage (RenderedBlock[] with text/html content).

import type { SurfaceSpec, SurfaceMessage, RenderedBlock, Block } from './surface-ir';

// ── WebProjector ──────────────────────────────────────────────────────────────

export function project(
  spec:            SurfaceSpec,
  revision:        number,
  op:              'replace' | 'patch' = 'replace',
): SurfaceMessage {
  const rendered = spec.blocks.map(renderBlock);
  return {
    op,
    targetId:  spec.targetSurfaceId,
    blocks:    rendered,
    intentId:  spec.specId,
    revision,
  };
}

// ── Block renderers ───────────────────────────────────────────────────────────

function renderBlock(block: Block): RenderedBlock {
  return {
    blockId:     block.blockId,
    contentType: 'text/html',
    content:     renderHtml(block),
  };
}

function renderHtml(block: Block): string {
  switch (block.blockType) {
    case 'text':
      return renderText(block.label, block.value, block.emitPrimitive);

    case 'metric':
      return `<div class="surface-block surface-metric emit-${block.emitPrimitive.toLowerCase()}"
                   data-block-id="${esc(block.blockId)}">
        <span class="metric-label">${esc(block.label)}</span>
        <span class="metric-value">${block.value}${block.unit ? `<small>${esc(block.unit)}</small>` : ''}</span>
        ${block.trend ? `<span class="metric-trend trend-${block.trend}"></span>` : ''}
      </div>`;

    case 'status':
      return `<div class="surface-block surface-status state-${block.state} emit-${block.emitPrimitive.toLowerCase()}"
                   data-block-id="${esc(block.blockId)}">
        <span class="status-indicator"></span>
        <span class="status-label">${esc(block.label)}</span>
        ${block.detail ? `<span class="status-detail">${esc(block.detail)}</span>` : ''}
      </div>`;

    case 'log':
      const entries = (block.entries ?? []).map(e =>
        `<li class="log-entry level-${e.level}">
          <time>${esc(e.ts)}</time>
          <span>${esc(e.text)}</span>
        </li>`
      ).join('');
      return `<div class="surface-block surface-log emit-${block.emitPrimitive.toLowerCase()}"
                   data-block-id="${esc(block.blockId)}">
        ${block.label ? `<h4>${esc(block.label)}</h4>` : ''}
        <ul class="log-entries">${entries}</ul>
      </div>`;

    case 'container':
      const children = block.children.map(renderHtml).join('');
      return `<div class="surface-block surface-container layout-${block.layout} emit-${block.emitPrimitive.toLowerCase()}"
                   data-block-id="${esc(block.blockId)}">
        ${block.title ? `<h3 class="container-title">${esc(block.title)}</h3>` : ''}
        <div class="container-children">${children}</div>
      </div>`;
  }
}

function renderText(label: string, value: string, emit: string): string {
  if (!label) {
    // Plain prose paragraph
    return `<p class="surface-block surface-text emit-${emit.toLowerCase()}"
               data-block-id="text">${esc(value)}</p>`;
  }
  return `<div class="surface-block surface-text emit-${emit.toLowerCase()}">
    <span class="text-label">${esc(label)}</span>
    <span class="text-value">${esc(value)}</span>
  </div>`;
}

function esc(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
