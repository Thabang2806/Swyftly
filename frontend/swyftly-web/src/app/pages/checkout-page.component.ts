import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { getApiErrorMessage } from '../auth/api-error';
import { BuyerPaymentRedirectService, BuyerPaymentService } from '../buyer/buyer-payment.service';
import { BuyerDeliveryAddressResponse } from '../buyer/buyer-settings.models';
import { BuyerSettingsService } from '../buyer/buyer-settings.service';
import { CartResponse, CartShippingOptionResponse, OrderDeliveryAddressRequest } from '../cart/cart.models';
import { CartService } from '../cart/cart.service';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-checkout-page',
  imports: [
    CurrencyPipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page checkout-page">
      <a class="admin-back-link" routerLink="/cart">Back to cart</a>

      <section class="hf-checkout-hero" aria-labelledby="checkout-heading">
        <div>
          <span class="eyebrow">Secure checkout</span>
          <h1 id="checkout-heading">Complete your order</h1>
          <p>Confirm delivery details, reserve the cart, and continue to the hosted payment flow.</p>
        </div>
        <div class="hf-checkout-progress" aria-label="Checkout progress">
          <span class="is-complete" aria-label="Address"></span>
          <span class="is-complete" aria-label="Delivery"></span>
          <span aria-label="Payment"></span>
          <span aria-label="Review"></span>
        </div>
      </section>

      @if (isLoading()) {
        <div class="route-card">Loading checkout...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (!cart()?.items?.length) {
          <app-empty-state
            eyebrow="Empty"
            heading="No items to checkout"
            message="Add products to your cart before checkout."
          >
            <a mat-flat-button routerLink="/shop">Shop products</a>
          </app-empty-state>
        } @else {
          <div class="checkout-layout">
            <form [formGroup]="shippingForm" class="checkout-form hf-checkout-form" (ngSubmit)="startCheckout()" novalidate>
              <section class="hf-checkout-card">
                <div class="checkout-section-heading">
                  <app-status-badge label="Delivery information" tone="accent" />
                  <h2>Shipping address</h2>
                </div>
                <p>Select a saved delivery address or enter a one-off address for this order.</p>

                @if (savedAddresses().length > 0) {
                  <div class="checkout-address-book">
                    @for (address of savedAddresses(); track address.deliveryAddressId) {
                      <button
                        type="button"
                        class="checkout-address-option"
                        [class.is-selected]="selectedDeliveryAddressId() === address.deliveryAddressId && !useManualAddress()"
                        (click)="selectSavedAddress(address.deliveryAddressId)"
                      >
                        <strong>{{ address.label }}</strong>
                        <span>{{ address.recipientName }} - {{ address.addressLine1 }}, {{ address.city }}</span>
                        @if (address.isDefault) {
                          <small>Default</small>
                        }
                      </button>
                    }
                    <button
                      type="button"
                      class="checkout-address-option"
                      [class.is-selected]="useManualAddress()"
                      (click)="useOneOffAddress()"
                    >
                      <strong>Use another address</strong>
                      <span>Enter a one-off delivery address for this order.</span>
                    </button>
                  </div>
                }

                @if (!useManualAddress() && selectedDeliveryAddress()) {
                  <div class="checkout-address-snapshot">
                    <strong>{{ selectedDeliveryAddress()!.recipientName }}</strong>
                    <span>{{ selectedDeliveryAddress()!.phoneNumber }}</span>
                    <span>{{ selectedDeliveryAddress()!.addressLine1 }}</span>
                    @if (selectedDeliveryAddress()!.addressLine2) {
                      <span>{{ selectedDeliveryAddress()!.addressLine2 }}</span>
                    }
                    @if (selectedDeliveryAddress()!.suburb) {
                      <span>{{ selectedDeliveryAddress()!.suburb }}</span>
                    }
                    <span>{{ selectedDeliveryAddress()!.city }}, {{ selectedDeliveryAddress()!.province }}</span>
                    <span>{{ selectedDeliveryAddress()!.postalCode }} {{ selectedDeliveryAddress()!.countryCode }}</span>
                    @if (selectedDeliveryAddress()!.deliveryInstructions) {
                      <span>Instructions: {{ selectedDeliveryAddress()!.deliveryInstructions }}</span>
                    }
                  </div>
                } @else {
                  <div class="form-grid">
                  <mat-form-field appearance="outline">
                    <mat-label>Full name</mat-label>
                    <input matInput formControlName="fullName">
                    @if (shippingForm.controls.fullName.hasError('required')) {
                      <mat-error>Full name is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Phone</mat-label>
                    <input matInput formControlName="phone">
                    @if (shippingForm.controls.phone.hasError('required')) {
                      <mat-error>Phone is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Address line 1</mat-label>
                    <input matInput formControlName="addressLine1">
                    @if (shippingForm.controls.addressLine1.hasError('required')) {
                      <mat-error>Address is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Address line 2</mat-label>
                    <input matInput formControlName="addressLine2">
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Suburb</mat-label>
                    <input matInput formControlName="suburb">
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>City</mat-label>
                    <input matInput formControlName="city">
                    @if (shippingForm.controls.city.hasError('required')) {
                      <mat-error>City is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Province</mat-label>
                    <input matInput formControlName="province">
                    @if (shippingForm.controls.province.hasError('required')) {
                      <mat-error>Province is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Postal code</mat-label>
                    <input matInput formControlName="postalCode">
                    @if (shippingForm.controls.postalCode.hasError('required')) {
                      <mat-error>Postal code is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Country code</mat-label>
                    <input matInput formControlName="countryCode" maxlength="2">
                    @if (shippingForm.controls.countryCode.invalid) {
                      <mat-error>Use a two-letter country code.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Delivery instructions</mat-label>
                    <textarea matInput rows="3" formControlName="deliveryInstructions" maxlength="500"></textarea>
                    @if (shippingForm.controls.deliveryInstructions.hasError('maxlength')) {
                      <mat-error>Delivery instructions must be 500 characters or fewer.</mat-error>
                    }
                  </mat-form-field>
                </div>
                }
              </section>

              <section class="hf-checkout-card">
                <div class="checkout-section-heading">
                  <app-status-badge label="Delivery options" tone="accent" />
                  <h2>Choose a seller delivery method</h2>
                </div>
                <p>Delivery rates are managed by the seller and matched to the selected delivery address.</p>

                <button
                  mat-stroked-button
                  type="button"
                  (click)="loadShippingOptions()"
                  [disabled]="isLoadingShipping() || !cart()?.cartId"
                >
                  {{ isLoadingShipping() ? 'Checking delivery...' : 'Check delivery options' }}
                </button>

                @if (shippingOptions().length > 0) {
                  <div class="checkout-delivery-options">
                    @for (option of shippingOptions(); track option.deliveryMethodId) {
                      <button
                        type="button"
                        class="checkout-delivery-option"
                        [class.is-selected]="selectedDeliveryMethodId() === option.deliveryMethodId"
                        (click)="selectDeliveryMethod(option.deliveryMethodId)"
                      >
                        <span>
                          <strong>{{ option.name }}</strong>
                          <small>{{ option.methodType }} - {{ deliveryEstimate(option) }}</small>
                          @if (option.description) {
                            <small>{{ option.description }}</small>
                          }
                        </span>
                        <span>
                          <strong>{{ option.shippingAmount | currency:'ZAR':'symbol-narrow' }}</strong>
                          @if (option.freeShippingApplied) {
                            <small>Free shipping applied</small>
                          }
                        </span>
                      </button>
                    }
                  </div>
                } @else {
                  <p class="checkout-muted-note">Select an address, then check delivery options before starting checkout.</p>
                }
              </section>

              <section class="checkout-support-grid">
                <article class="hf-checkout-card hf-checkout-option-card">
                  <div class="checkout-section-heading">
                    <app-status-badge label="Delivery" />
                    <h2>Seller fulfilment</h2>
                  </div>
                  <p>After payment is confirmed, the seller fulfils the order and adds tracking from the seller workspace.</p>
                  <strong>Address and seller context stay attached to the order.</strong>
                </article>

                <article class="hf-checkout-card hf-checkout-option-card is-selected">
                  <div class="checkout-section-heading">
                    <app-status-badge label="Payment" tone="success" />
                    <h2>Hosted checkout</h2>
                  </div>
                  <p>Your order is paid only after Swyftly receives a signed payment-provider webhook.</p>
                  <strong>No card details are collected by this Angular app.</strong>
                </article>
              </section>

              <section class="hf-checkout-card hf-checkout-review-card">
                <div class="checkout-section-heading">
                  <app-status-badge label="Review" tone="success" />
                  <h2>Review and start checkout</h2>
                </div>
                <p>Review seller, items, and delivery details before creating the reserved order.</p>
                <button mat-flat-button class="checkout-primary-action" type="submit" [disabled]="isSubmitting()">
                  {{ isSubmitting() ? 'Starting checkout...' : 'Start checkout' }}
                </button>
              </section>
            </form>

            <aside class="order-summary hf-order-summary">
              <div class="order-summary-heading">
                <div>
                  <h2>Order summary</h2>
                  <span class="product-card-seller">{{ cart()?.sellerStoreName ?? 'Seller' }}</span>
                </div>
                <app-status-badge label="Single seller cart" />
              </div>
              @for (item of cart()?.items; track item.cartItemId) {
                <div class="checkout-summary-item">
                  <div class="checkout-summary-thumb" aria-hidden="true">{{ item.productTitle?.charAt(0) ?? 'S' }}</div>
                  <div>
                    <strong>{{ item.productTitle ?? 'Product' }}</strong>
                    <span>{{ item.size || item.colour ? (item.size + ' ' + item.colour).trim() : item.sku }} - Qty {{ item.quantity }}</span>
                  </div>
                  <strong>{{ item.lineTotal | currency:'ZAR':'symbol-narrow' }}</strong>
                </div>
              }
              <div class="summary-row">
                <span>Delivery</span>
                <strong>{{ (selectedShippingOption()?.shippingAmount ?? 0) | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
              @if (selectedShippingOption(); as option) {
                <div class="summary-row">
                  <span>Delivery method</span>
                  <strong>{{ option.name }} - {{ deliveryEstimate(option) }}</strong>
                </div>
              } @else {
                <div class="summary-row">
                  <span>Delivery method</span>
                  <strong>Select option</strong>
                </div>
              }
              <div class="summary-row">
                <span>Items subtotal</span>
                <strong>{{ cart()?.subtotal | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
              <div class="summary-row">
                <span>Payment status</span>
                <strong>Pending after start</strong>
              </div>
              <div class="summary-row total">
                <span>Estimated total</span>
                <strong>{{ orderTotal() | currency:'ZAR':'symbol-narrow' }}</strong>
              </div>
              <div class="checkout-safe-funds">
                <strong>Funds are handled safely</strong>
                <span>Stock is reserved when checkout starts. Payment status changes only from verified provider events.</span>
              </div>
            </aside>
          </div>

          <section class="checkout-trust-strip" aria-label="Checkout trust">
            <div>
              <strong>Reserved before payment</strong>
              <span>The cart is converted into a pending order before provider checkout opens.</span>
            </div>
            <div>
              <strong>Provider-confirmed payment</strong>
              <span>Signed webhooks remain the source of truth for paid orders.</span>
            </div>
            <div>
              <strong>Seller fulfilment trail</strong>
              <span>Tracking, delivery, returns, and support are handled from the account workspaces.</span>
            </div>
          </section>
        }
      }
    </section>
  `
})
export class CheckoutPageComponent implements OnInit {
  private readonly cartService = inject(CartService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly paymentRedirectService = inject(BuyerPaymentRedirectService);
  private readonly paymentService = inject(BuyerPaymentService);
  private readonly router = inject(Router);
  private readonly settingsService = inject(BuyerSettingsService);

  protected readonly cart = signal<CartResponse | null>(null);
  protected readonly savedAddresses = signal<BuyerDeliveryAddressResponse[]>([]);
  protected readonly selectedDeliveryAddressId = signal<string | null>(null);
  protected readonly useManualAddress = signal(true);
  protected readonly shippingOptions = signal<CartShippingOptionResponse[]>([]);
  protected readonly selectedDeliveryMethodId = signal<string | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isLoadingShipping = signal(false);
  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly shippingForm = this.formBuilder.group({
    fullName: ['', Validators.required],
    phone: ['', Validators.required],
    addressLine1: ['', Validators.required],
    addressLine2: [''],
    suburb: [''],
    city: ['', Validators.required],
    province: ['', Validators.required],
    postalCode: ['', Validators.required],
    countryCode: ['ZA', [Validators.required, Validators.minLength(2), Validators.maxLength(2)]],
    deliveryInstructions: ['', Validators.maxLength(500)]
  });

  async ngOnInit(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    try {
      const [cart, addresses] = await Promise.all([
        this.cartService.getCart(),
        this.settingsService.listDeliveryAddresses()
      ]);
      this.cart.set(cart);
      this.savedAddresses.set(addresses);
      this.applyDefaultAddress(addresses);
      if (cart.items.length > 0 && this.selectedDeliveryAddressId()) {
        await this.loadShippingOptions();
      }
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.cart.set(null);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async startCheckout(): Promise<void> {
    const deliveryAddressId = this.useManualAddress() ? null : this.selectedDeliveryAddressId();
    if (!deliveryAddressId) {
      this.shippingForm.markAllAsTouched();
    }

    if ((deliveryAddressId ? false : this.shippingForm.invalid) || !this.cart()?.cartId) {
      return;
    }

    if (!this.selectedDeliveryMethodId()) {
      this.errorMessage.set('Choose a delivery method before starting checkout.');
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    let orderId: string | null = null;
    try {
      const order = await this.cartService.createOrderFromCart({
        cartId: this.cart()?.cartId ?? null,
        reservationMinutes: null,
        deliveryAddressId,
        deliveryAddress: deliveryAddressId ? null : this.createManualDeliveryAddress(),
        deliveryMethodId: this.selectedDeliveryMethodId()
      });
      orderId = order.orderId;
      const payment = await this.paymentService.initiatePayment(order.orderId);
      if (payment.checkoutUrl) {
        this.paymentRedirectService.redirect(payment.checkoutUrl);
        return;
      }

      await this.router.navigate(['/checkout/success'], { queryParams: { orderId: order.orderId } });
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      await this.router.navigate(
        ['/checkout/failed'],
        orderId ? { queryParams: { orderId } } : undefined);
    } finally {
      this.isSubmitting.set(false);
    }
  }

  protected selectSavedAddress(deliveryAddressId: string): void {
    this.selectedDeliveryAddressId.set(deliveryAddressId);
    this.useManualAddress.set(false);
    this.resetShippingSelection();
    void this.loadShippingOptions();
  }

  protected selectedDeliveryAddress(): BuyerDeliveryAddressResponse | null {
    const selectedId = this.selectedDeliveryAddressId();
    return this.savedAddresses().find(address => address.deliveryAddressId === selectedId) ?? null;
  }

  protected useOneOffAddress(): void {
    this.selectedDeliveryAddressId.set(null);
    this.useManualAddress.set(true);
    this.resetShippingSelection();
  }

  protected async loadShippingOptions(): Promise<void> {
    if (!this.cart()?.cartId) {
      return;
    }

    if (this.useManualAddress() && this.shippingForm.invalid) {
      this.shippingForm.markAllAsTouched();
      return;
    }

    this.isLoadingShipping.set(true);
    this.errorMessage.set(null);
    this.resetShippingSelection();
    try {
      const response = await this.cartService.getShippingOptions({
        cartId: this.cart()?.cartId ?? null,
        deliveryAddressId: this.useManualAddress() ? null : this.selectedDeliveryAddressId(),
        deliveryAddress: this.useManualAddress() ? this.createManualDeliveryAddress() : null
      });
      this.shippingOptions.set(response.options);
      this.selectedDeliveryMethodId.set(response.options[0]?.deliveryMethodId ?? null);
    } catch (error) {
      this.shippingOptions.set([]);
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoadingShipping.set(false);
    }
  }

  protected selectDeliveryMethod(deliveryMethodId: string): void {
    this.selectedDeliveryMethodId.set(deliveryMethodId);
  }

  protected selectedShippingOption(): CartShippingOptionResponse | null {
    const selectedId = this.selectedDeliveryMethodId();
    return this.shippingOptions().find(option => option.deliveryMethodId === selectedId) ?? null;
  }

  protected orderTotal(): number {
    return (this.cart()?.subtotal ?? 0) + (this.selectedShippingOption()?.shippingAmount ?? 0);
  }

  protected deliveryEstimate(option: CartShippingOptionResponse): string {
    return option.estimatedMinDays === option.estimatedMaxDays
      ? `${option.estimatedMinDays} day${option.estimatedMinDays === 1 ? '' : 's'}`
      : `${option.estimatedMinDays}-${option.estimatedMaxDays} days`;
  }

  private applyDefaultAddress(addresses: BuyerDeliveryAddressResponse[]): void {
    const defaultAddress = addresses.find(address => address.isDefault) ?? addresses[0] ?? null;
    if (defaultAddress) {
      this.selectedDeliveryAddressId.set(defaultAddress.deliveryAddressId);
      this.useManualAddress.set(false);
      this.resetShippingSelection();
      return;
    }

    this.useOneOffAddress();
  }

  private createManualDeliveryAddress(): OrderDeliveryAddressRequest {
    return {
      recipientName: this.shippingForm.controls.fullName.value,
      phoneNumber: this.shippingForm.controls.phone.value,
      addressLine1: this.shippingForm.controls.addressLine1.value,
      addressLine2: emptyToNull(this.shippingForm.controls.addressLine2.value),
      suburb: emptyToNull(this.shippingForm.controls.suburb.value),
      city: this.shippingForm.controls.city.value,
      province: this.shippingForm.controls.province.value,
      postalCode: this.shippingForm.controls.postalCode.value,
      countryCode: this.shippingForm.controls.countryCode.value,
      deliveryInstructions: emptyToNull(this.shippingForm.controls.deliveryInstructions.value)
    };
  }

  private resetShippingSelection(): void {
    this.shippingOptions.set([]);
    this.selectedDeliveryMethodId.set(null);
  }
}

function emptyToNull(value: string): string | null {
  const normalized = value.trim();
  return normalized.length > 0 ? normalized : null;
}
