import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerOrderResult } from '../buyer/buyer-order.models';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerWorkspaceNavComponent } from '../buyer/buyer-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-buyer-order-detail-page',
  imports: [
    BuyerWorkspaceNavComponent,
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page buyer-ops-page">
      <app-buyer-workspace-nav />

      <a class="admin-back-link" routerLink="/account/orders">Back to orders</a>

      <app-page-header
        eyebrow="Buyer account"
        [heading]="order() ? 'Order ' + order()!.orderId : 'Order'"
        description="Review order items, status history, shipment progress, and after-sales options."
      >
        <div pageHeaderActions>
          @if (order()) {
            <app-status-badge [label]="order()!.status" [tone]="statusTone(order()!.status)" />
            @if (canRetryPayment()) {
              <button mat-flat-button type="button" [disabled]="isSaving()" (click)="retryPayment()">Retry payment</button>
            } @else if (order()!.status === 'Cancelled') {
              <a mat-stroked-button routerLink="/cart">Start checkout again</a>
            }
          }
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading order...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (order()) {
          <div class="buyer-detail-grid">
            <section class="buyer-panel">
              <h2>Order summary</h2>
              <dl class="seller-facts">
                <div><dt>Items subtotal</dt><dd>{{ order()!.itemsSubtotal | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Shipping</dt><dd>{{ order()!.shippingAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                @if (order()!.deliveryMethodName) {
                  <div><dt>Delivery method</dt><dd>{{ order()!.deliveryMethodName }} - {{ deliveryEstimate(order()!) }}</dd></div>
                }
                <div><dt>Platform fee</dt><dd>{{ order()!.platformFeeAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Discount</dt><dd>{{ order()!.discountAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
                <div><dt>Total</dt><dd>{{ order()!.totalAmount | currency:'ZAR':'symbol-narrow' }}</dd></div>
              </dl>
            </section>

            <section class="buyer-panel">
              <h2>Delivery address</h2>
              @if (order()!.deliveryAddress) {
                <dl class="seller-facts">
                  <div><dt>Recipient</dt><dd>{{ order()!.deliveryAddress!.recipientName }}</dd></div>
                  <div><dt>Phone</dt><dd>{{ order()!.deliveryAddress!.phoneNumber }}</dd></div>
                  <div><dt>Address</dt><dd>{{ formatDeliveryAddress(order()!.deliveryAddress!) }}</dd></div>
                  @if (order()!.deliveryAddress!.deliveryInstructions) {
                    <div><dt>Instructions</dt><dd>{{ order()!.deliveryAddress!.deliveryInstructions }}</dd></div>
                  }
                </dl>
              } @else {
                <app-ui-alert tone="info">This older order does not have a saved delivery-address snapshot.</app-ui-alert>
              }
            </section>

            <section class="buyer-panel">
              <h2>After-sales options</h2>
              @if (canRequestReturn()) {
                <p>Delivered orders can be submitted for return review. Choose one line item per request.</p>
                <form [formGroup]="returnForm" (ngSubmit)="createReturn()" class="buyer-form-grid" novalidate>
                  <mat-form-field appearance="outline">
                    <mat-label>Item</mat-label>
                    <mat-select formControlName="orderItemId">
                      @for (item of order()!.items; track item.orderItemId) {
                        <mat-option [value]="item.orderItemId">{{ item.productTitle ?? item.sku }} - {{ item.quantity }} available</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Quantity</mat-label>
                    <input matInput type="number" min="1" formControlName="quantity" />
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Reason</mat-label>
                    <input matInput formControlName="reason" />
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Item condition</mat-label>
                    <mat-select formControlName="isOpenedOrUnsealed">
                      <mat-option [value]="false">Unopened or sealed</mat-option>
                      <mat-option [value]="true">Opened or unsealed</mat-option>
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Details</mat-label>
                    <textarea matInput rows="3" formControlName="details"></textarea>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Item note</mat-label>
                    <textarea matInput rows="2" formControlName="note"></textarea>
                  </mat-form-field>

                  <button mat-flat-button type="submit" [disabled]="isSaving()">Request return</button>
                </form>
              } @else {
                <app-ui-alert tone="info">Returns can be requested after an order is delivered. Current status: {{ order()!.status }}.</app-ui-alert>
                @if (canRetryPayment()) {
                  <app-ui-alert tone="warning">Payment is still pending. Retry payment before this order can move to fulfilment.</app-ui-alert>
                } @else if (order()!.status === 'Cancelled') {
                  <app-ui-alert tone="warning">This order was cancelled after payment failure. Start checkout again from your cart.</app-ui-alert>
                }
                <div class="buyer-action-row">
                  <a mat-stroked-button routerLink="/account/support">Contact support</a>
                  <a mat-stroked-button routerLink="/account/disputes">View disputes</a>
                </div>
              }
            </section>
          </div>

          @if (canRequestReturn()) {
            <section class="buyer-panel">
              <h2>Leave a product review</h2>
              <p>Reviews are tied to delivered order items and appear publicly after they are saved.</p>
              <form [formGroup]="reviewForm" (ngSubmit)="createReview()" class="buyer-form-grid" novalidate>
                <mat-form-field appearance="outline">
                  <mat-label>Item</mat-label>
                  <mat-select formControlName="orderItemId">
                    @for (item of order()!.items; track item.orderItemId) {
                      <mat-option [value]="item.orderItemId">{{ item.productTitle ?? item.sku }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Rating</mat-label>
                  <mat-select formControlName="rating">
                    @for (rating of ratings; track rating) {
                      <mat-option [value]="rating">{{ rating }} star{{ rating === 1 ? '' : 's' }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Title</mat-label>
                  <input matInput formControlName="title" />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Review</mat-label>
                  <textarea matInput rows="3" formControlName="body"></textarea>
                </mat-form-field>

                <div class="buyer-action-row">
                  <button mat-flat-button type="submit" [disabled]="isSaving()">Submit review</button>
                  <a mat-stroked-button routerLink="/account/reviews">My reviews</a>
                </div>
              </form>
            </section>
          }

          <section class="buyer-panel">
            <h2>Items</h2>
            <div class="seller-item-list">
              @for (item of order()!.items; track item.orderItemId) {
                <div class="seller-item-row">
                  <span>
                    <strong>{{ item.productTitle ?? 'Untitled product' }}</strong>
                    <small>SKU {{ item.sku }} - {{ item.size }} / {{ item.colour }}</small>
                  </span>
                  <span>{{ item.quantity }} x {{ item.unitPrice | currency:'ZAR':'symbol-narrow' }}</span>
                  <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>
                </div>
              }
            </div>
          </section>

          <div class="buyer-detail-grid">
            <section class="buyer-panel">
              <h2>Status history</h2>
              <div class="seller-timeline">
                @for (history of order()!.statusHistory; track history.statusHistoryId) {
                  <div>
                    <app-status-badge [label]="history.newStatus" [tone]="statusTone(history.newStatus)" />
                    <span>{{ history.changedAtUtc | date:'medium' }}</span>
                    @if (history.reason) {
                      <small>{{ history.reason }}</small>
                    }
                  </div>
                }
              </div>
            </section>

            <section class="buyer-panel">
              <h2>Shipment tracking</h2>
              @if (order()!.shipments.length === 0) {
                <p>No shipment has been added yet.</p>
              } @else {
                <div class="seller-timeline">
                  @for (shipment of order()!.shipments; track shipment.shipmentId) {
                    <div>
                      <app-status-badge [label]="shipment.status" [tone]="statusTone(shipment.status)" />
                      <span>{{ shipment.carrierName ?? 'Carrier not set' }}</span>
                      @if (shipment.trackingNumber) {
                        <small>{{ shipment.trackingNumber }}</small>
                      }
                      @if (shipment.trackingUrl) {
                        <a [href]="shipment.trackingUrl" target="_blank" rel="noreferrer">Track shipment</a>
                      }
                      @if (shipment.status === 'DeliveryFailed' || shipment.status === 'ReturnedToSender') {
                        <a routerLink="/account/support">Contact support</a>
                      }
                      @for (event of shipment.events; track event.shipmentEventId) {
                        <small>{{ event.eventType }} - {{ event.occurredAtUtc | date:'medium' }}{{ event.message ? ': ' + event.message : '' }}</small>
                      }
                    </div>
                  }
                </div>
              }
            </section>
          </div>
        }
      }
    </section>
  `
})
export class BuyerOrderDetailPageComponent implements OnInit {
  private readonly engagementService = inject(BuyerEngagementService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly orderService = inject(BuyerOrderService);
  private readonly paymentRedirectService = inject(BuyerPaymentRedirectService);
  private readonly paymentService = inject(BuyerPaymentService);
  private readonly returnService = inject(BuyerReturnService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly order = signal<BuyerOrderResult | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly ratings = [5, 4, 3, 2, 1] as const;
  protected readonly returnForm = this.formBuilder.group({
    orderItemId: ['', [Validators.required]],
    quantity: [1, [Validators.required, Validators.min(1)]],
    reason: ['', [Validators.required]],
    isOpenedOrUnsealed: [false],
    details: [''],
    note: ['']
  });
  protected readonly reviewForm = this.formBuilder.group({
    orderItemId: ['', [Validators.required]],
    rating: [5, [Validators.required, Validators.min(1), Validators.max(5)]],
    title: [''],
    body: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadOrder();
  }

  protected canRequestReturn(): boolean {
    return this.order()?.status === 'Delivered';
  }

  protected canRetryPayment(): boolean {
    return this.order()?.status === 'PendingPayment';
  }

  protected async retryPayment(): Promise<void> {
    const order = this.order();
    if (!order || !this.canRetryPayment() || this.isSaving()) {
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      const payment = await this.paymentService.initiatePayment(order.orderId);
      if (payment.checkoutUrl) {
        this.paymentRedirectService.redirect(payment.checkoutUrl);
      } else {
        this.successMessage.set('Payment is pending. Refresh this order after completing provider checkout.');
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected async createReturn(): Promise<void> {
    const order = this.order();
    const selectedItem = this.selectedReturnItem();
    if (!order || !selectedItem || this.returnForm.invalid || this.isSaving()) {
      this.returnForm.markAllAsTouched();
      return;
    }

    const value = this.returnForm.getRawValue();
    const quantity = Math.min(value.quantity, selectedItem.quantity);

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const returnRequest = await this.returnService.createReturn(order.orderId, {
        reason: value.reason,
        details: emptyToNull(value.details),
        items: [{
          orderItemId: value.orderItemId,
          quantity,
          reason: value.reason,
          isOpenedOrUnsealed: value.isOpenedOrUnsealed,
          note: emptyToNull(value.note)
        }]
      });
      this.successMessage.set('Return request created.');
      await this.router.navigate(['/account/returns', returnRequest.returnRequestId]);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected async createReview(): Promise<void> {
    const order = this.order();
    if (!order || this.reviewForm.invalid || this.isSaving()) {
      this.reviewForm.markAllAsTouched();
      return;
    }

    const value = this.reviewForm.getRawValue();
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await this.engagementService.createReview(order.orderId, value.orderItemId, {
        rating: value.rating,
        title: emptyToNull(value.title),
        body: emptyToNull(value.body)
      });
      this.reviewForm.patchValue({ title: '', body: '' });
      this.successMessage.set('Review submitted for moderation.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Paid', 'Processing', 'ReadyToShip', 'AwaitingFulfilment'].includes(status)) {
      return 'accent';
    }

    if (['Shipped', 'Delivered', 'Completed', 'InTransit'].includes(status)) {
      return 'success';
    }

    if (['Cancelled', 'Refunded', 'Disputed', 'Failed', 'DeliveryFailed', 'ReturnedToSender'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  protected formatDeliveryAddress(address: NonNullable<BuyerOrderResult['deliveryAddress']>): string {
    return [
      address.addressLine1,
      address.addressLine2,
      address.suburb,
      address.city,
      address.province,
      address.postalCode,
      address.countryCode
    ].filter(Boolean).join(', ');
  }

  protected deliveryEstimate(order: BuyerOrderResult): string {
    if (order.deliveryEstimatedMinDays === null
      || order.deliveryEstimatedMinDays === undefined
      || order.deliveryEstimatedMaxDays === null
      || order.deliveryEstimatedMaxDays === undefined) {
      return 'estimate unavailable';
    }

    return order.deliveryEstimatedMinDays === order.deliveryEstimatedMaxDays
      ? `${order.deliveryEstimatedMinDays} day${order.deliveryEstimatedMinDays === 1 ? '' : 's'}`
      : `${order.deliveryEstimatedMinDays}-${order.deliveryEstimatedMaxDays} days`;
  }

  private selectedReturnItem() {
    const order = this.order();
    const selectedId = this.returnForm.controls.orderItemId.value;
    return order?.items.find(item => item.orderItemId === selectedId) ?? order?.items[0] ?? null;
  }

  private async loadOrder(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const order = await this.orderService.getOrder(this.orderId());
      this.order.set(order);
      this.returnForm.patchValue({ orderItemId: order.items[0]?.orderItemId ?? '' });
      this.reviewForm.patchValue({ orderItemId: order.items[0]?.orderItemId ?? '' });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private orderId(): string {
    return this.route.snapshot.paramMap.get('orderId') ?? '';
  }
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
