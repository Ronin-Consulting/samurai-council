import { Component, input } from '@angular/core';

/** Shared chrome for every pre-made Studio component: titled card + form badge + optional footer. */
@Component({
  selector: 'app-studio-card',
  template: `
    <div class="my-3 overflow-hidden rounded-2xl border border-neutral-200 dark:border-neutral-700 bg-white/60 dark:bg-neutral-900/40">
      <div class="flex items-center justify-between border-b border-neutral-200 dark:border-neutral-800 px-4 py-2.5">
        <div class="font-semibold text-neutral-900 dark:text-neutral-50">{{ title() || 'Result' }}</div>
        <span class="rounded-full bg-red-600/10 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-red-600">{{ badge() }}</span>
      </div>
      <div class="p-4"><ng-content /></div>
      @if (footer()) {
        <div class="border-t border-neutral-200 dark:border-neutral-800 px-4 py-2 text-xs text-neutral-500">{{ footer() }}</div>
      }
    </div>
  `,
})
export class StudioCard {
  title = input<string>('');
  badge = input<string>('');
  footer = input<string>('');
}
