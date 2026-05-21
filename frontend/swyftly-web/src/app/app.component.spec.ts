import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { AuthRole } from './auth/auth.models';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  let authService: jasmine.SpyObj<Pick<AuthService, 'initialize' | 'logout' | 'hasAnyRole' | 'isAuthenticated'>>;

  beforeEach(async () => {
    sessionStorage.clear();
    authService = jasmine.createSpyObj('AuthService', ['initialize', 'logout', 'hasAnyRole', 'isAuthenticated']);
    authService.initialize.and.resolveTo();
    authService.logout.and.resolveTo();
    authService.hasAnyRole.and.returnValue(false);
    authService.isAuthenticated.and.returnValue(false);

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AuthService, useValue: authService }
      ]
    }).compileComponents();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the brand navigation', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('Swyftly');
    expect(compiled.querySelector('.brand-mark')?.textContent?.trim()).toBe('S');
    expect(compiled.querySelector('.header-search')?.textContent).toContain('Search fashion');
    expect(compiled.querySelector('.nav-link--featured')?.textContent).toContain('AI Style Finder');
    expect(compiled.querySelector('app-mobile-bottom-nav')).not.toBeNull();
  });

  it('should prioritize admin mobile navigation for multi-role users', () => {
    authService.hasAnyRole.and.callFake((roles: readonly AuthRole[]) => roles.includes('Admin') || roles.includes('Buyer'));
    authService.isAuthenticated.and.returnValue(true);

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const labels = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.hf-mobile-bottom-nav a'))
      .map(anchor => anchor.textContent?.trim());

    expect(labels).toEqual(['Home', 'Search', 'Admin', 'Queues', 'Finance']);
  });
});
