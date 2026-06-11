import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminCategoryResponse, AdminCategoryAttributeResponse } from '../admin/admin-category.models';
import { AdminCategoryService } from '../admin/admin-category.service';
import { AdminWorkspaceNavComponent } from '../admin/admin-workspace-nav.component';
import { getApiErrorMessage } from '../auth/api-error';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-admin-categories-page',
  imports: [
    AdminWorkspaceNavComponent,
    EmptyStateComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page admin-support-page">
      <app-admin-workspace-nav />

      <app-page-header
        eyebrow="Catalog operations"
        heading="Categories and attributes"
        description="Manage the taxonomy and seller product-form attributes without deleting historical product data."
      >
        <div pageHeaderActions>
          <a data-ui-button="secondary" routerLink="/products">Product review</a>
        </div>
      </app-page-header>

      <app-ui-alert tone="info">
        Categories and attributes can be deactivated, not deleted. Existing products keep their current category and attribute data.
      </app-ui-alert>

      @if (successMessage()) {
        <app-ui-alert tone="success">{{ successMessage() }}</app-ui-alert>
      }

      @if (errorMessage()) {
        <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
      }

      @if (isLoading()) {
        <div class="route-card">Loading categories...</div>
      } @else if (categories().length === 0) {
        <app-empty-state
          eyebrow="Catalog"
          heading="No categories found"
          message="Create the first category to start building the marketplace taxonomy."
        />
      } @else {
        <div class="admin-catalog-workspace">
          <section class="admin-category-grid" aria-label="Category list">
            @for (category of categories(); track category.categoryId) {
              <article
                class="admin-category-card"
                [class.selected]="selectedCategory()?.categoryId === category.categoryId"
              >
                <div class="admin-category-header">
                  <div>
                    <h2>{{ category.name }}</h2>
                    <p>{{ category.slug }}</p>
                  </div>
                  <app-status-badge [label]="category.isActive ? 'Active' : 'Inactive'" [tone]="category.isActive ? 'success' : 'neutral'" />
                </div>

                <dl class="seller-facts">
                  <div><dt>Products</dt><dd>{{ category.productCount }}</dd></div>
                  <div><dt>Attributes</dt><dd>{{ category.attributes.length }}</dd></div>
                  <div><dt>Children</dt><dd>{{ category.childCount }}</dd></div>
                </dl>

                <div class="buyer-action-row">
                  <button data-ui-button="secondary" type="button" (click)="selectCategory(category)">Select</button>
                  <button data-ui-button="secondary" type="button" (click)="editCategory(category)">Edit</button>
                  @if (category.isActive) {
                    <button data-ui-button="secondary" type="button" (click)="deactivateCategory(category)">Deactivate</button>
                  } @else {
                    <button data-ui-button="secondary" type="button" (click)="activateCategory(category)">Activate</button>
                  }
                </div>

                @if (category.attributes.length > 0) {
                  <div class="admin-category-attributes">
                    @for (attribute of category.attributes; track attribute.attributeId) {
                      <span>{{ attribute.name }}{{ attribute.isRequired ? ' *' : '' }}</span>
                    }
                  </div>
                }
              </article>
            }
          </section>

          <aside class="admin-catalog-side">
            <section class="route-card">
              <div class="admin-section-heading">
                <div>
                  <p class="eyebrow">{{ editingCategoryId() ? 'Edit category' : 'Create category' }}</p>
                  <h2>Category details</h2>
                </div>
                <button data-ui-button="secondary" type="button" (click)="startNewCategory()">New</button>
              </div>

              <form [formGroup]="categoryForm" (ngSubmit)="saveCategory()" class="admin-finance-form" novalidate>
                <label class="ui-field">
                  <span>Parent category</span>
                  <select formControlName="parentCategoryId">
                    <option [value]="''">None</option>
                    @for (category of parentOptions(); track category.categoryId) {
                      <option [value]="category.categoryId">{{ category.name }}</option>
                    }
                  </select>
                </label>

                <label class="ui-field">
                  <span>Name</span>
                  <input formControlName="name" />
                </label>

                <label class="ui-field">
                  <span>Slug</span>
                  <input formControlName="slug" />
                </label>

                <label class="ui-field">
                  <span>Display order</span>
                  <input type="number" min="0" formControlName="displayOrder" />
                </label>

                <button data-ui-button="primary" type="submit" [disabled]="isActing()">Save category</button>
              </form>
            </section>

            @if (selectedCategory()) {
              <section class="route-card">
                <div class="admin-section-heading">
                  <div>
                    <p class="eyebrow">Selected category</p>
                    <h2>{{ selectedCategory()!.name }}</h2>
                  </div>
                  <button data-ui-button="secondary" type="button" (click)="startNewAttribute()">New attribute</button>
                </div>

                @if (selectedCategory()!.attributes.length === 0) {
                  <app-empty-state
                    eyebrow="Attributes"
                    heading="No attributes"
                    message="Add attributes that sellers must or can provide for this category."
                  />
                } @else {
                  <div class="admin-table" role="table" aria-label="Category attributes">
                    @for (attribute of selectedCategory()!.attributes; track attribute.attributeId) {
                      <div class="admin-table-row admin-category-attribute-row" role="row">
                        <span role="cell">
                          <strong>{{ attribute.name }}</strong>
                          <small>{{ attribute.key }} - {{ attribute.dataType }}</small>
                        </span>
                        <span role="cell">
                          <app-status-badge [label]="attribute.isActive ? 'Active' : 'Inactive'" [tone]="attribute.isActive ? 'success' : 'neutral'" />
                          <small>{{ attribute.isRequired ? 'Required' : 'Optional' }}</small>
                        </span>
                        <span role="cell">
                          <small>{{ attribute.allowedValues.length ? attribute.allowedValues.join(', ') : 'No allowed values' }}</small>
                        </span>
                        <span role="cell">
                          <button data-ui-button="secondary" type="button" (click)="editAttribute(attribute)">Edit</button>
                          @if (attribute.isActive) {
                            <button data-ui-button="secondary" type="button" (click)="deactivateAttribute(attribute)">Deactivate</button>
                          } @else {
                            <button data-ui-button="secondary" type="button" (click)="activateAttribute(attribute)">Activate</button>
                          }
                        </span>
                      </div>
                    }
                  </div>
                }

                <form [formGroup]="attributeForm" (ngSubmit)="saveAttribute()" class="admin-finance-form" novalidate>
                  <h3>{{ editingAttributeId() ? 'Edit attribute' : 'Create attribute' }}</h3>
                  <app-ui-alert tone="warning">
                    Key, type, required, and allowed-value changes may be blocked when product listings already depend on them.
                  </app-ui-alert>

                  <label class="ui-field">
                    <span>Name</span>
                    <input formControlName="name" />
                  </label>

                  <label class="ui-field">
                    <span>Key</span>
                    <input formControlName="key" />
                  </label>

                  <label class="ui-field">
                    <span>Data type</span>
                    <select formControlName="dataType">
                      @for (dataType of dataTypes; track dataType) {
                        <option [value]="dataType">{{ dataType }}</option>
                      }
                    </select>
                  </label>

                  <label class="ui-checkbox"><input type="checkbox" formControlName="isRequired" /><span>Required for seller listings</span></label>

                  <label class="ui-field">
                    <span>Allowed values</span>
                    <textarea rows="3" formControlName="allowedValuesText" placeholder="One per line or comma-separated"></textarea>
                  </label>

                  <label class="ui-field">
                    <span>Display order</span>
                    <input type="number" min="0" formControlName="displayOrder" />
                  </label>

                  <div class="buyer-action-row">
                    <button data-ui-button="primary" type="submit" [disabled]="isActing()">Save attribute</button>
                    <button data-ui-button="secondary" type="button" (click)="startNewAttribute()">Clear</button>
                  </div>
                </form>
              </section>
            }
          </aside>
        </div>
      }
    </section>
  `
})
export class AdminCategoriesPageComponent implements OnInit {
  private readonly categoryService = inject(AdminCategoryService);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly dataTypes = ['Text', 'Number', 'Decimal', 'Boolean', 'Select', 'MultiSelect', 'Date'];
  protected readonly categories = signal<AdminCategoryResponse[]>([]);
  protected readonly selectedCategoryId = signal<string | null>(null);
  protected readonly editingCategoryId = signal<string | null>(null);
  protected readonly editingAttributeId = signal<string | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isActing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly selectedCategory = computed(() =>
    this.categories().find(category => category.categoryId === this.selectedCategoryId()) ?? null);

  protected readonly parentOptions = computed(() =>
    this.categories().filter(category => category.categoryId !== this.editingCategoryId()));

  protected readonly categoryForm = this.formBuilder.group({
    parentCategoryId: [''],
    name: ['', [Validators.required]],
    slug: ['', [Validators.required]],
    displayOrder: [0, [Validators.required, Validators.min(0)]]
  });

  protected readonly attributeForm = this.formBuilder.group({
    name: ['', [Validators.required]],
    key: ['', [Validators.required]],
    dataType: ['Text', [Validators.required]],
    isRequired: [false],
    allowedValuesText: [''],
    displayOrder: [0, [Validators.required, Validators.min(0)]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadCategories();
  }

  protected selectCategory(category: AdminCategoryResponse): void {
    this.selectedCategoryId.set(category.categoryId);
  }

  protected startNewCategory(): void {
    this.editingCategoryId.set(null);
    this.categoryForm.reset({ parentCategoryId: '', name: '', slug: '', displayOrder: 0 });
  }

  protected editCategory(category: AdminCategoryResponse): void {
    this.editingCategoryId.set(category.categoryId);
    this.selectedCategoryId.set(category.categoryId);
    this.categoryForm.reset({
      parentCategoryId: category.parentCategoryId ?? '',
      name: category.name,
      slug: category.slug,
      displayOrder: category.displayOrder
    });
  }

  protected async saveCategory(): Promise<void> {
    if (this.categoryForm.invalid || this.isActing()) {
      this.categoryForm.markAllAsTouched();
      return;
    }

    const value = this.categoryForm.getRawValue();
    const request = {
      parentCategoryId: value.parentCategoryId || null,
      name: value.name,
      slug: value.slug,
      displayOrder: value.displayOrder
    };

    await this.runAction(async () => {
      const categoryId = this.editingCategoryId();
      const saved = categoryId
        ? await this.categoryService.updateCategory(categoryId, request)
        : await this.categoryService.createCategory(request);
      this.successMessage.set(categoryId ? 'Category updated.' : 'Category created.');
      await this.loadCategories(saved.categoryId, true);
      this.editCategory(saved);
    });
  }

  protected async activateCategory(category: AdminCategoryResponse): Promise<void> {
    await this.runAction(async () => {
      const updated = await this.categoryService.activateCategory(category.categoryId);
      this.successMessage.set('Category activated.');
      await this.loadCategories(updated.categoryId, true);
    });
  }

  protected async deactivateCategory(category: AdminCategoryResponse): Promise<void> {
    await this.runAction(async () => {
      const updated = await this.categoryService.deactivateCategory(category.categoryId);
      this.successMessage.set('Category deactivated.');
      await this.loadCategories(updated.categoryId, true);
    });
  }

  protected startNewAttribute(): void {
    this.editingAttributeId.set(null);
    this.attributeForm.reset({
      name: '',
      key: '',
      dataType: 'Text',
      isRequired: false,
      allowedValuesText: '',
      displayOrder: 0
    });
  }

  protected editAttribute(attribute: AdminCategoryAttributeResponse): void {
    this.editingAttributeId.set(attribute.attributeId);
    this.attributeForm.reset({
      name: attribute.name,
      key: attribute.key,
      dataType: attribute.dataType,
      isRequired: attribute.isRequired,
      allowedValuesText: attribute.allowedValues.join('\n'),
      displayOrder: attribute.displayOrder
    });
  }

  protected async saveAttribute(): Promise<void> {
    const category = this.selectedCategory();
    if (!category || this.attributeForm.invalid || this.isActing()) {
      this.attributeForm.markAllAsTouched();
      return;
    }

    const value = this.attributeForm.getRawValue();
    const request = {
      name: value.name,
      key: value.key,
      dataType: value.dataType,
      isRequired: value.isRequired,
      allowedValues: this.parseAllowedValues(value.allowedValuesText),
      displayOrder: value.displayOrder
    };

    await this.runAction(async () => {
      const attributeId = this.editingAttributeId();
      const updatedCategory = attributeId
        ? await this.categoryService.updateAttribute(category.categoryId, attributeId, request)
        : await this.categoryService.createAttribute(category.categoryId, request);
      this.successMessage.set(attributeId ? 'Attribute updated.' : 'Attribute created.');
      await this.loadCategories(updatedCategory.categoryId, true);
      this.startNewAttribute();
    });
  }

  protected async activateAttribute(attribute: AdminCategoryAttributeResponse): Promise<void> {
    const category = this.selectedCategory();
    if (!category) {
      return;
    }

    await this.runAction(async () => {
      const updated = await this.categoryService.activateAttribute(category.categoryId, attribute.attributeId);
      this.successMessage.set('Attribute activated.');
      await this.loadCategories(updated.categoryId, true);
    });
  }

  protected async deactivateAttribute(attribute: AdminCategoryAttributeResponse): Promise<void> {
    const category = this.selectedCategory();
    if (!category) {
      return;
    }

    await this.runAction(async () => {
      const updated = await this.categoryService.deactivateAttribute(category.categoryId, attribute.attributeId);
      this.successMessage.set('Attribute deactivated.');
      await this.loadCategories(updated.categoryId, true);
    });
  }

  private async loadCategories(selectedCategoryId?: string, keepLoadingState = false): Promise<void> {
    if (!keepLoadingState) {
      this.isLoading.set(true);
    }
    this.errorMessage.set(null);

    try {
      const categories = await this.categoryService.listCategories();
      this.categories.set(categories);
      this.selectedCategoryId.set(selectedCategoryId ?? this.selectedCategoryId() ?? categories[0]?.categoryId ?? null);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.categories.set([]);
    } finally {
      this.isLoading.set(false);
    }
  }

  private async runAction(action: () => Promise<void>): Promise<void> {
    this.isActing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      await action();
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isActing.set(false);
    }
  }

  private parseAllowedValues(value: string): string[] {
    return value
      .split(/[\n,]/)
      .map(item => item.trim())
      .filter(item => item.length > 0);
  }
}
