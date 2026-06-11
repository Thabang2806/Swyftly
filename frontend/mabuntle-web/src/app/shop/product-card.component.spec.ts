import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { BuyerWishlistStateService } from '../buyer/buyer-wishlist-state.service';
import { ProductSearchItemResponse } from './public-catalog.models';
import { ProductCardComponent } from './product-card.component';

describe('ProductCardComponent', () => {
  let fixture: ComponentFixture<ProductCardComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let wishlistState: jasmine.SpyObj<BuyerWishlistStateService>;

  const product: ProductSearchItemResponse = {
    productId: 'product-1',
    sellerId: 'seller-1',
    sellerStoreName: 'Luxe Studio',
    sellerStoreSlug: 'luxe-studio',
    categoryId: 'category-1',
    categoryPath: 'Jewellery / Earrings',
    brandId: null,
    title: 'Gold Hoop Earrings',
    slug: 'gold-hoop-earrings',
    shortDescription: 'Polished everyday hoops with a soft shine.',
    primaryImageUrl: null,
    primaryImageAltText: null,
    priceMin: 349,
    compareAtPriceMin: 429,
    inStock: true,
    tags: ['gold', 'jewellery'],
    publishedAtUtc: '2026-01-01T00:00:00Z'
  };

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['initialize', 'hasAnyRole']);
    wishlistState = jasmine.createSpyObj<BuyerWishlistStateService>('BuyerWishlistStateService', ['load', 'isSaved', 'save', 'remove']);
    authService.initialize.and.resolveTo();
    authService.hasAnyRole.and.returnValue(true);
    wishlistState.load.and.resolveTo();
    wishlistState.isSaved.and.returnValue(false);
    wishlistState.save.and.resolveTo({
      wishlistItemId: 'wishlist-id',
      createdAtUtc: '2026-05-19T10:00:00Z',
      product,
      availableVariants: []
    });
    wishlistState.remove.and.resolveTo();

    await TestBed.configureTestingModule({
      imports: [ProductCardComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: BuyerWishlistStateService, useValue: wishlistState }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductCardComponent);
  });

  it('renders the high-fidelity fallback visual when the product has no image', () => {
    fixture.componentRef.setInput('product', product);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const fallback = compiled.querySelector('.hf-product-visual');

    expect(fallback).not.toBeNull();
    expect(fallback?.classList).toContain('hf-product-visual--jewel');
    expect(fallback?.textContent).toContain('Gold Hoop Earrings');
    expect(compiled.textContent).toContain('gold');
    expect(compiled.textContent).toContain('jewellery');
    expect(hasMojibakeMarker(compiled.textContent ?? '')).toBeFalse();
  });

  it('renders the product image when one is available', () => {
    fixture.componentRef.setInput('product', {
      ...product,
      primaryImageUrl: '/assets/products/earrings.jpg',
      primaryImageAltText: 'Gold earrings on ivory background'
    });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const image = compiled.querySelector<HTMLImageElement>('.product-card-media img');

    expect(image?.src).toContain('/assets/products/earrings.jpg');
    expect(image?.alt).toBe('Gold earrings on ivory background');
    expect(compiled.querySelector('.hf-product-visual')).toBeNull();
  });

  it('toggles the product wishlist state', async () => {
    fixture.componentRef.setInput('product', product);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const saveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Save')) as HTMLButtonElement;
    saveButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(wishlistState.save).toHaveBeenCalledWith('product-1');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Remove');
  });
});

function hasMojibakeMarker(value: string): boolean {
  const commonMojibakeCharCodes = [0xc2, 0xc3, 0xe2, 0xf0, 0xfffd];
  return [...value].some(character => commonMojibakeCharCodes.includes(character.charCodeAt(0)));
}
