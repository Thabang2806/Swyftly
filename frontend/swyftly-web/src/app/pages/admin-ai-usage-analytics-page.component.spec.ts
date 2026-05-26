import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminAiUsageAnalyticsResponse } from '../admin/admin-ai-usage-analytics.models';
import { AdminAiUsageAnalyticsService } from '../admin/admin-ai-usage-analytics.service';
import { AdminAiUsageAnalyticsPageComponent } from './admin-ai-usage-analytics-page.component';

describe('AdminAiUsageAnalyticsPageComponent', () => {
  let fixture: ComponentFixture<AdminAiUsageAnalyticsPageComponent>;
  let analyticsService: jasmine.SpyObj<AdminAiUsageAnalyticsService>;

  beforeEach(async () => {
    analyticsService = jasmine.createSpyObj<AdminAiUsageAnalyticsService>('AdminAiUsageAnalyticsService', [
      'getAnalytics'
    ]);
    analyticsService.getAnalytics.and.resolveTo(createAnalytics());

    await TestBed.configureTestingModule({
      imports: [AdminAiUsageAnalyticsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminAiUsageAnalyticsService, useValue: analyticsService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminAiUsageAnalyticsPageComponent);
  });

  it('loads and displays AI usage analytics', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('AI usage dashboard');
    expect(compiled.textContent).toContain('Suggestions generated');
    expect(compiled.textContent).toContain('ListingAssistant');
    expect(compiled.textContent).toContain('AI Seller');
    expect(compiled.textContent).toContain('Pre-AI baseline quality scores are not captured yet');
  });

  it('submits filters to the analytics service', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const fromInput = compiled.querySelector('input[formControlName="from"]') as HTMLInputElement;
    const toInput = compiled.querySelector('input[formControlName="to"]') as HTMLInputElement;
    const featureInput = compiled.querySelector('input[formControlName="featureName"]') as HTMLInputElement;
    const sellerInput = compiled.querySelector('input[formControlName="sellerId"]') as HTMLInputElement;

    fromInput.value = '2026-05-01T08:00';
    toInput.value = '2026-05-19T17:00';
    featureInput.value = 'ListingAssistant';
    sellerInput.value = 'seller-id';
    fromInput.dispatchEvent(new Event('input'));
    toInput.dispatchEvent(new Event('input'));
    featureInput.dispatchEvent(new Event('input'));
    sellerInput.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(analyticsService.getAnalytics).toHaveBeenCalledWith(jasmine.objectContaining({
      fromUtc: new Date('2026-05-01T08:00').toISOString(),
      toUtc: new Date('2026-05-19T17:00').toISOString(),
      featureName: 'ListingAssistant',
      sellerId: 'seller-id'
    }));
  });
});

function createAnalytics(): AdminAiUsageAnalyticsResponse {
  return {
    fromUtc: '2026-05-01T00:00:00.000Z',
    toUtc: '2026-05-19T00:00:00.000Z',
    generatedAtUtc: '2026-05-19T10:00:00.000Z',
    featureName: null,
    sellerId: null,
    totals: {
      requests: 3,
      successfulRequests: 2,
      failedRequests: 1,
      failureRate: 0.3333,
      inputTokens: 250,
      outputTokens: 210,
      estimatedCost: 0.04,
      averageLatencyMs: 150
    },
    suggestions: {
      productSuggestionsGenerated: 2,
      productSuggestionsAccepted: 1,
      suggestionAcceptanceRate: 0.5,
      productSuggestionsApplied: 1,
      productsTouchedByAi: 2,
      productsImprovedWithAi: 1,
      averageListingQualityScore: 70,
      averageQualityScoreImprovement: null,
      qualityScoreImprovementNote: 'Pre-AI baseline quality scores are not captured yet; improvement is unavailable until baseline capture is added.',
      fieldAuditCount: 2,
      fieldValuesAccepted: 1,
      fieldValuesEdited: 1
    },
    moderation: {
      moderationChecks: 1,
      adminReviewFlags: 1,
      lowRiskFlags: 0,
      mediumRiskFlags: 0,
      highRiskFlags: 1
    },
    featureUsage: [{
      featureName: 'ListingAssistant',
      requests: 2,
      successfulRequests: 1,
      failedRequests: 1,
      estimatedCost: 0.03,
      averageLatencyMs: 200
    }],
    modelUsage: [{
      modelUsed: 'fake-model',
      requests: 2,
      inputTokens: 200,
      outputTokens: 200,
      estimatedCost: 0.03,
      averageLatencyMs: 200
    }],
    topSellers: [{
      sellerId: 'seller-id',
      sellerDisplayName: 'AI Seller',
      requests: 2,
      failedRequests: 1,
      estimatedCost: 0.03,
      averageLatencyMs: 200
    }]
  };
}
