import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminCategoryAttributeResponse, AdminCategoryResponse } from '../admin/admin-category.models';
import { AdminCategoryService } from '../admin/admin-category.service';
import { AdminCategoriesPageComponent } from './admin-categories-page.component';

describe('AdminCategoriesPageComponent', () => {
  let fixture: ComponentFixture<AdminCategoriesPageComponent>;
  let categoryService: jasmine.SpyObj<AdminCategoryService>;

  beforeEach(async () => {
    categoryService = jasmine.createSpyObj<AdminCategoryService>('AdminCategoryService', [
      'listCategories',
      'createCategory',
      'updateCategory',
      'activateCategory',
      'deactivateCategory',
      'createAttribute',
      'updateAttribute',
      'activateAttribute',
      'deactivateAttribute'
    ]);
    categoryService.listCategories.and.resolveTo([
      createCategory(),
      createCategory({
        categoryId: 'child-category-id',
        parentCategoryId: 'category-id',
        name: 'Dresses',
        slug: 'dresses',
        productCount: 4,
        childCount: 0
      })
    ]);
    categoryService.createCategory.and.resolveTo(createCategory({ categoryId: 'new-category-id', name: 'Shoes', slug: 'shoes' }));
    categoryService.updateCategory.and.resolveTo(createCategory({ name: 'Fashion Edit', slug: 'fashion-edit' }));
    categoryService.activateCategory.and.resolveTo(createCategory({ isActive: true }));
    categoryService.deactivateCategory.and.resolveTo(createCategory({ isActive: false }));
    categoryService.createAttribute.and.resolveTo(createCategory({
      attributes: [createAttribute(), createAttribute({ attributeId: 'new-attribute-id', name: 'Size', key: 'size', dataType: 'Select', allowedValues: ['S', 'M'] })]
    }));
    categoryService.updateAttribute.and.resolveTo(createCategory({
      attributes: [createAttribute({ name: 'Colour family' })]
    }));
    categoryService.activateAttribute.and.resolveTo(createCategory());
    categoryService.deactivateAttribute.and.resolveTo(createCategory());

    await TestBed.configureTestingModule({
      imports: [AdminCategoriesPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminCategoryService, useValue: categoryService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminCategoriesPageComponent);
  });

  it('renders category workspace and attribute metadata', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Categories and attributes');
    expect(compiled.textContent).toContain('Fashion');
    expect(compiled.textContent).toContain('Dresses');
    expect(compiled.textContent).toContain('Colour *');
    expect(compiled.textContent).not.toContain('write APIs are not available yet');
  });

  it('creates and updates category payloads', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      categoryForm: {
        setValue(value: { parentCategoryId: string; name: string; slug: string; displayOrder: number }): void;
      };
      saveCategory(): Promise<void>;
      editCategory(category: AdminCategoryResponse): void;
    };

    component.categoryForm.setValue({ parentCategoryId: '', name: 'Shoes', slug: 'shoes', displayOrder: 3 });
    await component.saveCategory();
    expect(categoryService.createCategory).toHaveBeenCalledWith({
      parentCategoryId: null,
      name: 'Shoes',
      slug: 'shoes',
      displayOrder: 3
    });

    component.editCategory(createCategory());
    component.categoryForm.setValue({ parentCategoryId: '', name: 'Fashion Edit', slug: 'fashion-edit', displayOrder: 4 });
    await component.saveCategory();
    expect(categoryService.updateCategory).toHaveBeenCalledWith('category-id', {
      parentCategoryId: null,
      name: 'Fashion Edit',
      slug: 'fashion-edit',
      displayOrder: 4
    });
  });

  it('activates and deactivates categories', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      activateCategory(category: AdminCategoryResponse): Promise<void>;
      deactivateCategory(category: AdminCategoryResponse): Promise<void>;
    };

    await component.deactivateCategory(createCategory());
    await component.activateCategory(createCategory({ isActive: false }));

    expect(categoryService.deactivateCategory).toHaveBeenCalledWith('category-id');
    expect(categoryService.activateCategory).toHaveBeenCalledWith('category-id');
  });

  it('creates and updates attribute payloads', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      selectCategory(category: AdminCategoryResponse): void;
      attributeForm: {
        setValue(value: {
          name: string;
          key: string;
          dataType: string;
          isRequired: boolean;
          allowedValuesText: string;
          displayOrder: number;
        }): void;
      };
      saveAttribute(): Promise<void>;
      editAttribute(attribute: AdminCategoryAttributeResponse): void;
    };

    component.selectCategory(createCategory());
    component.attributeForm.setValue({
      name: 'Size',
      key: 'size',
      dataType: 'Select',
      isRequired: true,
      allowedValuesText: 'S\nM, L',
      displayOrder: 2
    });
    await component.saveAttribute();
    expect(categoryService.createAttribute).toHaveBeenCalledWith('category-id', {
      name: 'Size',
      key: 'size',
      dataType: 'Select',
      isRequired: true,
      allowedValues: ['S', 'M', 'L'],
      displayOrder: 2
    });

    component.editAttribute(createAttribute());
    component.attributeForm.setValue({
      name: 'Colour family',
      key: 'colour',
      dataType: 'Text',
      isRequired: false,
      allowedValuesText: '',
      displayOrder: 1
    });
    await component.saveAttribute();
    expect(categoryService.updateAttribute).toHaveBeenCalledWith('category-id', 'attribute-id', {
      name: 'Colour family',
      key: 'colour',
      dataType: 'Text',
      isRequired: false,
      allowedValues: [],
      displayOrder: 1
    });
  });

  it('displays backend errors', async () => {
    categoryService.createCategory.and.rejectWith(new HttpErrorResponse({
      status: 409,
      error: { detail: 'Duplicate slug' }
    }));

    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance as unknown as {
      categoryForm: {
        setValue(value: { parentCategoryId: string; name: string; slug: string; displayOrder: number }): void;
      };
      saveCategory(): Promise<void>;
    };
    component.categoryForm.setValue({ parentCategoryId: '', name: 'Shoes', slug: 'shoes', displayOrder: 3 });
    await component.saveCategory();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Duplicate slug');
  });
});

function createCategory(overrides: Partial<AdminCategoryResponse> = {}): AdminCategoryResponse {
  return {
    categoryId: 'category-id',
    parentCategoryId: null,
    name: 'Fashion',
    slug: 'fashion',
    displayOrder: 1,
    isActive: true,
    productCount: 2,
    childCount: 1,
    attributes: [createAttribute()],
    ...overrides
  };
}

function createAttribute(overrides: Partial<AdminCategoryAttributeResponse> = {}): AdminCategoryAttributeResponse {
  return {
    attributeId: 'attribute-id',
    name: 'Colour',
    key: 'colour',
    dataType: 'Text',
    isRequired: true,
    allowedValues: ['Black', 'White'],
    displayOrder: 1,
    isActive: true,
    ...overrides
  };
}
