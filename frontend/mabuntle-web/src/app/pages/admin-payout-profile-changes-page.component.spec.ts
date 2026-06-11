import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminPayoutProfileChangeRequestResponse } from '../admin/admin-payout-profile-change.models';
import { AdminPayoutProfileChangeService } from '../admin/admin-payout-profile-change.service';
import { AuthRole } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { AdminPayoutProfileChangesPageComponent } from './admin-payout-profile-changes-page.component';

describe('AdminPayoutProfileChangesPageComponent', () => {
  let fixture: ComponentFixture<AdminPayoutProfileChangesPageComponent>;
  let currentRoles: AuthRole[];
  let service: jasmine.SpyObj<AdminPayoutProfileChangeService>;

  beforeEach(async () => {
    currentRoles = ['FinanceApprover'];
    service = jasmine.createSpyObj<AdminPayoutProfileChangeService>(
      'AdminPayoutProfileChangeService',
      ['list', 'get', 'approve', 'reject']);
    service.list.and.resolveTo([createRequest()]);
    service.get.and.resolveTo(createRequest());
    service.approve.and.resolveTo(createRequest({ status: 'Approved', reviewReason: 'Verified.' }));
    service.reject.and.resolveTo(createRequest({ status: 'Rejected', reviewReason: 'Could not verify.' }));

    await TestBed.configureTestingModule({
      imports: [AdminPayoutProfileChangesPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminPayoutProfileChangeService, useValue: service },
        {
          provide: AuthService,
          useValue: {
            hasAnyRole: (roles: readonly AuthRole[]) => roles.some(role => currentRoles.includes(role))
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminPayoutProfileChangesPageComponent);
  });

  it('renders the queue and submits an approval payload', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Payout profile changes');
    expect(compiled.textContent).toContain('provider-ref-current');
    expect(compiled.textContent).toContain('provider-ref-next');

    const component = fixture.componentInstance as unknown as {
      reviewForm: { setValue(value: { reason: string }): void };
    };
    component.reviewForm.setValue({ reason: 'Verified.' });
    clickButton(compiled, 'Approve');
    await fixture.whenStable();

    expect(service.approve).toHaveBeenCalledWith('request-id', { reason: 'Verified.' });
  });

  it('disables review actions for read-only finance users', async () => {
    currentRoles = ['FinanceOperator'];

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Read only');
    expect(findButton(compiled, 'Approve')?.disabled).toBeTrue();
    expect(findButton(compiled, 'Reject')?.disabled).toBeTrue();
  });
});

function createRequest(
  overrides: Partial<AdminPayoutProfileChangeRequestResponse> = {}
): AdminPayoutProfileChangeRequestResponse {
  return {
    requestId: 'request-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Luxe Seller',
    sellerContactEmail: 'seller@example.test',
    sellerVerificationStatus: 'Verified',
    currentPayoutProviderReference: 'provider-ref-current',
    currentPayoutIsAdminApproved: true,
    proposedPayoutProviderReference: 'provider-ref-next',
    reason: 'Updated payout provider reference.',
    status: 'PendingReview',
    requestedByUserId: 'requester-user-id',
    submittedAtUtc: '2026-05-21T10:00:00Z',
    cancelledAtUtc: null,
    reviewedByUserId: null,
    reviewedAtUtc: null,
    reviewReason: null,
    createdAtUtc: '2026-05-21T09:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z',
    ...overrides
  };
}

function clickButton(compiled: HTMLElement, text: string): void {
  findButton(compiled, text)?.dispatchEvent(new Event('click'));
}

function findButton(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
  return Array.from(compiled.querySelectorAll('button'))
    .find(button => button.textContent?.includes(text)) as HTMLButtonElement | undefined;
}
