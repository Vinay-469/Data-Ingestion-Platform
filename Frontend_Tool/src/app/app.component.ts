import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/theme.service';
import { LoadingService } from './core/loading.service';
import { AuthService } from './core/auth.service';
import { NotificationService } from './core/notification.service';
import { ToastService } from './core/toast.service';
import { effect } from '@angular/core';
import { UploadProgressPanelComponent } from './shared/upload-progress-panel/upload-progress-panel.component';
import { Router } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, UploadProgressPanelComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'frontend-tool';
  constructor(readonly theme: ThemeService, readonly loading: LoadingService, readonly auth: AuthService, readonly notifications: NotificationService, readonly toast: ToastService, private readonly router: Router) {
    effect(() => {
      if (auth.userName() && auth.isAuthenticated()) void notifications.connect();
      else { void notifications.disconnect(); notifications.clear(); toast.clear(); if (router.url !== '/login') void router.navigate(['/login']); }
    }, { allowSignalWrites: true });
  }

}
