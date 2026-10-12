import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, tap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginResponse } from './models';
import { ToastService } from './toast.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly userName = signal(localStorage.getItem('ingestion.user') ?? '');
  constructor(private readonly http: HttpClient, private readonly router: Router, private readonly toast: ToastService) {}
  login(userName: string, password: string) { return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, { userName, password }).pipe(tap(response => { this.store(response); this.toast.show('success', `Welcome back, ${response.userName}.`); }), catchError(error => { this.toast.show('error', error.error?.message ?? 'Invalid username or password.'); return throwError(() => error); })); }
  register(userName: string, password: string) { return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/register`, { userName, password }).pipe(tap(response => { this.store(response); this.toast.show('success', 'Registration successful.'); }), catchError(error => { this.toast.show('error', error.error?.message ?? 'Registration could not be completed.'); return throwError(() => error); })); }
  private store(response: LoginResponse) { localStorage.setItem('ingestion.token', response.accessToken); localStorage.setItem('ingestion.user', response.userName); localStorage.setItem('ingestion.role', response.role); this.userName.set(response.userName); }
  token() { return localStorage.getItem('ingestion.token'); }
  // Keep this check pure. It is called by guards and root effects; clearing
  // signals from here would violate Angular's signal-write rules.
  isAuthenticated() { const token = this.token(); if (!token) return false; try { const parts = token.split('.'); if (parts.length !== 3) return false; const payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'))); return !payload.exp || payload.exp * 1000 > Date.now(); } catch { return false; } }
  role() { const role = localStorage.getItem('ingestion.role') ?? 'Customer'; return role.trim().toLowerCase() === 'admin' ? 'Admin' : role; }
  clearSession() { localStorage.removeItem('ingestion.token'); localStorage.removeItem('ingestion.user'); localStorage.removeItem('ingestion.role'); this.userName.set(''); this.toast.clear(); }
  logout() { this.clearSession(); void this.router.navigate(['/login']); }
}
