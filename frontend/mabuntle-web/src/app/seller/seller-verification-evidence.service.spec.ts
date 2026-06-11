import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerVerificationEvidenceService } from './seller-verification-evidence.service';

describe('SellerVerificationEvidenceService', () => {
  let service: SellerVerificationEvidenceService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerVerificationEvidenceService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('lists seller verification evidence', async () => {
    const promise = service.list();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/verification-evidence`);
    expect(request.request.method).toBe('GET');
    request.flush([createEvidence()]);

    const response = await promise;
    expect(response[0].evidenceType).toBe('BusinessRegistration');
  });

  it('uploads multipart evidence payloads', async () => {
    const file = new File(['%PDF-1.4'], 'registration.pdf', { type: 'application/pdf' });
    const promise = service.upload(file, 'BusinessRegistration', 'CIPC registration');

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/verification-evidence/upload`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body instanceof FormData).toBeTrue();
    request.flush(createEvidence());

    await expectAsync(promise).toBeResolved();
  });

  it('removes and downloads evidence', async () => {
    const removePromise = service.remove('evidence-id');
    const removeRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/verification-evidence/evidence-id`);
    expect(removeRequest.request.method).toBe('DELETE');
    removeRequest.flush(null);

    const downloadPromise = service.download('evidence-id');
    const downloadRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/verification-evidence/evidence-id/download`);
    expect(downloadRequest.request.method).toBe('GET');
    expect(downloadRequest.request.responseType).toBe('blob');
    downloadRequest.flush(new Blob(['evidence'], { type: 'application/pdf' }));

    await expectAsync(removePromise).toBeResolved();
    await expectAsync(downloadPromise).toBeResolved();
  });
});

function createEvidence() {
  return {
    evidenceId: 'evidence-id',
    evidenceType: 'BusinessRegistration',
    originalFileName: 'registration.pdf',
    contentType: 'application/pdf',
    byteSize: 1234,
    sha256Hash: 'hash',
    note: 'CIPC registration',
    uploadedAtUtc: '2026-05-26T10:00:00Z',
    removedAtUtc: null
  };
}
