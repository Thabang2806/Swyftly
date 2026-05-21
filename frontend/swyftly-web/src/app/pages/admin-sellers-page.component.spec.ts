import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AdminSellerSummaryResponse } from '../admin/admin-seller.models';
import { AdminSellerService } from '../admin/admin-seller.service';
import { AdminSellersPageComponent } from './admin-sellers-page.component';

describe('AdminSellersPageComponent', () => {
  let fixture: ComponentFixture<AdminSellersPageComponent>;
  let adminSellerService: jasmine.SpyObj<AdminSellerService>;

  beforeEach(async () => {
    adminSellerService = jasmine.createSpyObj<AdminSellerService>('AdminSellerService', ['getPendingSellers']);
    adminSellerService.getPendingSellers.and.resolveTo([createSellerSummary()]);

    await TestBed.configureTestingModule({
      imports: [AdminSellersPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminSellerService, useValue: adminSellerService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSellersPageComponent);
  });

  it('loads and displays pending sellers', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('UnderReview');
    expect(compiled.querySelector('.hf-admin-review-layout')).toBeTruthy();
    expect(compiled.textContent).toContain('Evidence snapshot');
    const reviewLink = Array.from(compiled.querySelectorAll('a'))
      .find(link => link.getAttribute('href') === '/admin/sellers/seller-id');
    expect(reviewLink).toBeTruthy();
  });

  it('filters sellers by storefront', async () => {
    adminSellerService.getPendingSellers.and.resolveTo([
      createSellerSummary(),
      createSellerSummary({
        sellerId: 'second-seller-id',
        displayName: 'Second Seller',
        storeName: 'Second Store',
        storeSlug: 'second-store'
      })
    ]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const storefrontInput = compiled.querySelector('input[formControlName="storefront"]') as HTMLInputElement;
    storefrontInput.value = 'second-store';
    storefrontInput.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Second Store');
    expect(compiled.textContent).not.toContain('Seller Store');
  });

  it('shows an empty state when there are no pending sellers', async () => {
    adminSellerService.getPendingSellers.and.resolveTo([]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No pending sellers');
  });
});

function createSellerSummary(overrides: Partial<AdminSellerSummaryResponse> = {}): AdminSellerSummaryResponse {
  return {
    sellerId: 'seller-id',
    displayName: 'Seller Store',
    contactEmail: 'seller@example.test',
    storeName: 'Seller Store',
    storeSlug: 'seller-store',
    verificationStatus: 'UnderReview',
    submittedAtUtc: '2026-05-18T12:00:00Z',
    ...overrides
  };
}
