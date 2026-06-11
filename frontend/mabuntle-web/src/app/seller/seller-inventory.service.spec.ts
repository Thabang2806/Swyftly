import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SellerInventoryBulkAdjustmentResponse } from './seller-inventory.models';
import { SellerInventoryService } from './seller-inventory.service';

describe('SellerInventoryService', () => {
  let service: SellerInventoryService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SellerInventoryService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads seller inventory', async () => {
    const promise = service.listInventory();

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/inventory`);
    expect(request.request.method).toBe('GET');
    request.flush([createInventoryItem()]);

    const response = await promise;
    expect(response[0].variantId).toBe('variant-id');
  });

  it('adjusts inventory with stock, status, and reason', async () => {
    const payload = {
      stockQuantity: 7,
      status: 'OutOfStock' as const,
      reason: 'Stocktake correction'
    };

    const promise = service.adjustInventory('variant-id', payload);

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/inventory/variant-id/adjust`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(payload);
    request.flush(createInventoryItem({ stockQuantity: 7, variantStatus: 'OutOfStock' }));

    const response = await promise;
    expect(response.stockQuantity).toBe(7);
    expect(response.variantStatus).toBe('OutOfStock');
  });

  it('loads inventory movement history', async () => {
    const historyPromise = service.listHistory({
      variantId: 'variant-id',
      movementType: 'SellerAdjustment',
      orderId: 'order-id'
    });

    const historyRequest = httpTestingController.expectOne(request =>
      request.url === `${environment.apiBaseUrl}/api/seller/inventory/history` &&
      request.params.get('variantId') === 'variant-id' &&
      request.params.get('movementType') === 'SellerAdjustment' &&
      request.params.get('orderId') === 'order-id');
    expect(historyRequest.request.method).toBe('GET');
    historyRequest.flush([createMovement()]);
    await expectAsync(historyPromise).toBeResolvedTo([createMovement()]);

    const variantHistoryPromise = service.listVariantHistory('variant-id');
    const variantHistoryRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/inventory/variant-id/history`);
    expect(variantHistoryRequest.request.method).toBe('GET');
    variantHistoryRequest.flush([createMovement()]);
    await expectAsync(variantHistoryPromise).toBeResolvedTo([createMovement()]);
  });

  it('calls bulk inventory import and export endpoints', async () => {
    const file = new File(['sku,stockQuantity,status\nSKU-1,8,Active'], 'inventory.csv', { type: 'text/csv' });
    const previewResponse = createBulkResponse();
    const applyRequest = {
      reason: 'Monthly stocktake',
      items: [{ variantId: 'variant-id', sku: 'SUMMER-DRESS-M-BLACK', stockQuantity: 8, status: 'Active' as const }]
    };

    const exportPromise = service.exportInventoryCsv();
    const exportRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/inventory/export.csv`);
    expect(exportRequest.request.method).toBe('GET');
    expect(exportRequest.request.responseType).toBe('blob');
    exportRequest.flush(new Blob(['csv'], { type: 'text/csv' }));
    await expectAsync(exportPromise).toBeResolved();

    const templatePromise = service.downloadImportTemplate();
    const templateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/inventory/import-template.csv`);
    expect(templateRequest.request.method).toBe('GET');
    expect(templateRequest.request.responseType).toBe('blob');
    templateRequest.flush(new Blob(['template'], { type: 'text/csv' }));
    await expectAsync(templatePromise).toBeResolved();

    const previewPromise = service.previewImport(file);
    const previewRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/inventory/import/preview`);
    expect(previewRequest.request.method).toBe('POST');
    expect(previewRequest.request.body instanceof FormData).toBeTrue();
    previewRequest.flush(previewResponse);
    await expectAsync(previewPromise).toBeResolvedTo(previewResponse);

    const applyPromise = service.bulkAdjust(applyRequest);
    const applyHttpRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/seller/inventory/bulk-adjust`);
    expect(applyHttpRequest.request.method).toBe('POST');
    expect(applyHttpRequest.request.body).toEqual(applyRequest);
    applyHttpRequest.flush(previewResponse);
    await expectAsync(applyPromise).toBeResolvedTo(previewResponse);
  });
});

function createInventoryItem(overrides: Record<string, unknown> = {}) {
  return {
    productId: 'product-id',
    variantId: 'variant-id',
    productTitle: 'Summer Dress',
    productSlug: 'summer-dress',
    productStatus: 'Published',
    primaryImageUrl: null,
    primaryImageAltText: null,
    sku: 'SUMMER-DRESS-M-BLACK',
    barcode: '6001000000012',
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

function createBulkResponse(): SellerInventoryBulkAdjustmentResponse {
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
      barcode: '6001000000012',
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
    }]
  };
}

function createMovement() {
  return {
    movementId: 'movement-id',
    productId: 'product-id',
    variantId: 'variant-id',
    productTitle: 'Summer Dress',
    productSlug: 'summer-dress',
    sku: 'SUMMER-DRESS-M-BLACK',
    barcode: '6001000000012',
    size: 'M',
    colour: 'Black',
    movementType: 'SellerAdjustment' as const,
    stockQuantityBefore: 10,
    stockQuantityAfter: 7,
    reservedQuantityBefore: 2,
    reservedQuantityAfter: 2,
    quantityDelta: -3,
    reservedQuantityDelta: 0,
    statusBefore: 'Active' as const,
    statusAfter: 'Inactive' as const,
    source: 'SellerInventoryAdjust',
    reason: 'Stocktake correction',
    actorUserId: 'seller-user-id',
    batchReference: null,
    cartId: null,
    orderId: 'order-id',
    reservationId: null,
    paymentId: null,
    returnRequestId: null,
    refundId: null,
    relatedRoute: '/seller/orders/order-id',
    occurredAtUtc: '2026-05-21T08:00:00Z'
  };
}
