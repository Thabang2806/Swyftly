import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { getApiErrorMessage } from '../auth/api-error';
import {
  SellerInventoryBulkAdjustmentResponse,
  SellerInventoryBulkAdjustmentRowResponse,
  SellerInventoryItemResponse,
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

@Component({
  selector: 'app-seller-inventory-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    EmptyStateComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
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
          <mat-form-field appearance="outline">
            <mat-label>Search inventory</mat-label>
            <input matInput [value]="searchTerm()" (input)="updateSearch($event)" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Stock state</mat-label>
            <mat-select [value]="stockFilter()" (selectionChange)="updateStockFilter($event)">
              <mat-option value="All">All inventory</mat-option>
              <mat-option value="LowStock">Low stock</mat-option>
              <mat-option value="OutOfStock">Out of stock</mat-option>
              <mat-option value="Reserved">Reserved stock</mat-option>
            </mat-select>
          </mat-form-field>
        </section>

        <section class="route-card seller-inventory-bulk" aria-label="Bulk inventory import and export">
          <div>
            <span class="eyebrow">Bulk stocktake</span>
            <h2>Import or export inventory</h2>
            <p>Use CSV for stock quantity and variant status only. Product details, pricing, and variant structure stay managed in the product editor.</p>
          </div>

          <div class="auth-actions">
            <button mat-stroked-button type="button" [disabled]="isDownloading()" (click)="downloadExport()">
              Export inventory CSV
            </button>
            <button mat-button type="button" [disabled]="isDownloading()" (click)="downloadTemplate()">
              Download template
            </button>
          </div>

          <form class="seller-bulk-import-form" [formGroup]="bulkForm" (ngSubmit)="applyBulkAdjustment()" novalidate>
            <label class="seller-file-input">
              <span>CSV file</span>
              <input type="file" accept=".csv,text/csv" (change)="onImportFileSelected($event)" />
              <strong>{{ selectedImportFile()?.name ?? 'Choose inventory CSV' }}</strong>
            </label>

            <button mat-stroked-button type="button" [disabled]="!selectedImportFile() || isPreviewing()" (click)="previewImport()">
              {{ isPreviewing() ? 'Previewing...' : 'Preview import' }}
            </button>

            @if (importPreview()) {
              <mat-form-field appearance="outline">
                <mat-label>Batch reason</mat-label>
                <textarea matInput rows="3" formControlName="reason"></textarea>
                @if (bulkForm.controls.reason.hasError('required')) {
                  <mat-error>A batch reason is required for audit.</mat-error>
                }
              </mat-form-field>

              <button
                mat-flat-button
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
                    <small>{{ row.size ?? '-' }} / {{ row.colour ?? '-' }}</small>
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
            <a mat-flat-button routerLink="/seller/products">Open products</a>
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
                <div class="admin-table-row seller-ops-table-row" role="row">
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
                    <button mat-stroked-button type="button" (click)="selectItem(item)">Adjust</button>
                    <a mat-button [routerLink]="['/seller/products', item.productId, 'edit']">Product</a>
                  </span>
                </div>
              }
            </div>

            <aside class="route-card seller-inventory-adjustment" aria-label="Inventory adjustment panel">
              @if (selectedItem(); as selected) {
                <span class="eyebrow">Adjust stock</span>
                <h2>{{ selected.productTitle ?? 'Selected product' }}</h2>
                <p>{{ selected.sku }} currently has {{ selected.availableQuantity }} available and {{ selected.reservedQuantity }} reserved.</p>

                <form [formGroup]="adjustmentForm" (ngSubmit)="submitAdjustment()" class="wizard-form" novalidate>
                  <mat-form-field appearance="outline">
                    <mat-label>Stock quantity</mat-label>
                    <input matInput type="number" min="0" formControlName="stockQuantity" />
                    @if (adjustmentForm.controls.stockQuantity.hasError('required')) {
                      <mat-error>Stock quantity is required.</mat-error>
                    } @else if (adjustmentForm.controls.stockQuantity.hasError('min')) {
                      <mat-error>Stock cannot be negative.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Variant status</mat-label>
                    <mat-select formControlName="status">
                      @for (status of variantStatuses; track status) {
                        <mat-option [value]="status">{{ status }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Reason</mat-label>
                    <textarea matInput rows="4" formControlName="reason"></textarea>
                    @if (adjustmentForm.controls.reason.hasError('required')) {
                      <mat-error>A reason is required for audit.</mat-error>
                    }
                  </mat-form-field>

                  @if (stockBelowReserved()) {
                    <app-ui-alert tone="warning">
                      Stock cannot be lower than the {{ selected.reservedQuantity }} units currently reserved.
                    </app-ui-alert>
                  }

                  <button mat-flat-button type="submit" [disabled]="isSaving() || adjustmentForm.invalid || stockBelowReserved()">
                    {{ isSaving() ? 'Saving...' : 'Save adjustment' }}
                  </button>
                </form>
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
  protected readonly stockFilter = signal<InventoryFilter>('All');
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly isDownloading = signal(false);
  protected readonly isPreviewing = signal(false);
  protected readonly isApplyingBulk = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly selectedImportFile = signal<File | null>(null);
  protected readonly importPreview = signal<SellerInventoryBulkAdjustmentResponse | null>(null);
  protected readonly variantStatuses: readonly SellerInventoryVariantStatus[] = ['Active', 'Inactive', 'OutOfStock'];

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

  protected updateStockFilter(event: MatSelectChange): void {
    this.stockFilter.set(event.value as InventoryFilter);
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
      'swyftly-inventory-export.csv');
  }

  protected async downloadTemplate(): Promise<void> {
    await this.downloadCsv(
      () => this.inventoryService.downloadImportTemplate(),
      'swyftly-inventory-import-template.csv');
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
            stockQuantity: row.proposedStockQuantity ?? 0,
            status: row.proposedStatus as SellerInventoryVariantStatus
          }))
      });

      this.importPreview.set(result);
      await this.loadInventory();
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

  protected stockBelowReserved(): boolean {
    const selected = this.selectedItem();
    return selected !== null && this.adjustmentForm.controls.stockQuantity.value < selected.reservedQuantity;
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
