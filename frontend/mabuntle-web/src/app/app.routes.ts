import { Routes } from '@angular/router';
import { requireRoleGuard } from './auth/auth.guard';
import { AccountPageComponent } from './pages/account-page.component';
import { HomePageComponent } from './pages/home-page.component';
import { ADMIN_ROLES, FINANCE_READ_ROLES, SUPPORT_ROLES } from './route-roles';

export { ADMIN_ROLES, FINANCE_READ_ROLES, SUPPORT_ROLES } from './route-roles';

export const routes: Routes = [
  { path: '', component: HomePageComponent, title: 'Mabuntle' },
  {
    path: 'shop',
    loadComponent: () => import('./pages/shop-page.component').then(component => component.ShopPageComponent),
    title: 'Shop | Mabuntle'
  },
  {
    path: 'sell',
    loadComponent: () => import('./pages/sell-on-mabuntle-page.component').then(component => component.SellOnMabuntlePageComponent),
    title: 'Sell on Mabuntle | Mabuntle'
  },
  {
    path: 'seller/products',
    loadComponent: () => import('./pages/seller-products-page.component').then(component => component.SellerProductsPageComponent),
    title: 'Seller products | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/inventory',
    loadComponent: () => import('./pages/seller-inventory-page.component').then(component => component.SellerInventoryPageComponent),
    title: 'Seller inventory | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/settings/store',
    loadComponent: () => import('./pages/seller-store-settings-page.component').then(component => component.SellerStoreSettingsPageComponent),
    title: 'Store settings | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/products/new',
    loadComponent: () => import('./pages/seller-product-form-page.component').then(component => component.SellerProductFormPageComponent),
    title: 'New product | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/products/:id/edit',
    loadComponent: () => import('./pages/seller-product-form-page.component').then(component => component.SellerProductFormPageComponent),
    title: 'Edit product | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/orders',
    loadComponent: () => import('./pages/seller-orders-page.component').then(component => component.SellerOrdersPageComponent),
    title: 'Seller orders | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/orders/:orderId',
    loadComponent: () => import('./pages/seller-order-detail-page.component').then(component => component.SellerOrderDetailPageComponent),
    title: 'Seller order | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/returns',
    loadComponent: () => import('./pages/seller-returns-page.component').then(component => component.SellerReturnsPageComponent),
    title: 'Seller returns | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/returns/:returnRequestId',
    loadComponent: () => import('./pages/seller-return-detail-page.component').then(component => component.SellerReturnDetailPageComponent),
    title: 'Seller return | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/payouts',
    loadComponent: () => import('./pages/seller-payouts-page.component').then(component => component.SellerPayoutsPageComponent),
    title: 'Seller payouts | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/support',
    loadComponent: () => import('./pages/seller-support-page.component').then(component => component.SellerSupportPageComponent),
    title: 'Seller support | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/support/:ticketId',
    loadComponent: () => import('./pages/seller-support-detail-page.component').then(component => component.SellerSupportDetailPageComponent),
    title: 'Seller support ticket | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/notifications',
    loadComponent: () => import('./pages/seller-notifications-page.component').then(component => component.SellerNotificationsPageComponent),
    title: 'Seller notifications | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/ads',
    loadComponent: () => import('./pages/seller-ad-campaigns-page.component').then(component => component.SellerAdCampaignsPageComponent),
    title: 'Seller ads | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/ads/new',
    loadComponent: () => import('./pages/seller-ad-campaign-form-page.component').then(component => component.SellerAdCampaignFormPageComponent),
    title: 'New ad campaign | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/ads/:id',
    loadComponent: () => import('./pages/seller-ad-campaign-detail-page.component').then(component => component.SellerAdCampaignDetailPageComponent),
    title: 'Ad campaign | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller/analytics',
    loadComponent: () => import('./pages/seller-analytics-page.component').then(component => component.SellerAnalyticsPageComponent),
    title: 'Seller analytics | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'seller',
    loadComponent: () => import('./pages/seller-page.component').then(component => component.SellerPageComponent),
    title: 'Seller | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'admin',
    loadComponent: () => import('./pages/admin-page.component').then(component => component.AdminPageComponent),
    title: 'Admin | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/sellers',
    loadComponent: () => import('./pages/admin-sellers-page.component').then(component => component.AdminSellersPageComponent),
    title: 'Seller approvals | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/sellers/:sellerId',
    loadComponent: () => import('./pages/admin-seller-detail-page.component').then(component => component.AdminSellerDetailPageComponent),
    title: 'Seller review | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/products',
    loadComponent: () => import('./pages/admin-products-page.component').then(component => component.AdminProductsPageComponent),
    title: 'Product review queue | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/products/revisions/:revisionId',
    loadComponent: () => import('./pages/admin-product-revision-detail-page.component').then(component => component.AdminProductRevisionDetailPageComponent),
    title: 'Product revision review | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/products/variant-revisions/:revisionId',
    loadComponent: () => import('./pages/admin-product-variant-revision-detail-page.component').then(component => component.AdminProductVariantRevisionDetailPageComponent),
    title: 'Product variant revision review | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/products/:productId',
    loadComponent: () => import('./pages/admin-product-detail-page.component').then(component => component.AdminProductDetailPageComponent),
    title: 'Product review | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/reviews',
    loadComponent: () => import('./pages/admin-reviews-page.component').then(component => component.AdminReviewsPageComponent),
    title: 'Buyer review moderation | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/audit-logs',
    loadComponent: () => import('./pages/admin-audit-logs-page.component').then(component => component.AdminAuditLogsPageComponent),
    title: 'Audit logs | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/reports',
    loadComponent: () => import('./pages/admin-marketplace-reports-page.component').then(component => component.AdminMarketplaceReportsPageComponent),
    title: 'Marketplace reports | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/ai-usage',
    loadComponent: () => import('./pages/admin-ai-usage-analytics-page.component').then(component => component.AdminAiUsageAnalyticsPageComponent),
    title: 'AI usage analytics | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/orders',
    loadComponent: () => import('./pages/admin-orders-page.component').then(component => component.AdminOrdersPageComponent),
    title: 'Admin orders | Mabuntle',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'admin/orders/:orderId',
    loadComponent: () => import('./pages/admin-order-detail-page.component').then(component => component.AdminOrderDetailPageComponent),
    title: 'Admin order | Mabuntle',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'admin/payments',
    loadComponent: () => import('./pages/admin-payments-page.component').then(component => component.AdminPaymentsPageComponent),
    title: 'Admin payments | Mabuntle',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'admin/payments/:paymentId',
    loadComponent: () => import('./pages/admin-payment-detail-page.component').then(component => component.AdminPaymentDetailPageComponent),
    title: 'Admin payment | Mabuntle',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'admin/refunds',
    loadComponent: () => import('./pages/admin-refunds-page.component').then(component => component.AdminRefundsPageComponent),
    title: 'Admin refunds | Mabuntle',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'admin/disputes',
    loadComponent: () => import('./pages/admin-disputes-page.component').then(component => component.AdminDisputesPageComponent),
    title: 'Admin disputes | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/payouts',
    loadComponent: () => import('./pages/admin-payouts-page.component').then(component => component.AdminPayoutsPageComponent),
    title: 'Admin payouts | Mabuntle',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'admin/payout-profile-changes',
    loadComponent: () => import('./pages/admin-payout-profile-changes-page.component').then(component => component.AdminPayoutProfileChangesPageComponent),
    title: 'Payout profile changes | Mabuntle',
    canActivate: [requireRoleGuard(FINANCE_READ_ROLES)]
  },
  {
    path: 'admin/support',
    loadComponent: () => import('./pages/admin-support-page.component').then(component => component.AdminSupportPageComponent),
    title: 'Support tickets | Mabuntle',
    canActivate: [requireRoleGuard(SUPPORT_ROLES)]
  },
  {
    path: 'admin/support/:ticketId',
    loadComponent: () => import('./pages/admin-support-detail-page.component').then(component => component.AdminSupportDetailPageComponent),
    title: 'Support ticket | Mabuntle',
    canActivate: [requireRoleGuard(SUPPORT_ROLES)]
  },
  {
    path: 'admin/categories',
    loadComponent: () => import('./pages/admin-categories-page.component').then(component => component.AdminCategoriesPageComponent),
    title: 'Catalog categories | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/pickup-points',
    loadComponent: () => import('./pages/admin-pickup-points-page.component').then(component => component.AdminPickupPointsPageComponent),
    title: 'Pickup points | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'admin/ads',
    loadComponent: () => import('./pages/admin-ad-campaigns-page.component').then(component => component.AdminAdCampaignsPageComponent),
    title: 'Ad campaign review queue | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)],
  },
  {
    path: 'admin/ads/:id',
    loadComponent: () => import('./pages/admin-ad-campaign-detail-page.component').then(component => component.AdminAdCampaignDetailPageComponent),
    title: 'Ad campaign review | Mabuntle',
    canActivate: [requireRoleGuard(ADMIN_ROLES)]
  },
  {
    path: 'account',
    component: AccountPageComponent,
    title: 'Account | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/orders',
    loadComponent: () => import('./pages/buyer-orders-page.component').then(component => component.BuyerOrdersPageComponent),
    title: 'Account orders | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/orders/:orderId',
    loadComponent: () => import('./pages/buyer-order-detail-page.component').then(component => component.BuyerOrderDetailPageComponent),
    title: 'Account order | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/returns',
    loadComponent: () => import('./pages/buyer-returns-page.component').then(component => component.BuyerReturnsPageComponent),
    title: 'Account returns | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/returns/:returnRequestId',
    loadComponent: () => import('./pages/buyer-return-detail-page.component').then(component => component.BuyerReturnDetailPageComponent),
    title: 'Account return | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/refunds',
    loadComponent: () => import('./pages/buyer-refunds-page.component').then(component => component.BuyerRefundsPageComponent),
    title: 'Account refunds | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/wishlist',
    loadComponent: () => import('./pages/buyer-wishlist-page.component').then(component => component.BuyerWishlistPageComponent),
    title: 'Wishlist | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/reviews',
    loadComponent: () => import('./pages/buyer-reviews-page.component').then(component => component.BuyerReviewsPageComponent),
    title: 'Product reviews | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/notifications',
    loadComponent: () => import('./pages/buyer-notifications-page.component').then(component => component.BuyerNotificationsPageComponent),
    title: 'Notifications | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/ai-history',
    loadComponent: () => import('./pages/buyer-ai-history-page.component').then(component => component.BuyerAiHistoryPageComponent),
    title: 'AI history | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/settings',
    loadComponent: () => import('./pages/buyer-settings-page.component').then(component => component.BuyerSettingsPageComponent),
    title: 'Account settings | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/disputes',
    loadComponent: () => import('./pages/buyer-disputes-page.component').then(component => component.BuyerDisputesPageComponent),
    title: 'Account disputes | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/support',
    loadComponent: () => import('./pages/buyer-support-page.component').then(component => component.BuyerSupportPageComponent),
    title: 'Account support | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'account/support/:ticketId',
    loadComponent: () => import('./pages/buyer-support-detail-page.component').then(component => component.BuyerSupportDetailPageComponent),
    title: 'Account support ticket | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'cart',
    loadComponent: () => import('./pages/cart-page.component').then(component => component.CartPageComponent),
    title: 'Cart | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'assistant',
    loadComponent: () => import('./pages/buyer-ai-assistant-page.component').then(component => component.BuyerAiAssistantPageComponent),
    title: 'Shopping assistant | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'visual-search',
    loadComponent: () => import('./pages/buyer-visual-search-page.component').then(component => component.BuyerVisualSearchPageComponent),
    title: 'Visual search | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'checkout',
    loadComponent: () => import('./pages/checkout-page.component').then(component => component.CheckoutPageComponent),
    title: 'Checkout | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'checkout/success',
    loadComponent: () => import('./pages/checkout-success-page.component').then(component => component.CheckoutSuccessPageComponent),
    title: 'Checkout started | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'checkout/failed',
    loadComponent: () => import('./pages/checkout-failed-page.component').then(component => component.CheckoutFailedPageComponent),
    title: 'Checkout issue | Mabuntle',
    canActivate: [requireRoleGuard(['Buyer'])]
  },
  {
    path: 'login',
    loadComponent: () => import('./auth/login-page.component').then(component => component.LoginPageComponent),
    title: 'Sign in | Mabuntle'
  },
  {
    path: 'register/buyer',
    loadComponent: () => import('./auth/register-page.component').then(component => component.RegisterPageComponent),
    title: 'Create buyer account | Mabuntle',
    data: { role: 'Buyer' }
  },
  {
    path: 'register/seller',
    loadComponent: () => import('./auth/register-page.component').then(component => component.RegisterPageComponent),
    title: 'Create seller account | Mabuntle',
    data: { role: 'Seller' }
  },
  {
    path: 'access-denied',
    loadComponent: () => import('./auth/access-denied-page.component').then(component => component.AccessDeniedPageComponent),
    title: 'Access denied | Mabuntle'
  },
  {
    path: 'category/:slug',
    loadComponent: () => import('./pages/category-page.component').then(component => component.CategoryPageComponent),
    title: 'Category | Mabuntle'
  },
  {
    path: 'product/:slug',
    loadComponent: () => import('./pages/product-detail-page.component').then(component => component.ProductDetailPageComponent),
    title: 'Product | Mabuntle'
  },
  {
    path: 'seller/:storeSlug',
    loadComponent: () => import('./pages/seller-storefront-page.component').then(component => component.SellerStorefrontPageComponent),
    title: 'Seller storefront | Mabuntle'
  },
  { path: '**', redirectTo: '' }
];
