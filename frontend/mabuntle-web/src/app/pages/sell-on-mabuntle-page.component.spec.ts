import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { SellOnMabuntlePageComponent } from './sell-on-mabuntle-page.component';

describe('SellOnMabuntlePageComponent', () => {
  let fixture: ComponentFixture<SellOnMabuntlePageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SellOnMabuntlePageComponent],
      providers: [provideNoopAnimations(), provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(SellOnMabuntlePageComponent);
  });

  it('renders seller acquisition content and account CTAs', () => {
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const links = Array.from(compiled.querySelectorAll('a')).map(anchor => anchor.getAttribute('href'));

    expect(compiled.querySelector('.sell-hero')).not.toBeNull();
    expect(compiled.textContent).toContain('Build a reviewed fashion, beauty, or jewellery storefront.');
    expect(compiled.textContent).toContain('How selling works');
    expect(compiled.textContent).toContain('Real carrier adapters');
    expect(links).toContain('https://seller.mabuntle.com/register/seller');
    expect(links).toContain('https://seller.mabuntle.com/login');
  });
});
