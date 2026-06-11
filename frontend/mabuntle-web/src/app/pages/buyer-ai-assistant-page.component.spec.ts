import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerAiAssistantService } from '../buyer/buyer-ai-assistant.service';
import { BuyerGrowthTelemetryService } from '../buyer/buyer-growth-telemetry.service';
import { BuyerAiAssistantPageComponent } from './buyer-ai-assistant-page.component';

describe('BuyerAiAssistantPageComponent', () => {
  let fixture: ComponentFixture<BuyerAiAssistantPageComponent>;
  let assistantService: jasmine.SpyObj<BuyerAiAssistantService>;
  let telemetryService: jasmine.SpyObj<BuyerGrowthTelemetryService>;

  beforeEach(async () => {
    localStorage.removeItem('mabuntle.buyer.assistant.recentPrompts');
    assistantService = jasmine.createSpyObj<BuyerAiAssistantService>('BuyerAiAssistantService', ['search']);
    telemetryService = jasmine.createSpyObj<BuyerGrowthTelemetryService>('BuyerGrowthTelemetryService', ['recordEvent']);
    assistantService.search.and.resolveTo({
      intent: {
        category: 'Dresses',
        subcategory: null,
        budgetMax: 1500,
        budgetMin: null,
        size: 'M',
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
      products: [{
        productId: 'product-id',
        title: 'Black Wedding Dress',
        slug: 'black-wedding-dress',
        sellerDisplayName: 'Assistant Seller',
        imageUrl: null,
        price: 999,
        currency: 'ZAR',
        matchReasons: ['Available in Black.', 'Available in size M.'],
        personalizationApplied: true,
        personalizationReasons: ['Similar to saved items']
      }],
      summary: 'These matches come only from published Mabuntle products returned by the backend search.',
      safetyNote: null
    });

    await TestBed.configureTestingModule({
      imports: [BuyerAiAssistantPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerAiAssistantService, useValue: assistantService },
        { provide: BuyerGrowthTelemetryService, useValue: telemetryService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerAiAssistantPageComponent);
  });

  it('submits a message and displays product cards', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('textarea') as HTMLTextAreaElement;
    input.value = 'black dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(assistantService.search).toHaveBeenCalledWith({ message: 'black dress' });
    expect((fixture.nativeElement as HTMLElement).querySelector('.ai-chat-panel')).not.toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('.ai-recommendations-panel')).not.toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('black dress');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Black Wedding Dress');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Dresses');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Up to R1,500');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Available in Black.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Personalized');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Similar to saved items');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('High confidence');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('View product');
    expect(telemetryService.recordEvent).toHaveBeenCalledWith(jasmine.objectContaining({
      eventType: 'AssistantSearchSubmitted',
      sourceTool: 'Assistant',
      resultCount: 1,
      confidenceBand: 'High',
      category: 'Dresses',
      colour: 'Black',
      sourceRoute: '/assistant'
    }));
  });

  it('records product, shop handoff, and structured feedback telemetry without changing navigation links', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('textarea') as HTMLTextAreaElement;
    input.value = 'black dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    const productLink = compiled.querySelector('a[href="/product/black-wedding-dress"]') as HTMLAnchorElement;
    productLink.click();
    const shopLink = Array.from(compiled.querySelectorAll('a'))
      .find(anchor => anchor.textContent?.includes('Refine in shop')) as HTMLAnchorElement;
    shopLink.click();
    const feedbackButton = Array.from(compiled.querySelectorAll('.ai-feedback-card button'))
      .find(button => button.textContent?.includes('Too broad')) as HTMLButtonElement;
    feedbackButton.click();
    fixture.detectChanges();

    expect(telemetryService.recordEvent).toHaveBeenCalledWith(jasmine.objectContaining({
      eventType: 'AssistantProductOpened',
      productId: 'product-id'
    }));
    expect(telemetryService.recordEvent).toHaveBeenCalledWith(jasmine.objectContaining({
      eventType: 'AssistantShopHandoff'
    }));
    expect(telemetryService.recordEvent).toHaveBeenCalledWith(jasmine.objectContaining({
      eventType: 'AssistantFeedbackSubmitted',
      feedbackReason: 'TooBroad'
    }));
    expect(compiled.textContent).toContain('Feedback was saved without storing your prompt.');
  });

  it('stores, reuses, and clears recent prompts locally without storing results', async () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('textarea') as HTMLTextAreaElement;
    input.value = 'black dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    const stored = JSON.parse(localStorage.getItem('mabuntle.buyer.assistant.recentPrompts') ?? '[]');
    expect(stored[0].text).toBe('black dress');
    expect(stored[0].savedAtUtc).toEqual(jasmine.any(String));
    expect(stored[0].products).toBeUndefined();

    input.value = '';
    input.dispatchEvent(new Event('input'));
    const recentButton = Array.from(compiled.querySelectorAll('.ai-recent-panel button'))
      .find(button => button.textContent?.includes('black dress')) as HTMLButtonElement;
    recentButton.click();
    fixture.detectChanges();

    expect(input.value).toBe('black dress');

    const clearButton = Array.from(compiled.querySelectorAll('.ai-recent-panel button'))
      .find(button => button.textContent?.includes('Clear')) as HTMLButtonElement;
    clearButton.click();
    fixture.detectChanges();

    expect(localStorage.getItem('mabuntle.buyer.assistant.recentPrompts')).toBeNull();
    expect(compiled.querySelector('.ai-recent-panel')).toBeNull();
  });

  it('populates the form from an example prompt without submitting', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const exampleButton = Array.from(compiled.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Minimal gold earrings')) as HTMLButtonElement;

    exampleButton.click();
    fixture.detectChanges();

    const input = compiled.querySelector('textarea') as HTMLTextAreaElement;
    expect(input.value).toBe('Minimal gold earrings for everyday wear');
    expect(assistantService.search).not.toHaveBeenCalled();
  });

  it('renders an empty state when the backend returns no products', async () => {
    assistantService.search.and.resolveTo({
      intent: {
        category: null,
        subcategory: null,
        budgetMax: null,
        budgetMin: null,
        size: null,
        colour: null,
        occasion: null,
        style: null,
        material: null,
        brand: null,
        beautySkinType: null,
        beautyConcern: null,
        searchText: 'specific request',
        isVague: true,
        clarificationPrompt: 'Can you add a category or budget?'
      },
      products: [],
      summary: 'No published products matched this request.',
      safetyNote: 'Beauty results are product discovery only.'
    });

    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('textarea') as HTMLTextAreaElement;
    input.value = 'specific request';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Needs more detail');
    expect(compiled.textContent).toContain('Low confidence');
    expect(compiled.textContent).toContain('Can you add a category or budget?');
    expect(compiled.textContent).toContain('No product cards to show');
  });

  it('renders backend errors', async () => {
    assistantService.search.and.rejectWith(new HttpErrorResponse({
      status: 503,
      error: { title: 'Assistant unavailable' }
    }));

    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('textarea') as HTMLTextAreaElement;
    input.value = 'black dress';
    input.dispatchEvent(new Event('input'));

    const form = compiled.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Assistant unavailable');
  });
});
