import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from './api.service';
import { Conversation, ConversationSummary, Message } from '../models';

/** Signals-based UI state (replaces the per-circuit Blazor ConversationState). */
@Injectable({ providedIn: 'root' })
export class ConversationStore {
  private api = inject(ApiService);

  readonly conversations = signal<ConversationSummary[]>([]);
  readonly current = signal<Conversation | null>(null);
  readonly isProcessing = signal(false);

  async loadList(): Promise<void> {
    try { this.conversations.set(await firstValueFrom(this.api.listConversations())); } catch { /* ignore */ }
  }

  /**
   * Fetches a conversation. Returns false on failure without touching `current` — a transient
   * backend blip (e.g. the container mid-restart) must not look identical to "this conversation
   * doesn't exist" and wipe out whatever's already showing. Only a genuine 404 clears `current`.
   */
  async load(id: string): Promise<boolean> {
    try {
      this.current.set(await firstValueFrom(this.api.getConversation(id)));
      return true;
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) this.current.set(null);
      return false;
    }
  }

  startNew(): void {
    this.current.set(null);
  }

  async create(): Promise<Conversation> {
    const conv = await firstValueFrom(this.api.createConversation());
    this.conversations.update((list) => [
      { id: conv.id, title: conv.title, created_at: conv.created_at, updated_at: null },
      ...list,
    ]);
    this.current.set(conv);
    return conv;
  }

  setTitle(id: string, title: string): void {
    this.conversations.update((l) => l.map((c) => (c.id === id ? { ...c, title } : c)));
    const cur = this.current();
    if (cur && cur.id === id) this.current.set({ ...cur, title });
  }

  async remove(id: string): Promise<void> {
    const prev = this.conversations();
    this.conversations.update((l) => l.filter((c) => c.id !== id));
    if (this.current()?.id === id) this.current.set(null);
    try { await firstValueFrom(this.api.deleteConversation(id)); }
    catch { this.conversations.set(prev); }
  }

  async removeMany(ids: string[]): Promise<void> {
    const prev = this.conversations();
    const set = new Set(ids);
    this.conversations.update((l) => l.filter((c) => !set.has(c.id)));
    const cur = this.current();
    if (cur && set.has(cur.id)) this.current.set(null);
    try { await firstValueFrom(this.api.bulkDelete(ids)); }
    catch { this.conversations.set(prev); }
  }

  // --- message mutation during streaming (new array references => zoneless change detection) ---

  appendMessage(m: Message): void {
    const cur = this.current();
    if (!cur) return;
    this.current.set({ ...cur, messages: [...cur.messages, m] });
  }

  updateLastMessage(updater: (m: Message) => Message): void {
    const cur = this.current();
    if (!cur || cur.messages.length === 0) return;
    const msgs = cur.messages.slice();
    msgs[msgs.length - 1] = updater(msgs[msgs.length - 1]);
    this.current.set({ ...cur, messages: msgs });
  }
}
