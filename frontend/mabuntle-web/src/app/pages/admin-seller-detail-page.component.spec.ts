import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AdminSellerDetailResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { AdminSellerDetailPageComponent } from './admin-seller-detail-page.component';
import { createSellerPolicy } from './shop-page.component.spec';

describe('AdminSellerDetailPageComponent', () => {
  let fixture: ComponentFixture<AdminSellerDetailPageComponent>;
  let adminSellerService: jasmine.SpyObj<AdminSellerService>;

  beforeEach(async () => {
    adminSellerService = jasmine.createSpyObj<AdminSellerService>(
      'AdminSellerService',
      ['getSeller', 'approveSeller', 'rejectSeller', 'suspendSeller', 'downloadVerificationEvidence']);
    adminSellerService.getSeller.and.resolveTo(createSellerDetail());
    adminSellerService.approveSeller.and.resolveTo(createSellerDetail({
      verificationStatus: 'Verified',
      payout: {
        payoutProviderReference: 'provider-ref-123',
        hasSubmittedPlaceholder: true,
        isAdminApproved: true
      },
      auditTrail: [{
        id: 'audit-id',
        actionType: 'SellerApproved',
        actorUserId: 'admin-id',
        actorRole: 'Admin',
        reason: null,
        createdAtUtc: '2026-05-18T12:30:00Z'
      }]
    }));
    adminSellerService.rejectSeller.and.resolveTo(createSellerDetail({
      verificationStatus: 'Rejected',
      auditTrail: [{
        id: 'audit-rejected',
        actionType: 'SellerRejected',
        actorUserId: 'admin-id',
        actorRole: 'Admin',
        reason: 'Business details do not match.',
        createdAtUtc: '2026-05-18T12:35:00Z'
      }]
    }));
    adminSellerService.suspendSeller.and.resolveTo(createSellerDetail({
      verificationStatus: 'Suspended',
      auditTrail: [{
        id: 'audit-suspended',
        actionType: 'SellerSuspended',
        actorUserId: 'admin-id',
        actorRole: 'Admin',
        reason: 'Policy issue.',
        createdAtUtc: '2026-05-18T12:40:00Z'
      }]
    }));
    adminSellerService.downloadVerificationEvidence.and.resolveTo(new Blob(['evidence'], { type: 'application/pdf' }));

    await TestBed.configureTestingModule({
      imports: [AdminSellerDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ sellerId: 'seller-id' })
            }
          }
        },
        { provide: AdminSellerService, useValue: adminSellerService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSellerDetailPageComponent);
  });

  it('loads seller review details and audit trail', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('RegisteredBusiness');
    expect(compiled.textContent).toContain('Review completeness');
    expect(compiled.textContent).toContain('Profile');
    expect(compiled.textContent).toContain('Store policies');
    expect(compiled.textContent).toContain('Returns are reviewed');
    expect(compiled.textContent).toContain('Verification evidence');
    expect(compiled.textContent).toContain('registration.pdf');
    expect(compiled.textContent).toContain('SellerSubmitted');
  });

  it('shows missing completeness indicators for incomplete onboarding data', async () => {
    adminSellerService.getSeller.and.resolveTo(createSellerDetail({
      phoneNumber: null,
      payout: null
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Review completeness');
    expect(compiled.textContent).toContain('Missing');
    expect(compiled.textContent).toContain('Payout setup');
  });

  it('approves the loaded seller and displays success state', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const approveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Approve seller'));
    approveButton?.dispatchEvent(new Event('click'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminSellerService.approveSeller).toHaveBeenCalledWith('seller-id');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Seller approved.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('SellerApproved');
  });

  it('rejects the loaded seller with the unchanged reason payload', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const rejectTextarea = ((fixture.nativeElement as HTMLElement).querySelectorAll('textarea[formControlName="reason"]')[0]) as HTMLTextAreaElement;
    rejectTextarea.value = 'Business details do not match.';
    rejectTextarea.dispatchEvent(new Event('input'));
    rejectTextarea.closest('form')?.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminSellerService.rejectSeller).toHaveBeenCalledWith('seller-id', { reason: 'Business details do not match.' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Seller rejected.');
  });

  it('suspends the loaded seller with the unchanged reason payload', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const suspendTextarea = ((fixture.nativeElement as HTMLElement).querySelectorAll('textarea[formControlName="reason"]')[1]) as HTMLTextAreaElement;
    suspendTextarea.value = 'Policy issue.';
    suspendTextarea.dispatchEvent(new Event('input'));
    suspendTextarea.closest('form')?.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminSellerService.suspendSeller).toHaveBeenCalledWith('seller-id', { reason: 'Policy issue.' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Seller suspended.');
  });

  it('downloads seller verification evidence from the review panel', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const downloadButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Download'));
    downloadButton?.dispatchEvent(new Event('click'));

    await fixture.whenStable();

    expect(adminSellerService.downloadVerificationEvidence).toHaveBeenCalledWith('seller-id', 'evidence-id');
  });
});

function createSellerDetail(overrides: Partial<AdminSellerDetailResponse> = {}): AdminSellerDetailResponse {
  return {
    sellerId: 'seller-id',
    userId: 'user-id',
    verificationStatus: 'UnderReview',
    displayName: 'Seller Store',
    contactEmail: 'seller@example.test',
    phoneNumber: '+27110000000',
    businessType: 'RegisteredBusiness',
    businessName: 'Seller Trading',
    storefront: {
      storeName: 'Seller Store',
      slug: 'seller-store',
      description: 'Seller storefront',
      logoUrl: null,
      bannerUrl: null,
      isPublished: false
    },
    address: {
      addressLine1: '1 Market Street',
      addressLine2: null,
      city: 'Johannesburg',
      province: 'Gauteng',
      postalCode: '2000',
      countryCode: 'ZA'
    },
    payout: {
      payoutProviderReference: 'provider-ref-123',
      hasSubmittedPlaceholder: true,
      isAdminApproved: false
    },
    storePolicy: createSellerPolicy(),
    verificationEvidence: [{
      evidenceId: 'evidence-id',
      evidenceType: 'BusinessRegistration',
      originalFileName: 'registration.pdf',
      contentType: 'application/pdf',
      byteSize: 1234,
      sha256Hash: 'hash',
      note: 'CIPC registration document',
      uploadedAtUtc: '2026-05-26T10:00:00Z',
      removedAtUtc: null
    }],
    auditTrail: [{
      id: 'audit-id',
      actionType: 'SellerSubmitted',
      actorUserId: 'seller-id',
      actorRole: 'Seller',
      reason: null,
      createdAtUtc: '2026-05-18T12:00:00Z'
    }],
    ...overrides
  };
}
