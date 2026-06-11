import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BuyerSupportTicketResponse } from '../buyer/buyer-support.models';
import { BuyerSupportService } from '../buyer/buyer-support.service';
import { BuyerSupportDetailPageComponent } from './buyer-support-detail-page.component';

describe('BuyerSupportDetailPageComponent', () => {
  let fixture: ComponentFixture<BuyerSupportDetailPageComponent>;
  let supportService: jasmine.SpyObj<BuyerSupportService>;

  beforeEach(async () => {
    supportService = jasmine.createSpyObj<BuyerSupportService>('BuyerSupportService', ['getTicket', 'addMessage']);
    supportService.getTicket.and.resolveTo(createTicket());
    supportService.addMessage.and.resolveTo(createTicket({ messages: [{
      supportMessageId: 'message-id',
      senderUserId: 'buyer-user-id',
      senderRole: 'Buyer',
      message: 'Following up.',
      isInternal: false,
      createdAtUtc: '2026-05-18T13:00:00Z'
    }]}));

    await TestBed.configureTestingModule({
      imports: [BuyerSupportDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerSupportService, useValue: supportService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ ticketId: 'ticket-id' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerSupportDetailPageComponent);
  });

  it('adds a buyer support message', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      messageForm: { patchValue: (value: unknown) => void };
    };
    component.messageForm.patchValue({ message: 'Following up.' });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(supportService.addMessage).toHaveBeenCalledWith('ticket-id', { message: 'Following up.' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Message sent.');
  });
});

function createTicket(overrides: Partial<BuyerSupportTicketResponse> = {}): BuyerSupportTicketResponse {
  return {
    supportTicketId: 'ticket-id',
    createdByUserId: 'buyer-user-id',
    createdByRole: 'Buyer',
    buyerId: 'buyer-id',
    sellerId: null,
    category: 'OrderIssue',
    status: 'Open',
    subject: 'Need help',
    description: 'Order help',
    linkedOrderId: 'order-id',
    linkedProductId: null,
    linkedSellerId: null,
    linkedPaymentId: null,
    assignedSupportUserId: null,
    openedAtUtc: '2026-05-18T12:00:00Z',
    resolvedAtUtc: null,
    closedAtUtc: null,
    messages: [],
    ...overrides
  };
}
