import { Routes } from '@angular/router';
import { requireRoleGuard } from './auth/auth.guard';
import { AccountPageComponent } from './pages/account-page.component';
import { HomePageComponent } from './pages/home-page.component';

export const clientRoutes: Routes = [
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
    redirectTo: 'sell',
    pathMatch: 'full'
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

