import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ConversationStore } from '../services/conversation.store';

@Component({
  selector: 'app-sidebar',
  template: `
    <div class="flex h-full flex-col">
      <button (click)="newChat()"
        class="m-3 rounded-lg bg-red-600 px-3 py-2 text-sm font-medium text-white hover:bg-red-700">
        + New Chat
      </button>

      <div class="flex-1 overflow-y-auto px-2">
        @for (c of store.conversations(); track c.id) {
          <div (click)="select(c.id)"
            class="group flex items-center justify-between rounded-lg px-3 py-2 text-sm cursor-pointer"
            [class]="isActive(c.id) ? 'bg-neutral-200 dark:bg-neutral-800' : 'hover:bg-neutral-100 dark:hover:bg-neutral-800/60'">
            <span class="truncate">{{ c.title }}</span>
            <button (click)="del(c.id, $event)"
              class="ml-2 hidden text-neutral-400 hover:text-red-600 group-hover:block">✕</button>
          </div>
        } @empty {
          <p class="px-3 py-2 text-sm text-neutral-500">No conversations yet</p>
        }
      </div>

      <div class="p-3 text-xs text-neutral-400">SamurAI Council · v2.0</div>
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
