import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpRequest, provideHttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { authGuard } from './auth.guard';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { LoadingService } from './loading.service';
import { CellValuePipe } from './cell-value.pipe';
import { ThemeService } from './theme.service';

describe('frontend security and UI utilities', () => {
  it('allows authenticated users and redirects anonymous users', () => {
    const auth = { isAuthenticated: jasmine.createSpy('isAuthenticated') };
    const router = { createUrlTree: jasmine.createSpy('createUrlTree').and.returnValue('login-tree') };
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }, { provide: Router, useValue: router }] });
    auth.isAuthenticated.and.returnValue(true);
    expect(TestBed.runInInjectionContext(() => authGuard({} as never, {} as never))).toBeTrue();
    auth.isAuthenticated.and.returnValue(false);
    expect(TestBed.runInInjectionContext(() => authGuard({} as never, {} as never)) as any).toBe('login-tree');
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
  });

  it('adds a bearer token and starts/stops loading for regular, AI and upload calls', () => {
    const auth = { token: jasmine.createSpy('token').and.returnValue('abc'), clearSession: jasmine.createSpy('clearSession') };
    const loading = { start: jasmine.createSpy('start'), stop: jasmine.createSpy('stop') };
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }, { provide: LoadingService, useValue: loading }, provideHttpClient()] });
    const next = jasmine.createSpy('next').and.returnValue(of('ok'));
    const request = new HttpRequest('POST', '/api/uploads/validate', {});
    TestBed.runInInjectionContext(() => authInterceptor(request, next)).subscribe();
    const forwarded = next.calls.mostRecent().args[0] as HttpRequest<unknown>;
    expect(forwarded.headers.get('Authorization')).toBe('Bearer abc');
    expect(loading.start).not.toHaveBeenCalled();
    expect(loading.stop).not.toHaveBeenCalled();

    const aiRequest = new HttpRequest('GET', '/api/ai/chat/1');
    TestBed.runInInjectionContext(() => authInterceptor(aiRequest, next)).subscribe();
    expect(loading.start).toHaveBeenCalledWith(true, false);
    expect(loading.stop).toHaveBeenCalledWith(true, false);
  });

  it('clears the session on unauthorized non-auth requests and preserves auth errors', () => {
    const auth = { token: jasmine.createSpy('token').and.returnValue(null), clearSession: jasmine.createSpy('clearSession') };
    const loading = { start: jasmine.createSpy('start'), stop: jasmine.createSpy('stop') };
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }, { provide: LoadingService, useValue: loading }] });
    const error = new HttpErrorResponse({ status: 401, url: '/api/uploads' });
    const next = () => throwError(() => error);
    expect(() => TestBed.runInInjectionContext(() => authInterceptor(new HttpRequest('GET', '/api/uploads'), next)).subscribe({ error: () => undefined })).not.toThrow();
    expect(auth.clearSession).toHaveBeenCalled();
    auth.clearSession.calls.reset();
    TestBed.runInInjectionContext(() => authInterceptor(new HttpRequest('POST', '/api/auth/login', {}), next)).subscribe({ error: () => undefined });
    expect(auth.clearSession).not.toHaveBeenCalled();
    expect(loading.stop).toHaveBeenCalled();
  });

  it('formats cell values and applies/toggles the saved theme', () => {
    const pipe = new CellValuePipe();
    expect(pipe.transform([{ columnId: 1, columnName: 'A', value: ' value ', detectedDataType: 'string', validationStatus: 'Valid' }], 1)).toBe('value');
    expect(pipe.transform([], 1)).toBe('—');
    expect(pipe.transform([{ columnId: 1, columnName: 'A', value: '   ', detectedDataType: 'string', validationStatus: 'Valid' }], 1)).toBe('—');

    localStorage.setItem('ingestion.theme', 'dark');
    const theme = new ThemeService();
    expect(theme.isDark()).toBeTrue();
    expect(document.body.dataset['theme']).toBe('dark');
    theme.toggle();
    expect(theme.isDark()).toBeFalse();
    expect(localStorage.getItem('ingestion.theme')).toBe('light');
    expect(document.body.dataset['theme']).toBe('light');
    theme.toggle();
    expect(theme.isDark()).toBeTrue();
    expect(localStorage.getItem('ingestion.theme')).toBe('dark');
    localStorage.removeItem('ingestion.theme');
  });
});
