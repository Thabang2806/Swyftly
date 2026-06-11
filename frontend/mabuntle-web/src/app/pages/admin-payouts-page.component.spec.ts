import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminPayoutResponse } from '../admin/admin-payout.models';
import { AdminPayoutService } from '../admin/admin-payout.service';
import { AuthRole } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { AdminPayoutsPageComponent } from './admin-payouts-page.component';

describe('AdminPayoutsPageComponent', () => {
  let fixture: ComponentFixture<AdminPayoutsPageComponent>;
  let currentRoles: AuthRole[];
  let payoutService: jasmine.SpyObj<AdminPayoutService>;

  beforeEach(async () => {
    currentRoles = ['SuperAdmin'];
    payoutService = jasmine.createSpyObj<AdminPayoutService>(
      'AdminPayoutService',
      ['getPendingPayouts', 'holdPayout', 'releasePayout', 'makePayoutAvailable', 'processPayout', 'reconcilePayout']);
    payoutService.getPendingPayouts.and.resolveTo([createPayout()]);
    payoutService.holdPayout.and.resolveTo(createPayout({ status: 'OnHold', holdReason: 'Risk review' }));
    payoutService.releasePayout.and.resolveTo(createPayout({ status: 'Pending' }));
    payoutService.makePayoutAvailable.and.resolveTo(createPayout({ status: 'Available' }));
    payoutService.processPayout.and.resolveTo(createPayout({ status: 'Processing' }));
    payoutService.reconcilePayout.and.resolveTo(createPayout({ status: 'PaidOut' }));

    await TestBed.configureTestingModule({
      imports: [AdminPayoutsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminPayoutService, useValue: payoutService },
        {
          provide: AuthService,
          useValue: {
            hasAnyRole: (roles: readonly AuthRole[]) => roles.some(role => currentRoles.includes(role))
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminPayoutsPageComponent);
  });

  it('renders payouts and submits a hold action payload', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('payout-id');
    expect(compiled.textContent).toContain('Operate and approve');
    expect(compiled.querySelector('.hf-admin-finance-layout')).toBeTruthy();
    expect(compiled.textContent).toContain('Ledger snapshot');

    selectPayout(compiled);
    setReason('Risk review');
    clickButton(compiled, 'Hold');
    await fixture.whenStable();

    expect(payoutService.holdPayout).toHaveBeenCalledWith('payout-id', { reason: 'Risk review' });
  });

  it('disables action buttons for read-only finance viewers', async () => {
    currentRoles = ['Admin'];

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    selectPayout(compiled);
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Read only');
    const holdButton = findButton(compiled, 'Hold');
    const processButton = findButton(compiled, 'Process');
    expect(holdButton?.disabled).toBeTrue();
    expect(processButton?.disabled).toBeTrue();
  });

  it('shows pending payout-profile change warning and disables processing', async () => {
    payoutService.getPendingPayouts.and.resolveTo([
      createPayout({
        hasPendingPayoutProfileChange: true,
        pendingPayoutProfileChangeRequestId: 'change-request-id'
      })
    ]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Payout profile change pending');
    expect(compiled.textContent).toContain('processing is blocked');
    expect(findButton(compiled, 'Process')?.disabled).toBeTrue();
  });

  function selectPayout(compiled: HTMLElement): void {
    clickButton(compiled, 'Select');
    fixture.detectChanges();
  }

  function setReason(reason: string): void {
    const component = fixture.componentInstance as unknown as {
      reasonForm: { setValue(value: { reason: string }): void };
    };
    component.reasonForm.setValue({ reason });
  }
});

export function createPayout(overrides: Partial<AdminPayoutResponse> = {}): AdminPayoutResponse {
  return {
    payoutId: 'payout-id',
    sellerId: 'seller-id',
    amount: 150,
    currency: 'ZAR',
    status: 'Pending',
    createdAtUtc: '2026-05-19T10:00:00Z',
    heldAtUtc: null,
    holdReason: null,
    releasedAtUtc: null,
    releaseReason: null,
    hasPendingPayoutProfileChange: false,
    pendingPayoutProfileChangeRequestId: null,
    items: [{
      payoutItemId: 'payout-item-id',
      ledgerEntryId: 'ledger-id',
      orderId: 'order-id',
      paymentId: 'payment-id',
      amount: 150,
      currency: 'ZAR',
      createdAtUtc: '2026-05-19T10:00:00Z'
    }],
    ...overrides
  };
}

function clickButton(compiled: HTMLElement, text: string): void {
  findButton(compiled, text)?.dispatchEvent(new Event('click'));
}

function findButton(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
  return Array.from(compiled.querySelectorAll('button'))
    .find(button => button.textContent?.includes(text)) as HTMLButtonElement | undefined;
}
