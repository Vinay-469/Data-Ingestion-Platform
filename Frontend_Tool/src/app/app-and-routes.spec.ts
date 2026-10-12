import { AppComponent } from './app.component';
import { appConfig } from './app.config';
import { routes } from './app.routes';
import { TestBed } from '@angular/core/testing';

describe('application configuration', () => {
  it('defines the root component dependencies and routes', () => {
    const theme = {};
    const loading = {};
    const component = TestBed.runInInjectionContext(() => new AppComponent(theme as any, loading as any, { userName: () => null } as any, { connect: () => Promise.resolve(), disconnect: () => Promise.resolve(), markRead: () => undefined } as any, {} as any, {} as any));
    expect(component.title).toBe('frontend-tool');
    expect(component.theme).toBe(theme as any);
    expect(component.loading).toBe(loading as any);
    expect(routes.find(route => route.path === 'login')?.canActivate?.length).toBe(1);
    expect(routes.find(route => route.path === 'dashboard')?.canActivate?.length).toBe(1);
    expect(routes.find(route => route.path === '')?.redirectTo).toBe('dashboard');
    expect(routes.find(route => route.path === '**')?.redirectTo).toBe('dashboard');
  });

  it('provides router and HTTP interceptor configuration', () => {
    expect(appConfig.providers?.length).toBeGreaterThan(0);
  });
});
