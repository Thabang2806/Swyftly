import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SellerPayoutService } from '../seller/seller-payout.service';
import { SellerPayoutsPageComponent } from './seller-payouts-page.component';

describe('SellerPayoutsPageComponent', () => {
  let fixture: ComponentFixture<SellerPayoutsPageComponent>;
  let payoutService: jasmine.SpyObj<SellerPayoutService>;

  beforeEach(async () => {
    payoutService = jasmine.createSpyObj<SellerPayoutService>('SellerPayoutService', ['getBalance', 'listPayouts']);
    payoutService.getBalance.and.resolveTo({
      sellerId: 'seller-id',
      balances: [{
        currency: 'ZAR',
        pendingBalance: 100,
        availableBalance: 200,
        heldBalance: 50
      }]
    });
    payoutService.listPayouts.and.resolveTo([{
      payoutId: 'payout-id',
      sellerId: 'seller-id',
      amount: 100,
      currency: 'ZAR',
      status: 'Pending',
      createdAtUtc: '2026-05-19T10:00:00Z',
      heldAtUtc: null,
      holdReason: null,
      releasedAtUtc: null,
      releaseReason: null,
      items: [{
        amount: 100,
        currency: 'ZAR',
        createdAtUtc: '2026-05-19T10:00:00Z',
        sourceType: 'Order'
      }]
    }]);

    await TestBed.configureTestingModule({
      imports: [SellerPayoutsPageComponent],
      providers: [
        provideRouter([]),
        { provide: SellerPayoutService, useValue: payoutService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerPayoutsPageComponent);
  });

  it('renders seller balances and payout history', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Pending');
    expect(compiled.textContent).toContain('Available');
    expect(compiled.textContent).toContain('payout-id');
    expect(compiled.textContent).toContain('Order item');
    expect(compiled.textContent).not.toContain('order-id');
    expect(compiled.textContent).not.toContain('ledger-id');
    expect(compiled.textContent).not.toContain('payment-id');
  });
});
