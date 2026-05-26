import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AdminAdCampaignDetailResponse } from '../admin/admin-ad-campaign.models';
import { AdminAdCampaignService } from '../admin/admin-ad-campaign.service';
import { AdminAdCampaignDetailPageComponent } from './admin-ad-campaign-detail-page.component';

describe('AdminAdCampaignDetailPageComponent', () => {
  let fixture: ComponentFixture<AdminAdCampaignDetailPageComponent>;
  let adminAdCampaignService: jasmine.SpyObj<AdminAdCampaignService>;

  beforeEach(async () => {
    adminAdCampaignService = jasmine.createSpyObj<AdminAdCampaignService>(
      'AdminAdCampaignService',
      ['getCampaign', 'approveCampaign', 'rejectCampaign']);
    adminAdCampaignService.getCampaign.and.resolveTo(createCampaignDetail());
    adminAdCampaignService.approveCampaign.and.resolveTo(createCampaignDetail({
      status: 'Active',
      auditTrail: [{
        id: 'audit-approved',
        actionType: 'AdCampaignApproved',
        actorUserId: 'admin-id',
        actorRole: 'Admin',
        reason: null,
        createdAtUtc: '2026-05-19T12:30:00Z'
      }]
    }));
    adminAdCampaignService.rejectCampaign.and.resolveTo(createCampaignDetail({
      status: 'Rejected',
      auditTrail: [{
        id: 'audit-rejected',
        actionType: 'AdCampaignRejected',
        actorUserId: 'admin-id',
        actorRole: 'Admin',
        reason: 'Product mismatch.',
        createdAtUtc: '2026-05-19T12:35:00Z'
      }]
    }));

    await TestBed.configureTestingModule({
      imports: [AdminAdCampaignDetailPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 'campaign-id' })
            }
          }
        },
        { provide: AdminAdCampaignService, useValue: adminAdCampaignService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminAdCampaignDetailPageComponent);
  });

  it('loads campaign review detail with seller, products, budget, and eligibility', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('Launch campaign');
    expect(compiled.textContent).toContain('Seller Store');
    expect(compiled.textContent).toContain('Sponsored Dress');
    expect(compiled.textContent).toContain('Quality score 90');
  });

  it('approves a campaign', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const approveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Approve campaign')) as HTMLButtonElement;
    approveButton.click();

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminAdCampaignService.approveCampaign).toHaveBeenCalledWith('campaign-id');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Campaign approved.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('AdCampaignApproved');
  });

  it('requires a rejection reason before rejecting', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const rejectForm = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    rejectForm.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminAdCampaignService.rejectCampaign).not.toHaveBeenCalled();
  });

  it('rejects a campaign when a reason is provided', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const textarea = (fixture.nativeElement as HTMLElement).querySelector('textarea[formControlName="reason"]') as HTMLTextAreaElement;
    textarea.value = 'Product mismatch.';
    textarea.dispatchEvent(new Event('input'));

    const rejectForm = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    rejectForm.dispatchEvent(new Event('submit'));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(adminAdCampaignService.rejectCampaign).toHaveBeenCalledWith('campaign-id', { reason: 'Product mismatch.' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Campaign rejected.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('AdCampaignRejected');
  });
});

function createCampaignDetail(overrides: Partial<AdminAdCampaignDetailResponse> = {}): AdminAdCampaignDetailResponse {
  return {
    adCampaignId: 'campaign-id',
    sellerId: 'seller-id',
    seller: {
      displayName: 'Seller Store',
      contactEmail: 'seller@example.test',
      verificationStatus: 'Verified'
    },
    name: 'Launch campaign',
    campaignType: 'FeaturedProduct',
    status: 'PendingReview',
    startsAtUtc: '2026-05-20T12:00:00Z',
    endsAtUtc: '2026-06-03T12:00:00Z',
    submittedAtUtc: '2026-05-19T12:00:00Z',
    approvedAtUtc: null,
    pausedAtUtc: null,
    completedAtUtc: null,
    cancelledAtUtc: null,
    rejectionReason: null,
    products: [{
      productId: 'product-id',
      title: 'Sponsored Dress',
      status: 'Published',
      publishedAtUtc: '2026-05-18T12:00:00Z'
    }],
    budget: {
      currency: 'ZAR',
      dailyBudget: 100,
      totalBudget: 1000,
      maxCostPerClick: 5,
      spentAmount: 0
    },
    eligibility: {
      isEligible: true,
      sellerReasons: [],
      products: [{
        productId: 'product-id',
        isEligible: true,
        qualityScore: 90,
        reasons: []
      }]
    },
    auditTrail: [],
    ...overrides
  };
}
