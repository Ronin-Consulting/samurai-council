import { Component, inject } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartModalService } from '../services/chart-modal.service';

/** Full-screen modal that re-renders the currently-expanded chart much larger, so cramped axis/legend labels have room. Mounted once in app.html. */
@Component({
  selector: 'app-chart-modal',
  imports: [NgxEchartsDirective],
  template: `
    @if (chartModal.state(); as s) {
      <div
        class="fixed inset-0 z-50 grid place-items-center overflow-y-auto bg-black/50 p-6"
        (click)="chartModal.close()"
        (document:keydown.escape)="chartModal.close()"
      >
        <div
          class="overflow-hidden rounded-2xl border border-neutral-200 bg-white dark:border-neutral-700 dark:bg-neutral-900"
          (click)="$event.stopPropagation()"
        >
          <div class="flex items-center justify-between border-b border-neutral-200 px-4 py-2.5 dark:border-neutral-800">
            <div class="font-semibold text-neutral-900 dark:text-neutral-50">{{ s.title || 'Result' }}</div>
            <div class="flex items-center gap-2">
              @if (s.badge) {
                <span class="rounded-full bg-red-600/10 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-red-600">{{ s.badge }}</span>
              }
              <button
                (click)="chartModal.close()"
                class="rounded-lg p-1 text-neutral-500 transition-colors hover:bg-neutral-100 hover:text-neutral-800 dark:hover:bg-neutral-800 dark:hover:text-neutral-200"
                aria-label="Close"
              >✕</button>
            </div>
          </div>
          <div class="p-4">
            <div echarts [options]="s.options" class="h-[80vh] w-[90vw]"></div>
          </div>
        </div>
      </div>
    }
  `,
})
export class ChartModal {
  chartModal = inject(ChartModalService);
}
