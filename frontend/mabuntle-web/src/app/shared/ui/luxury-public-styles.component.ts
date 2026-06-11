import { DOCUMENT } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, ViewEncapsulation, inject } from '@angular/core';
import { ensureLazyStylesheet } from './lazy-stylesheet';

@Component({
  selector: 'app-luxury-public-styles',
  encapsulation: ViewEncapsulation.None,
  host: { style: 'display: none' },
  template: ``
})
export class LuxuryPublicStylesComponent implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);

  ngOnInit(): void {
    ensureLazyStylesheet(this.document, this.platformId, 'luxury-public', '/styles/luxury-public.css');
  }
}
