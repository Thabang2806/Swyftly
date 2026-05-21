import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BuyerEngagementService } from '../buyer/buyer-engagement.service';
import { BuyerDisputeService } from '../buyer/buyer-dispute.service';
import { BuyerOrderService } from '../buyer/buyer-order.service';
import { BuyerReturnService } from '../buyer/buyer-return.service';
import { BuyerSupportService } from '../buyer/buyer-support.service';
import { AccountPageComponent } from './account-page.component';

describe('AccountPageComponent', () => {
  let fixture: ComponentFixture<AccountPageComponent>;
  let orderService: jasmine.SpyObj<BuyerOrderService>;
  let returnService: jasmine.SpyObj<BuyerReturnService>;
  let disputeService: jasmine.SpyObj<BuyerDisputeService>;
  let supportService: jasmine.SpyObj<BuyerSupportService>;
  let engagementService: jasmine.SpyObj<BuyerEngagementService>;

  beforeEach(async () => {
    orderService = jasmine.createSpyObj<BuyerOrderService>('BuyerOrderService', ['listOrders']);
    returnService = jasmine.createSpyObj<BuyerReturnService>('BuyerReturnService', ['listReturns']);
    disputeService = jasmine.createSpyObj<BuyerDisputeService>('BuyerDisputeService', ['listDisputes']);
    supportService = jasmine.createSpyObj<BuyerSupportService>('BuyerSupportService', ['listTickets']);
    engagementService = jasmine.createSpyObj<BuyerEngagementService>('BuyerEngagementService', ['listWishlist', 'listBuyerReviews', 'listNotifications']);
    orderService.listOrders.and.resolveTo([createOrder()]);
    returnService.listReturns.and.resolveTo([createReturn()]);
    disputeService.listDisputes.and.resolveTo([createDispute()]);
    supportService.listTickets.and.resolveTo([createTicket()]);
    engagementService.listWishlist.and.resolveTo([{ wishlistItemId: 'wishlist-id', createdAtUtc: '2026-05-18T12:00:00Z', product: createProduct(), availableVariants: [] }]);
    engagementService.listBuyerReviews.and.resolveTo([createReview()]);
    engagementService.listNotifications.and.resolveTo([createNotification()]);

    await TestBed.configureTestingModule({
      imports: [AccountPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: BuyerOrderService, useValue: orderService },
        { provide: BuyerReturnService, useValue: returnService },
        { provide: BuyerDisputeService, useValue: disputeService },
        { provide: BuyerSupportService, useValue: supportService },
        { provide: BuyerEngagementService, useValue: engagementService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AccountPageComponent);
  });

  it('loads account operation summaries', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Account dashboard');
    expect(text).toContain('1 order');
    expect(text).toContain('1 active return');
    expect(text).toContain('1 open dispute');
    expect(text).toContain('1 open support ticket');
    expect(text).toContain('1 saved product');
    expect(text).toContain('1 product review');
    expect(text).toContain('1 unread notification');
    expect(text).toContain('Summer Dress');
  });
});

function createOrder() {
  return {
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    cartId: 'cart-id',
    status: 'Delivered',
    items: [{
      orderItemId: 'order-item-id',
      productId: 'product-id',
      productVariantId: 'variant-id',
      productTitle: 'Summer Dress',
      sku: 'SKU-1',
      size: 'M',
      colour: 'Black',
      unitPrice: 500,
      quantity: 1,
      lineTotal: 500
    }],
    itemsSubtotal: 500,
    shippingAmount: 0,
    platformFeeAmount: 0,
    discountAmount: 0,
    totalAmount: 500,
    statusHistory: [{ statusHistoryId: 'history-id', previousStatus: null, newStatus: 'Delivered', changedAtUtc: '2026-05-18T12:00:00Z', reason: null }],
    shipments: []
  };
}

function createReturn() {
  return {
    returnRequestId: 'return-id',
    orderId: 'order-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    status: 'Requested',
    reason: 'Damaged',
    details: null,
    requestedAtUtc: '2026-05-18T12:00:00Z',
    sellerRespondedAtUtc: null,
    sellerResponseReason: null,
    disputedAtUtc: null,
    disputeReason: null,
    items: [],
    messages: []
  };
}

function createDispute() {
  return {
    disputeId: 'dispute-id',
    orderId: 'order-id',
    returnRequestId: 'return-id',
    buyerId: 'buyer-id',
    sellerId: 'seller-id',
    status: 'Open',
    reason: 'Need review',
    openedAtUtc: '2026-05-18T12:00:00Z',
    resolvedAtUtc: null,
    resolutionReason: null,
    messages: [],
    evidence: []
  };
}

function createTicket() {
  return {
    supportTicketId: 'ticket-id',
    createdByUserId: 'buyer-user-id',
    createdByRole: 'Buyer',
    buyerId: 'buyer-id',
    sellerId: null,
    category: 'OrderIssue',
    status: 'Open',
    subject: 'Need help',
    description: 'Order help',
    linkedOrderId: 'order-id',
    linkedProductId: null,
    linkedSellerId: null,
    linkedPaymentId: null,
    assignedSupportUserId: null,
    openedAtUtc: '2026-05-18T12:00:00Z',
    resolvedAtUtc: null,
    closedAtUtc: null,
    messages: []
  };
}

function createProduct() {
  return {
    productId: 'product-id',
    sellerId: 'seller-id',
    sellerStoreName: 'Seller Store',
    sellerStoreSlug: 'seller-store',
    categoryId: null,
    categoryPath: 'Women > Dresses',
    brandId: null,
    title: 'Summer Dress',
    slug: 'summer-dress',
    shortDescription: null,
    primaryImageUrl: null,
    primaryImageAltText: null,
    priceMin: 500,
    compareAtPriceMin: null,
    inStock: true,
    tags: [],
    publishedAtUtc: '2026-05-18T12:00:00Z'
  };
}

function createReview() {
  return {
    reviewId: 'review-id',
    productId: 'product-id',
    orderId: 'order-id',
    orderItemId: 'order-item-id',
    rating: 5,
    title: 'Great',
    body: 'Loved it',
    status: 'Published',
    moderationReason: null,
    moderatedAtUtc: null,
    createdAtUtc: '2026-05-18T12:00:00Z',
    updatedAtUtc: '2026-05-18T12:00:00Z',
    product: null
  };
}

function createNotification() {
  return {
    notificationId: 'notification-id',
    recipientUserId: 'buyer-user-id',
    type: 'OrderUpdate',
    title: 'Order shipped',
    message: 'Your order has shipped.',
    relatedEntityType: 'Order',
    relatedEntityId: 'order-id',
    readAtUtc: null,
    createdAtUtc: '2026-05-18T12:00:00Z'
  };
}
