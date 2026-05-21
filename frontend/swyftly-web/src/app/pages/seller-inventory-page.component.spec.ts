import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerInventoryBulkAdjustmentResponse, SellerInventoryItemResponse } from '../seller/seller-inventory.models';
import { SellerInventoryService } from '../seller/seller-inventory.service';
import { SellerInventoryPageComponent } from './seller-inventory-page.component';

describe('SellerInventoryPageComponent', () => {
  let fixture: ComponentFixture<SellerInventoryPageComponent>;
  let inventoryService: jasmine.SpyObj<SellerInventoryService>;

  beforeEach(async () => {
    inventoryService = jasmine.createSpyObj<SellerInventoryService>('SellerInventoryService', [
      'listInventory',
      'adjustInventory',
      'exportInventoryCsv',
      'downloadImportTemplate',
      'previewImport',
      'bulkAdjust'
    ]);
    inventoryService.listInventory.and.resolveTo([
      createInventoryItem(),
      createInventoryItem({
        variantId: 'variant-id-2',
        productTitle: 'Leather Belt',
        productSlug: 'leather-belt',
        sku: 'BELT-OS-BROWN',
        stockQuantity: 0,
        availableQuantity: 0,
        reservedQuantity: 0,
        variantStatus: 'OutOfStock'
      })
    ]);
    inventoryService.adjustInventory.and.resolveTo(createInventoryItem({
      stockQuantity: 7,
      availableQuantity: 5,
      variantStatus: 'Inactive'
    }));
    inventoryService.previewImport.and.resolveTo(createBulkResponse());
    inventoryService.bulkAdjust.and.resolveTo(createBulkResponse({ changedRows: 1, unchangedRows: 0 }));

    await TestBed.configureTestingModule({
      imports: [SellerInventoryPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerInventoryService, useValue: inventoryService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerInventoryPageComponent);
  });

  it('renders inventory rows and stock states', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('10 stock, 2 reserved');
    expect(compiled.textContent).toContain('Leather Belt');
    expect(compiled.textContent).toContain('OutOfStock');
  });

  it('filters inventory by search text', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const searchInput = compiled.querySelector('input') as HTMLInputElement;
    searchInput.value = 'belt';
    searchInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Leather Belt');
    expect(compiled.textContent).not.toContain('Summer Dress');
  });

  it('submits stock adjustment payload and shows success', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      selectItem(item: SellerInventoryItemResponse): void;
      adjustmentForm: { patchValue(value: Record<string, unknown>): void };
      submitAdjustment(): Promise<void>;
    };
    const item = createInventoryItem();
    component.selectItem(item);
    component.adjustmentForm.patchValue({
      stockQuantity: 7,
      status: 'Inactive',
      reason: 'Stocktake correction'
    });

    await component.submitAdjustment();
    fixture.detectChanges();

    expect(inventoryService.adjustInventory).toHaveBeenCalledWith('variant-id', {
      stockQuantity: 7,
      status: 'Inactive',
      reason: 'Stocktake correction'
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Inventory adjustment saved.');
  });

  it('previews bulk import and applies changed rows', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      selectedImportFile: { set(value: File): void };
      bulkForm: { patchValue(value: Record<string, unknown>): void };
      previewImport(): Promise<void>;
      applyBulkAdjustment(): Promise<void>;
    };
    const file = new File(['sku,stockQuantity,status\nSUMMER-DRESS-M-BLACK,8,Active'], 'inventory.csv', { type: 'text/csv' });
    component.selectedImportFile.set(file);

    await component.previewImport();
    fixture.detectChanges();

    expect(inventoryService.previewImport).toHaveBeenCalledWith(file);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('1 changed');

    component.bulkForm.patchValue({ reason: 'Monthly stocktake' });
    await component.applyBulkAdjustment();

    expect(inventoryService.bulkAdjust).toHaveBeenCalledWith({
      reason: 'Monthly stocktake',
      items: [{
        variantId: 'variant-id',
        sku: 'SUMMER-DRESS-M-BLACK',
        stockQuantity: 8,
        status: 'Active'
      }]
    });
    expect(inventoryService.listInventory).toHaveBeenCalledTimes(2);
  });
});

function createInventoryItem(overrides: Partial<SellerInventoryItemResponse> = {}): SellerInventoryItemResponse {
  return {
    productId: 'product-id',
    variantId: 'variant-id',
    productTitle: 'Summer Dress',
    productSlug: 'summer-dress',
    productStatus: 'Published',
    primaryImageUrl: null,
    primaryImageAltText: null,
    sku: 'SUMMER-DRESS-M-BLACK',
    size: 'M',
    colour: 'Black',
    price: 499.99,
    stockQuantity: 10,
    reservedQuantity: 2,
    availableQuantity: 8,
    variantStatus: 'Active',
    updatedAtUtc: '2026-05-21T08:00:00Z',
    ...overrides
  };
}

function createBulkResponse(overrides: Partial<SellerInventoryBulkAdjustmentResponse> = {}): SellerInventoryBulkAdjustmentResponse {
  return {
    totalRows: 1,
    validRows: 1,
    errorRows: 0,
    changedRows: 1,
    unchangedRows: 0,
    rows: [{
      rowNumber: 2,
      variantId: 'variant-id',
      sku: 'SUMMER-DRESS-M-BLACK',
      productId: 'product-id',
      productTitle: 'Summer Dress',
      productSlug: 'summer-dress',
      size: 'M',
      colour: 'Black',
      currentStockQuantity: 10,
      currentReservedQuantity: 2,
      currentStatus: 'Active',
      proposedStockQuantity: 8,
      proposedStatus: 'Active',
      rowStatus: 'Changed',
      messages: []
    }],
    ...overrides
  };
}
