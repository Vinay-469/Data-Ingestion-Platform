import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error';
export interface ToastMessage { id: number; kind: ToastKind; message: string; }

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly items = signal<ToastMessage[]>([]);
  private nextId = 1;

  show(kind: ToastKind, message: string, duration = 5000) {
    const id = this.nextId++;
    this.items.update(items => [...items, { id, kind, message }].slice(-4));
    window.setTimeout(() => this.dismiss(id), duration);
  }

  dismiss(id: number) { this.items.update(items => items.filter(item => item.id !== id)); }
  clear() { this.items.set([]); }
}
