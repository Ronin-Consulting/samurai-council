import { Injectable } from '@angular/core';

export interface StreamCallbacks {
  onEvent: (event: string, data: any) => void;
  onError?: (err: unknown) => void;
  onDone?: () => void;
}

/**
 * Consumes the API's Server-Sent-Events council stream. The endpoint is a POST, so we can't use
 * the native EventSource (GET-only) — we read the response body stream and parse SSE frames.
 */
@Injectable({ providedIn: 'root' })
export class CouncilStreamService {
  async run(conversationId: string, content: string, cb: StreamCallbacks, signal?: AbortSignal): Promise<void> {
    try {
      const res = await fetch(`/api/conversations/${conversationId}/messages`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
        body: JSON.stringify({ content }),
        signal,
      });

      if (!res.ok || !res.body) {
        cb.onError?.(new Error(`Stream failed: HTTP ${res.status}`));
        return;
      }

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        let sep: number;
        while ((sep = buffer.indexOf('\n\n')) >= 0) {
          const frame = buffer.slice(0, sep);
          buffer = buffer.slice(sep + 2);
          this.parseFrame(frame, cb);
        }
      }
      cb.onDone?.();
    } catch (err) {
      if ((err as any)?.name === 'AbortError') return;
      cb.onError?.(err);
    }
  }

  private parseFrame(frame: string, cb: StreamCallbacks): void {
    let event = 'message';
    let data = '';
    for (const line of frame.split('\n')) {
      if (line.startsWith('event:')) event = line.slice(6).trim();
      else if (line.startsWith('data:')) data += line.slice(5).trim();
    }
    if (!data) { cb.onEvent(event, null); return; }
    try { cb.onEvent(event, JSON.parse(data)); }
    catch { cb.onEvent(event, data); }
  }
}
