import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import {
  SellerInventoryBulkAdjustmentResponse,
  SellerInventoryBulkAdjustmentRowResponse,
  SellerInventoryItemResponse,
  SellerInventoryMovementResponse,
  SellerInventoryMovementType,
  SellerInventoryVariantStatus
} from '../seller/seller-inventory.models';
import { SellerInventoryService } from '../seller/seller-inventory.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { MetricTileComponent } from '../shared/ui/metric-tile.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

type InventoryFilter = 'All' | 'LowStock' | 'OutOfStock' | 'Reserved';
type InventoryHistoryTypeFilter = 'All' | SellerInventoryMovementType;

@Component({
  selector: 'app-seller-inventory-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    MetricTileComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page seller-inventory-page">
      <app-seller-workspace-nav />

      <app-page-header
        eyebrow="Seller operations"
        heading="Inventory"
        description="Adjust stock and sellable variant status for live and draft products without reopening the product editor."
      />

      @if (isLoading()) {
        <div class="route-card">Loading inventory...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (successMessage()) {
          <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
        }

        <section class="hf-metric-grid seller-inventory-metrics" aria-label="Inventory summary">
          <app-metric-tile label="Variants" [value]="items().length.toString()" badge="Total rows" badgeTone="neutral" />
          <app-metric-tile label="Low stock" [value]="lowStockCount().toString()" badge="1-5 available" badgeTone="warning" />
          <app-metric-tile label="Out of stock" [value]="outOfStockCount().toString()" badge="No available units" badgeTone="danger" />
          <app-metric-tile label="Reserved" [value]="reservedCount().toString()" badge="Active reservations" badgeTone="accent" />
        </section>

        <section class="seller-filter-bar" aria-label="Inventory filters">
          <label class="ui-field">
            <span>Search inventory</span>
            <input [value]="searchTerm()" (input)="updateSearch($event)" placeholder="Product, SKU, or barcode" />
          </label>

          <label class="ui-field seller-scanner-field">
            <span>Scanner quick search</span>
            <input
              [value]="scannerTerm()"
              (input)="updateScannerTerm($event)"
              (keydown.enter)="submitScannerSearch($event)"
              placeholder="Scan SKU or barcode"
            />
          </label>

          <button data-ui-button="secondary" type="button" class="seller-scanner-action" (click)="submitScannerSearch()">
            Find scanned item
          </button>

          <label class="ui-field">
            <span>Stock state</span>
            <select [value]="stockFilter()" (change)="updateStockFilter($event)">
              <option value="All">All inventory</option>
              <option value="LowStock">Low stock</option>
              <option value="OutOfStock">Out of stock</option>
              <option value="Reserved">Reserved stock</option>
            </select>
          </label>

          <label class="ui-field">
            <span>History type</span>
            <select [value]="historyTypeFilter()" (change)="updateHistoryTypeFilter($event)">
              @for (option of historyTypeOptions; track option.value) {
                <option [value]="option.value">{{ option.label }}</option>
              }
            </select>
          </label>
        </section>

        @if (scannerMessage()) {
          <app-ui-alert tone="info">{{ scannerMessage() }}</app-ui-alert>
        }

        <section class="route-card seller-inventory-history-summary" aria-label="Inventory movement search">
          <div>
            <span class="eyebrow">Movement history</span>
            <h2>Search stock changes</h2>
            <p>Use product, SKU, or barcode search, or scan into the quick search field and press Enter to open an exact variant match.</p>
          </div>
          <button data-ui-button="secondary" type="button" [disabled]="isHistoryLoading()" (click)="loadFilteredHistory()">
            {{ isHistoryLoading() ? 'Loading history...' : 'Load matching history' }}
          </button>
        </section>

        @if (historyErrorMessage()) {
          <app-ui-alert tone="error">{{ historyErrorMessage() }}</app-ui-alert>
        }

        @if (filteredHistory().length > 0) {
          <section class="route-card seller-inventory-history-card" aria-label="Filtered inventory movement history">
            <div class="seller-section-heading">
              <span class="eyebrow">Recent movements</span>
              <h2>{{ filteredHistory().length }} matching stock changes</h2>
            </div>
            <div class="admin-table seller-ops-table seller-inventory-history-table" role="table" aria-label="Inventory movement history">
              <div class="admin-table-row heading seller-ops-table-row" role="row">
                <span role="columnheader">Variant</span>
                <span role="columnheader">Movement</span>
                <span role="columnheader">Stock / reserved</span>
                <span role="columnheader">Reason</span>
              </div>
              @for (movement of filteredHistory(); track movement.movementId) {
                <div class="admin-table-row seller-ops-table-row" role="row">
                  <span role="cell">
                    <strong>{{ movement.productTitle }}</strong>
                    <small>{{ movement.sku }}{{ movement.barcode ? ' / ' + movement.barcode : '' }}</small>
                  </span>
                  <span role="cell">
                    <app-status-badge [label]="movementLabel(movement)" [tone]="movementTone(movement)" />
                    <small>{{ movement.occurredAtUtc | date:'medium' }}</small>
                  </span>
                  <span role="cell">
                    <strong>Stock {{ movement.stockQuantityBefore }} -> {{ movement.stockQuantityAfter }}</strong>
                    <small>{{ movement.quantityDelta > 0 ? '+' : '' }}{{ movement.quantityDelta }} stock units</small>
                    <small>Reserved {{ movement.reservedQuantityBefore }} -> {{ movement.reservedQuantityAfter }} ({{ movement.reservedQuantityDelta > 0 ? '+' : '' }}{{ movement.reservedQuantityDelta }})</small>
                  </span>
                  <span role="cell">
                    <strong>{{ movement.reason }}</strong>
                    <small>{{ movement.batchReference ?? movement.source }}</small>
                    @if (movement.relatedRoute) {
                      <a [routerLink]="movement.relatedRoute">Open related record</a>
                    }
                  </span>
                </div>
              }
            </div>
          </section>
        }

        <section class="route-card seller-inventory-bulk" aria-label="Bulk inventory import and export">
          <div>
            <span class="eyebrow">Bulk stocktake</span>
            <h2>Import or export inventory</h2>
            <p>Use CSV for stock quantity and variant status only. Product details, pricing, and variant structure stay managed in the product editor.</p>
          </div>

          <div class="auth-actions">
            <button data-ui-button="secondary" type="button" [disabled]="isDownloading()" (click)="downloadExport()">
              Export inventory CSV
            </button>
            <button data-ui-button="ghost" type="button" [disabled]="isDownloading()" (click)="downloadTemplate()">
              Download template
            </button>
          </div>

          <form class="seller-bulk-import-form" [formGroup]="bulkForm" (ngSubmit)="applyBulkAdjustment()" novalidate>
            <label class="seller-file-input">
              <span>CSV file</span>
              <input type="file" accept=".csv,text/csv" (change)="onImportFileSelected($event)" />
              <strong>{{ selectedImportFile()?.name ?? 'Choose inventory CSV' }}</strong>
            </label>

            <button data-ui-button="secondary" type="button" [disabled]="!selectedImportFile() || isPreviewing()" (click)="previewImport()">
              {{ isPreviewing() ? 'Previewing...' : 'Preview import' }}
            </button>

            @if (importPreview()) {
              <label class="ui-field">
                <span>Batch reason</span>
                <textarea rows="3" formControlName="reason"></textarea>
                @if (bulkForm.controls.reason.hasError('required')) {
                  <span class="ui-field-error">A batch reason is required for audit.</span>
                }
              </label>

              <button
                data-ui-button="primary"
                type="submit"
                [disabled]="isApplyingBulk() || bulkForm.invalid || importPreview()!.errorRows > 0 || importPreview()!.changedRows === 0"
              >
                {{ isApplyingBulk() ? 'Applying...' : 'Apply valid import' }}
              </button>
            }
          </form>

          @if (importPreview(); as preview) {
            <div class="seller-import-summary">
              <app-status-badge [label]="preview.changedRows + ' changed'" tone="accent" />
              <app-status-badge [label]="preview.unchangedRows + ' unchanged'" tone="neutral" />
              <app-status-badge [label]="preview.errorRows + ' errors'" [tone]="preview.errorRows > 0 ? 'danger' : 'success'" />
            </div>

            @if (preview.errorRows > 0) {
              <app-ui-alert tone="warning">
                Fix the CSV rows with errors before applying. Bulk inventory updates are all-or-nothing.
              </app-ui-alert>
            } @else if (preview.changedRows === 0) {
              <app-ui-alert tone="info">
                The preview is valid, but no stock or status values would change.
              </app-ui-alert>
            }

            <div class="admin-table seller-ops-table seller-import-preview-table" role="table" aria-label="Inventory import preview">
              <div class="admin-table-row heading seller-ops-table-row" role="row">
                <span role="columnheader">Row</span>
                <span role="columnheader">Variant</span>
                <span role="columnheader">Current</span>
                <span role="columnheader">Proposed</span>
                <span role="columnheader">Status</span>
              </div>

              @for (row of preview.rows; track row.rowNumber) {
                <div class="admin-table-row seller-ops-table-row" role="row">
                  <span role="cell">#{{ row.rowNumber }}</span>
                  <span role="cell">
                    <strong>{{ row.productTitle ?? row.sku ?? row.variantId ?? 'Unmatched row' }}</strong>
                    <small>{{ row.size ?? '-' }} / {{ row.colour ?? '-' }}{{ row.barcode ? ' / ' + row.barcode : '' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ row.currentStockQuantity ?? '-' }} stock</strong>
                    <small>{{ row.currentStatus ?? 'Unknown' }}</small>
                  </span>
                  <span role="cell">
                    <strong>{{ row.proposedStockQuantity ?? '-' }} stock</strong>
                    <small>{{ row.proposedStatus ?? 'Unknown' }}</small>
                  </span>
                  <span role="cell">
                    <app-status-badge [label]="row.rowStatus" [tone]="importRowTone(row)" />
                    @if (row.messages.length > 0) {
                      <small>{{ row.messages.join(' ') }}</small>
                    }
                  </span>
                </div>
              }
            </div>
          }
        </section>

        @if (items().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Inventory"
            heading="No variants yet"
            message="Create product variants before managing operational stock."
          >
            <a data-ui-button="primary" routerLink="/products">Open products</a>
          </app-empty-state>
        } @else if (filteredItems().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Filters"
            heading="No inventory rows match"
            message="Adjust the search or stock filter to inspect another product variant."
          />
        } @else {
          <div class="seller-inventory-layout">
            <div class="admin-table seller-ops-table seller-inventory-table" role="table" aria-label="Seller inventory">
              <div class="admin-table-row heading seller-ops-table-row" role="row">
                <span role="columnheader">Product</span>
                <span role="columnheader">Variant</span>
                <span role="columnheader">Stock</span>
                <span role="columnheader">Status</span>
                <span role="columnheader">Action</span>
              </div>

              @for (item of filteredItems(); track item.variantId) {
                <div
                  class="admin-table-row seller-ops-table-row"
                  [class.seller-inventory-row-scanned]="selectedItem()?.variantId === item.variantId"
                  role="row"
                >
                  <span role="cell" class="seller-inventory-product-cell">
                    @if (item.primaryImageUrl) {
                      <img [src]="item.primaryImageUrl" [alt]="item.primaryImageAltText ?? item.productTitle ?? 'Product image'" />
                    } @else {
                      <span class="seller-inventory-thumb-fallback">SW</span>
                    }
                    <span>
                      <strong>{{ item.productTitle ?? 'Untitled product' }}</strong>
                      <small>{{ item.productSlug ?? item.productId }}</small>
                    </span>
                  </span>
                  <span role="cell">
                    <strong>{{ item.sku }}</strong>
                    <small>{{ item.size }} / {{ item.colour }} / {{ item.price | currency:'ZAR':'symbol-narrow' }}</small>
                    @if (item.barcode) {
                      <small>Barcode {{ item.barcode }}</small>
                    }
                  </span>
                  <span role="cell">
                    <strong>{{ item.availableQuantity }} available</strong>
                    <small>{{ item.stockQuantity }} stock, {{ item.reservedQuantity }} reserved</small>
                  </span>
                  <span role="cell">
                    <app-status-badge [label]="item.variantStatus" [tone]="variantTone(item)" />
                    <small>{{ item.productStatus }} product, updated {{ item.updatedAtUtc | date:'mediumDate' }}</small>
                  </span>
                  <span role="cell" class="auth-actions">
                    <button data-ui-button="secondary" type="button" (click)="selectItem(item)">Adjust</button>
                    <button data-ui-button="ghost" type="button" (click)="selectItem(item)">History</button>
                    <a data-ui-button="ghost" [routerLink]="['/products', item.productId, 'edit']">Product</a>
                  </span>
                </div>
              }
            </div>

            <aside class="route-card seller-inventory-adjustment" aria-label="Inventory adjustment panel">
              @if (selectedItem(); as selected) {
                <span class="eyebrow">Adjust stock</span>
                <h2>{{ selected.productTitle ?? 'Selected product' }}</h2>
                <p>{{ selected.sku }} currently has {{ selected.availableQuantity }} available and {{ selected.reservedQuantity }} reserved.</p>
                @if (selected.barcode) {
                  <p class="seller-muted-copy">Barcode {{ selected.barcode }}</p>
                }

                <form [formGroup]="adjustmentForm" (ngSubmit)="submitAdjustment()" class="wizard-form" novalidate>
                  <label class="ui-field">
                    <span>Stock quantity</span>
                    <input type="number" min="0" formControlName="stockQuantity" />
                    @if (adjustmentForm.controls.stockQuantity.hasError('required')) {
                      <span class="ui-field-error">Stock quantity is required.</span>
                    } @else if (adjustmentForm.controls.stockQuantity.hasError('min')) {
                      <span class="ui-field-error">Stock cannot be negative.</span>
                    }
                  </label>

                  <label class="ui-field">
                    <span>Variant status</span>
                    <select formControlName="status">
                      @for (status of variantStatuses; track status) {
                        <option [value]="status">{{ status }}</option>
                      }
                    </select>
                  </label>

                  <label class="ui-field">
                    <span>Reason</span>
                    <textarea rows="4" formControlName="reason"></textarea>
                    @if (adjustmentForm.controls.reason.hasError('required')) {
                      <span class="ui-field-error">A reason is required for audit.</span>
                    }
                  </label>

                  @if (stockBelowReserved()) {
                    <app-ui-alert tone="warning">
                      Stock cannot be lower than the {{ selected.reservedQuantity }} units currently reserved.
                    </app-ui-alert>
                  }

                  <button data-ui-button="primary" type="submit" [disabled]="isSaving() || adjustmentForm.invalid || stockBelowReserved()">
                    {{ isSaving() ? 'Saving...' : 'Save adjustment' }}
                  </button>
                </form>

                <div class="seller-inventory-history-panel">
                  <div class="seller-section-heading">
                    <span class="eyebrow">Variant history</span>
                    <h3>Recent stock changes</h3>
                  </div>

                  @if (isVariantHistoryLoading()) {
                    <p>Loading movement history...</p>
                  } @else if (selectedHistory().length === 0) {
                    <p>No stock movement history has been recorded for this variant yet.</p>
                  } @else {
                    <div class="seller-timeline">
                      @for (movement of selectedHistory(); track movement.movementId) {
                        <article class="seller-timeline-item">
                          <app-status-badge [label]="movementLabel(movement)" [tone]="movementTone(movement)" />
                          <strong>{{ movement.stockQuantityBefore }} -> {{ movement.stockQuantityAfter }} stock</strong>
                          <small>{{ movement.quantityDelta > 0 ? '+' : '' }}{{ movement.quantityDelta }} stock units, {{ movement.occurredAtUtc | date:'medium' }}</small>
                          <small>Reserved {{ movement.reservedQuantityBefore }} -> {{ movement.reservedQuantityAfter }} ({{ movement.reservedQuantityDelta > 0 ? '+' : '' }}{{ movement.reservedQuantityDelta }})</small>
                          <p>{{ movement.reason }}</p>
                          @if (movement.batchReference) {
                            <small>Batch {{ movement.batchReference }}</small>
                          }
                          @if (movement.relatedRoute) {
                            <a [routerLink]="movement.relatedRoute">Open related record</a>
                          }
                        </article>
                      }
                    </div>
                  }
                </div>
              } @else {
                <span class="eyebrow">Stock control</span>
                <h2>Select a variant</h2>
                <p>Choose an inventory row to adjust absolute stock quantity or take the variant out of active selling.</p>
              }
            </aside>
          </div>
        }
      }
    </section>
  `
})
export class SellerInventoryPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly inventoryService = inject(SellerInventoryService);

  protected readonly items = signal<SellerInventoryItemResponse[]>([]);
  protected readonly selectedItem = signal<SellerInventoryItemResponse | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly scannerTerm = signal('');
  protected readonly stockFilter = signal<InventoryFilter>('All');
  protected readonly historyTypeFilter = signal<InventoryHistoryTypeFilter>('All');
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly isDownloading = signal(false);
  protected readonly isPreviewing = signal(false);
  protected readonly isApplyingBulk = signal(false);
  protected readonly isHistoryLoading = signal(false);
  protected readonly isVariantHistoryLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly historyErrorMessage = signal<string | null>(null);
  protected readonly scannerMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly selectedImportFile = signal<File | null>(null);
  protected readonly importPreview = signal<SellerInventoryBulkAdjustmentResponse | null>(null);
  protected readonly filteredHistory = signal<SellerInventoryMovementResponse[]>([]);
  protected readonly selectedHistory = signal<SellerInventoryMovementResponse[]>([]);
  protected readonly variantStatuses: readonly SellerInventoryVariantStatus[] = ['Active', 'Inactive', 'OutOfStock'];
  protected readonly historyTypeOptions: readonly { value: InventoryHistoryTypeFilter; label: string }[] = [
    { value: 'All', label: 'All movements' },
    { value: 'SellerAdjustment', label: 'Single adjustments' },
    { value: 'BulkImportAdjustment', label: 'Bulk imports' },
    { value: 'ReservationCreated', label: 'Reservations created' },
    { value: 'ReservationReleased', label: 'Reservations released' },
    { value: 'ReservationExpired', label: 'Reservations expired' },
    { value: 'ReservationConfirmed', label: 'Reservations confirmed' },
    { value: 'PaymentFailedReservationReleased', label: 'Payment-failed releases' },
    { value: 'ReturnRequested', label: 'Return requests' },
    { value: 'RefundCompleted', label: 'Refund completions' },
    { value: 'ReturnRestocked', label: 'Return restocks' }
  ];

  protected readonly adjustmentForm = this.formBuilder.group({
    stockQuantity: [0, [Validators.required, Validators.min(0)]],
    status: ['Active' as SellerInventoryVariantStatus, [Validators.required]],
    reason: ['', [Validators.required]]
  });

  protected readonly bulkForm = this.formBuilder.group({
    reason: ['', [Validators.required]]
  });

  protected readonly filteredItems = computed(() => {
    const search = this.searchTerm().trim().toLowerCase();
    const filter = this.stockFilter();

    return this.items().filter(item => {
      const matchesSearch = search.length === 0 ||
        (item.productTitle ?? '').toLowerCase().includes(search) ||
        (item.productSlug ?? '').toLowerCase().includes(search) ||
        item.sku.toLowerCase().includes(search) ||
        (item.barcode ?? '').toLowerCase().includes(search) ||
        item.size.toLowerCase().includes(search) ||
        item.colour.toLowerCase().includes(search);
      const matchesFilter = filter === 'All' ||
        (filter === 'LowStock' && this.isLowStock(item)) ||
        (filter === 'OutOfStock' && this.isOutOfStock(item)) ||
        (filter === 'Reserved' && item.reservedQuantity > 0);

      return matchesSearch && matchesFilter;
    });
  });

  protected readonly lowStockCount = computed(() => this.items().filter(item => this.isLowStock(item)).length);
  protected readonly outOfStockCount = computed(() => this.items().filter(item => this.isOutOfStock(item)).length);
  protected readonly reservedCount = computed(() => this.items().filter(item => item.reservedQuantity > 0).length);

  async ngOnInit(): Promise<void> {
    await this.loadInventory();
  }

  protected updateSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  protected updateScannerTerm(event: Event): void {
    this.scannerTerm.set((event.target as HTMLInputElement).value);
  }

  protected submitScannerSearch(event?: Event): void {
    event?.preventDefault();
    const term = this.scannerTerm().trim();
    this.scannerMessage.set(null);
    if (!term) {
      return;
    }

    const termLower = term.toLowerCase();
    const match = this.items().find(item =>
      item.sku.toLowerCase() === termLower ||
      (item.barcode ?? '').toLowerCase() === termLower);

    this.searchTerm.set(term);
    if (!match) {
      this.scannerMessage.set(`No exact SKU or barcode match was found for ${term}.`);
      return;
    }

    this.selectItem(match);
    this.scannerMessage.set(`Scanner matched ${match.sku}. The variant is selected and its stock history is loading.`);
  }

  protected updateStockFilter(event: Event): void {
    this.stockFilter.set((event.target as HTMLSelectElement).value as InventoryFilter);
  }

  protected updateHistoryTypeFilter(event: Event): void {
    this.historyTypeFilter.set((event.target as HTMLSelectElement).value as InventoryHistoryTypeFilter);
  }

  protected onImportFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedImportFile.set(input.files?.[0] ?? null);
    this.importPreview.set(null);
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  protected async downloadExport(): Promise<void> {
    await this.downloadCsv(
      () => this.inventoryService.exportInventoryCsv(),
      'mabuntle-inventory-export.csv');
  }

  protected async downloadTemplate(): Promise<void> {
    await this.downloadCsv(
      () => this.inventoryService.downloadImportTemplate(),
      'mabuntle-inventory-import-template.csv');
  }

  protected async previewImport(): Promise<void> {
    const file = this.selectedImportFile();
    if (!file || this.isPreviewing()) {
      return;
    }

    this.isPreviewing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const preview = await this.inventoryService.previewImport(file);
      this.importPreview.set(preview);
      this.successMessage.set(`Preview loaded: ${preview.changedRows} changed, ${preview.errorRows} errors.`);
    } catch (error) {
      this.importPreview.set(null);
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isPreviewing.set(false);
    }
  }

  protected async applyBulkAdjustment(): Promise<void> {
    const preview = this.importPreview();
    if (!preview || this.bulkForm.invalid || preview.errorRows > 0 || preview.changedRows === 0 || this.isApplyingBulk()) {
      this.bulkForm.markAllAsTouched();
      return;
    }

    this.isApplyingBulk.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const reason = this.bulkForm.controls.reason.value;
      const result = await this.inventoryService.bulkAdjust({
        reason,
        items: preview.rows
          .filter(row => row.rowStatus !== 'Error')
          .map(row => ({
            variantId: row.variantId,
            sku: row.sku,
            barcode: row.barcode,
            stockQuantity: row.proposedStockQuantity ?? 0,
            status: row.proposedStatus as SellerInventoryVariantStatus
          }))
      });

      this.importPreview.set(result);
      await this.loadInventory();
      await this.refreshSelectedHistory();
      this.successMessage.set(`Bulk inventory applied: ${result.changedRows} changed, ${result.unchangedRows} unchanged.`);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isApplyingBulk.set(false);
    }
  }

  protected selectItem(item: SellerInventoryItemResponse): void {
    this.selectedItem.set(item);
    this.successMessage.set(null);
    this.adjustmentForm.reset({
      stockQuantity: item.stockQuantity,
      status: item.variantStatus,
      reason: ''
    });
    void this.loadVariantHistory(item.variantId);
  }

  protected async submitAdjustment(): Promise<void> {
    const selected = this.selectedItem();
    if (!selected || this.adjustmentForm.invalid || this.stockBelowReserved() || this.isSaving()) {
      this.adjustmentForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const value = this.adjustmentForm.getRawValue();
      const adjusted = await this.inventoryService.adjustInventory(selected.variantId, {
        stockQuantity: value.stockQuantity,
        status: value.status,
        reason: value.reason
      });

      this.items.update(items => items.map(item => item.variantId === adjusted.variantId ? adjusted : item));
      this.selectItem(adjusted);
      await this.loadFilteredHistory({ silentIfEmpty: true });
      this.successMessage.set('Inventory adjustment saved.');
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  protected variantTone(item: SellerInventoryItemResponse): StatusBadgeTone {
    if (this.isOutOfStock(item)) {
      return 'danger';
    }

    if (this.isLowStock(item)) {
      return 'warning';
    }

    if (item.variantStatus === 'Active') {
      return 'success';
    }

    return 'neutral';
  }

  protected importRowTone(row: SellerInventoryBulkAdjustmentRowResponse): StatusBadgeTone {
    if (row.rowStatus === 'Error') {
      return 'danger';
    }

    return row.rowStatus === 'Changed' ? 'accent' : 'neutral';
  }

  protected movementTone(movement: SellerInventoryMovementResponse): StatusBadgeTone {
    if (movement.movementType === 'PaymentFailedReservationReleased' || movement.movementType === 'ReturnRequested') {
      return 'warning';
    }

    if (movement.movementType === 'RefundCompleted') {
      return 'accent';
    }

    if (movement.movementType === 'ReturnRestocked') {
      return 'success';
    }

    if (movement.quantityDelta < 0) {
      return 'warning';
    }

    if (movement.quantityDelta > 0 || movement.reservedQuantityDelta > 0) {
      return 'success';
    }

    if (movement.reservedQuantityDelta < 0) {
      return 'accent';
    }

    return movement.statusBefore === movement.statusAfter ? 'neutral' : 'accent';
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

  protected async loadFilteredHistory(options: { silentIfEmpty?: boolean } = {}): Promise<void> {
    if (this.isHistoryLoading()) {
      return;
    }

    const search = this.searchTerm().trim();
    const movementType = this.historyTypeFilter();
    const searchLower = search.toLowerCase();
    const matchedVariant = search.length > 0
      ? this.items().find(item => item.sku.toLowerCase() === searchLower || (item.barcode ?? '').toLowerCase() === searchLower)
      : null;
    this.isHistoryLoading.set(true);
    this.historyErrorMessage.set(null);

    try {
      const history = await this.inventoryService.listHistory({
        variantId: matchedVariant?.variantId ?? null,
        movementType: movementType === 'All' ? null : movementType
      });
      this.filteredHistory.set(history);
      if (!options.silentIfEmpty && history.length === 0) {
        this.successMessage.set('No inventory movement history matched the current filters.');
      }
    } catch (error) {
      this.historyErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isHistoryLoading.set(false);
    }
  }

  protected stockBelowReserved(): boolean {
    const selected = this.selectedItem();
    return selected !== null && this.adjustmentForm.controls.stockQuantity.value < selected.reservedQuantity;
  }

  private async loadVariantHistory(variantId: string): Promise<void> {
    this.isVariantHistoryLoading.set(true);
    this.historyErrorMessage.set(null);

    try {
      this.selectedHistory.set(await this.inventoryService.listVariantHistory(variantId));
    } catch (error) {
      this.selectedHistory.set([]);
      this.historyErrorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isVariantHistoryLoading.set(false);
    }
  }

  private async refreshSelectedHistory(): Promise<void> {
    const selected = this.selectedItem();
    if (selected) {
      await this.loadVariantHistory(selected.variantId);
    }
  }

  private async loadInventory(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.items.set(await this.inventoryService.listInventory());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async downloadCsv(loader: () => Promise<Blob>, filename: string): Promise<void> {
    if (this.isDownloading()) {
      return;
    }

    this.isDownloading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const blob = await loader();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = filename;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isDownloading.set(false);
    }
  }

  private isLowStock(item: SellerInventoryItemResponse): boolean {
    return item.availableQuantity > 0 && item.availableQuantity <= 5;
  }

  private isOutOfStock(item: SellerInventoryItemResponse): boolean {
    return item.availableQuantity <= 0 || item.variantStatus === 'OutOfStock';
  }
}
