import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ConversationStore } from '../services/conversation.store';

@Component({
  selector: 'app-sidebar',
  template: `
    <div class="flex h-full flex-col">
      <button (click)="newChat()"
        class="m-3 rounded-lg bg-red-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-[var(--accent-hover)]">
        + New Chat
      </button>

      <div class="flex-1 overflow-y-auto px-2">
        @for (c of store.conversations(); track c.id) {
          <div (click)="select(c.id)"
            class="group flex items-center justify-between rounded-md py-2 pl-2.5 pr-3 text-sm cursor-pointer border-l-2 transition-colors"
            [class]="isActive(c.id) ? 'border-red-600 bg-neutral-100 dark:bg-neutral-800/70 text-neutral-900 dark:text-neutral-100' : 'border-transparent text-neutral-600 dark:text-neutral-400 hover:bg-neutral-100 dark:hover:bg-neutral-800/50'">
            <span class="truncate">{{ c.title }}</span>
            <button (click)="del(c.id, $event)"
              class="ml-2 hidden text-neutral-400 hover:text-red-600 group-hover:block">✕</button>
          </div>
        } @empty {
          <p class="px-3 py-2 text-sm text-neutral-500">No conversations yet</p>
        }
      </div>

      <div class="border-t border-neutral-200 p-3 text-xs text-neutral-400 dark:border-neutral-800">SamurAI Council · v2.0</div>
    </div>
  `,
})
export class Sidebar implements OnInit {
  store = inject(ConversationStore);
  private router = inject(Router);

  ngOnInit(): void {
    this.store.loadList();
  }

  isActive(id: string): boolean {
    return this.store.current()?.id === id;
  }

  newChat(): void {
    this.store.startNew();
    this.router.navigate(['/']);
  }

  select(id: string): void {
    this.router.navigate(['/chat', id]);
  }

  async del(id: string, ev: Event): Promise<void> {
    ev.stopPropagation();
    if (!confirm('Delete this conversation?')) return;
    await this.store.remove(id);
    if (!this.store.current()) this.router.navigate(['/']);
  }
}
