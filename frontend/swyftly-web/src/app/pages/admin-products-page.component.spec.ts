import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminProductSummaryResponse } from '../admin/admin-product.models';
import { AdminProductService } from '../admin/admin-product.service';
import { AdminProductsPageComponent } from './admin-products-page.component';

describe('AdminProductsPageComponent', () => {
  let fixture: ComponentFixture<AdminProductsPageComponent>;
  let adminProductService: jasmine.SpyObj<AdminProductService>;

  beforeEach(async () => {
    adminProductService = jasmine.createSpyObj<AdminProductService>('AdminProductService', ['getPendingReviewProducts', 'getPendingRevisions']);
    adminProductService.getPendingReviewProducts.and.resolveTo([createProductSummary()]);
    adminProductService.getPendingRevisions.and.resolveTo([]);

    await TestBed.configureTestingModule({
      imports: [AdminProductsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminProductService, useValue: adminProductService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminProductsPageComponent);
  });

  it('loads and displays pending review products', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Summer Dress');
    expect(compiled.textContent).toContain('PendingReview');
    expect(compiled.textContent).toContain('1 high-risk flag');
    expect(compiled.querySelector('.hf-admin-review-layout')).toBeTruthy();
    expect(compiled.textContent).toContain('Selected review');
    const reviewLink = Array.from(compiled.querySelectorAll('a'))
      .find(link => link.getAttribute('href') === '/admin/products/product-id');
    expect(reviewLink).toBeTruthy();
  });

  it('filters products by seller and risk', async () => {
    adminProductService.getPendingReviewProducts.and.resolveTo([
      createProductSummary(),
      createProductSummary({
        productId: 'second-product-id',
        sellerId: 'second-seller-id',
        sellerDisplayName: 'Second Seller',
        title: 'Canvas Sneakers',
        categoryPath: 'Women > Shoes',
        highRiskFlagCount: 0
      })
    ]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const sellerInput = compiled.querySelector('input[formControlName="seller"]') as HTMLInputElement;
    const riskInput = compiled.querySelector('input[formControlName="risk"]') as HTMLInputElement;

    sellerInput.value = 'Second Seller';
    sellerInput.dispatchEvent(new Event('input'));
    riskInput.value = 'none';
    riskInput.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Canvas Sneakers');
    expect(compiled.textContent).not.toContain('Summer Dress');
  });

  it('shows an empty state when there are no pending products', async () => {
    adminProductService.getPendingReviewProducts.and.resolveTo([]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No products pending review');
  });
});

function createProductSummary(overrides: Partial<AdminProductSummaryResponse> = {}): AdminProductSummaryResponse {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    sellerVerificationStatus: 'Verified',
    title: 'Summer Dress',
    categoryPath: 'Women > Clothing > Dresses',
    status: 'PendingReview',
    highRiskFlagCount: 1,
    updatedAtUtc: '2026-05-18T12:00:00Z',
    ...overrides
  };
}
