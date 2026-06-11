import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { HomePageComponent } from './home-page.component';

describe('HomePageComponent', () => {
  let fixture: ComponentFixture<HomePageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomePageComponent],
      providers: [provideNoopAnimations(), provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(HomePageComponent);
  });

  it('renders the high-fidelity marketplace hero and featured edit cards', () => {
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('.market-home-hero')).not.toBeNull();
    expect(compiled.querySelectorAll('.market-feature-card').length).toBe(4);
    expect(compiled.textContent).toContain('Shop local style, beauty, and jewellery. Mabuntle.');
    expect(compiled.textContent).toContain('Live inventory still comes from the shop APIs.');
    expect(compiled.querySelector('a[href="/sell"]')?.textContent).toContain('Start selling');
  });
});
