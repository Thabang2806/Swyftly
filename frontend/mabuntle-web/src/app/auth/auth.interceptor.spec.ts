import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { authTokenInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authTokenInterceptor', () => {
  let httpClient: HttpClient;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authTokenInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            accessToken: 'access-token'
          }
        }
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('adds the bearer token header only for the Mabuntle API origin', () => {
    httpClient.get(`${environment.apiBaseUrl}/api/example`).subscribe();
    httpClient.get('https://payments.example.test/session').subscribe();
    httpClient.get(`${environment.apiBaseUrl}.evil.test/api/example`).subscribe();

    const apiRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/example`);
    expect(apiRequest.request.headers.get('Authorization')).toBe('Bearer access-token');
    apiRequest.flush({});

    const externalRequest = httpTestingController.expectOne('https://payments.example.test/session');
    expect(externalRequest.request.headers.has('Authorization')).toBeFalse();
    externalRequest.flush({});

    const deceptivePrefixRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}.evil.test/api/example`);
    expect(deceptivePrefixRequest.request.headers.has('Authorization')).toBeFalse();
    deceptivePrefixRequest.flush({});
  });
});
