import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { SellerNotificationService } from './seller-notification.service';
import { SellerWorkspaceNavComponent } from './seller-workspace-nav.component';

describe('SellerWorkspaceNavComponent', () => {
  let fixture: ComponentFixture<SellerWorkspaceNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SellerWorkspaceNavComponent],
      providers: [
        provideRouter([]),
        {
          provide: SellerNotificationService,
          useValue: {
            unreadCount: signal(2),
            refreshUnreadCount: jasmine.createSpy('refreshUnreadCount').and.resolveTo()
          }
        }
      ]
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
    expect(compiled.textContent).toContain('Notifications');
    expect(compiled.querySelector('.workspace-nav-badge')?.textContent?.trim()).toBe('2');
    expect(compiled.querySelector('a[href="/products"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/inventory"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/orders"]')).not.toBeNull();
    expect(compiled.querySelector('a[href="/ads"]')).not.toBeNull();
  });
});
