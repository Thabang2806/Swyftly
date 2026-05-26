import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminWorkspaceNavComponent } from './admin-workspace-nav.component';

describe('AdminWorkspaceNavComponent', () => {
  let fixture: ComponentFixture<AdminWorkspaceNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminWorkspaceNavComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminWorkspaceNavComponent);
  });

  it('renders core admin workspace links', () => {
    fixture.detectChanges();

    const groupLabels = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.workspace-nav-group-label'))
      .map(label => label.textContent?.trim());
    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
      .map(link => ({
        label: link.textContent?.trim(),
        href: link.getAttribute('href')
      }));

    expect(groupLabels).toEqual(['Overview', 'Moderation', 'Operations', 'Finance', 'Platform']);
    expect(links).toContain(jasmine.objectContaining({ label: 'Dashboard', href: '/admin' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Sellers', href: '/admin/sellers' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Products', href: '/admin/products' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Reviews', href: '/admin/reviews' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Ads', href: '/admin/ads' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Pickup points', href: '/admin/pickup-points' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Orders', href: '/admin/orders' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Support', href: '/admin/support' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Payments', href: '/admin/payments' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Refunds', href: '/admin/refunds' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Payout profile', href: '/admin/payout-profile-changes' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Reports', href: '/admin/reports' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'AI usage', href: '/admin/ai-usage' }));
    expect(links).toContain(jasmine.objectContaining({ label: 'Audit logs', href: '/admin/audit-logs' }));
  });
});
