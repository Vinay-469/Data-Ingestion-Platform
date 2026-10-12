import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  const router = { navigate: jasmine.createSpy('navigate') };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting(), { provide: Router, useValue: router }]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('stores the token and user details after login', () => {
    service.login('test-user', 'UnitTestPassword_123!').subscribe();
    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ userName: 'test-user', password: 'UnitTestPassword_123!' });
    request.flush({ accessToken: 'eyJhbGciOiJub25lIn0.eyJleHAiOjQxMDI0NDQ4MDAwMDB9.signature', userName: 'admin', role: 'Admin', expiresAt: '2026-01-01T00:00:00Z' });

    expect(service.token()).toContain('eyJhbGciOiJub25lIn0');
    expect(service.userName()).toBe('admin');
    expect(service.role()).toBe('Admin');
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('registers a user and stores the returned session', () => {
    service.register('new-user', 'UnitTestPassword_123!').subscribe();
    const request = http.expectOne('/api/auth/register');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ userName: 'new-user', password: 'UnitTestPassword_123!' });
    request.flush({ accessToken: 'new-token', userName: 'new-user', role: 'Customer', expiresAt: '2026-01-01T00:00:00Z' });
    expect(service.token()).toBe('new-token');
    expect(service.role()).toBe('Customer');
  });

  it('clears the session and navigates to login on logout', () => {
    localStorage.setItem('ingestion.token', 'token');
    localStorage.setItem('ingestion.user', 'admin');
    localStorage.setItem('ingestion.role', 'Admin');

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.userName()).toBe('');
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('uses Customer as the default role when no role is stored', () => {
    localStorage.removeItem('ingestion.role');
    expect(service.role()).toBe('Customer');
  });
});
