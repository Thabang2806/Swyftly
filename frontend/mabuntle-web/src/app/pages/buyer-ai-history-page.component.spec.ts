import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BuyerAiDiscoveryService } from '../buyer/buyer-ai-discovery.service';
import { BuyerAiHistoryPageComponent } from './buyer-ai-history-page.component';

describe('BuyerAiHistoryPageComponent', () => {
  let fixture: ComponentFixture<BuyerAiHistoryPageComponent>;
  let aiDiscoveryService: jasmine.SpyObj<BuyerAiDiscoveryService>;

  beforeEach(async () => {
    aiDiscoveryService = jasmine.createSpyObj<BuyerAiDiscoveryService>('BuyerAiDiscoveryService', [
      'getPreferences',
      'getHistory',
      'deleteHistoryItem',
      'clearHistory'
    ]);
    aiDiscoveryService.getPreferences.and.resolveTo({ historyEnabled: true, personalizationEnabled: false, updatedAtUtc: '2026-05-29T12:00:00Z' });
    aiDiscoveryService.getHistory.and.resolveTo({
      items: [createHistoryItem()],
      totalCount: 1,
      page: 1,
      pageSize: 25
    });
    aiDiscoveryService.deleteHistoryItem.and.resolveTo();
    aiDiscoveryService.clearHistory.and.resolveTo();

    await TestBed.configureTestingModule({
      imports: [BuyerAiHistoryPageComponent],
      providers: [
        provideRouter([]),
        { provide: BuyerAiDiscoveryService, useValue: aiDiscoveryService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BuyerAiHistoryPageComponent);
  });

  it('renders saved safe AI discovery rows', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('AI discovery history');
    expect(compiled.textContent).toContain('Assistant');
    expect(compiled.textContent).toContain('Dresses / Black / Linen');
    expect(compiled.textContent).toContain('Rose linen midi dress');
    expect(compiled.textContent).toContain('Server history is separate from browser-local recent prompts.');
  });

  it('renders disabled privacy state without loading history', async () => {
    aiDiscoveryService.getPreferences.and.resolveTo({ historyEnabled: false, personalizationEnabled: false, updatedAtUtc: null });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('AI history is off');
    expect(aiDiscoveryService.getHistory).not.toHaveBeenCalled();
  });

  it('deletes a history row and clears all history', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    const component = fixture.componentInstance as unknown as {
      historyItems(): ReturnType<typeof createHistoryItem>[];
      deleteHistoryItem(item: ReturnType<typeof createHistoryItem>): Promise<void>;
      clearAllHistory(): Promise<void>;
    };

    await component.deleteHistoryItem(component.historyItems()[0]);
    await component.clearAllHistory();

    expect(aiDiscoveryService.deleteHistoryItem).toHaveBeenCalledWith('history-id');
    expect(aiDiscoveryService.clearHistory).toHaveBeenCalled();
  });
});

function createHistoryItem() {
  return {
    historyId: 'history-id',
    sourceTool: 'Assistant' as const,
    category: 'Dresses',
    colour: 'Black',
    material: 'Linen',
    confidenceBand: 'Medium' as const,
    resultCount: 4,
    productIds: ['product-id'],
    products: [
      {
        productId: 'product-id',
        title: 'Rose linen midi dress',
        slug: 'rose-linen-midi-dress'
      }
    ],
    sourceRoute: '/assistant',
    createdAtUtc: '2026-05-29T12:00:00Z'
  };
}
