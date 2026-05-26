import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminSupportTicketResponse } from '../admin/admin-support.models';
import { AdminSupportService } from '../admin/admin-support.service';
import { AdminSupportPageComponent } from './admin-support-page.component';

describe('AdminSupportPageComponent', () => {
  let fixture: ComponentFixture<AdminSupportPageComponent>;
  let supportService: jasmine.SpyObj<AdminSupportService>;

  beforeEach(async () => {
    supportService = jasmine.createSpyObj<AdminSupportService>('AdminSupportService', ['listTickets']);
    supportService.listTickets.and.resolveTo([createAdminSupportTicket()]);

    await TestBed.configureTestingModule({
      imports: [AdminSupportPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminSupportService, useValue: supportService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSupportPageComponent);
  });

  it('loads support tickets and links to detail', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('Damaged order');
    expect(compiled.textContent).toContain('OrderIssue');
    expect(compiled.querySelector('a[href="/admin/support/ticket-id"]')).not.toBeNull();
  });

  it('filters tickets by status', async () => {
    supportService.listTickets.and.resolveTo([
      createAdminSupportTicket(),
      createAdminSupportTicket({ supportTicketId: 'resolved-ticket-id', subject: 'Closed case', status: 'Resolved' })
    ]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { setValue(value: { search: string; status: string; category: string }): void };
    };
    component.filtersForm.setValue({ search: '', status: 'Resolved', category: '' });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form');
    form?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Closed case');
    expect(compiled.textContent).not.toContain('Damaged order');
  });
});

export function createAdminSupportTicket(
  overrides: Partial<AdminSupportTicketResponse> = {}
): AdminSupportTicketResponse {
  return {
    supportTicketId: 'ticket-id',
    createdByUserId: 'buyer-user-id',
    createdByRole: 'Buyer',
    buyerId: 'buyer-id',
    sellerId: null,
    category: 'OrderIssue',
    status: 'Open',
    subject: 'Damaged order',
    description: 'The order arrived damaged.',
    linkedOrderId: 'order-id',
    linkedProductId: null,
    linkedSellerId: null,
    linkedPaymentId: null,
    assignedSupportUserId: null,
    openedAtUtc: '2026-05-19T10:00:00Z',
    resolvedAtUtc: null,
    closedAtUtc: null,
    messages: [{
      supportMessageId: 'message-id',
      senderUserId: 'buyer-user-id',
      senderRole: 'Buyer',
      message: 'The order arrived damaged.',
      isInternal: false,
      createdAtUtc: '2026-05-19T10:00:00Z'
    }],
    ...overrides
  };
}
