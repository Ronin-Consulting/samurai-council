import { Injectable, effect, signal } from '@angular/core';

export type ViewMode = 'classic' | 'studio';

/**
 * Which visualization version renders results:
 *  - 'classic' = V1 generative (LLM-picked form, dynamic renderer)
 *  - 'studio'  = V2 pre-made components (deterministic classification)
 * Persisted so the choice survives reloads during the meeting demo.
 */
@Injectable({ providedIn: 'root' })
export class ViewModeService {
  readonly mode = signal<ViewMode>(this.readInitial());

  constructor() {
    effect(() => {
      try { localStorage.setItem('view-mode', this.mode()); } catch { /* ignore */ }
    });
  }

  set(mode: ViewMode): void { this.mode.set(mode); }
  toggle(): void { this.mode.update((m) => (m === 'classic' ? 'studio' : 'classic')); }

  private readInitial(): ViewMode {
    try {
      const s = localStorage.getItem('view-mode');
      if (s === 'studio' || s === 'classic') return s;
    } catch { /* ignore */ }
    return 'classic';
  }
}
