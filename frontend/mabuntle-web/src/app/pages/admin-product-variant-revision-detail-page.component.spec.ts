import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AdminProductVariantRevisionDetailResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { AdminProductVariantRevisionDetailPageComponent } from './admin-product-variant-revision-detail-page.component';

describe('AdminProductVariantRevisionDetailPageComponent', () => {
  let fixture: ComponentFixture<AdminProductVariantRevisionDetailPageComponent>;
  let adminProductService: jasmine.SpyObj<AdminProductService>;

  beforeEach(async () => {
    adminProductService = jasmine.createSpyObj<AdminProductService>(
      'AdminProductService',
      ['getVariantRevision', 'approveVariantRevision', 'rejectVariantRevision']);
    adminProductService.getVariantRevision.and.resolveTo(createRevision());
    adminProductService.approveVariantRevision.and.resolveTo(createRevision({ status: 'Approved' }));
    adminProductService.rejectVariantRevision.and.resolveTo(createRevision({
      status: 'Rejected',
      rejectionReason: 'Price evidence missing.'
    }));

    await TestBed.configureTestingModule({
      imports: [AdminProductVariantRevisionDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ revisionId: 'revision-id' })
            }
          }
        },
        { provide: AdminProductService, useValue: adminProductService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminProductVariantRevisionDetailPageComponent);
  });

  it('loads current and proposed variants for review', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Variant and pricing revision');
    expect(compiled.textContent).toContain('Current live variants');
    expect(compiled.textContent).toContain('Proposed final variants');
    expect(compiled.textContent).toContain('SKU-UPDATED');
  });

  it('approves a pending variant revision', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const approveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Approve variant revision')) as HTMLButtonElement;
    approveButton.click();

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminProductService.approveVariantRevision).toHaveBeenCalledWith('revision-id');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Variant revision approved and applied.');
  });

  it('rejects with the unchanged reason payload', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const textarea = (fixture.nativeElement as HTMLElement).querySelector('textarea[formControlName="reason"]') as HTMLTextAreaElement;
    textarea.value = 'Price evidence missing.';
    textarea.dispatchEvent(new Event('input'));
    textarea.closest('form')?.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminProductService.rejectVariantRevision).toHaveBeenCalledWith('revision-id', { reason: 'Price evidence missing.' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Variant revision rejected.');
  });
});

function createRevision(overrides: Partial<AdminProductVariantRevisionDetailResponse> = {}): AdminProductVariantRevisionDetailResponse {
  return {
    revisionId: 'revision-id',
    productId: 'product-id',
    sellerId: 'seller-id',
    seller: {
      displayName: 'Seller Store',
      contactEmail: 'seller@example.test',
      verificationStatus: 'Verified'
    },
    productTitle: 'Summer Dress',
    productSlug: 'summer-dress',
    status: 'PendingReview',
    sellerReason: 'Seasonal price update.',
    rejectionReason: null,
    submittedAtUtc: '2026-05-18T12:00:00Z',
    reviewedAtUtc: null,
    currentVariants: [createVariant({ changeType: 'Unchanged' })],
    items: [{
      revisionItemId: 'revision-item-id',
      operation: 'Update',
      sourceVariantId: 'variant-id',
      sku: 'SKU-UPDATED',
      size: 'M',
      colour: 'Black',
      price: 129,
      compareAtPrice: null,
      initialStockQuantity: null,
      proposedStatus: 'Active',
      barcode: '1234567890'
    }],
    proposedFinalVariants: [createVariant({
      sku: 'SKU-UPDATED',
      price: 129,
      changeType: 'Update'
    })],
    validationErrors: {},
    auditTrail: [],
    ...overrides
  };
}

function createVariant(overrides: Partial<AdminProductVariantRevisionDetailResponse['currentVariants'][number]> = {}) {
  return {
    sourceVariantId: 'variant-id',
    changeType: 'Unchanged',
    sku: 'SKU-1',
    size: 'M',
    colour: 'Black',
    price: 100,
    compareAtPrice: null,
    stockQuantity: 10,
    reservedQuantity: 0,
    status: 'Active',
    barcode: null,
    availableQuantity: 10,
    ...overrides
  };
}
