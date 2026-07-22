import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MarkdownComponent } from 'ngx-markdown';
import { AssistantMessage, Message } from '../models';
import { ConversationStore } from '../services/conversation.store';
import { CouncilStreamService } from '../services/council-stream.service';
import { AssistantMessagePanel } from './assistant-message';

@Component({
  selector: 'app-chat-page',
  imports: [MarkdownComponent, AssistantMessagePanel],
  template: `
    <div class="flex h-full flex-col">
      <div class="flex-1 overflow-y-auto">
        <div class="mx-auto max-w-3xl px-4 py-6">
          @if (!store.current() || store.current()!.messages.length === 0) {
            <div class="mt-24 flex flex-col items-center text-center">
              <span class="mb-5 flex items-center gap-3 text-[11px] font-semibold uppercase tracking-[0.18em] text-neutral-400">
                <span class="h-px w-8 bg-neutral-300 dark:bg-neutral-700"></span>
                <span class="inline-block h-1.5 w-1.5 rounded-full bg-red-600"></span>
                SamurAI Council
                <span class="h-px w-8 bg-neutral-300 dark:bg-neutral-700"></span>
              </span>
              <h1 class="font-display text-4xl font-medium tracking-tight text-neutral-900 dark:text-neutral-100">How can the council help?</h1>
              <p class="mt-3 text-neutral-500">Ask a question — three models deliberate, a chairman synthesizes.</p>
              <div class="mt-7 flex flex-wrap justify-center gap-2">
                @for (ex of examples; track ex) {
                  <button (click)="sendText(ex)"
                    class="rounded-full border border-neutral-200 px-3.5 py-1.5 text-sm text-neutral-600 transition-colors hover:border-red-600 hover:text-red-600 dark:border-neutral-700 dark:text-neutral-300 dark:hover:text-red-500">
                    {{ ex }}
                  </button>
                }
              </div>
            </div>
          } @else {
            @for (m of store.current()!.messages; track $index; let i = $index) {
              @if (m.role === 'user') {
                <div class="mb-5 flex justify-end">
                  <div class="max-w-[80%] rounded-2xl border border-neutral-200 bg-neutral-100 px-4 py-2.5 text-neutral-900 dark:border-neutral-700 dark:bg-neutral-800 dark:text-neutral-100">
                    <markdown [data]="userText(m)"></markdown>
                  </div>
                </div>
              } @else {
                <div class="mb-6">
                  <app-assistant-message
                    [message]="asAssistant(m)"
                    [query]="queryFor(i)"
                    [conversationId]="store.current()!.id" />
                </div>
              }
            }
          }
        </div>
      </div>

      <div class="border-t border-neutral-200 dark:border-neutral-800 p-4">
        <div class="mx-auto flex max-w-3xl items-end gap-2">
          <textarea #ta [value]="input()" (input)="input.set(ta.value)" (keydown)="onKey($event)"
            rows="1" placeholder="Ask the council…" [disabled]="store.isProcessing()"
            class="flex-1 resize-none rounded-xl border border-neutral-200 bg-white px-4 py-2.5 text-neutral-900 placeholder:text-neutral-400 transition-colors focus:border-red-600 focus:outline-none focus:ring-2 focus:ring-red-600/40 disabled:opacity-50 dark:border-neutral-700 dark:bg-neutral-900 dark:text-neutral-100"></textarea>
          <button (click)="sendText(input())" [disabled]="store.isProcessing() || !input().trim()"
            class="rounded-xl bg-red-600 px-4 py-2.5 font-medium text-white transition-colors hover:bg-[var(--accent-hover)] disabled:opacity-40">
            Send
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ChatPage implements OnInit {
  store = inject(ConversationStore);
  private stream = inject(CouncilStreamService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  input = signal('');

  readonly examples = [
    'Show top 5 products by revenue in 2008',
    'Show sales by country',
    'Show monthly sales trends',
    'What percentage of sales come from each channel?',
  ];

  ngOnInit(): void {
    this.route.paramMap.subscribe((pm) => {
      const id = pm.get('id');
      if (id) {
        if (this.store.current()?.id !== id) {
          this.store.load(id).then(() => this.resumeIfInterrupted(id));
        }
      } else if (!this.store.isProcessing()) {
        this.store.startNew();
      }
    });
  }

  /**
   * A page load (e.g. a browser refresh mid-turn) can land on a conversation whose last
   * message is a user prompt with no assistant reply yet — the backend keeps running the
   * council turn independently of any live connection, so the reply usually just hasn't been
   * persisted yet. Show the same loading stepper a live run would and poll until it lands,
   * rather than silently looking like nothing happened.
   */
  private async resumeIfInterrupted(convId: string): Promise<void> {
    const conv = this.store.current();
    if (!conv || conv.id !== convId || conv.messages.length === 0) return;
    if (conv.messages[conv.messages.length - 1].role !== 'user') return;

    this.store.appendMessage({ role: 'assistant', loading: { stage1: true, stage2: false, stage3: false } });
    this.store.isProcessing.set(true);

    const POLL_INTERVAL_MS = 3000;
    const MAX_ATTEMPTS = 200; // ~10 minutes
    for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
      await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
      if (this.store.current()?.id !== convId) return; // navigated away
      if (await this.pollOnce(convId)) return;
    }

    if (this.store.current()?.id === convId) {
      this.store.updateLastMessage((m) => ({
        ...(m as AssistantMessage),
        loading: null,
        stage3: {
          model: 'error',
          response: 'This is taking longer than expected. Refresh the page in a bit to check again.',
          tool_usages: [],
        },
      }));
      this.store.isProcessing.set(false);
    }
  }

  /** Re-fetches the conversation; returns true once a real assistant reply has landed. */
  private async pollOnce(convId: string): Promise<boolean> {
    const ok = await this.store.load(convId);
    if (!ok) return false; // transient fetch failure (e.g. backend mid-restart) — keep waiting

    const conv = this.store.current();
    if (!conv || conv.id !== convId) return true; // genuinely gone (404) or navigated away

    const last = conv.messages[conv.messages.length - 1];
    if (!last || last.role !== 'user') {
      this.store.isProcessing.set(false);
      return true;
    }

    // Still unanswered — store.load() replaced `current` with server truth, which wiped our
    // local loading placeholder. Re-append it so the stepper keeps showing.
    this.store.appendMessage({ role: 'assistant', loading: { stage1: true, stage2: false, stage3: false } });
    return false;
  }

  userText(m: Message): string {
    return m.role === 'user' ? m.content : '';
  }

  asAssistant(m: Message): AssistantMessage {
    return m as AssistantMessage;
  }

  queryFor(index: number): string {
    const msgs = this.store.current()?.messages ?? [];
    const prev = msgs[index - 1];
    return prev && prev.role === 'user' ? prev.content : '';
  }

  onKey(ev: KeyboardEvent): void {
    if (ev.key === 'Enter' && !ev.shiftKey) {
      ev.preventDefault();
      this.sendText(this.input());
    }
  }

  async sendText(raw: string): Promise<void> {
    const text = raw.trim();
    if (!text || this.store.isProcessing()) return;
    this.input.set('');

    if (!this.store.current()) {
      const conv = await this.store.create();
      this.router.navigate(['/chat', conv.id]);
    }
    const convId = this.store.current()!.id;

    this.store.appendMessage({ role: 'user', content: text });
    this.store.appendMessage({ role: 'assistant', loading: { stage1: true, stage2: false, stage3: false } });
    this.store.isProcessing.set(true);

    await this.stream.run(convId, text, {
      onEvent: (name, data) => {
        if (name === 'title') {
          this.store.setTitle(convId, data?.title ?? '');
          return;
        }
        // The user may have navigated to a different conversation while this stream was still
        // in flight — a late event for the old conversation must not mutate whatever's now shown.
        if (this.store.current()?.id !== convId) return;
        this.store.updateLastMessage((m) => {
          const a: AssistantMessage = { ...(m as AssistantMessage) };
          switch (name) {
            case 'loading': a.loading = data; break;
            case 'stage1': a.stage1 = data; break;
            case 'stage2': a.stage2 = data?.rankings ?? []; a.metadata = data?.metadata ?? null; break;
            case 'stage3': a.stage3 = data; a.loading = null; break;
            case 'error': a.stage3 = data?.stage3 ?? { model: 'error', response: 'An error occurred.', tool_usages: [] }; a.loading = null; break;
          }
          return a;
        });
      },
      onDone: () => this.store.isProcessing.set(false),
      onError: () => this.store.isProcessing.set(false),
    });
  }
}
