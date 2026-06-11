import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../environments/environment';
import { AuthResponse } from './auth.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    clearCookie('mabuntle_csrf');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AuthService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
    sessionStorage.clear();
    clearCookie('mabuntle_csrf');
  });

  it('stores access token in memory after login without writing session storage', async () => {
    const loginPromise = service.login({
      email: 'buyer@example.test',
      password: 'Password123'
    });

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/auth/login`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    request.flush(createAuthResponse());

    await loginPromise;

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.currentUser()?.email).toBe('buyer@example.test');
    expect(service.accessToken).toBe('access-token');
    expect(sessionStorage.length).toBe(0);
  });

  it('clears auth state on logout and calls cookie logout with CSRF header', async () => {
    document.cookie = 'mabuntle_csrf=csrf-token; path=/';
    const loginPromise = service.login({
      email: 'buyer@example.test',
      password: 'Password123'
    });
    httpTestingController.expectOne(`${environment.apiBaseUrl}/api/auth/login`).flush(createAuthResponse());
    await loginPromise;

    const logoutPromise = service.logout();
    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/auth/logout`);
    expect(request.request.body).toEqual({});
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.headers.get('X-Mabuntle-CSRF')).toBe('csrf-token');
    request.flush(null);
    await logoutPromise;

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.accessToken).toBeNull();
  });

  it('restores a browser session by refreshing with credentials', async () => {
    document.cookie = 'mabuntle_csrf=csrf-token; path=/';

    const initializePromise = service.initialize();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/auth/refresh`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.headers.get('X-Mabuntle-CSRF')).toBe('csrf-token');
    request.flush(createAuthResponse());

    await initializePromise;

    expect(service.isInitialized()).toBeTrue();
    expect(service.isAuthenticated()).toBeTrue();
  });
});

function createAuthResponse(): AuthResponse {
  return {
    userId: '8df688c9-4bdd-40cc-b6f6-7bd3b7fba019',
    email: 'buyer@example.test',
    roles: ['Buyer'],
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: '2026-05-18T12:30:00+00:00'
  };
}

function clearCookie(name: string): void {
  document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
}
