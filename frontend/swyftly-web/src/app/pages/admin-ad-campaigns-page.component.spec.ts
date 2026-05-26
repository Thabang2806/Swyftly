import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminAdCampaignSummaryResponse } from '../admin/admin-ad-campaign.models';
import { AdminAdCampaignService } from '../admin/admin-ad-campaign.service';
import { AdminAdCampaignsPageComponent } from './admin-ad-campaigns-page.component';

describe('AdminAdCampaignsPageComponent', () => {
  let fixture: ComponentFixture<AdminAdCampaignsPageComponent>;
  let adminAdCampaignService: jasmine.SpyObj<AdminAdCampaignService>;

  beforeEach(async () => {
    adminAdCampaignService = jasmine.createSpyObj<AdminAdCampaignService>('AdminAdCampaignService', ['getPendingCampaigns']);
    adminAdCampaignService.getPendingCampaigns.and.resolveTo([createCampaignSummary()]);

    await TestBed.configureTestingModule({
      imports: [AdminAdCampaignsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminAdCampaignService, useValue: adminAdCampaignService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminAdCampaignsPageComponent);
  });

  it('loads and displays pending campaign reviews', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('Launch campaign');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('FeaturedProduct');
    expect(compiled.querySelector('a[mat-stroked-button]')?.getAttribute('href')).toBe('/admin/ads/campaign-id');
  });

  it('shows an empty state when there are no pending campaigns', async () => {
    adminAdCampaignService.getPendingCampaigns.and.resolveTo([]);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No campaigns pending review');
  });
});

function createCampaignSummary(): AdminAdCampaignSummaryResponse {
  return {
    adCampaignId: 'campaign-id',
    sellerId: 'seller-id',
    sellerDisplayName: 'Seller Store',
    name: 'Launch campaign',
    campaignType: 'FeaturedProduct',
    status: 'PendingReview',
    startsAtUtc: '2026-05-20T12:00:00Z',
    endsAtUtc: '2026-06-03T12:00:00Z',
    submittedAtUtc: '2026-05-19T12:00:00Z',
    productCount: 1,
    totalBudget: 1000,
    currency: 'ZAR'
  };
}
