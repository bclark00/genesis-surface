'use client';
// hooks/useSurfaceChannel.ts
// React port of genesis-homebase/webapps/genesis/surface-channel-client.js.
//
// Connects to the session stream route via SSE and applies SurfaceMessage
// patches to the DOM by targetId — op: replace|patch|clear.
//
// Works identically in both contexts:
//   Electron:    React UI served by Next.js inside BrowserWindow
//   CEH/WebView2: React UI served by Next.js, loaded by Genesis.Windows.Ribosome
//
// Usage:
//   const { status, lastSpec } = useSurfaceChannel(sessionId);
//
// The hook manages DOM patching internally via refs. Callers mount
// <div id={surfaceTargetId} /> somewhere in their render tree and the
// hook patches innerHTML on incoming surface events.

import { useEffect, useRef, useState, useCallback } from 'react';
import type { SurfaceMessage, RenderedBlock }        from '@/lib/surface/surface-ir';

// ── Types ─────────────────────────────────────────────────────────────────────

export type ChannelStatus =
  | 'idle'
  | 'connecting'
  | 'thinking'
  | 'tool_call'
  | 'receiving'
  | 'done'
  | 'error';

export type ToolEvent = {
  tool:    string;
  input?:  unknown;
  result?: string;
  error?:  string;
};

export type SurfaceChannelState = {
  status:     ChannelStatus;
  specId:     string | null;
  toolEvents: ToolEvent[];
  error:      string | null;
  /** Send a user message and open the SSE stream. */
  send:       (message: string) => void;
  /** Abort the current stream. */
  abort:      () => void;
};

// ── Hook ──────────────────────────────────────────────────────────────────────

export function useSurfaceChannel(sessionId: string): SurfaceChannelState {
  const [status,     setStatus]     = useState<ChannelStatus>('idle');
  const [specId,     setSpecId]     = useState<string | null>(null);
  const [toolEvents, setToolEvents] = useState<ToolEvent[]>([]);
  const [error,      setError]      = useState<string | null>(null);

  const abortRef    = useRef<AbortController | null>(null);
  const revisionRef = useRef<number>(0);

  // Abort any in-flight stream
  const abort = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    setStatus('idle');
  }, []);

  // Send a message and open the SSE stream
  const send = useCallback(async (message: string) => {
    abort();

    const ac = new AbortController();
    abortRef.current = ac;

    setStatus('connecting');
    setError(null);
    setToolEvents([]);

    try {
      const resp = await fetch(`/api/sessions/${sessionId}/stream`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ message }),
        signal:  ac.signal,
      });

      if (!resp.ok) {
        throw new Error(`Stream ${resp.status}: ${await resp.text()}`);
      }

      const reader = resp.body!.getReader();
      const dec    = new TextDecoder();
      let   buf    = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buf += dec.decode(value, { stream: true });

        // Parse SSE frames
        const frames = buf.split('\n\n');
        buf = frames.pop() ?? '';

        for (const frame of frames) {
          const parsed = parseFrame(frame);
          if (!parsed) continue;
          handleEvent(parsed.event, parsed.data, {
            setStatus, setSpecId, setToolEvents, revisionRef,
          });
        }
      }

      setStatus(prev => prev !== 'error' ? 'done' : prev);

    } catch (err: unknown) {
      if ((err as Error).name === 'AbortError') return;
      setError(String(err));
      setStatus('error');
    }
  }, [sessionId, abort]);

  // Cleanup on unmount
  useEffect(() => () => abort(), [abort]);

  return { status, specId, toolEvents, error, send, abort };
}

// ── Event handler ─────────────────────────────────────────────────────────────

type SetFns = {
  setStatus:     (s: ChannelStatus | ((p: ChannelStatus) => ChannelStatus)) => void;
  setSpecId:     (id: string) => void;
  setToolEvents: (fn: (prev: ToolEvent[]) => ToolEvent[]) => void;
  revisionRef:   React.MutableRefObject<number>;
};

function handleEvent(event: string, data: unknown, fns: SetFns) {
  switch (event) {
    case 'status': {
      const { status } = data as { status: ChannelStatus };
      fns.setStatus(status);
      break;
    }

    case 'tool_call': {
      const { tool, input } = data as { tool: string; input: unknown };
      fns.setStatus('tool_call');
      fns.setToolEvents(prev => [...prev, { tool, input }]);
      break;
    }

    case 'tool_result': {
      const { tool, result } = data as { tool: string; result: string };
      fns.setToolEvents(prev => {
        const idx = [...prev].reverse().findIndex(e => e.tool === tool && !e.result);
        if (idx < 0) return [...prev, { tool, result }];
        const copy = [...prev];
        copy[prev.length - 1 - idx] = { ...copy[prev.length - 1 - idx], result };
        return copy;
      });
      break;
    }

    case 'tool_error': {
      const { tool, error } = data as { tool: string; error: string };
      fns.setToolEvents(prev => [...prev, { tool, error }]);
      break;
    }

    case 'surface': {
      // Core: apply SurfaceMessage DOM patch
      fns.setStatus('receiving');
      const msg = data as SurfaceMessage;
      applyPatch(msg, fns.revisionRef);
      break;
    }

    case 'done': {
      const { specId } = data as { specId: string };
      fns.setSpecId(specId);
      fns.setStatus('done');
      break;
    }

    case 'error': {
      const { error } = data as { error: string };
      fns.setStatus('error');
      console.error('[useSurfaceChannel]', error);
      break;
    }
  }
}

// ── DOM patcher (mirrors surface-channel-client.js) ───────────────────────────

function applyPatch(msg: SurfaceMessage, revisionRef: React.MutableRefObject<number>) {
  if (typeof window === 'undefined') return;

  const target = document.getElementById(msg.targetId);
  if (!target) {
    console.warn(`[useSurfaceChannel] target #${msg.targetId} not found`);
    return;
  }

  // Monotonic revision guard (same as surface-channel-client.js)
  if (msg.revision !== undefined && msg.revision <= revisionRef.current) return;
  if (msg.revision !== undefined) revisionRef.current = msg.revision;

  switch (msg.op) {
    case 'replace':
      target.innerHTML = renderBlocks(msg.blocks ?? []);
      break;

    case 'patch':
      patchBlocks(target, msg.blocks ?? []);
      break;

    case 'clear':
      target.innerHTML = '';
      break;
  }
}

function renderBlocks(blocks: RenderedBlock[]): string {
  return blocks.map(b => b.content).join('');
}

function patchBlocks(target: HTMLElement, blocks: RenderedBlock[]) {
  for (const block of blocks) {
    const existing = target.querySelector(`[data-block-id="${CSS.escape(block.blockId)}"]`);
    if (existing) {
      // Update in place
      existing.outerHTML = block.content;
    } else {
      // Append new block
      target.insertAdjacentHTML('beforeend', block.content);
    }
  }
}

// ── SSE frame parser ──────────────────────────────────────────────────────────

function parseFrame(frame: string): { event: string; data: unknown } | null {
  const lines  = frame.split('\n');
  let event    = 'message';
  let dataStr  = '';

  for (const line of lines) {
    if (line.startsWith('event: ')) event   = line.slice(7).trim();
    if (line.startsWith('data: '))  dataStr = line.slice(6).trim();
  }

  if (!dataStr) return null;
  try { return { event, data: JSON.parse(dataStr) }; }
  catch { return null; }
}
