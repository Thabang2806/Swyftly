import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { BuyerAiAssistantService } from './buyer-ai-assistant.service';

describe('BuyerAiAssistantService', () => {
  let service: BuyerAiAssistantService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BuyerAiAssistantService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('posts buyer assistant searches', async () => {
    const promise = service.search({ message: 'black dress' });

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/ai/shopping-assistant`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ message: 'black dress' });
    request.flush({
      intent: {
        category: 'Dresses',
        subcategory: null,
        budgetMax: null,
        budgetMin: null,
        size: null,
        colour: 'Black',
        occasion: null,
        style: null,
        material: null,
        brand: null,
        beautySkinType: null,
        beautyConcern: null,
        searchText: 'black dress',
        isVague: false,
        clarificationPrompt: null
      },
      products: [],
      summary: 'No exact products matched.',
      safetyNote: null
    });

    const response = await promise;
    expect(response.intent.category).toBe('Dresses');
  });
});
