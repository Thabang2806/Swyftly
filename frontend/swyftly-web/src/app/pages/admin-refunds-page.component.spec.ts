import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminRefundResponse } from '../admin/admin-refund.models';
import { AdminRefundService } from '../admin/admin-refund.service';
import { AuthRole } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { AdminRefundsPageComponent } from './admin-refunds-page.component';

describe('AdminRefundsPageComponent', () => {
  let fixture: ComponentFixture<AdminRefundsPageComponent>;
  let currentRoles: AuthRole[];
  let refundService: jasmine.SpyObj<AdminRefundService>;

  beforeEach(async () => {
    currentRoles = ['SuperAdmin'];
    refundService = jasmine.createSpyObj<AdminRefundService>(
      'AdminRefundService',
      ['getRefunds', 'createOrderRefund', 'createReturnRefund', 'approveRefund', 'confirmManualProviderRefund']);
    refundService.getRefunds.and.resolveTo([createRefund()]);
    refundService.createOrderRefund.and.resolveTo(createRefund({ refundId: 'order-refund-id' }));
    refundService.createReturnRefund.and.resolveTo(createRefund({ refundId: 'return-refund-id', returnRequestId: 'return-id' }));
    refundService.approveRefund.and.resolveTo(createRefund({ status: 'Approved', approvedAtUtc: '2026-05-19T11:00:00Z' }));
    refundService.confirmManualProviderRefund.and.resolveTo(createRefund({
      status: 'Refunded',
      providerRefundReference: 'PF-REF-1',
      refundedAtUtc: '2026-05-19T12:00:00Z'
    }));

    await TestBed.configureTestingModule({
      imports: [AdminRefundsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminRefundService, useValue: refundService },
        {
          provide: AuthService,
          useValue: {
            hasAnyRole: (roles: readonly AuthRole[]) => roles.some(role => currentRoles.includes(role))
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminRefundsPageComponent);
  });

  it('creates an order refund and approves a selected refund', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('refund-id');

    const component = fixture.componentInstance as unknown as {
      createForm: {
        setValue(value: { orderId: string; returnRequestId: string; amount: number; reason: string }): void;
      };
      approveForm: { setValue(value: { reason: string }): void };
    };
    component.createForm.setValue({
      orderId: 'order-id',
      returnRequestId: '',
      amount: 75,
      reason: 'Customer care'
    });

    const forms = compiled.querySelectorAll('form');
    forms[0]?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(refundService.createOrderRefund).toHaveBeenCalledWith('order-id', {
      amount: 75,
      reason: 'Customer care'
    });

    clickButton(compiled, 'Select');
    fixture.detectChanges();

    component.approveForm.setValue({ reason: 'Approved by finance' });
    const updatedForms = compiled.querySelectorAll('form');
    updatedForms[1]?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(refundService.approveRefund).toHaveBeenCalledWith('order-refund-id', {
      reason: 'Approved by finance'
    });
  });

  it('disables create and approve controls for read-only viewers', async () => {
    currentRoles = ['Admin'];

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Read only');
    expect(findButton(compiled, 'Create refund')?.disabled).toBeTrue();

    clickButton(compiled, 'Select');
    fixture.detectChanges();

    expect(findButton(compiled, 'Approve refund')?.disabled).toBeTrue();
  });

  it('shows PayFast manual refund guidance and confirms provider reference', async () => {
    refundService.getRefunds.and.resolveTo([createRefund({ status: 'Processing' })]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    clickButton(compiled, 'Select');
    fixture.detectChanges();
    expect(compiled.textContent).toContain('PayFast refunds are completed in the provider dashboard');

    const component = fixture.componentInstance as unknown as {
      manualConfirmForm: {
        setValue(value: { providerRefundReference: string; reason: string }): void;
      };
    };
    component.manualConfirmForm.setValue({
      providerRefundReference: 'PF-REF-1',
      reason: 'Dashboard refund completed.'
    });

    const forms = compiled.querySelectorAll('form');
    forms[2]?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(refundService.confirmManualProviderRefund).toHaveBeenCalledWith('refund-id', {
      providerRefundReference: 'PF-REF-1',
      reason: 'Dashboard refund completed.'
    });
  });
});

export function createRefund(overrides: Partial<AdminRefundResponse> = {}): AdminRefundResponse {
  return {
    refundId: 'refund-id',
    orderId: 'order-id',
    paymentId: 'payment-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    returnRequestId: null,
    amount: 75,
    currency: 'ZAR',
    status: 'Requested',
    reason: 'Customer care',
    providerRefundReference: null,
    failureReason: null,
    requestedAtUtc: '2026-05-19T10:00:00Z',
    approvedAtUtc: null,
    refundedAtUtc: null,
    events: [{
      refundEventId: 'event-id',
      status: 'Requested',
      eventType: 'Requested',
      message: 'Refund requested.',
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
