import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminBuyerGrowthReportResponse, AdminMarketplaceReportResponse } from '../admin/admin-marketplace-report.models';
import { AdminMarketplaceReportService } from '../admin/admin-marketplace-report.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-admin-marketplace-reports-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    AdminWorkspaceNavComponent,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <section class="page admin-review">
      <app-admin-workspace-nav />

      <a class="admin-back-link" routerLink="">Back to admin</a>

      <div class="page-header">
        <span class="eyebrow">Admin reports</span>
        <h1>Finance and marketplace reports</h1>
        <p>Review aggregate marketplace movement, platform fees, balances, payouts, disputes, sellers, and categories.</p>
      </div>

      <form [formGroup]="filtersForm" (ngSubmit)="loadReport()" class="route-card admin-audit-filters" novalidate>
        <label class="ui-field">
          <span>From</span>
          <input type="datetime-local" formControlName="from">
        </label>

        <label class="ui-field">
          <span>To</span>
          <input type="datetime-local" formControlName="to">
        </label>

        <div class="admin-audit-actions">
          <button data-ui-button="primary" type="submit" [disabled]="isLoading()">Apply range</button>
          @if (csvExportHref() && !errorMessage()) {
            <a data-ui-button="secondary" [href]="csvExportHref()" target="_blank" rel="noreferrer">Export CSV</a>
          }
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading marketplace report...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (report() && !errorMessage()) {
          <div class="route-card compact-card">
            <span class="status-pill">Generated {{ report()!.generatedAtUtc | date:'medium' }}</span>
            <p>{{ report()!.fromUtc | date:'medium' }} to {{ report()!.toUtc | date:'medium' }}</p>
          </div>

          <div class="dashboard-metrics" aria-label="Finance report metrics">
            @for (metric of financeMetrics(); track metric.label) {
              <div class="dashboard-metric-card">
                <span>{{ metric.label }}</span>
                <strong>{{ metric.value | currency:report()!.currency:'symbol':'1.2-2' }}</strong>
              </div>
            }
          </div>

          <div class="dashboard-metrics" aria-label="Marketplace operation metrics">
            @for (metric of operationMetrics(); track metric.label) {
              <div class="dashboard-metric-card">
                <span>{{ metric.label }}</span>
                <strong>{{ metric.value }}</strong>
              </div>
            }
          </div>

          <div class="admin-table audit-table" role="table" aria-label="Top sellers">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Seller</span>
              <span role="columnheader">Orders</span>
              <span role="columnheader">Items sold</span>
              <span role="columnheader">GMV</span>
            </div>

            @for (seller of report()!.topSellers; track seller.sellerId) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ seller.sellerDisplayName ?? 'Unnamed seller' }}</strong>
                  <small>{{ seller.sellerId }}</small>
                </span>
                <span role="cell">{{ seller.orderCount }}</span>
                <span role="cell">{{ seller.itemsSold }}</span>
                <span role="cell">{{ seller.grossMerchandiseValue | currency:report()!.currency:'symbol':'1.2-2' }}</span>
              </div>
            } @empty {
              <div class="admin-table-row" role="row">
                <span role="cell">No seller activity for this range.</span>
              </div>
            }
          </div>

          <div class="admin-table audit-table" role="table" aria-label="Top categories">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Category</span>
              <span role="columnheader">Quantity sold</span>
              <span role="columnheader">Revenue</span>
            </div>

            @for (category of report()!.topCategories; track category.categoryId ?? category.categoryName) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ category.categoryName ?? 'Uncategorised' }}</strong>
                  <small>{{ category.categoryId ?? 'No category id' }}</small>
                </span>
                <span role="cell">{{ category.quantitySold }}</span>
                <span role="cell">{{ category.revenue | currency:report()!.currency:'symbol':'1.2-2' }}</span>
              </div>
            } @empty {
              <div class="admin-table-row" role="row">
                <span role="cell">No category sales for this range.</span>
              </div>
            }
          </div>

          @if (buyerGrowthReport()) {
            <div class="page-header compact-header">
              <span class="eyebrow">Buyer AI discovery</span>
              <h2>Assistant and visual-search handoffs</h2>
              <p>Aggregated buyer-authenticated telemetry only. Prompts, uploaded images, provider payloads, and buyer identities are not shown.</p>
            </div>

            <div class="dashboard-metrics" aria-label="Buyer growth metrics">
              @for (metric of buyerGrowthMetrics(); track metric.label) {
                <div class="dashboard-metric-card">
                  <span>{{ metric.label }}</span>
                  <strong>{{ metric.value }}</strong>
                </div>
              }
            </div>

            <div class="dashboard-metrics" aria-label="Buyer growth outcome metrics">
              @for (metric of buyerGrowthOutcomeMetrics(); track metric.label) {
                <div class="dashboard-metric-card">
                  <span>{{ metric.label }}</span>
                  <strong>{{ metric.value }}</strong>
                </div>
              }
            </div>

            <div class="admin-table audit-table" role="table" aria-label="Buyer growth outcome funnel by source">
              <div class="admin-table-row heading" role="row">
                <span role="columnheader">Source</span>
                <span role="columnheader">Attributed opens</span>
                <span role="columnheader">Cart adds</span>
                <span role="columnheader">Checkout</span>
                <span role="columnheader">Orders</span>
                <span role="columnheader">Paid</span>
              </div>
              @for (row of buyerGrowthReport()!.outcomeSourceToolBreakdown; track row.name) {
                <div class="admin-table-row" role="row">
                  <span role="cell">{{ row.name }}</span>
                  <span role="cell">{{ row.productOpenedCount }}</span>
                  <span role="cell">{{ row.addToCartCount }}</span>
                  <span role="cell">{{ row.checkoutStartedCount }}</span>
                  <span role="cell">{{ row.orderCreatedCount }}</span>
                  <span role="cell">{{ row.paidOrderCount }}</span>
                </div>
              } @empty {
                <div class="admin-table-row" role="row">
                  <span role="cell">No attributed buyer outcomes for this range.</span>
                </div>
              }
            </div>

            <div class="admin-table audit-table" role="table" aria-label="Buyer growth top context">
              <div class="admin-table-row heading" role="row">
                <span role="columnheader">Context</span>
                <span role="columnheader">Top values</span>
              </div>
              <div class="admin-table-row" role="row">
                <span role="cell">Categories</span>
                <span role="cell">{{ contextSummary(buyerGrowthReport()!.topCategories) }}</span>
              </div>
              <div class="admin-table-row" role="row">
                <span role="cell">Colours</span>
                <span role="cell">{{ contextSummary(buyerGrowthReport()!.topColours) }}</span>
              </div>
              <div class="admin-table-row" role="row">
                <span role="cell">Materials</span>
                <span role="cell">{{ contextSummary(buyerGrowthReport()!.topMaterials) }}</span>
              </div>
            </div>

            <div class="admin-table audit-table" role="table" aria-label="Buyer growth trend">
              <div class="admin-table-row heading" role="row">
                <span role="columnheader">Period</span>
                <span role="columnheader">Searches</span>
                <span role="columnheader">Attributed opens</span>
                <span role="columnheader">Shop handoffs</span>
                <span role="columnheader">Cart adds</span>
                <span role="columnheader">Orders</span>
                <span role="columnheader">Paid</span>
                <span role="columnheader">Feedback</span>
              </div>
              @for (bucket of buyerGrowthReport()!.trend; track bucket.periodStartUtc) {
                <div class="admin-table-row" role="row">
                  <span role="cell">{{ bucket.periodStartUtc | date:'mediumDate' }}</span>
                  <span role="cell">{{ bucket.searchSubmittedCount }}</span>
                  <span role="cell">{{ bucket.attributedProductOpenCount }}</span>
                  <span role="cell">{{ bucket.shopHandoffCount }}</span>
                  <span role="cell">{{ bucket.addToCartCount }}</span>
                  <span role="cell">{{ bucket.orderCreatedCount }}</span>
                  <span role="cell">{{ bucket.paidOrderCount }}</span>
                  <span role="cell">{{ bucket.feedbackSubmittedCount }}</span>
                </div>
              } @empty {
                <div class="admin-table-row" role="row">
                  <span role="cell">No buyer growth telemetry for this range.</span>
                </div>
              }
            </div>
          }
        }
      }
    </section>
  `
})
export class AdminMarketplaceReportsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly reportService = inject(AdminMarketplaceReportService);

  protected readonly report = signal<AdminMarketplaceReportResponse | null>(null);
  protected readonly buyerGrowthReport = signal<AdminBuyerGrowthReportResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly csvExportHref = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    from: [this.toDateTimeLocalInput(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000))],
    to: [this.toDateTimeLocalInput(new Date())]
  });

  async ngOnInit(): Promise<void> {
    await this.loadReport();
  }

  protected financeMetrics() {
    const report = this.report();
    if (!report) {
      return [];
    }

    return [
      { label: 'Gross merchandise value', value: report.finance.grossMerchandiseValue },
      { label: 'Platform commission earned', value: report.finance.platformCommissionEarned },
      { label: 'Payment processing fees', value: report.finance.paymentProcessingFees },
      { label: 'Refunds', value: report.finance.refunds },
      { label: 'Seller pending balances', value: report.finance.sellerPendingBalances },
      { label: 'Seller available balances', value: report.finance.sellerAvailableBalances },
      { label: 'Seller held balances', value: report.finance.sellerHeldBalances },
      { label: 'Payouts processed', value: report.finance.payoutsProcessed },
      { label: 'Failed payouts', value: report.finance.failedPayouts }
    ];
  }

  protected operationMetrics() {
    const report = this.report();
    if (!report) {
      return [];
    }

    return [
      { label: 'Orders', value: report.operations.orderCount },
      { label: 'Refunds', value: report.operations.refundCount },
      { label: 'Processed payouts', value: report.operations.payoutsProcessedCount },
      { label: 'Failed payouts', value: report.operations.failedPayoutCount },
      { label: 'Disputes', value: report.operations.disputeCount },
      { label: 'Active disputes', value: report.operations.activeDisputeCount }
    ];
  }

  protected buyerGrowthMetrics() {
    const report = this.buyerGrowthReport();
    if (!report) {
      return [];
    }

    return [
      { label: 'Searches submitted', value: report.summary.searchSubmittedCount },
      { label: 'Product opens', value: report.summary.productOpenedCount },
      { label: 'Shop handoffs', value: report.summary.shopHandoffCount },
      { label: 'Feedback submissions', value: report.summary.feedbackSubmittedCount },
      { label: 'Assistant searches', value: report.summary.assistantSearchCount },
      { label: 'Visual searches', value: report.summary.visualSearchCount }
    ];
  }

  protected buyerGrowthOutcomeMetrics() {
    const report = this.buyerGrowthReport();
    if (!report) {
      return [];
    }

    return [
      { label: 'AI-attributed cart adds', value: report.outcomeSummary.addToCartCount },
      { label: 'AI-attributed checkout starts', value: report.outcomeSummary.checkoutStartedCount },
      { label: 'AI-attributed orders', value: report.outcomeSummary.orderCreatedCount },
      { label: 'AI-attributed paid orders', value: report.outcomeSummary.paidOrderCount },
      { label: 'Open to cart rate', value: this.renderRate(report.outcomeSummary.productOpenToCartRate) },
      { label: 'Order to paid rate', value: this.renderRate(report.outcomeSummary.orderToPaidRate) }
    ];
  }

  protected contextSummary(items: Array<{ value: string; count: number }>): string {
    return items.length > 0
      ? items.slice(0, 5).map(item => `${item.value} (${item.count})`).join(', ')
      : 'No context captured';
  }

  protected renderRate(value: number): string {
    return `${Math.round(value * 1000) / 10}%`;
  }

  protected async loadReport(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const request = this.getDateRangeRequest();
    if (request.fromUtc && request.toUtc && request.fromUtc > request.toUtc) {
      this.errorMessage.set('From must be earlier than or equal to To.');
      this.report.set(null);
      this.buyerGrowthReport.set(null);
      this.csvExportHref.set(null);
      this.isLoading.set(false);
      return;
    }

    try {
      const [report, buyerGrowthReport] = await Promise.all([
        this.reportService.getReport(request),
        this.reportService.getBuyerGrowthReport({ ...request, bucket: 'Day' })
      ]);
      this.report.set(report);
      this.buyerGrowthReport.set(buyerGrowthReport);
      this.csvExportHref.set(this.reportService.getCsvExportUrl(request));
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.report.set(null);
      this.buyerGrowthReport.set(null);
      this.csvExportHref.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  private getDateRangeRequest(): { fromUtc?: string; toUtc?: string } {
    const filters = this.filtersForm.getRawValue();
    return {
      fromUtc: this.toIsoStringOrUndefined(filters.from),
      toUtc: this.toIsoStringOrUndefined(filters.to)
    };
  }

  private toIsoStringOrUndefined(value: string): string | undefined {
    return value ? new Date(value).toISOString() : undefined;
  }

  private toDateTimeLocalInput(value: Date): string {
    const localTime = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
    return localTime.toISOString().slice(0, 16);
  }
}
