import { UrlTree, provideRouter, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { AuthRole } from './auth.models';
import { AuthService } from './auth.service';
import { requireRoleGuard } from './auth.guard';

describe('requireRoleGuard', () => {
  let fakeAuthService: {
    initialize: jasmine.Spy<() => Promise<void>>;
    isAuthenticated: jasmine.Spy<() => boolean>;
    hasAnyRole: jasmine.Spy<(roles: readonly AuthRole[]) => boolean>;
  };

  beforeEach(() => {
    fakeAuthService = {
      initialize: jasmine.createSpy('initialize').and.resolveTo(),
      isAuthenticated: jasmine.createSpy('isAuthenticated'),
      hasAnyRole: jasmine.createSpy('hasAnyRole')
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: fakeAuthService }
      ]
    });
  });

  it('allows authenticated users with the required role', async () => {
    fakeAuthService.isAuthenticated.and.returnValue(true);
    fakeAuthService.hasAnyRole.and.returnValue(true);

    const result = await invokeGuard(['Buyer'], '/account');

    expect(result).toBeTrue();
    expect(fakeAuthService.hasAnyRole).toHaveBeenCalledWith(['Buyer']);
  });

  it('sends unauthenticated users to login with returnUrl', async () => {
    fakeAuthService.isAuthenticated.and.returnValue(false);
    fakeAuthService.hasAnyRole.and.returnValue(false);

    const result = await invokeGuard(['Seller'], '/seller');

    expect(serializeUrl(result)).toBe('/login?returnUrl=%2Fseller');
  });

  it('blocks non-admins from admin routes', async () => {
    fakeAuthService.isAuthenticated.and.returnValue(true);
    fakeAuthService.hasAnyRole.and.returnValue(false);

    const result = await invokeGuard(['Admin', 'SuperAdmin'], '/admin');

    expect(serializeUrl(result)).toBe('/access-denied');
  });

  async function invokeGuard(roles: readonly AuthRole[], url: string): Promise<boolean | UrlTree> {
    return await TestBed.runInInjectionContext(async () => {
      const guard = requireRoleGuard(roles);
      return await guard({} as never, { url } as never) as boolean | UrlTree;
    });
  }

  function serializeUrl(result: boolean | UrlTree): string {
    expect(result instanceof UrlTree).toBeTrue();
    return TestBed.inject(Router).serializeUrl(result as UrlTree);
  }
});
