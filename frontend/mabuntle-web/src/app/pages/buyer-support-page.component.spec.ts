import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { BuyerSupportTicketResponse } from '../buyer/buyer-support.models';
import { BuyerSupportService } from '../buyer/buyer-support.service';
import { BuyerSupportPageComponent } from './buyer-support-page.component';

describe('BuyerSupportPageComponent', () => {
  let fixture: ComponentFixture<BuyerSupportPageComponent>;
  let supportService: jasmine.SpyObj<BuyerSupportService>;
  let router: Router;
  let queryParamMap$: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  async function setup(queryParams: Record<string, string> = {}): Promise<void> {
    supportService = jasmine.createSpyObj<BuyerSupportService>('BuyerSupportService', ['listTickets', 'createTicket']);
    supportService.listTickets.and.resolveTo([createTicket()]);
    supportService.createTicket.and.resolveTo(createTicket({ supportTicketId: 'created-ticket-id', subject: 'Created ticket' }));
    queryParamMap$ = new BehaviorSubject(convertToParamMap(queryParams));

    await TestBed.configureTestingModule({
      imports: [BuyerSupportPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            queryParamMap: queryParamMap$.asObservable(),
            snapshot: { queryParamMap: convertToParamMap(queryParams) }
          }
        },
        { provide: BuyerSupportService, useValue: supportService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(BuyerSupportPageComponent);
  }

  beforeEach(async () => {
    await setup();
  });

  it('prefills linked order and seller context from query params', async () => {
    TestBed.resetTestingModule();
    await setup({ orderId: 'order-id-123456', sellerId: 'seller-id' });
    supportService.listTickets.and.resolveTo([]);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      ticketForm: { getRawValue: () => { category: string; subject: string; linkedOrderId: string; linkedSellerId: string } };
    };
    const value = component.ticketForm.getRawValue();

    expect(value.category).toBe('OrderIssue');
    expect(value.subject).toBe('Question about order order-id');
    expect(value.linkedOrderId).toBe('order-id-123456');
    expect(value.linkedSellerId).toBe('seller-id');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('This ticket is linked to order order-id');
  });

  it('clears query-prefilled context when navigating back to plain support', async () => {
    TestBed.resetTestingModule();
    await setup({ orderId: 'order-id-123456', sellerId: 'seller-id' });
    supportService.listTickets.and.resolveTo([]);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      ticketForm: { getRawValue: () => { subject: string; linkedOrderId: string; linkedSellerId: string } };
    };

    queryParamMap$.next(convertToParamMap({}));
    fixture.detectChanges();

    expect(component.ticketForm.getRawValue().subject).toBe('');
    expect(component.ticketForm.getRawValue().linkedOrderId).toBe('');
    expect(component.ticketForm.getRawValue().linkedSellerId).toBe('');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('This ticket is linked to order');
  });

  it('creates a buyer support ticket with linked order context', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      ticketForm: { patchValue: (value: unknown) => void };
    };
    component.ticketForm.patchValue({
      category: 'OrderIssue',
      subject: 'Order arrived damaged',
      description: 'The box arrived damaged.',
      linkedOrderId: 'order-id',
      linkedSellerId: ''
    });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    await fixture.whenStable();

    expect(supportService.createTicket).toHaveBeenCalledWith({
      category: 'OrderIssue',
      subject: 'Order arrived damaged',
      description: 'The box arrived damaged.',
      linkedOrderId: 'order-id',
      linkedProductId: null,
      linkedSellerId: null,
      linkedPaymentId: null
    });
    expect(router.navigate).toHaveBeenCalledWith(['/account/support', 'created-ticket-id']);
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
