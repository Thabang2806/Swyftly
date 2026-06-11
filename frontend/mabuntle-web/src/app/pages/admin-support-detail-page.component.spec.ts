import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminSupportService } from '../admin/admin-support.service';
import { AdminSupportDetailPageComponent } from './admin-support-detail-page.component';
import { createAdminSupportTicket } from './admin-support-page.component.spec';

describe('AdminSupportDetailPageComponent', () => {
  let fixture: ComponentFixture<AdminSupportDetailPageComponent>;
  let supportService: jasmine.SpyObj<AdminSupportService>;

  beforeEach(async () => {
    supportService = jasmine.createSpyObj<AdminSupportService>(
      'AdminSupportService',
      ['getTicket', 'addPublicMessage', 'addInternalNote', 'resolveTicket', 'closeTicket', 'claimTicket', 'unclaimTicket', 'triageTicket', 'escalateTicket']);
    supportService.getTicket.and.resolveTo(createAdminSupportTicket({
      customerContext: {
        buyer: { buyerId: 'buyer-id', userId: 'buyer-user-id', displayName: 'Buyer One', email: 'buyer@example.test', phoneNumber: null },
        seller: null,
        order: { orderId: 'order-id', status: 'Paid', totalAmount: 599, createdAtUtc: '2026-05-19T10:00:00Z', buyerId: 'buyer-id', sellerId: 'seller-id', adminRoute: '/orders/order-id' },
        payment: null,
        product: null
      },
      messages: [
        createAdminSupportTicket().messages[0],
        {
          supportMessageId: 'internal-message-id',
          senderUserId: 'support-user-id',
          senderRole: 'SupportAgent',
          message: 'Check delivery evidence.',
          isInternal: true,
          createdAtUtc: '2026-05-19T10:05:00Z'
        }
      ]
    }));
    supportService.addPublicMessage.and.resolveTo(createAdminSupportTicket({ status: 'WaitingForCustomer' }));
    supportService.addInternalNote.and.resolveTo(createAdminSupportTicket());
    supportService.resolveTicket.and.resolveTo(createAdminSupportTicket({ status: 'Resolved' }));
    supportService.closeTicket.and.resolveTo(createAdminSupportTicket({ status: 'Closed' }));
    supportService.claimTicket.and.resolveTo(createAdminSupportTicket({ assignedSupportUserId: 'support-user-id' }));
    supportService.unclaimTicket.and.resolveTo(createAdminSupportTicket({ assignedSupportUserId: null }));
    supportService.triageTicket.and.resolveTo(createAdminSupportTicket({ priority: 'High' }));
    supportService.escalateTicket.and.resolveTo(createAdminSupportTicket({ status: 'Escalated', escalationReason: 'Needs senior review.' }));

    await TestBed.configureTestingModule({
      imports: [AdminSupportDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminSupportService, useValue: supportService },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ ticketId: 'ticket-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSupportDetailPageComponent);
  });

  it('renders public and internal messages', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('Damaged order');
    expect(compiled.textContent).toContain('Check delivery evidence.');
    expect(compiled.textContent).toContain('Internal');
    expect(compiled.textContent).toContain('Buyer One');
    expect(compiled.querySelector('a[href="/orders/order-id"]')).not.toBeNull();
  });

  it('sends public messages and internal notes', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      publicMessageForm: { setValue(value: { message: string }): void };
      internalNoteForm: { setValue(value: { message: string }): void };
    };
    component.publicMessageForm.setValue({ message: 'Please upload a photo.' });

    const forms = (fixture.nativeElement as HTMLElement).querySelectorAll('form');
    forms[2]?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    component.internalNoteForm.setValue({ message: 'Delivery evidence needed.' });
    forms[3]?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(supportService.addPublicMessage).toHaveBeenCalledWith('ticket-id', { message: 'Please upload a photo.' });
    expect(supportService.addInternalNote).toHaveBeenCalledWith('ticket-id', { message: 'Delivery evidence needed.' });
  });

  it('resolves and closes tickets', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    clickButton(fixture.nativeElement as HTMLElement, 'Resolve');
    clickButton(fixture.nativeElement as HTMLElement, 'Close');
    await fixture.whenStable();

    expect(supportService.resolveTicket).toHaveBeenCalledWith('ticket-id');
    expect(supportService.closeTicket).toHaveBeenCalledWith('ticket-id');
  });

  it('claims, triages, and escalates tickets', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    clickButton(fixture.nativeElement as HTMLElement, 'Claim');
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      triageForm: { setValue(value: { priority: string; internalNote: string }): void };
      escalationForm: { setValue(value: { reason: string }): void };
    };
    component.triageForm.setValue({ priority: 'High', internalNote: 'Escalate if no response.' });
    component.escalationForm.setValue({ reason: 'Needs senior review.' });

    const forms = (fixture.nativeElement as HTMLElement).querySelectorAll('form');
    forms[0]?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    forms[1]?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(supportService.claimTicket).toHaveBeenCalledWith('ticket-id');
    expect(supportService.triageTicket).toHaveBeenCalledWith('ticket-id', { priority: 'High', internalNote: 'Escalate if no response.' });
    expect(supportService.escalateTicket).toHaveBeenCalledWith('ticket-id', { reason: 'Needs senior review.' });
  });
});

function clickButton(compiled: HTMLElement, text: string): void {
  Array.from(compiled.querySelectorAll('button'))
    .find(button => button.textContent?.includes(text))
    ?.dispatchEvent(new Event('click'));
}
