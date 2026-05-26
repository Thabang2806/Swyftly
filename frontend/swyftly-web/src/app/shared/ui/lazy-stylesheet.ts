import { isPlatformBrowser } from '@angular/common';

export function ensureLazyStylesheet(
  document: Document,
  platformId: object,
  id: string,
  href: string
): void {
  if (!isPlatformBrowser(platformId)) {
    return;
  }

  if (document.head.querySelector(`link[data-swyftly-style="${id}"]`)) {
    return;
  }

  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = href;
  link.dataset['swyftlyStyle'] = id;
  document.head.appendChild(link);
}
