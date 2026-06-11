import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellerInventoryService } from '../seller/seller-inventory.service';
import { SellerReturnService } from '../seller/seller-return.service';
import { SellerReturnDetailPageComponent } from './seller-return-detail-page.component';
import { createReturnRequest } from './seller-returns-page.component.spec';

describe('SellerReturnDetailPageComponent', () => {
  let fixture: ComponentFixture<SellerReturnDetailPageComponent>;
  let inventoryService: jasmine.SpyObj<SellerInventoryService>;
  let returnService: jasmine.SpyObj<SellerReturnService>;

  beforeEach(async () => {
    inventoryService = jasmine.createSpyObj<SellerInventoryService>('SellerInventoryService', ['listHistory']);
    inventoryService.listHistory.and.resolveTo([]);
    returnService = jasmine.createSpyObj<SellerReturnService>(
      'SellerReturnService',
      ['getReturn', 'approveReturn', 'rejectReturn', 'listRestockDecisions', 'createRestockDecisions']);
    returnService.getReturn.and.resolveTo(createReturnRequest());
    returnService.approveReturn.and.resolveTo(createReturnRequest({ status: 'Approved' }));
    returnService.rejectReturn.and.resolveTo(createReturnRequest({ status: 'Rejected' }));
    returnService.listRestockDecisions.and.resolveTo([]);
    returnService.createRestockDecisions.and.resolveTo([{
      restockDecisionId: 'restock-id',
      returnRequestId: 'return-id',
      returnItemId: 'return-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      sku: 'SKU-RETURN',
      size: 'M',
      colour: 'Black',
      quantityReturned: 1,
      quantityRestocked: 1,
      condition: 'Sellable',
      reason: 'Inspected and sellable.',
      actorUserId: 'seller-user-id',
      createdAtUtc: '2026-05-19T11:00:00Z'
    }]);

    await TestBed.configureTestingModule({
      imports: [SellerReturnDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: SellerInventoryService, useValue: inventoryService },
        { provide: SellerReturnService, useValue: returnService },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ returnRequestId: 'return-id' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerReturnDetailPageComponent);
  });

  it('loads return details and approves a return', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Changed mind');
    expect(compiled.textContent).toContain('Store policy snapshot');

    const approveButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Approve return'));
    approveButton?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(returnService.approveReturn).toHaveBeenCalledWith('return-id', { message: null });
    expect(inventoryService.listHistory).toHaveBeenCalledWith({ returnRequestId: 'return-id' });
  });

  it('records restock decisions after approval', async () => {
    returnService.getReturn.and.resolveTo(createReturnRequest({ status: 'Approved' }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      restockForm: { patchValue: (value: Record<string, unknown>) => void };
      recordRestockDecision: () => Promise<void>;
    };
    component.restockForm.patchValue({
      returnItemId: 'return-item-id',
      quantityRestocked: 1,
      condition: 'Sellable',
      reason: 'Inspected and sellable.'
    });
    await component.recordRestockDecision();
    await fixture.whenStable();

    expect(returnService.createRestockDecisions).toHaveBeenCalledWith('return-id', {
      items: [{
        returnItemId: 'return-item-id',
        quantityRestocked: 1,
        condition: 'Sellable',
        reason: 'Inspected and sellable.'
      }]
    });
  });
});
