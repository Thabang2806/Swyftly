import { ActivatedRoute } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { RegisterPageComponent } from './register-page.component';

describe('RegisterPageComponent', () => {
  let fixture: ComponentFixture<RegisterPageComponent>;
  let authService: jasmine.SpyObj<Pick<AuthService, 'register'>>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj('AuthService', ['register']);
    authService.register.and.resolveTo({
      userId: 'seller-user-id',
      email: 'seller@example.test',
      role: 'Seller',
      sellerVerificationStatus: 'Draft',
      emailVerificationRequired: false
    });

    await TestBed.configureTestingModule({
      imports: [RegisterPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { role: 'Seller' } } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterPageComponent);
  });

  it('renders seller onboarding context and links back to sell landing', () => {
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Create seller account');
    expect(compiled.textContent).toContain('complete onboarding');
    expect(compiled.querySelector('a[href="https://mabuntle.com/sell"]')?.textContent).toContain('Review seller requirements');
  });

  it('submits the existing seller registration payload and shows success-to-login state', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const inputs = compiled.querySelectorAll('input');

    setInputValue(inputs[0], 'seller@example.test');
    setInputValue(inputs[1], 'Password123');
    setInputValue(inputs[2], 'Password123');
    compiled.querySelector('form')?.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(authService.register).toHaveBeenCalledWith({
      email: 'seller@example.test',
      password: 'Password123',
      role: 'Seller'
    });
    expect(compiled.textContent).toContain('Seller account created. You can sign in now.');
    expect(compiled.querySelector('a[href="/login"]')?.textContent).toContain('Sign in');
  });
});

function setInputValue(input: Element, value: string): void {
  const element = input as HTMLInputElement;
  element.value = value;
  element.dispatchEvent(new Event('input'));
}
