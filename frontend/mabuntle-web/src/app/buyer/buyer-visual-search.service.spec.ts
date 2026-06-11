import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { BuyerVisualSearchService } from './buyer-visual-search.service';

describe('BuyerVisualSearchService', () => {
  let service: BuyerVisualSearchService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BuyerVisualSearchService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('posts visual search requests', async () => {
    const requestBody = {
      imageReference: 'black dress',
      imageDataBase64: null,
      fileName: null,
      contentType: null
    };
    const promise = service.search(requestBody);

    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/buyer/ai/visual-search`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(requestBody);
    request.flush({
      attributes: {
        category: 'Dresses',
        colour: 'Black',
        style: 'Formal',
        shape: null,
        pattern: null,
        materialGuess: null,
        materialConfidence: null,
        confidence: 0.72,
        searchText: 'Dresses Black Formal',
        warnings: []
      },
      products: [],
      summary: 'No published in-stock products matched the extracted visual attributes.',
      imageRetentionNote: 'Uploaded image data is processed for this request only and is not persisted by the visual search MVP.'
    });

    const response = await promise;
    expect(response.attributes.category).toBe('Dresses');
  });
});
