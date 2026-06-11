import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminOrderSummaryResponse } from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AdminOrdersPageComponent } from './admin-orders-page.component';

describe('AdminOrdersPageComponent', () => {
  let fixture: ComponentFixture<AdminOrdersPageComponent>;
  let service: jasmine.SpyObj<AdminOrderPaymentService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<AdminOrderPaymentService>('AdminOrderPaymentService', ['getOrders']);
    service.getOrders.and.resolveTo([createAdminOrder()]);

    await TestBed.configureTestingModule({
      imports: [AdminOrdersPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminOrderPaymentService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminOrdersPageComponent);
  });

  it('renders admin orders and links to order detail', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Orders');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('Payment Paid');
    expect(compiled.querySelector('a[href="/orders/order-id"]')).not.toBeNull();
  });

  it('sends status filters to the admin order API', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { setValue(value: { search: string; status: string }): void };
      applyFilters(): Promise<void>;
    };
    component.filtersForm.setValue({ search: '', status: 'Paid' });
    await component.applyFilters();

    expect(service.getOrders).toHaveBeenCalledWith('Paid');
  });
});

export function createAdminOrder(overrides: Partial<AdminOrderSummaryResponse> = {}): AdminOrderSummaryResponse {
  return {
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    status: 'Paid',
    itemCount: 1,
    itemsSubtotal: 120,
    shippingAmount: 20,
    platformFeeAmount: 0,
    discountAmount: 0,
    totalAmount: 140,
    paymentStatus: 'Paid',
    shipmentStatus: 'InTransit',
    createdAtUtc: '2026-05-19T10:00:00Z',
    updatedAtUtc: '2026-05-19T10:05:00Z',
    ...overrides
  };
}
