import { Injectable, effect, signal } from '@angular/core';

/** Dark/light theme with localStorage persistence (fixes the non-persistence of the old Blazor ThemeService). */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly isDark = signal<boolean>(this.readInitial());

  constructor() {
    effect(() => {
      const dark = this.isDark();
      document.documentElement.classList.toggle('dark', dark);
      try { localStorage.setItem('theme-is-dark', String(dark)); } catch { /* ignore */ }
    });
  }

  toggle(): void {
    this.isDark.update((v) => !v);
  }

  private readInitial(): boolean {
    try {
      const stored = localStorage.getItem('theme-is-dark');
      if (stored !== null) return stored === 'true';
    } catch { /* ignore */ }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? true; // default dark
  }
}
