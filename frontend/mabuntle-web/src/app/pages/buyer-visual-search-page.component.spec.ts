import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerGrowthTelemetryService } from '../buyer/buyer-growth-telemetry.service';
import { BuyerVisualSearchService } from '../buyer/buyer-visual-search.service';
import { BuyerVisualSearchPageComponent } from './buyer-visual-search-page.component';

describe('BuyerVisualSearchPageComponent', () => {
  let fixture: ComponentFixture<BuyerVisualSearchPageComponent>;
  let visualSearchService: jasmine.SpyObj<BuyerVisualSearchService>;
  let telemetryService: jasmine.SpyObj<BuyerGrowthTelemetryService>;

  beforeEach(async () => {
    localStorage.removeItem('mabuntle.buyer.visualSearch.recentReferences');
    visualSearchService = jasmine.createSpyObj<BuyerVisualSearchService>('BuyerVisualSearchService', ['search']);
    telemetryService = jasmine.createSpyObj<BuyerGrowthTelemetryService>('BuyerGrowthTelemetryService', ['recordEvent']);
    visualSearchService.search.and.resolveTo({
      attributes: {
        category: 'Dresses',
        colour: 'Black',
        style: 'Formal',
        shape: 'Maxi',
        pattern: null,
        materialGuess: null,
        materialConfidence: null,
        confidence: 0.72,
        searchText: 'Dresses Black Formal Maxi',
        warnings: ['Material and brand are not inferred unless visible context is explicit.']
      },
      products: [{
        productId: 'product-id',
        title: 'Black Formal Maxi Dress',
        slug: 'black-formal-maxi-dress',
        sellerDisplayName: 'Visual Seller',
        imageUrl: null,
        price: 999,
        currency: 'ZAR',
        matchReasons: ['Available in Black.'],
        personalizationApplied: true,
        personalizationReasons: ['Matches recent cart interest']
      }],
      summary: 'These matches use extracted visual attributes against published Mabuntle products only.',
      imageRetentionNote: 'Uploaded image data is processed for this request only and is not persisted by the visual search MVP.'
    });

    await TestBed.configureTestingModule({
      imports: [BuyerVisualSearchPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerVisualSearchService, useValue: visualSearchService },
        { provide: BuyerGrowthTelemetryService, useValue: telemetryService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerVisualSearchPageComponent);
  });

  it('submits an image reference and displays product cards', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('input[formcontrolname="imageReference"]') as HTMLInputElement;
    input.value = 'black formal maxi dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(visualSearchService.search).toHaveBeenCalledWith({
      imageReference: 'black formal maxi dress',
      imageDataBase64: null,
      fileName: null,
      contentType: null
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Black Formal Maxi Dress');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Medium visual match (72%)');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Formal');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('not persisted');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('View product');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Personalized');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Matches recent cart interest');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Search manually');
    expect(telemetryService.recordEvent).toHaveBeenCalledWith(jasmine.objectContaining({
      eventType: 'VisualSearchSubmitted',
      sourceTool: 'VisualSearch',
      resultCount: 1,
      confidenceBand: 'Medium',
      category: 'Dresses',
      colour: 'Black',
      sourceRoute: '/visual-search'
    }));
  });

  it('records product, shop handoff, and structured feedback telemetry', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('input[formcontrolname="imageReference"]') as HTMLInputElement;
    input.value = 'black formal maxi dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    const productLink = compiled.querySelector('a[href="/product/black-formal-maxi-dress"]') as HTMLAnchorElement;
    productLink.click();
    const shopLink = Array.from(compiled.querySelectorAll('a'))
      .find(anchor => anchor.textContent?.includes('Search manually')) as HTMLAnchorElement;
    shopLink.click();
    const feedbackButton = Array.from(compiled.querySelectorAll('.ai-feedback-card button'))
      .find(button => button.textContent?.includes('Low confidence')) as HTMLButtonElement;
    feedbackButton.click();
    fixture.detectChanges();

    expect(telemetryService.recordEvent).toHaveBeenCalledWith(jasmine.objectContaining({
      eventType: 'VisualProductOpened',
      productId: 'product-id'
    }));
    expect(telemetryService.recordEvent).toHaveBeenCalledWith(jasmine.objectContaining({
      eventType: 'VisualShopHandoff'
    }));
    expect(telemetryService.recordEvent).toHaveBeenCalledWith(jasmine.objectContaining({
      eventType: 'VisualFeedbackSubmitted',
      feedbackReason: 'LowConfidence'
    }));
    expect(compiled.textContent).toContain('Feedback was saved without storing image data.');
  });

  it('stores, reuses, and clears text references without storing uploaded image data', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('input[formcontrolname="imageReference"]') as HTMLInputElement;
    input.value = 'black formal maxi dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    const stored = JSON.parse(localStorage.getItem('mabuntle.buyer.visualSearch.recentReferences') ?? '[]');
    expect(stored[0].text).toBe('black formal maxi dress');
    expect(stored[0].savedAtUtc).toEqual(jasmine.any(String));
    expect(stored[0].imageDataBase64).toBeUndefined();
    expect(stored[0].products).toBeUndefined();

    input.value = '';
    input.dispatchEvent(new Event('input'));
    const recentButton = Array.from(compiled.querySelectorAll('.ai-recent-panel button'))
      .find(button => button.textContent?.includes('black formal maxi dress')) as HTMLButtonElement;
    recentButton.click();
    fixture.detectChanges();

    expect(input.value).toBe('black formal maxi dress');

    const clearButton = Array.from(compiled.querySelectorAll('.ai-recent-panel button'))
      .find(button => button.textContent?.includes('Clear')) as HTMLButtonElement;
    clearButton.click();
    fixture.detectChanges();

    expect(localStorage.getItem('mabuntle.buyer.visualSearch.recentReferences')).toBeNull();
    expect(compiled.querySelector('.ai-recent-panel')).toBeNull();
  });

  it('resets selected search state without clearing local recent references', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('input[formcontrolname="imageReference"]') as HTMLInputElement;
    input.value = 'black formal maxi dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    const resetButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Reset search')) as HTMLButtonElement;
    resetButton.click();
    fixture.detectChanges();

    expect(input.value).toBe('');
    expect(compiled.textContent).not.toContain('Black Formal Maxi Dress');
    expect(localStorage.getItem('mabuntle.buyer.visualSearch.recentReferences')).toContain('black formal maxi dress');
  });

  it('renders low confidence visual guidance', async () => {
    visualSearchService.search.and.resolveTo({
      attributes: {
        category: null,
        colour: null,
        style: null,
        shape: null,
        pattern: null,
        materialGuess: null,
        materialConfidence: null,
        confidence: 0.21,
        searchText: 'unclear item',
        warnings: ['The image was difficult to interpret.']
      },
      products: [],
      summary: 'No confident visual attributes were extracted.',
      imageRetentionNote: 'Uploaded image data is processed for this request only and is not persisted by the visual search MVP.'
    });

    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('input[formcontrolname="imageReference"]') as HTMLInputElement;
    input.value = 'unclear item';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Low visual match (21%)');
    expect(compiled.textContent).toContain('The image or reference was hard to interpret.');
    expect(compiled.textContent).toContain('No product cards to show');
  });

  it('requires an upload or image reference', async () => {
    fixture.detectChanges();
    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;

    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(visualSearchService.search).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Upload an image or enter an image reference.');
  });

  it('rejects unsupported image types before calling the service', () => {
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector('input[type="file"]') as HTMLInputElement;
    setInputFile(input, new File(['not an image'], 'reference.gif', { type: 'image/gif' }));

    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(visualSearchService.search).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Upload a PNG, JPEG, or WebP image.');
  });

  it('rejects oversized image uploads before calling the service', () => {
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector('input[type="file"]') as HTMLInputElement;
    const largeFile = new File([new Uint8Array((5 * 1024 * 1024) + 1)], 'large.jpg', { type: 'image/jpeg' });
    setInputFile(input, largeFile);

    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(visualSearchService.search).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Upload an image up to 5 MB.');
  });
});

function setInputFile(input: HTMLInputElement, file: File): void {
  Object.defineProperty(input, 'files', {
    configurable: true,
    value: [file]
  });
}
