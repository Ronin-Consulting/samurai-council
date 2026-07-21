import { Injectable, signal } from '@angular/core';

export interface ChartModalState {
  title: string;
  options: any;
  badge?: string;
}

/** Holds the chart currently expanded into the full-screen modal. Shared by Classic (chart-display) and Studio (studio-chart) so there is exactly one modal instance for the whole app. */
@Injectable({ providedIn: 'root' })
export class ChartModalService {
  readonly state = signal<ChartModalState | null>(null);

  open(title: string, options: any, badge?: string): void {
    this.state.set({ title, options, badge });
    document.body.classList.add('overflow-hidden');
  }

  close(): void {
    this.state.set(null);
    document.body.classList.remove('overflow-hidden');
  }
}
