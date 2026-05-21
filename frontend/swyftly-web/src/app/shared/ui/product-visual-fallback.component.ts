import { Component, computed, input } from '@angular/core';

export type ProductVisualTone = 'dress' | 'jewel' | 'beauty' | 'bag' | 'shoe' | 'neutral';

@Component({
  selector: 'app-product-visual-fallback',
  template: `
    <div class="hf-product-visual {{ toneClass() }}">
      <span>{{ label() }}</span>
      <strong>{{ title() }}</strong>
    </div>
  `
})
export class ProductVisualFallbackComponent {
  readonly title = input<string | null>('Product');
  readonly label = input<string | null>('Swyftly edit');
  readonly tone = input<ProductVisualTone>('neutral');

  protected readonly toneClass = computed(() => `hf-product-visual--${this.tone()}`);
}
