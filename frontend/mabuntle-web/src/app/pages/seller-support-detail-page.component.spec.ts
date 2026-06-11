import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerSupportService } from '../seller/seller-support.service';
import { SellerSupportDetailPageComponent } from './seller-support-detail-page.component';
import { createSupportTicket } from './seller-support-page.component.spec';

describe('SellerSupportDetailPageComponent', () => {
  let fixture: ComponentFixture<SellerSupportDetailPageComponent>;
  let supportService: jasmine.SpyObj<SellerSupportService>;

  beforeEach(async () => {
    supportService = jasmine.createSpyObj<SellerSupportService>('SellerSupportService', ['getTicket', 'addMessage']);
    supportService.getTicket.and.resolveTo(createSupportTicket());
    supportService.addMessage.and.resolveTo(createSupportTicket({
      messages: [{
        supportMessageId: 'message-id',
        senderUserId: 'seller-user-id',
        senderRole: 'Seller',
        message: 'Thanks for checking.',
        isInternal: false,
        createdAtUtc: '2026-05-19T10:10:00Z'
      }]
    }));

    await TestBed.configureTestingModule({
      imports: [SellerSupportDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerSupportService, useValue: supportService },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ ticketId: 'ticket-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerSupportDetailPageComponent);
  });

  it('loads ticket details and sends a message', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      messageForm: { setValue(value: { message: string }): void };
    };
    component.messageForm.setValue({ message: 'Thanks for checking.' });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form');
    form?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(supportService.addMessage).toHaveBeenCalledWith('ticket-id', { message: 'Thanks for checking.' });
  });
});
