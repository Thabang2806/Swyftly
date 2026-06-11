import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerInventoryMovementResponse, SellerInventoryMovementType } from '../seller/seller-inventory.models';
import { SellerInventoryService } from '../seller/seller-inventory.service';
import {
  SellerReturnItemResult,
  SellerReturnRequestResult,
  SellerReturnRestockCondition,
  SellerReturnRestockDecisionResponse
} from '../seller/seller-return.models';
import { SellerReturnService } from '../seller/seller-return.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-return-detail-page',
  imports: [
    DatePipe,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page">
      <app-seller-workspace-nav />

      <a class="admin-back-link" routerLink="/returns">Back to returns</a>

      <app-page-header
        eyebrow="Seller operations"
        [heading]="returnRequest() ? 'Return ' + returnRequest()!.returnRequestId : 'Return'"
        description="Review the buyer return request and respond with a seller decision."
      >
        <div pageHeaderActions>
          @if (returnRequest()) {
            <app-status-badge [label]="returnRequest()!.status" [tone]="statusTone(returnRequest()!.status)" />
          }
        </div>
      </app-page-header>

      @if (isLoading()) {
        <div class="route-card">Loading return...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        @if (returnRequest()) {
          <div class="seller-detail-grid">
            <section class="seller-panel">
              <h2>Request summary</h2>
              <dl class="seller-facts">
                <div><dt>Order</dt><dd>{{ returnRequest()!.orderId }}</dd></div>
                <div><dt>Reason</dt><dd>{{ returnRequest()!.reason }}</dd></div>
                <div><dt>Requested</dt><dd>{{ returnRequest()!.requestedAtUtc | date:'medium' }}</dd></div>
                <div><dt>Details</dt><dd>{{ returnRequest()!.details ?? 'No extra details provided.' }}</dd></div>
                @if (returnRequest()!.disputeReason) {
                  <div><dt>Dispute</dt><dd>{{ returnRequest()!.disputeReason }}</dd></div>
                }
              </dl>
            </section>

            <section class="seller-panel">
              <h2>Seller response</h2>
              <p>Approve or reject the return based on the policy context and item condition provided by the buyer.</p>
              <form [formGroup]="responseForm" class="seller-form-grid" novalidate>
                <label class="ui-field">
                  <span>Response message</span>
                  <textarea rows="4" formControlName="message"></textarea>
                </label>

                <div class="seller-action-row">
                  <button data-ui-button="primary" type="button" [disabled]="isActing()" (click)="approveReturn()">Approve return</button>
                  <button data-ui-button="secondary" type="button" [disabled]="isActing()" (click)="rejectReturn()">Reject return</button>
                </div>
              </form>
            </section>

            <section class="seller-panel">
              <h2>Store policy snapshot</h2>
              <p>Checkout-time policy context for this order. Use it as guidance, not automatic approval logic.</p>
              @if (sellerPolicySnapshotEntries().length > 0) {
                <dl class="seller-facts">
                  @for (entry of sellerPolicySnapshotEntries(); track entry.label) {
                    <div><dt>{{ entry.label }}</dt><dd>{{ entry.value }}</dd></div>
                  }
                </dl>
              } @else {
                <app-ui-alert tone="info">This return does not have checkout-time store-policy context.</app-ui-alert>
              }
            </section>
          </div>

          <section class="seller-panel">
            <h2>Return items</h2>
            <div class="seller-item-list">
              @for (item of returnRequest()!.items; track item.returnItemId) {
                <div class="seller-item-row">
                  <span>
                    <strong>{{ item.quantity }} requested</strong>
                    <small>Order item {{ item.orderItemId }}</small>
                  </span>
                  <span>{{ item.reason }}</span>
                  <span>{{ item.isOpenedOrUnsealed ? 'Opened or unsealed' : 'Unopened' }}</span>
                  @if (item.note) {
                    <small>{{ item.note }}</small>
                  }
                </div>
              }
            </div>
          </section>

          <section class="seller-panel">
            <h2>Restock decision</h2>
            <p>Record what happened to the returned stock after inspection. This is traceability only; refunds and return status do not restock items automatically.</p>

            @if (restockError()) {
              <app-ui-alert tone="warning">{{ restockError() }}</app-ui-alert>
            }

            @if (isRestockLoading()) {
              <p>Loading restock decisions...</p>
            } @else {
              @if (restockDecisions().length > 0) {
                <div class="seller-timeline">
                  @for (decision of restockDecisions(); track decision.restockDecisionId) {
                    <article class="seller-timeline-item">
                      <app-status-badge [label]="restockConditionLabel(decision.condition)" [tone]="decision.quantityRestocked > 0 ? 'accent' : 'neutral'" />
                      <strong>{{ decision.sku }} - {{ decision.size }} / {{ decision.colour }}</strong>
                      <small>{{ decision.createdAtUtc | date:'medium' }}</small>
                      <small>{{ decision.quantityRestocked }} of {{ decision.quantityReturned }} returned item{{ decision.quantityReturned === 1 ? '' : 's' }} restocked</small>
                      <p>{{ decision.reason }}</p>
                    </article>
                  }
                </div>
              }

              @if (!canRecordRestockDecisions()) {
                <app-ui-alert tone="info">Restock decisions become available after the return is approved, returned, refunded, or closed.</app-ui-alert>
              } @else if (pendingRestockItems().length === 0) {
                <app-ui-alert tone="success">Restock decisions have been recorded for every item on this return.</app-ui-alert>
              } @else {
                <form [formGroup]="restockForm" class="seller-form-grid" novalidate (ngSubmit)="recordRestockDecision()">
                  <label class="ui-field">
                    <span>Return item</span>
                    <select formControlName="returnItemId">
                      @for (item of pendingRestockItems(); track item.returnItemId) {
                        <option [value]="item.returnItemId">{{ item.quantity }} x {{ item.reason }} - {{ item.productVariantId }}</option>
                      }
                    </select>
                  </label>

                  <label class="ui-field">
                    <span>Quantity restocked</span>
                    <input
                      type="number"
                      min="0"
                      [attr.max]="selectedRestockItem()?.quantity ?? null"
                      formControlName="quantityRestocked"
                    />
                  </label>

                  <label class="ui-field">
                    <span>Inspection condition</span>
                    <select formControlName="condition">
                      @for (condition of restockConditions; track condition.value) {
                        <option [value]="condition.value">{{ condition.label }}</option>
                      }
                    </select>
                  </label>

                  <label class="ui-field">
                    <span>Decision reason</span>
                    <textarea rows="3" formControlName="reason"></textarea>
                  </label>

                  <div class="seller-action-row">
                    <button data-ui-button="primary" type="submit" [disabled]="isRestocking() || pendingRestockItems().length === 0">
                      Record restock decision
                    </button>
                  </div>
                </form>
              }
            }
          </section>

          <section class="seller-panel">
            <h2>Stock ledger context</h2>
            <p>Return and refund stock ledger events are inrenderional. Returns and refunds do not automatically restock inventory.</p>
            @if (isStockLedgerLoading()) {
              <p>Loading stock ledger...</p>
            } @else if (stockLedgerError()) {
              <app-ui-alert tone="warning">{{ stockLedgerError() }}</app-ui-alert>
            } @else if (stockLedger().length === 0) {
              <p>No return or refund stock ledger events have been recorded yet.</p>
            } @else {
              <div class="seller-timeline">
                @for (movement of stockLedger(); track movement.movementId) {
                  <article class="seller-timeline-item">
                    <app-status-badge [label]="movementLabel(movement)" [tone]="movementTone(movement)" />
                    <strong>{{ movement.productTitle }}</strong>
                    <small>{{ movement.sku }} - {{ movement.occurredAtUtc | date:'medium' }}</small>
                    <small>Stock {{ movement.stockQuantityBefore }} -> {{ movement.stockQuantityAfter }} ({{ movement.quantityDelta > 0 ? '+' : '' }}{{ movement.quantityDelta }})</small>
                    <small>Reserved {{ movement.reservedQuantityBefore }} -> {{ movement.reservedQuantityAfter }} ({{ movement.reservedQuantityDelta > 0 ? '+' : '' }}{{ movement.reservedQuantityDelta }})</small>
                    <p>{{ movement.reason }}</p>
                  </article>
                }
              </div>
            }
          </section>

          <section class="seller-panel">
            <h2>Messages</h2>
            @if (returnRequest()!.messages.length === 0) {
              <p>No return messages yet.</p>
            } @else {
              <div class="seller-message-list">
                @for (message of returnRequest()!.messages; track message.returnMessageId) {
                  <article>
                    <strong>{{ message.senderRole }}</strong>
                    <span>{{ message.createdAtUtc | date:'medium' }}</span>
                    <p>{{ message.message }}</p>
                  </article>
                }
              </div>
            }
          </section>
        }
      }
    </section>
  `
})
export class SellerReturnDetailPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly inventoryService = inject(SellerInventoryService);
  private readonly returnService = inject(SellerReturnService);
  private readonly route = inject(ActivatedRoute);

  protected readonly returnRequest = signal<SellerReturnRequestResult | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly stockLedger = signal<SellerInventoryMovementResponse[]>([]);
  protected readonly isStockLedgerLoading = signal(false);
  protected readonly stockLedgerError = signal<string | null>(null);
  protected readonly restockDecisions = signal<SellerReturnRestockDecisionResponse[]>([]);
  protected readonly isRestockLoading = signal(false);
  protected readonly isRestocking = signal(false);
  protected readonly restockError = signal<string | null>(null);

  protected readonly restockConditions: { value: SellerReturnRestockCondition; label: string }[] = [
    { value: 'Sellable', label: 'Sellable' },
    { value: 'Damaged', label: 'Damaged' },
    { value: 'OpenedOrUsed', label: 'Opened or used' },
    { value: 'Missing', label: 'Missing' },
    { value: 'Other', label: 'Other' }
  ];

  protected readonly responseForm = this.formBuilder.group({
    message: ['']
  });

  protected readonly restockForm = this.formBuilder.group({
    returnItemId: [''],
    quantityRestocked: [0],
    condition: ['Sellable' as SellerReturnRestockCondition],
    reason: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.loadReturn();
  }

  protected async approveReturn(): Promise<void> {
    await this.respond(
      () => this.returnService.approveReturn(this.returnRequestId(), { message: this.messageValue() }),
      'Return approved.');
  }

  protected async rejectReturn(): Promise<void> {
    await this.respond(
      () => this.returnService.rejectReturn(this.returnRequestId(), { message: this.messageValue() }),
      'Return rejected.');
  }

  protected async recordRestockDecision(): Promise<void> {
    if (this.isRestocking()) {
      return;
    }

    const returnItem = this.selectedRestockItem();
    const reason = this.restockForm.controls.reason.value.trim();
    if (!returnItem) {
      this.restockError.set('Choose a return item before recording a restock decision.');
      return;
    }

    if (reason.length === 0) {
      this.restockError.set('Add a reason so the stock ledger explains the decision.');
      return;
    }

    this.isRestocking.set(true);
    this.restockError.set(null);
    this.successMessage.set(null);

    try {
      const quantityRestocked = Number(this.restockForm.controls.quantityRestocked.value);
      if (!Number.isInteger(quantityRestocked) || quantityRestocked < 0 || quantityRestocked > returnItem.quantity) {
        this.restockError.set(`Quantity restocked must be between 0 and ${returnItem.quantity}.`);
        return;
      }

      const decisions = await this.returnService.createRestockDecisions(this.returnRequestId(), {
        items: [{
          returnItemId: returnItem.returnItemId,
          quantityRestocked,
          condition: this.restockForm.controls.condition.value,
          reason
        }]
      });
      this.restockDecisions.set(decisions);
      this.selectNextRestockItem();
      await this.loadStockLedger(this.returnRequestId());
      this.successMessage.set('Restock decision recorded.');
    } catch (error) {
      this.restockError.set(getApiErrorMessage(error));
    } finally {
      this.isRestocking.set(false);
    }
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Requested', 'AwaitingSellerResponse', 'ReturnInTransit', 'ReturnedToSeller', 'RefundPending'].includes(status)) {
      return 'warning';
    }

    if (['Approved', 'Refunded', 'Closed'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'Disputed'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  protected sellerPolicySnapshotEntries(): { label: string; value: string }[] {
    const snapshot = this.returnRequest()?.sellerPolicySnapshot;
    if (!snapshot) {
      return [];
    }

    return [
      snapshot.returnWindowDays === null ? null : { label: 'Return window', value: `${snapshot.returnWindowDays} day${snapshot.returnWindowDays === 1 ? '' : 's'}` },
      snapshot.returnPolicy ? { label: 'Returns', value: snapshot.returnPolicy } : null,
      snapshot.exchangePolicy ? { label: 'Exchanges', value: snapshot.exchangePolicy } : null,
      snapshot.fulfilmentPolicy ? { label: 'Fulfilment', value: snapshot.fulfilmentPolicy } : null,
      snapshot.supportPolicy ? { label: 'Support', value: snapshot.supportPolicy } : null,
      snapshot.careInstructions ? { label: 'Care', value: snapshot.careInstructions } : null,
      snapshot.productDisclaimer ? { label: 'Disclaimer', value: snapshot.productDisclaimer } : null
    ].filter((entry): entry is { label: string; value: string } => entry !== null);
  }

  protected movementTone(movement: SellerInventoryMovementResponse): StatusBadgeTone {
    if (movement.movementType === 'RefundCompleted') {
      return 'accent';
    }

    if (movement.movementType === 'ReturnRestocked') {
      return 'success';
    }

    if (movement.movementType === 'ReturnRequested') {
      return 'warning';
    }

    return movement.quantityDelta === 0 && movement.reservedQuantityDelta === 0 ? 'neutral' : 'accent';
  }

  protected movementLabel(movement: SellerInventoryMovementResponse): string {
    const labels: Record<SellerInventoryMovementType, string> = {
      SellerAdjustment: 'Adjustment',
      BulkImportAdjustment: 'Bulk import',
      ReservationCreated: 'Reserved',
      ReservationReleased: 'Released',
      ReservationExpired: 'Expired',
      ReservationConfirmed: 'Confirmed',
      PaymentFailedReservationReleased: 'Payment release',
      ReturnRequested: 'Return requested',
      RefundCompleted: 'Refund completed',
      ReturnRestocked: 'Restocked'
    };

    return labels[movement.movementType] ?? movement.movementType;
  }

  protected canRecordRestockDecisions(): boolean {
    return ['Approved', 'ReturnedToSeller', 'RefundPending', 'Refunded', 'Closed'].includes(this.returnRequest()?.status ?? '');
  }

  protected pendingRestockItems(): SellerReturnItemResult[] {
    const decidedItemIds = new Set(this.restockDecisions().map(decision => decision.returnItemId));
    return this.returnRequest()?.items.filter(item => !decidedItemIds.has(item.returnItemId)) ?? [];
  }

  protected selectedRestockItem(): SellerReturnItemResult | null {
    const selectedId = this.restockForm.controls.returnItemId.value;
    return this.pendingRestockItems().find(item => item.returnItemId === selectedId) ?? this.pendingRestockItems()[0] ?? null;
  }

  protected restockConditionLabel(condition: SellerReturnRestockCondition): string {
    return this.restockConditions.find(item => item.value === condition)?.label ?? condition;
  }

  private async loadReturn(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const returnRequest = await this.returnService.getReturn(this.returnRequestId());
      this.returnRequest.set(returnRequest);
      await this.loadRestockDecisions(returnRequest.returnRequestId);
      await this.loadStockLedger(returnRequest.returnRequestId);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async loadRestockDecisions(returnRequestId: string): Promise<void> {
    this.isRestockLoading.set(true);
    this.restockError.set(null);

    try {
      this.restockDecisions.set(await this.returnService.listRestockDecisions(returnRequestId));
      this.selectNextRestockItem();
    } catch (error) {
      this.restockError.set(getApiErrorMessage(error));
    } finally {
      this.isRestockLoading.set(false);
    }
  }

  private async loadStockLedger(returnRequestId: string): Promise<void> {
    this.isStockLedgerLoading.set(true);
    this.stockLedgerError.set(null);

    try {
      this.stockLedger.set(await this.inventoryService.listHistory({ returnRequestId }));
    } catch (error) {
      this.stockLedgerError.set(getApiErrorMessage(error));
    } finally {
      this.isStockLedgerLoading.set(false);
    }
  }

  private async respond(action: () => Promise<SellerReturnRequestResult>, successMessage: string): Promise<void> {
    if (this.isActing()) {
      return;
    }

    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const returnRequest = await action();
      this.returnRequest.set(returnRequest);
      await this.loadRestockDecisions(returnRequest.returnRequestId);
      await this.loadStockLedger(returnRequest.returnRequestId);
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  private messageValue(): string | null {
    const trimmed = this.responseForm.controls.message.value.trim();
    return trimmed.length === 0 ? null : trimmed;
  }

  private returnRequestId(): string {
    return this.route.snapshot.paramMap.get('returnRequestId') ?? '';
  }

  private selectNextRestockItem(): void {
    const nextItem = this.pendingRestockItems()[0];
    this.restockForm.patchValue({
      returnItemId: nextItem?.returnItemId ?? '',
      quantityRestocked: 0,
      condition: 'Sellable',
      reason: ''
    });
  }
}
