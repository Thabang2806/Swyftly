import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AdminCategoryService } from './admin-category.service';

describe('AdminCategoryService', () => {
  let service: AdminCategoryService;
  let httpTestingController: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/api/admin/categories`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AdminCategoryService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads categories', async () => {
    const promise = service.listCategories();

    const request = httpTestingController.expectOne(baseUrl);
    expect(request.request.method).toBe('GET');
    request.flush([{ categoryId: 'category-id', attributes: [] }]);

    const response = await promise;
    expect(response[0].categoryId).toBe('category-id');
  });

  it('sends category write requests', async () => {
    const requestBody = { parentCategoryId: null, name: 'Shoes', slug: 'shoes', displayOrder: 2 };
    const createPromise = service.createCategory(requestBody);
    const createRequest = httpTestingController.expectOne(baseUrl);
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual(requestBody);
    createRequest.flush({ categoryId: 'category-id' });
    await createPromise;

    const updatePromise = service.updateCategory('category-id', requestBody);
    const updateRequest = httpTestingController.expectOne(`${baseUrl}/category-id`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(requestBody);
    updateRequest.flush({ categoryId: 'category-id' });
    await updatePromise;

    const deactivatePromise = service.deactivateCategory('category-id');
    const deactivateRequest = httpTestingController.expectOne(`${baseUrl}/category-id/deactivate`);
    expect(deactivateRequest.request.method).toBe('POST');
    deactivateRequest.flush({ categoryId: 'category-id' });
    await deactivatePromise;

    const activatePromise = service.activateCategory('category-id');
    const activateRequest = httpTestingController.expectOne(`${baseUrl}/category-id/activate`);
    expect(activateRequest.request.method).toBe('POST');
    activateRequest.flush({ categoryId: 'category-id' });
    await activatePromise;
  });

  it('sends attribute write requests', async () => {
    const requestBody = {
      name: 'Size',
      key: 'size',
      dataType: 'Select',
      isRequired: true,
      allowedValues: ['S', 'M'],
      displayOrder: 1
    };
    const createPromise = service.createAttribute('category-id', requestBody);
    const createRequest = httpTestingController.expectOne(`${baseUrl}/category-id/attributes`);
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual(requestBody);
    createRequest.flush({ categoryId: 'category-id' });
    await createPromise;

    const updatePromise = service.updateAttribute('category-id', 'attribute-id', requestBody);
    const updateRequest = httpTestingController.expectOne(`${baseUrl}/category-id/attributes/attribute-id`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(requestBody);
    updateRequest.flush({ categoryId: 'category-id' });
    await updatePromise;

    const activatePromise = service.activateAttribute('category-id', 'attribute-id');
    const activateRequest = httpTestingController.expectOne(`${baseUrl}/category-id/attributes/attribute-id/activate`);
    expect(activateRequest.request.method).toBe('POST');
    activateRequest.flush({ categoryId: 'category-id' });
    await activatePromise;

    const deactivatePromise = service.deactivateAttribute('category-id', 'attribute-id');
    const deactivateRequest = httpTestingController.expectOne(`${baseUrl}/category-id/attributes/attribute-id/deactivate`);
    expect(deactivateRequest.request.method).toBe('POST');
    deactivateRequest.flush({ categoryId: 'category-id' });
    await deactivatePromise;
  });
});
