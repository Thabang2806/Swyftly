import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BuyerWorkspaceNavComponent } from './buyer-workspace-nav.component';

describe('BuyerWorkspaceNavComponent', () => {
  let fixture: ComponentFixture<BuyerWorkspaceNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BuyerWorkspaceNavComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerWorkspaceNavComponent);
  });

  it('renders buyer account workspace links', () => {
    fixture.detectChanges();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
      .map(link => ({
        label: link.textContent?.trim(),
        href: link.getAttribute('href')
      }));

    expect(links).toContain(jasmine.objectContaining({ label: 'Dashboard', href: '/account' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Orders', href: '/account/orders' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Returns', href: '/account/returns' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Wishlist', href: '/account/wishlist' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Reviews', href: '/account/reviews' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Notifications', href: '/account/notifications' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Settings', href: '/account/settings' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Disputes', href: '/account/disputes' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Support', href: '/account/support' }));
  });
});
