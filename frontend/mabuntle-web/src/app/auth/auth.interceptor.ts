import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export const authTokenInterceptor: HttpInterceptorFn = (request, next) => {
  const accessToken = inject(AuthService).accessToken;

  if (!accessToken || !isMabuntleApiRequest(request.url)) {
    return next(request);
  }

  return next(request.clone({
    setHeaders: {
      Authorization: `Bearer ${accessToken}`
    }
  }));
};

export function isMabuntleApiRequest(requestUrl: string): boolean {
  try {
    const apiUrl = new URL(environment.apiBaseUrl);
    const url = new URL(requestUrl, apiUrl.origin);
    if (url.origin !== apiUrl.origin) {
      return false;
    }

    const apiPath = normalizePath(apiUrl.pathname);
    if (apiPath === '/') {
      return true;
    }

    return url.pathname === apiPath.slice(0, -1) || url.pathname.startsWith(apiPath);
  } catch {
    return false;
  }
}

function normalizePath(pathname: string): string {
  if (!pathname || pathname === '/') {
    return '/';
  }

  return pathname.endsWith('/') ? pathname : `${pathname}/`;
}
