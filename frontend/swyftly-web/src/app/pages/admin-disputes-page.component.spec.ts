import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminDisputeResponse } from '../admin/admin-dispute.models';
import { AdminDisputeService } from '../admin/admin-dispute.service';
import { AdminDisputesPageComponent } from './admin-disputes-page.component';

describe('AdminDisputesPageComponent', () => {
  let fixture: ComponentFixture<AdminDisputesPageComponent>;
  let disputeService: jasmine.SpyObj<AdminDisputeService>;

  beforeEach(async () => {
    disputeService = jasmine.createSpyObj<AdminDisputeService>('AdminDisputeService', ['getDisputes', 'resolveDispute']);
    disputeService.getDisputes.and.resolveTo([createDispute()]);
    disputeService.resolveDispute.and.resolveTo(createDispute({
      status: 'Resolved',
      resolutionReason: 'Evidence supports seller.',
      resolvedAtUtc: '2026-05-19T11:00:00Z'
    }));

    await TestBed.configureTestingModule({
      imports: [AdminDisputesPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminDisputeService, useValue: disputeService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminDisputesPageComponent);
  });

  it('renders dispute evidence and resolves a dispute', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('Damaged item');

    const reviewButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Review'));
    reviewButton?.dispatchEvent(new Event('click'));
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Buyer message');
    expect(compiled.textContent).toContain('photo.jpg');

    const component = fixture.componentInstance as unknown as {
      resolveForm: { setValue(value: { outcome: 'BuyerFavoured' | 'SellerFavoured'; reason: string }): void };
    };
    component.resolveForm.setValue({
      outcome: 'SellerFavoured',
      reason: 'Evidence supports seller.'
    });

    const form = compiled.querySelector('form');
    form?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(disputeService.resolveDispute).toHaveBeenCalledWith('dispute-id', {
      outcome: 'SellerFavoured',
      reason: 'Evidence supports seller.'
    });
  });
});

export function createDispute(overrides: Partial<AdminDisputeResponse> = {}): AdminDisputeResponse {
  return {
    disputeId: 'dispute-id',
    orderId: 'order-id',
    returnRequestId: null,
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    status: 'Open',
    reason: 'Damaged item',
    openedAtUtc: '2026-05-19T10:00:00Z',
    resolvedAtUtc: null,
    resolutionReason: null,
    messages: [{
      disputeMessageId: 'message-id',
      senderUserId: 'buyer-user-id',
      senderRole: 'Buyer',
      message: 'Buyer message',
      createdAtUtc: '2026-05-19T10:05:00Z'
    }],
    evidence: [{
      disputeEvidenceId: 'evidence-id',
      submittedByUserId: 'buyer-user-id',
      submittedByRole: 'Buyer',
      evidenceType: 'Photo',
      storageReference: 'photo.jpg',
      description: 'Package damage',
      createdAtUtc: '2026-05-19T10:06:00Z'
    }],
    ...overrides
  };
}
