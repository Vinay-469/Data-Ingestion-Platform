import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly isDark = signal(localStorage.getItem('ingestion.theme') === 'dark');

  constructor() { this.apply(); }

  toggle(): void {
    this.isDark.update(value => !value);
    localStorage.setItem('ingestion.theme', this.isDark() ? 'dark' : 'light');
    this.apply();
  }

  private apply(): void {
    document.body.dataset['theme'] = this.isDark() ? 'dark' : 'light';
  }
}
