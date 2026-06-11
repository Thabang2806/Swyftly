import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthRole } from './auth.models';
import { AuthService } from './auth.service';

export function requireRoleGuard(allowedRoles: readonly AuthRole[]): CanActivateFn {
  return async (_route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    await authService.initialize();

    if (!authService.isAuthenticated()) {
      return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url }
      });
    }

    if (authService.hasAnyRole(allowedRoles)) {
      return true;
    }

    return router.createUrlTree(['/access-denied']);
  };
}
