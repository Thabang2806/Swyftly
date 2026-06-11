import { CurrencyPipe, DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  SellerAdPerformanceDetailResponse,
  SellerAnalyticsCsvReport,
  SellerAnalyticsPerformanceRequest,
  SellerAnalyticsPerformanceResponse,
  SellerAnalyticsSummaryResponse,
  SellerFunnelSourceBreakdownResponse,
  SellerFunnelSourceCategory,
  SellerFunnelTrendBucketResponse,
  SellerInventoryPerformanceResponse,
  SellerProductFunnelResponse,
  SellerProductPerformanceResponse,
  SellerReportFrequency,
  SellerReportRange,
  SellerReportScheduleResponse,
  SellerSalesTrendBucketResponse
} from '../seller/seller-analytics.models';
import { SellerAnalyticsService } from '../seller/seller-analytics.service';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';

@Component({
  selector: 'app-seller-analytics-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    PercentPipe,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent
  ],
  template: `
    <section class="page seller-ops-page seller-products seller-analytics-page">
      <app-seller-workspace-nav />

      <div class="page-header seller-products-header">
        <div>
          <span class="eyebrow">Seller analytics</span>
          <h1>Performance workspace</h1>
          <p>Review seller-owned sales, product, inventory, ad, customer-care, and storefront conversion signals.</p>
        </div>
        <a data-ui-button="secondary" routerLink="">Seller workspace</a>
      </div>

      <form [formGroup]="filtersForm" (ngSubmit)="loadAnalytics()" class="route-card admin-audit-filters" novalidate>
        <label class="ui-field mabuntle-field">
          <span>From</span>
          <input type="datetime-local" formControlName="from">
        </label>

        <label class="ui-field mabuntle-field">
          <span>To</span>
          <input type="datetime-local" formControlName="to">
        </label>

        <label class="ui-field mabuntle-field">
          <span>Bucket</span>
          <select formControlName="bucket">
            <option value="Day">Daily</option>
            <option value="Week">Weekly</option>
          </select>
        </label>

        <label class="ui-field mabuntle-field">
          <span>Funnel source</span>
          <select formControlName="sourceCategory">
            <option value="">All sources</option>
            @for (source of sourceCategories; track source) {
              <option [value]="source">{{ source }}</option>
            }
          </select>
        </label>

        <div class="admin-audit-actions">
          <button data-ui-button="primary" type="submit" [disabled]="isLoading()">Apply filters</button>
          @if (performance() && !errorMessage()) {
            @for (report of csvReports; track report) {
              <a data-ui-button="secondary" [href]="getCsvExportUrl(report)" target="_blank" rel="noreferrer">{{ report }} CSV</a>
            }
          }
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading analytics...</div>
      } @else if (summary() && performance()) {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        <div class="route-card compact-card">
          <span class="status-pill">Range {{ performance()!.fromUtc | date:'mediumDate' }} to {{ performance()!.toUtc | date:'mediumDate' }}</span>
          <p>Bucketed by {{ performance()!.bucket.toLowerCase() }}. Funnel events are first-party and best-effort, so operational actions remain authoritative in orders and payments.</p>
        </div>

        <div class="dashboard-metrics" aria-label="Seller analytics metrics">
          <div class="dashboard-metric-card"><span>Total sales</span><strong>{{ summary()!.totalSales | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Orders</span><strong>{{ summary()!.orderCount }}</strong></div>
          <div class="dashboard-metric-card"><span>Average order value</span><strong>{{ summary()!.averageOrderValue | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Products sold</span><strong>{{ summary()!.productsSold }}</strong></div>
          <div class="dashboard-metric-card"><span>Refund rate</span><strong>{{ summary()!.refundRate | percent:'1.0-2' }}</strong></div>
          <div class="dashboard-metric-card"><span>Return rate</span><strong>{{ summary()!.returnRate | percent:'1.0-2' }}</strong></div>
        </div>

        <div class="dashboard-metrics" aria-label="Selected range metrics">
          <div class="dashboard-metric-card"><span>Range gross sales</span><strong>{{ rangeGrossSales() | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Range net sales</span><strong>{{ rangeNetSales() | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Range refunds</span><strong>{{ rangeRefunds() | currency:'ZAR':'symbol-narrow' }}</strong></div>
          <div class="dashboard-metric-card"><span>Range units sold</span><strong>{{ rangeUnitsSold() }}</strong></div>
        </div>

        <div class="dashboard-metrics" aria-label="Storefront conversion metrics">
          <div class="dashboard-metric-card"><span>Product views</span><strong>{{ performance()!.funnelSummary.productViews }}</strong></div>
          <div class="dashboard-metric-card"><span>Add to cart</span><strong>{{ performance()!.funnelSummary.addToCartCount }}</strong></div>
          <div class="dashboard-metric-card"><span>Checkout starts</span><strong>{{ performance()!.funnelSummary.checkoutStartCount }}</strong></div>
          <div class="dashboard-metric-card"><span>Paid orders</span><strong>{{ performance()!.funnelSummary.paidOrderCount }}</strong></div>
          <div class="dashboard-metric-card"><span>View to cart</span><strong>{{ performance()!.funnelSummary.productViewToCartRate | percent:'1.0-2' }}</strong></div>
          <div class="dashboard-metric-card"><span>Checkout to paid</span><strong>{{ performance()!.funnelSummary.checkoutToPaidRate | percent:'1.0-2' }}</strong></div>
        </div>

        <div class="admin-detail-layout">
          <div class="admin-detail-main">
            <article class="route-card admin-detail-card">
              <h2>Source breakdown</h2>
              @if (performance()!.sourceBreakdown.length === 0) {
                <p>No source attribution has been recorded for this range yet.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Storefront source breakdown">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Source</span>
                    <span role="columnheader">Views</span>
                    <span role="columnheader">Cart</span>
                    <span role="columnheader">Checkout</span>
                    <span role="columnheader">Paid</span>
                    <span role="columnheader">Top detail</span>
                  </div>
                  @for (source of visibleSourceBreakdown(); track source.sourceCategory) {
                    <div class="admin-table-row" role="row">
                      <span role="cell"><span class="status-pill">{{ source.sourceCategory }}</span></span>
                      <span role="cell">{{ source.productViews }}</span>
                      <span role="cell">{{ source.addToCartCount }} / {{ source.productViewToCartRate | percent:'1.0-2' }}</span>
                      <span role="cell">{{ source.checkoutStartCount }}</span>
                      <span role="cell">{{ source.paidOrderCount }} / {{ source.checkoutToPaidRate | percent:'1.0-2' }}</span>
                      <span role="cell">{{ source.topUtmSource ?? source.topReferrerHost ?? 'No detail' }}</span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Sales trend</h2>
              @if (performance()!.salesTrend.length === 0) {
                <p>No sales activity exists for this range.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Sales trend">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Period</span>
                    <span role="columnheader">Orders</span>
                    <span role="columnheader">Gross</span>
                    <span role="columnheader">Refunds</span>
                    <span role="columnheader">Net</span>
                    <span role="columnheader">Units</span>
                  </div>
                  @for (bucket of visibleSalesTrend(); track bucket.periodStartUtc) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">{{ bucket.periodStartUtc | date:'mediumDate' }}</span>
                      <span role="cell">{{ bucket.orderCount }}</span>
                      <span role="cell">{{ bucket.grossSales | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell">{{ bucket.refundedAmount | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell">{{ bucket.netSales | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell">{{ bucket.unitsSold }}</span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Conversion funnel</h2>
              @if (performance()!.funnelTrend.length === 0) {
                <p>No storefront funnel events have been recorded for this range.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Storefront conversion trend">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Period</span>
                    <span role="columnheader">Views</span>
                    <span role="columnheader">Cart</span>
                    <span role="columnheader">Checkout</span>
                    <span role="columnheader">Paid</span>
                    <span role="columnheader">Rate</span>
                  </div>
                  @for (bucket of visibleFunnelTrend(); track bucket.periodStartUtc) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">{{ bucket.periodStartUtc | date:'mediumDate' }}</span>
                      <span role="cell">{{ bucket.productViews }}</span>
                      <span role="cell">{{ bucket.addToCartCount }}</span>
                      <span role="cell">{{ bucket.checkoutStartCount }}</span>
                      <span role="cell">{{ bucket.paidOrderCount }}</span>
                      <span role="cell">{{ bucket.productViewToCartRate | percent:'1.0-2' }}</span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Product funnel</h2>
              @if (performance()!.productFunnel.length === 0) {
                <p>No product-level funnel rows are available yet. Views begin populating after buyers browse public product pages.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Product funnel">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Product</span>
                    <span role="columnheader">Views</span>
                    <span role="columnheader">Cart</span>
                    <span role="columnheader">Paid</span>
                    <span role="columnheader">Revenue</span>
                    <span role="columnheader">Action</span>
                  </div>
                  @for (product of visibleProductFunnel(); track product.productId) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">
                        <strong>{{ product.productTitle ?? 'Untitled product' }}</strong>
                        <small>{{ product.productViewToCartRate | percent:'1.0-2' }} view-to-cart / {{ product.dominantSourceCategory }}</small>
                      </span>
                      <span role="cell">{{ product.productViews }}</span>
                      <span role="cell">{{ product.addToCartCount }}</span>
                      <span role="cell">{{ product.paidOrderCount }}</span>
                      <span role="cell">{{ product.revenue | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell"><a data-ui-button="secondary" [routerLink]="['/products', product.productId, 'edit']">Open</a></span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Product performance</h2>
              @if (performance()!.productPerformance.length === 0) {
                <p>No product data is available yet.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Product performance">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Product</span>
                    <span role="columnheader">Sold</span>
                    <span role="columnheader">Revenue</span>
                    <span role="columnheader">Returns</span>
                    <span role="columnheader">Available</span>
                    <span role="columnheader">Action</span>
                  </div>
                  @for (product of visibleProductPerformance(); track product.productId) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">
                        <strong>{{ product.productTitle ?? 'Untitled product' }}</strong>
                        <small>{{ product.status }}</small>
                      </span>
                      <span role="cell">{{ product.unitsSold }}</span>
                      <span role="cell">{{ product.grossSales | currency:'ZAR':'symbol-narrow' }}</span>
                      <span role="cell">{{ product.returnCount }} / {{ product.returnRate | percent:'1.0-2' }}</span>
                      <span role="cell">{{ product.availableQuantity }}</span>
                      <span role="cell"><a data-ui-button="secondary" [routerLink]="['/products', product.productId, 'edit']">Open</a></span>
                    </div>
                  }
                </div>
              }
            </article>

            <article class="route-card admin-detail-card">
              <h2>Inventory performance</h2>
              @if (performance()!.inventoryPerformance.length === 0) {
                <p>No variants are available for this seller yet.</p>
              } @else {
                <div class="admin-table audit-table" role="table" aria-label="Inventory performance">
                  <div class="admin-table-row heading" role="row">
                    <span role="columnheader">Variant</span>
                    <span role="columnheader">Barcode</span>
                    <span role="columnheader">Stock</span>
                    <span role="columnheader">Reserved</span>
                    <span role="columnheader">Available</span>
                    <span role="columnheader">State</span>
                  </div>
                  @for (item of visibleInventoryPerformance(); track item.productVariantId) {
                    <div class="admin-table-row" role="row">
                      <span role="cell">
                        <strong>{{ item.productTitle ?? 'Untitled product' }}</strong>
                        <small>{{ item.sku }} / {{ item.size }} / {{ item.colour }}</small>
                      </span>
                      <span role="cell">{{ item.barcode ?? 'Not set' }}</span>
                      <span role="cell">{{ item.stockQuantity }}</span>
                      <span role="cell">{{ item.reservedQuantity }}</span>
                      <span role="cell">{{ item.availableQuantity }}</span>
                      <span role="cell">
                        <span class="status-pill">{{ item.isOutOfStock ? 'Out of stock' : item.isLowStock ? 'Low stock' : item.status }}</span>
                      </span>
                    </div>
                  }
                </div>
              }
            </article>
          </div>

          <aside class="admin-actions">
            <div class="route-card admin-action-card seller-report-schedule-card">
              <h2>Scheduled reports</h2>
              <p>Opt into transactional analytics digests. Emails use your seller notification preferences and link back to this workspace.</p>

              @if (scheduleErrorMessage()) {
                <p class="auth-alert error" role="alert">{{ scheduleErrorMessage() }}</p>
              }
              @if (scheduleSuccessMessage()) {
                <p class="auth-alert success" role="status">{{ scheduleSuccessMessage() }}</p>
              }

              <form [formGroup]="scheduleForm" (ngSubmit)="saveSchedule()" class="seller-report-schedule-form" novalidate>
                <label class="ui-checkbox"><input type="checkbox" formControlName="isEnabled"><span>Enable scheduled digest</span></label>

                <label class="ui-field mabuntle-field">
                  <span>Frequency</span>
                  <select formControlName="frequency">
                    <option value="Weekly">Weekly</option>
                    <option value="Monthly">Monthly</option>
                  </select>
                </label>

                <label class="ui-field mabuntle-field">
                  <span>Report range</span>
                  <select formControlName="reportRange">
                    <option value="Last7Days">Last 7 days</option>
                    <option value="Last30Days">Last 30 days</option>
                    <option value="MonthToDate">Month to date</option>
                  </select>
                </label>

                @if (scheduleForm.controls.frequency.value === 'Weekly') {
                  <label class="ui-field mabuntle-field">
                    <span>Send day</span>
                    <select formControlName="sendDayOfWeek">
                      @for (day of daysOfWeek; track day) {
                        <option [value]="day">{{ day }}</option>
                      }
                    </select>
                  </label>
                } @else {
                  <label class="ui-field mabuntle-field">
                    <span>Send day of month</span>
                    <input type="number" min="1" max="28" formControlName="sendDayOfMonth">
                  </label>
                }

                <label class="ui-field mabuntle-field">
                  <span>Send time</span>
                  <input type="time" formControlName="sendTimeLocal">
                </label>

                <label class="ui-field mabuntle-field">
                  <span>Time zone</span>
                  <input formControlName="timeZoneId">
                </label>

                @if (schedule(); as currentSchedule) {
                  <dl class="admin-facts">
                    <div><dt>Next send</dt><dd>{{ currentSchedule.nextRunAtUtc ? (currentSchedule.nextRunAtUtc | date:'medium') : 'Not scheduled' }}</dd></div>
                    <div><dt>Last sent</dt><dd>{{ currentSchedule.lastSentAtUtc ? (currentSchedule.lastSentAtUtc | date:'medium') : 'Never' }}</dd></div>
                    <div><dt>Last period</dt><dd>{{ currentSchedule.lastReportPeriodStartUtc ? (currentSchedule.lastReportPeriodStartUtc | date:'mediumDate') + ' to ' + (currentSchedule.lastReportPeriodEndUtc | date:'mediumDate') : 'None' }}</dd></div>
                  </dl>
                  @if (currentSchedule.lastFailureReason) {
                    <p class="auth-alert error">Last failure: {{ currentSchedule.lastFailureReason }}</p>
                  }
                }

                <div class="form-actions">
                  <button data-ui-button="primary" type="submit" [disabled]="isScheduleSaving()">Save schedule</button>
                  <button data-ui-button="secondary" type="button" [disabled]="isScheduleSaving()" (click)="sendTestDigest()">Send test digest</button>
                </div>
              </form>
            </div>

            <div class="route-card admin-action-card">
              <h2>Customer care</h2>
              <dl class="admin-facts">
                <div><dt>Returns</dt><dd>{{ performance()!.customerCareSummary.returnCount }}</dd></div>
                <div><dt>Open returns</dt><dd>{{ performance()!.customerCareSummary.openReturnCount }}</dd></div>
                <div><dt>Refunds</dt><dd>{{ performance()!.customerCareSummary.refundCount }}</dd></div>
                <div><dt>Refunded</dt><dd>{{ performance()!.customerCareSummary.refundedAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Support tickets</dt><dd>{{ performance()!.customerCareSummary.supportTicketCount }}</dd></div>
                <div><dt>Open support</dt><dd>{{ performance()!.customerCareSummary.openSupportTicketCount }}</dd></div>
                <div><dt>Disputes</dt><dd>{{ performance()!.customerCareSummary.disputeCount }}</dd></div>
                <div><dt>Active disputes</dt><dd>{{ performance()!.customerCareSummary.activeDisputeCount }}</dd></div>
              </dl>
            </div>

            <div class="route-card admin-action-card">
              <h2>Ad performance</h2>
              @if (performance()!.adPerformance.length === 0) {
                <p>No ad activity has been recorded for this range.</p>
              } @else {
                <div class="admin-product-risks">
                  @for (campaign of visibleAdPerformance(); track campaign.adCampaignId) {
                    <div>
                      <span class="status-pill">{{ campaign.status }}</span>
                      <strong>{{ campaign.name }}</strong>
                      <span>{{ campaign.clicks }} clicks / {{ campaign.revenueGenerated | currency:'ZAR':'symbol-narrow' }} revenue</span>
                      <small>Spend {{ campaign.spend | currency:'ZAR':'symbol-narrow' }} / ROAS {{ campaign.returnOnAdSpend | number:'1.0-2' }}</small>
                    </div>
                  }
                </div>
              }
            </div>

            <div class="route-card admin-action-card">
              <h2>AI usage</h2>
              <dl class="admin-facts">
                <div><dt>Requests</dt><dd>{{ summary()!.aiUsage.requests }}</dd></div>
                <div><dt>Successful</dt><dd>{{ summary()!.aiUsage.successfulRequests }}</dd></div>
                <div><dt>Failed</dt><dd>{{ summary()!.aiUsage.failedRequests }}</dd></div>
                <div><dt>Estimated cost</dt><dd>{{ summary()!.aiUsage.estimatedCost | currency:'USD':'symbol-narrow' }}</dd></div>
                <div><dt>Average latency</dt><dd>{{ summary()!.aiUsage.averageLatencyMs }} ms</dd></div>
                <div><dt>Acceptance rate</dt><dd>{{ summary()!.aiUsage.suggestionAcceptanceRate | percent:'1.0-2' }}</dd></div>
              </dl>
              <p>{{ summary()!.aiUsage.qualityScoreImprovementNote }}</p>
            </div>
          </aside>
        </div>
      } @else {
        <p class="auth-alert error" role="alert">{{ errorMessage() ?? 'Analytics could not be loaded.' }}</p>
      }
    </section>
  `
})
export class SellerAnalyticsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly analyticsService = inject(SellerAnalyticsService);

  protected readonly summary = signal<SellerAnalyticsSummaryResponse | null>(null);
  protected readonly performance = signal<SellerAnalyticsPerformanceResponse | null>(null);
  protected readonly schedule = signal<SellerReportScheduleResponse | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isScheduleSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly scheduleErrorMessage = signal<string | null>(null);
  protected readonly scheduleSuccessMessage = signal<string | null>(null);
  protected readonly csvReports: SellerAnalyticsCsvReport[] = ['Sales', 'Products', 'Inventory', 'Ads', 'Returns', 'Funnel'];
  protected readonly daysOfWeek = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  protected readonly sourceCategories: SellerFunnelSourceCategory[] = ['Direct', 'Search', 'Social', 'Email', 'Ads', 'Referral', 'Unknown'];

  protected readonly filtersForm = this.formBuilder.group({
    from: [this.toDateTimeLocalInput(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000))],
    to: [this.toDateTimeLocalInput(new Date())],
    bucket: ['Day' as 'Day' | 'Week'],
    sourceCategory: ['' as SellerFunnelSourceCategory | '']
  });

  protected readonly scheduleForm = this.formBuilder.group({
    isEnabled: [false],
    frequency: ['Weekly' as SellerReportFrequency],
    reportRange: ['Last30Days' as SellerReportRange],
    sendDayOfWeek: ['Monday'],
    sendDayOfMonth: [1],
    sendTimeLocal: ['08:00'],
    timeZoneId: ['Africa/Johannesburg']
  });

  async ngOnInit(): Promise<void> {
    await this.loadAnalytics();
  }

  protected async loadAnalytics(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const request = this.getPerformanceRequest();
    if (request.fromUtc && request.toUtc && request.fromUtc > request.toUtc) {
      this.errorMessage.set('From must be earlier than or equal to To.');
      this.summary.set(null);
      this.performance.set(null);
      this.isLoading.set(false);
      return;
    }

    try {
      const [summary, performance] = await Promise.all([
        this.analyticsService.getSummary(),
        this.analyticsService.getPerformance(request)
      ]);
      this.summary.set(summary);
      this.performance.set(performance);
      if (!this.schedule()) {
        this.setSchedule(await this.analyticsService.getReportSchedule());
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.summary.set(null);
      this.performance.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected getCsvExportUrl(report: SellerAnalyticsCsvReport): string {
    return this.analyticsService.getCsvExportUrl(report, this.getPerformanceRequest());
  }

  protected async saveSchedule(): Promise<void> {
    if (this.isScheduleSaving()) {
      return;
    }

    this.isScheduleSaving.set(true);
    this.scheduleErrorMessage.set(null);
    this.scheduleSuccessMessage.set(null);

    try {
      this.setSchedule(await this.analyticsService.updateReportSchedule(this.createScheduleRequest()));
      this.scheduleSuccessMessage.set('Scheduled report settings saved.');
    } catch (error) {
      this.scheduleErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isScheduleSaving.set(false);
    }
  }

  protected async sendTestDigest(): Promise<void> {
    if (this.isScheduleSaving()) {
      return;
    }

    this.isScheduleSaving.set(true);
    this.scheduleErrorMessage.set(null);
    this.scheduleSuccessMessage.set(null);

    try {
      await this.analyticsService.sendTestReportDigest();
      this.scheduleSuccessMessage.set('Test digest queued using your notification preferences.');
    } catch (error) {
      this.scheduleErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isScheduleSaving.set(false);
    }
  }

  protected visibleSalesTrend(): SellerSalesTrendBucketResponse[] {
    return this.performance()?.salesTrend.slice(-12) ?? [];
  }

  protected visibleFunnelTrend(): SellerFunnelTrendBucketResponse[] {
    return this.performance()?.funnelTrend.slice(-12) ?? [];
  }

  protected visibleSourceBreakdown(): SellerFunnelSourceBreakdownResponse[] {
    return this.performance()?.sourceBreakdown.slice(0, 8) ?? [];
  }

  protected visibleProductFunnel(): SellerProductFunnelResponse[] {
    return this.performance()?.productFunnel.slice(0, 10) ?? [];
  }

  protected visibleProductPerformance(): SellerProductPerformanceResponse[] {
    return this.performance()?.productPerformance.slice(0, 10) ?? [];
  }

  protected visibleInventoryPerformance(): SellerInventoryPerformanceResponse[] {
    return this.performance()?.inventoryPerformance.slice(0, 10) ?? [];
  }

  protected visibleAdPerformance(): SellerAdPerformanceDetailResponse[] {
    return this.performance()?.adPerformance.slice(0, 5) ?? [];
  }

  protected rangeGrossSales(): number {
    return this.performance()?.salesTrend.reduce((total, bucket) => total + bucket.grossSales, 0) ?? 0;
  }

  protected rangeNetSales(): number {
    return this.performance()?.salesTrend.reduce((total, bucket) => total + bucket.netSales, 0) ?? 0;
  }

  protected rangeRefunds(): number {
    return this.performance()?.salesTrend.reduce((total, bucket) => total + bucket.refundedAmount, 0) ?? 0;
  }

  protected rangeUnitsSold(): number {
    return this.performance()?.salesTrend.reduce((total, bucket) => total + bucket.unitsSold, 0) ?? 0;
  }

  private getPerformanceRequest(): SellerAnalyticsPerformanceRequest {
    const filters = this.filtersForm.getRawValue();
    return {
      fromUtc: this.toIsoStringOrUndefined(filters.from),
      toUtc: this.toIsoStringOrUndefined(filters.to),
      bucket: filters.bucket,
      sourceCategory: filters.sourceCategory
    };
  }

  private createScheduleRequest() {
    const value = this.scheduleForm.getRawValue();
    return {
      isEnabled: value.isEnabled,
      frequency: value.frequency,
      reportRange: value.reportRange,
      sendDayOfWeek: value.frequency === 'Weekly' ? value.sendDayOfWeek : null,
      sendDayOfMonth: value.frequency === 'Monthly' ? Number(value.sendDayOfMonth) : null,
      sendTimeLocal: value.sendTimeLocal,
      timeZoneId: value.timeZoneId
    };
  }

  private setSchedule(schedule: SellerReportScheduleResponse): void {
    this.schedule.set(schedule);
    this.scheduleForm.patchValue({
      isEnabled: schedule.isEnabled,
      frequency: schedule.frequency,
      reportRange: schedule.reportRange,
      sendDayOfWeek: schedule.sendDayOfWeek ?? 'Monday',
      sendDayOfMonth: schedule.sendDayOfMonth ?? 1,
      sendTimeLocal: schedule.sendTimeLocal,
      timeZoneId: schedule.timeZoneId
    });
  }

  private toIsoStringOrUndefined(value: string): string | undefined {
    return value ? new Date(value).toISOString() : undefined;
  }

  private toDateTimeLocalInput(value: Date): string {
    const localTime = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
    return localTime.toISOString().slice(0, 16);
  }
}
