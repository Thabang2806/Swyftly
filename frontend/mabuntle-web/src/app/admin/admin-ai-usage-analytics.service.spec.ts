import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminAiUsageAnalyticsService } from './admin-ai-usage-analytics.service';

describe('AdminAiUsageAnalyticsService', () => {
  let service: AdminAiUsageAnalyticsService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminAiUsageAnalyticsService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads AI usage analytics with filters', async () => {
    const promise = service.getAnalytics({
      fromUtc: '2026-05-01T00:00:00.000Z',
      toUtc: '2026-05-19T00:00:00.000Z',
      featureName: 'ListingAssistant',
      sellerId: 'seller-id'
    });

    const request = httpTestingController.expectOne(req =>
      req.url === `${environment.apiBaseUrl}/api/admin/analytics/ai-usage`
      && req.params.get('fromUtc') === '2026-05-01T00:00:00.000Z'
      && req.params.get('toUtc') === '2026-05-19T00:00:00.000Z'
      && req.params.get('featureName') === 'ListingAssistant'
      && req.params.get('sellerId') === 'seller-id');
    expect(request.request.method).toBe('GET');
    request.flush(createAnalytics());

    const response = await promise;
    expect(response.totals.requests).toBe(3);
  });
});

function createAnalytics() {
  return {
    fromUtc: '2026-05-01T00:00:00.000Z',
    toUtc: '2026-05-19T00:00:00.000Z',
    generatedAtUtc: '2026-05-19T10:00:00.000Z',
    featureName: 'ListingAssistant',
    sellerId: 'seller-id',
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
      qualityScoreImprovementNote: 'Baseline unavailable.',
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
    featureUsage: [],
    modelUsage: [],
    topSellers: []
  };
}
