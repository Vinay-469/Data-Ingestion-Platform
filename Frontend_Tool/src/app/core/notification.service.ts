import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { ToastService } from './toast.service';

export interface AppNotification {
  type: string;
  title: string;
  message: string;
  uploadId?: number;
  fileName?: string;
  userName?: string;
  createdOn: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  readonly items = signal<AppNotification[]>([]);
  readonly unreadCount = signal(0);
  readonly connected = signal(false);
  private connection?: HubConnection;
  private starting = false;
  private retryTimer?: number;
  private readonly recentEvents = new Map<string, number>();
  constructor(private readonly toast: ToastService) {}

  async connect() {
    if (this.connection || this.starting || !localStorage.getItem('ingestion.token')) return;
    this.starting = true;
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/notifications', { accessTokenFactory: () => localStorage.getItem('ingestion.token') ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('notification', (notification: AppNotification) => {
      const eventKey = `${notification.type}|${notification.uploadId ?? ''}|${notification.fileName ?? ''}`;
      const now = Date.now();
      const previous = this.recentEvents.get(eventKey);
      if (previous && now - previous < 10_000) return;
      this.recentEvents.set(eventKey, now);
      for (const [key, timestamp] of this.recentEvents) if (now - timestamp >= 10_000) this.recentEvents.delete(key);
      this.items.update(items => [notification, ...items].slice(0, 30));
      this.unreadCount.update(value => value + 1);
      this.toast.show(notification.type === 'upload-deactivated' ? 'error' : 'success', notification.message);
    });
    connection.onreconnecting(() => this.connected.set(false));
    connection.onreconnected(() => this.connected.set(true));
    connection.onclose(() => {
      this.connected.set(false);
      if (this.connection === connection) this.connection = undefined;
      this.scheduleReconnect();
    });
    this.connection = connection;
    try {
      await connection.start();
      this.connected.set(true);
    } catch {
      this.connection = undefined;
      this.scheduleReconnect();
    } finally {
      this.starting = false;
    }
  }

  async disconnect() {
    if (this.retryTimer) window.clearTimeout(this.retryTimer);
    this.retryTimer = undefined;
    const connection = this.connection;
    this.connection = undefined;
    this.starting = false;
    this.connected.set(false);
    if (connection) await connection.stop();
  }

  markRead() { this.unreadCount.set(0); }
  clear() { this.items.set([]); this.unreadCount.set(0); }

  private scheduleReconnect() {
    if (this.retryTimer || !localStorage.getItem('ingestion.token')) return;
    this.retryTimer = window.setTimeout(() => {
      this.retryTimer = undefined;
      void this.connect();
    }, 3000);
  }
}
