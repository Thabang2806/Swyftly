import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { environment } from '../../environments/environment';
import { LoginPageComponent } from './login-page.component';

describe('LoginPageComponent', () => {
  let fixture: ComponentFixture<LoginPageComponent>;
  let httpTestingController: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [LoginPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginPageComponent);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
    sessionStorage.clear();
  });

  it('displays backend login errors clearly', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const inputs = compiled.querySelectorAll('input');
    setInputValue(inputs[0], 'buyer@example.test');
    setInputValue(inputs[1], 'Password123');

    compiled.querySelector('form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/auth/login`);
    request.flush({
      title: 'Identity.InvalidCredentials',
      detail: 'The email address or password is incorrect.'
    }, {
      status: 401,
      statusText: 'Unauthorized'
    });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(compiled.querySelector('.auth-alert.error')?.textContent)
      .toContain('The email address or password is incorrect.');
  });
});

function setInputValue(input: Element, value: string): void {
  const element = input as HTMLInputElement;
  element.value = value;
  element.dispatchEvent(new Event('input'));
}
