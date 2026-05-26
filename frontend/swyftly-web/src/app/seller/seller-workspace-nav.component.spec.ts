import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SellerWorkspaceNavComponent } from './seller-workspace-nav.component';

describe('SellerWorkspaceNavComponent', () => {
  let fixture: ComponentFixture<SellerWorkspaceNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SellerWorkspaceNavComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(SellerWorkspaceNavComponent);
  });

  it('renders grouped seller workspace links', () => {
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Seller studio');
    expect(compiled.textContent).toContain('Overview');
    expect(compiled.textContent).toContain('Catalog');
    expect(compiled.textContent).toContain('Operations');
    expect(compiled.textContent).toContain('Growth and finance');
    expect(compiled.querySelector('a[href="/seller/products"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/inventory"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/orders"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/seller/ads"]')).not.toBeNull();
  });
});
