import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AuthResponse,
  AuthRole,
  AuthState,
  AuthTokens,
  AuthUser,
  LoginRequest,
  RegisterRequest,
  RegisterResponse
} from './auth.models';

const INITIAL_AUTH_STATE: AuthState = {
  currentUser: null,
  tokens: null,
  initialized: false,
  loading: false
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly state = signal<AuthState>(INITIAL_AUTH_STATE);
  private initializePromise: Promise<void> | null = null;

  readonly currentUser = computed(() => this.state().currentUser);
  readonly isAuthenticated = computed(() => this.currentUser() !== null && this.state().tokens !== null);
  readonly isInitialized = computed(() => this.state().initialized);
  readonly isLoading = computed(() => this.state().loading);

  get accessToken(): string | null {
    return this.state().tokens?.accessToken ?? null;
  }

  async initialize(): Promise<void> {
    if (this.state().initialized) {
      return;
    }

    this.initializePromise ??= this.tryRefresh();
    await this.initializePromise;
    this.initializePromise = null;
  }

  async register(request: RegisterRequest): Promise<RegisterResponse> {
    return await firstValueFrom(
      this.http.post<RegisterResponse>(this.authUrl('/register'), request)
    );
  }

  async login(request: LoginRequest): Promise<AuthResponse> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>(this.authUrl('/login'), request, { withCredentials: true })
    );

    this.setAuthenticatedSession(response);
    return response;
  }

  async logout(): Promise<void> {
    this.clearSession();

    try {
      await firstValueFrom(
        this.http.post<void>(this.authUrl('/logout'), {}, this.cookieAuthOptions())
      );
    } catch {
      // Local logout should not fail because the server-side token was already invalid.
    }
  }

  hasAnyRole(allowedRoles: readonly AuthRole[]): boolean {
    const roles = this.currentUser()?.roles ?? [];
    return allowedRoles.some(role => roles.includes(role));
  }

  private async tryRefresh(): Promise<void> {
    if (!this.readCookie('mabuntle_csrf')) {
      this.state.set({ ...INITIAL_AUTH_STATE, initialized: true });
      return;
    }

    this.state.set({
      currentUser: null,
      tokens: null,
      initialized: false,
      loading: true
    });

    try {
      const response = await firstValueFrom(
        this.http.post<AuthResponse>(this.authUrl('/refresh'), {}, this.cookieAuthOptions())
      );
      this.setAuthenticatedSession(response);
    } catch {
      this.clearSession(true);
    }
  }

  private setAuthenticatedSession(response: AuthResponse): void {
    const tokens: AuthTokens = {
      accessToken: response.accessToken,
      accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc
    };

    const currentUser: AuthUser = {
      userId: response.userId,
      email: response.email,
      roles: response.roles
    };

    this.setSession(tokens, currentUser, true);
  }

  private setSession(tokens: AuthTokens, currentUser: AuthUser, initialized: boolean): void {
    this.state.set({
      currentUser,
      tokens,
      initialized,
      loading: false
    });
  }

  private clearSession(initialized = this.state().initialized): void {
    this.state.set({
      ...INITIAL_AUTH_STATE,
      initialized
    });
  }

  private authUrl(path: string): string {
    return `${environment.apiBaseUrl}/api/auth${path}`;
  }

  private cookieAuthOptions(): { withCredentials: true; headers?: Record<string, string> } {
    const csrfToken = this.readCookie('mabuntle_csrf');
    return csrfToken
      ? { withCredentials: true, headers: { 'X-Mabuntle-CSRF': csrfToken } }
      : { withCredentials: true };
  }

  private readCookie(name: string): string | null {
    if (!this.isBrowser) {
      return null;
    }

    const encodedName = `${encodeURIComponent(name)}=`;
    const value = document.cookie
      .split(';')
      .map(cookie => cookie.trim())
      .find(cookie => cookie.startsWith(encodedName));

    return value ? decodeURIComponent(value.slice(encodedName.length)) : null;
  }

}
