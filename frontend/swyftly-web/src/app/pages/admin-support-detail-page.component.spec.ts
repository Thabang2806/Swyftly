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
      ['getTicket', 'addPublicMessage', 'addInternalNote', 'resolveTicket', 'closeTicket']);
    supportService.getTicket.and.resolveTo(createAdminSupportTicket({
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
    forms[0]?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    component.internalNoteForm.setValue({ message: 'Delivery evidence needed.' });
    forms[1]?.dispatchEvent(new Event('submit'));
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
});

function clickButton(compiled: HTMLElement, text: string): void {
  Array.from(compiled.querySelectorAll('button'))
    .find(button => button.textContent?.includes(text))
    ?.dispatchEvent(new Event('click'));
}
