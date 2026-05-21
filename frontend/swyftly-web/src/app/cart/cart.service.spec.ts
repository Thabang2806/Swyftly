import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { CartService } from './cart.service';

describe('CartService', () => {
  let service: CartService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(CartService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('loads the active cart', async () => {
    const promise = service.getCart();
    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/cart`);
    expect(request.request.method).toBe('GET');
    request.flush(createCart());

    await expectAsync(promise).toBeResolvedTo(createCart());
  });

  it('updates and removes cart items', async () => {
    const updatePromise = service.updateItem('cart-item-id', { quantity: 3 });
    const updateRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/cart/items/cart-item-id`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual({ quantity: 3 });
    updateRequest.flush({ ...createCart(), totalQuantity: 3 });
    await expectAsync(updatePromise).toBeResolved();

    const removePromise = service.removeItem('cart-item-id');
    const removeRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/cart/items/cart-item-id`);
    expect(removeRequest.request.method).toBe('DELETE');
    removeRequest.flush({ ...createCart(), items: [], totalQuantity: 0, subtotal: 0 });
    await expectAsync(removePromise).toBeResolved();

    const movePromise = service.moveItemToWishlist('cart-item-id');
    const moveRequest = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/cart/items/cart-item-id/move-to-wishlist`);
    expect(moveRequest.request.method).toBe('POST');
    moveRequest.flush({ ...createCart(), items: [], totalQuantity: 0, subtotal: 0 });
    await expectAsync(movePromise).toBeResolved();
  });

  it('creates an order from the cart', async () => {
    const body = {
      cartId: 'cart-id',
      reservationMinutes: null,
      deliveryAddressId: 'address-id',
      deliveryAddress: null,
      deliveryMethodId: 'delivery-method-id'
    };
    const promise = service.createOrderFromCart(body);
    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/orders/from-cart`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({
      orderId: 'order-id',
      buyerId: 'buyer-id',
      sellerId: 'seller-id',
      cartId: 'cart-id',
      status: 'PendingPayment',
      items: [],
      itemsSubtotal: 0,
      shippingAmount: 0,
      platformFeeAmount: 0,
      discountAmount: 0,
      totalAmount: 0,
      statusHistory: []
    });

    await expectAsync(promise).toBeResolved();
  });

  it('loads cart shipping options', async () => {
    const body = {
      cartId: 'cart-id',
      deliveryAddressId: 'address-id',
      deliveryAddress: null
    };
    const promise = service.getShippingOptions(body);
    const request = httpTestingController.expectOne(`${environment.apiBaseUrl}/api/cart/shipping-options`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({
      cartId: 'cart-id',
      sellerId: 'seller-id',
      cartSubtotal: 998,
      options: [{
        deliveryMethodId: 'delivery-method-id',
        name: 'Standard courier',
        description: null,
        methodType: 'Standard',
        countryCode: 'ZA',
        province: 'Gauteng',
        basePrice: 75,
        freeShippingThreshold: 1000,
        shippingAmount: 75,
        freeShippingApplied: false,
        estimatedMinDays: 2,
        estimatedMaxDays: 5,
        displayOrder: 10
      }]
    });

    const response = await promise;
    expect(response.options[0].deliveryMethodId).toBe('delivery-method-id');
  });
});

export function createCart() {
  return {
    cartId: 'cart-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    sellerStoreName: 'Seller Store',
    items: [{
      cartItemId: 'cart-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      productTitle: 'Summer Dress',
      productSlug: 'summer-dress',
      primaryImageUrl: 'https://example.test/summer-dress.jpg',
      primaryImageAltText: 'Summer dress',
      sku: 'SKU-1',
      size: 'M',
      colour: 'Black',
      unitPrice: 499,
      quantity: 2,
      lineTotal: 998
    }],
    totalQuantity: 2,
    subtotal: 998
  };
}
