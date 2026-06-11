import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { SellerSupportTicketResponse } from '../seller/seller-support.models';
import { SellerSupportService } from '../seller/seller-support.service';
import { SellerSupportPageComponent } from './seller-support-page.component';

describe('SellerSupportPageComponent', () => {
  let fixture: ComponentFixture<SellerSupportPageComponent>;
  let router: Router;
  let supportService: jasmine.SpyObj<SellerSupportService>;

  beforeEach(async () => {
    supportService = jasmine.createSpyObj<SellerSupportService>('SellerSupportService', ['listTickets', 'createTicket']);
    supportService.listTickets.and.resolveTo([createSupportTicket()]);
    supportService.createTicket.and.resolveTo(createSupportTicket({ supportTicketId: 'new-ticket-id' }));

    await TestBed.configureTestingModule({
      imports: [SellerSupportPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerSupportService, useValue: supportService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(SellerSupportPageComponent);
  });

  it('loads tickets and creates a new support ticket', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      ticketForm: {
        setValue(value: {
          category: string;
          subject: string;
          description: string;
          linkedOrderId: string;
          linkedProductId: string;
        }): void;
      };
    };
    component.ticketForm.setValue({
      category: 'OrderIssue',
      subject: 'Order question',
      description: 'Please check this order.',
      linkedOrderId: 'order-id',
      linkedProductId: ''
    });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form');
    form?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(supportService.createTicket).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/support', 'new-ticket-id']);
  });
});

export function createSupportTicket(
  overrides: Partial<SellerSupportTicketResponse> = {}
): SellerSupportTicketResponse {
  return {
    supportTicketId: 'ticket-id',
    createdByUserId: 'user-id',
    createdByRole: 'Seller',
    buyerId: null,
    sellerId: 'seller-id',
    category: 'OrderIssue',
    status: 'Open',
    subject: 'Order help',
    description: 'Need help with an order.',
    linkedOrderId: 'order-id',
    linkedProductId: null,
    linkedSellerId: null,
    linkedPaymentId: null,
    assignedSupportUserId: null,
    openedAtUtc: '2026-05-19T10:00:00Z',
    resolvedAtUtc: null,
    closedAtUtc: null,
    messages: [],
    ...overrides
  };
}
