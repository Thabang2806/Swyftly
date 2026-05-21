import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerAiAssistantService } from '../buyer/buyer-ai-assistant.service';
import { BuyerAiAssistantPageComponent } from './buyer-ai-assistant-page.component';

describe('BuyerAiAssistantPageComponent', () => {
  let fixture: ComponentFixture<BuyerAiAssistantPageComponent>;
  let assistantService: jasmine.SpyObj<BuyerAiAssistantService>;

  beforeEach(async () => {
    assistantService = jasmine.createSpyObj<BuyerAiAssistantService>('BuyerAiAssistantService', ['search']);
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
        matchReasons: ['Available in Black.', 'Available in size M.']
      }],
      summary: 'These matches come only from published Swyftly products returned by the backend search.',
      safetyNote: null
    });

    await TestBed.configureTestingModule({
      imports: [BuyerAiAssistantPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerAiAssistantService, useValue: assistantService }
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
