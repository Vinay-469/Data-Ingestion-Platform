import { inject } from '@angular/core';
import { CanActivateFn, Routes, Router } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { AuthService } from './core/auth.service';
import { LoginComponent } from './pages/login/login.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { HistoryComponent } from './pages/history/history.component';
import { ViewerComponent } from './pages/viewer/viewer.component';
import { ExplorerComponent } from './pages/explorer/explorer.component';

const guestGuard: CanActivateFn = () => { const auth = inject(AuthService); return auth.isAuthenticated() ? inject(Router).createUrlTree(['/dashboard']) : true; };

export const routes: Routes = [
  { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'history', component: HistoryComponent, canActivate: [authGuard] },
  { path: 'explorer', component: ExplorerComponent, canActivate: [authGuard] },
  { path: 'viewer/:uploadId', component: ViewerComponent, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: '**', redirectTo: 'dashboard' }
];
