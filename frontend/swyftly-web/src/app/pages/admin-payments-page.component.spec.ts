import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminPaymentSummaryResponse } from '../admin/admin-order-payment.models';
import { AdminOrderPaymentService } from '../admin/admin-order-payment.service';
import { AuthRole } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { AdminPaymentsPageComponent } from './admin-payments-page.component';

describe('AdminPaymentsPageComponent', () => {
  let fixture: ComponentFixture<AdminPaymentsPageComponent>;
  let service: jasmine.SpyObj<AdminOrderPaymentService>;
  let currentRoles: AuthRole[];

  beforeEach(async () => {
    currentRoles = ['SuperAdmin'];
    service = jasmine.createSpyObj<AdminOrderPaymentService>('AdminOrderPaymentService', [
      'getPayments',
      'getPaymentReconciliationCandidates',
      'createPaymentReconciliationReview'
    ]);
    service.getPayments.and.resolveTo([createAdminPayment()]);
    service.getPaymentReconciliationCandidates.and.resolveTo([{
      ...createAdminPayment({
        paymentId: 'reconciliation-payment-id',
        status: 'Pending',
        paidAtUtc: null
      }),
      reasonCode: 'StalePendingPayment',
      recommendedAction: 'Check the provider dashboard.',
      latestEvent: null,
      latestReview: null
    }]);
    service.createPaymentReconciliationReview.and.resolveTo({
      reviewId: 'review-id',
      paymentId: 'reconciliation-payment-id',
      provider: 'fake-pay',
      providerReference: 'provider-payment-1',
      observedProviderStatus: 'COMPLETE',
      observedAmount: 140,
      observedCurrency: 'ZAR',
      outcome: 'ProviderPaidMissingWebhook',
      reason: 'Provider dashboard shows complete.',
      reviewedByUserId: 'admin-id',
      reviewedAtUtc: '2026-05-19T11:00:00Z',
      nextReviewAfterUtc: null
    });

    await TestBed.configureTestingModule({
      imports: [AdminPaymentsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminOrderPaymentService, useValue: service },
        {
          provide: AuthService,
          useValue: {
            hasAnyRole: (roles: readonly AuthRole[]) => roles.some(role => currentRoles.includes(role))
          }
        },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ orderId: 'order-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminPaymentsPageComponent);
  });

  it('renders admin payments and respects the order query filter', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(service.getPayments).toHaveBeenCalledWith('', 'order-id');
    expect(service.getPaymentReconciliationCandidates).toHaveBeenCalledWith(30, false);
    expect(compiled.textContent).toContain('fake-pay');
    expect(compiled.textContent).toContain('provider-payment-1');
    expect(compiled.textContent).toContain('Manual reconciliation');
    expect(compiled.textContent).toContain('StalePendingPayment');
    expect(compiled.querySelector('a[href="/admin/payments/payment-id"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/admin/payments/reconciliation-payment-id"]')).not.toBeNull();
  });

  it('sends status and order filters to the admin payment API', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { setValue(value: { search: string; status: string; orderId: string }): void };
      applyFilters(): Promise<void>;
    };
    component.filtersForm.setValue({ search: '', status: 'Paid', orderId: 'order-id' });
    await component.applyFilters();

    expect(service.getPayments).toHaveBeenCalledWith('Paid', 'order-id');
  });

  it('records reconciliation review evidence and warns on paid missing webhook', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      reconciliationCandidates(): Array<{
        paymentId: string;
      }>;
      selectCandidate(candidate: unknown): void;
      reviewForm: {
        setValue(value: {
          observedProviderStatus: string;
          observedAmount: number;
          observedCurrency: string;
          outcome: string;
          reason: string;
          nextReviewAfterUtc: string;
        }): void;
      };
      recordReconciliationReview(): Promise<void>;
    };

    component.selectCandidate(component.reconciliationCandidates()[0]);
    component.reviewForm.setValue({
      observedProviderStatus: 'COMPLETE',
      observedAmount: 140,
      observedCurrency: 'ZAR',
      outcome: 'ProviderPaidMissingWebhook',
      reason: 'Provider dashboard shows complete.',
      nextReviewAfterUtc: ''
    });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Do not mark this order paid');

    await component.recordReconciliationReview();

    expect(service.createPaymentReconciliationReview).toHaveBeenCalledWith('reconciliation-payment-id', {
      observedProviderStatus: 'COMPLETE',
      observedAmount: 140,
      observedCurrency: 'ZAR',
      outcome: 'ProviderPaidMissingWebhook',
      reason: 'Provider dashboard shows complete.',
      nextReviewAfterUtc: null
    });
  });

  it('prevents read-only viewers from submitting reconciliation reviews', async () => {
    currentRoles = ['Admin'];

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      reconciliationCandidates(): unknown[];
      selectCandidate(candidate: unknown): void;
    };
    component.selectCandidate(component.reconciliationCandidates()[0]);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('FinanceApprove is required');
    expect(findButton(compiled, 'Save review')?.disabled).toBeTrue();
  });
});

export function createAdminPayment(overrides: Partial<AdminPaymentSummaryResponse> = {}): AdminPaymentSummaryResponse {
  return {
    paymentId: 'payment-id',
    orderId: 'order-id',
    buyerId: 'buyer-id',
    provider: 'fake-pay',
    providerReference: 'provider-payment-1',
    amount: 140,
    currency: 'ZAR',
    status: 'Paid',
    paidAtUtc: '2026-05-19T10:05:00Z',
    failedAtUtc: null,
    createdAtUtc: '2026-05-19T10:00:00Z',
    updatedAtUtc: '2026-05-19T10:05:00Z',
    ...overrides
  };
}

function findButton(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
  return Array.from(compiled.querySelectorAll('button'))
    .find(button => button.textContent?.includes(text)) as HTMLButtonElement | undefined;
}
