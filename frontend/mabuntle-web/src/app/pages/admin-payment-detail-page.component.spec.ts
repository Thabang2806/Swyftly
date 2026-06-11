import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminPaymentDetailResponse } from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AdminPaymentDetailPageComponent } from './admin-payment-detail-page.component';
import { createAdminPayment } from './admin-payments-page.component.spec';

describe('AdminPaymentDetailPageComponent', () => {
  let fixture: ComponentFixture<AdminPaymentDetailPageComponent>;
  let service: jasmine.SpyObj<AdminOrderPaymentService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<AdminOrderPaymentService>('AdminOrderPaymentService', ['getPayment']);
    service.getPayment.and.resolveTo(createAdminPaymentDetail());

    await TestBed.configureTestingModule({
      imports: [AdminPaymentDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminOrderPaymentService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ paymentId: 'payment-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminPaymentDetailPageComponent);
  });

  it('loads a payment detail with related order and webhook events', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(service.getPayment).toHaveBeenCalledWith('payment-id');
    expect(compiled.textContent).toContain('provider-payment-1');
    expect(compiled.textContent).toContain('payment.captured');
    expect(compiled.textContent).toContain('provider-event-1');
    expect(compiled.querySelector('a[href="/orders/order-id"]')).not.toBeNull();
  });
});

export function createAdminPaymentDetail(overrides: Partial<AdminPaymentDetailResponse> = {}): AdminPaymentDetailResponse {
  return {
    ...createAdminPayment(),
    order: {
      orderId: 'order-id',
      buyerId: 'buyer-id',
      sellerId: 'seller-id',
      status: 'Paid',
      itemCount: 1,
      totalAmount: 140,
      createdAtUtc: '2026-05-19T10:00:00Z'
    },
    events: [{
      paymentEventId: 'payment-event-id',
      provider: 'fake-pay',
      providerEventId: 'provider-event-1',
      eventType: 'payment.captured',
      processingStatus: 'Processed',
      receivedAtUtc: '2026-05-19T10:05:00Z',
      processedAtUtc: '2026-05-19T10:05:01Z',
      errorMessage: null
    }],
    ...overrides
  };
}
