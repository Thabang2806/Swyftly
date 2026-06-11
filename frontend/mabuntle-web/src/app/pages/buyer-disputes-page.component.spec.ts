import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerDisputeResponse } from '../buyer/buyer-dispute.models';
import { BuyerDisputeService } from '../buyer/buyer-dispute.service';
import { BuyerDisputesPageComponent } from './buyer-disputes-page.component';

describe('BuyerDisputesPageComponent', () => {
  let fixture: ComponentFixture<BuyerDisputesPageComponent>;
  let disputeService: jasmine.SpyObj<BuyerDisputeService>;

  beforeEach(async () => {
    disputeService = jasmine.createSpyObj<BuyerDisputeService>('BuyerDisputeService', ['listDisputes', 'addMessage', 'addEvidence']);
    disputeService.listDisputes.and.resolveTo([createDispute()]);
    disputeService.addMessage.and.resolveTo(createDispute({ messages: [{
      disputeMessageId: 'message-id',
      senderUserId: 'buyer-user-id',
      senderRole: 'Buyer',
      message: 'Adding detail.',
      createdAtUtc: '2026-05-18T13:00:00Z'
    }]}));
    disputeService.addEvidence.and.resolveTo(createDispute({ evidence: [{
      disputeEvidenceId: 'evidence-id',
      submittedByUserId: 'buyer-user-id',
      submittedByRole: 'Buyer',
      evidenceType: 'Image',
      storageReference: 'https://example.test/photo.jpg',
      description: 'Photo',
      createdAtUtc: '2026-05-18T13:00:00Z'
    }]}));

    await TestBed.configureTestingModule({
      imports: [BuyerDisputesPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerDisputeService, useValue: disputeService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerDisputesPageComponent);
  });

  it('adds buyer dispute messages and evidence', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      messageForm: { patchValue: (value: unknown) => void };
      evidenceForm: { patchValue: (value: unknown) => void };
    };
    component.messageForm.patchValue({ message: 'Adding detail.' });
    ((fixture.nativeElement as HTMLElement).querySelectorAll('form')[0] as HTMLFormElement)
      .dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    component.evidenceForm.patchValue({
      evidenceType: 'Image',
      storageReference: 'https://example.test/photo.jpg',
      description: 'Photo'
    });
    ((fixture.nativeElement as HTMLElement).querySelectorAll('form')[1] as HTMLFormElement)
      .dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(disputeService.addMessage).toHaveBeenCalledWith('dispute-id', { message: 'Adding detail.' });
    expect(disputeService.addEvidence).toHaveBeenCalledWith('dispute-id', {
      evidenceType: 'Image',
      storageReference: 'https://example.test/photo.jpg',
      description: 'Photo'
    });
  });

  it('explains that return escalations are not always standalone disputes', async () => {
    disputeService.listDisputes.and.resolveTo([]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Standalone dispute cases will appear here');
    expect(text).toContain('Return escalations stay on the related return');
    expect(text).toContain('refund outcome appears under Refunds');
  });
});

function createDispute(overrides: Partial<BuyerDisputeResponse> = {}): BuyerDisputeResponse {
  return {
    disputeId: 'dispute-id',
    orderId: 'order-id',
    returnRequestId: 'return-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    status: 'Open',
    reason: 'Need review',
    openedAtUtc: '2026-05-18T12:00:00Z',
    resolvedAtUtc: null,
    resolutionReason: null,
    messages: [],
    evidence: [],
    ...overrides
  };
}
