import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerRefundService } from '../buyer/buyer-refund.service';
import { BuyerRefundsPageComponent } from './buyer-refunds-page.component';

describe('BuyerRefundsPageComponent', () => {
  let fixture: ComponentFixture<BuyerRefundsPageComponent>;
  let refundService: jasmine.SpyObj<BuyerRefundService>;

  beforeEach(async () => {
    refundService = jasmine.createSpyObj<BuyerRefundService>('BuyerRefundService', ['listRefunds']);
    refundService.listRefunds.and.resolveTo([{
      refundId: 'refund-id',
      orderId: 'order-id',
      returnRequestId: 'return-id',
      amount: 499,
      currency: 'ZAR',
      status: 'Processing',
      statusMessage: 'Your refund is being processed.',
      requestedAtUtc: '2026-05-29T10:00:00Z',
      approvedAtUtc: '2026-05-29T11:00:00Z',
      refundedAtUtc: null,
      timeline: [{
        status: 'Processing',
        eventType: 'ProviderRefundActionRequired',
        message: 'Finance or provider action is still in progress.',
        createdAtUtc: '2026-05-29T11:30:00Z'
      }]
    }]);

    await TestBed.configureTestingModule({
      imports: [BuyerRefundsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerRefundService, useValue: refundService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerRefundsPageComponent);
  });

  it('renders buyer-safe refund rows and timeline copy', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(refundService.listRefunds).toHaveBeenCalled();
    expect(text).toContain('Refunds');
    expect(text).toContain('refund-id');
    expect(text).toContain('Your refund is being processed.');
    expect(text).toContain('Finance or provider action is still in progress.');
  });

  it('renders an empty state when no refunds exist', async () => {
    refundService.listRefunds.and.resolveTo([]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No refund activity');
  });
});
