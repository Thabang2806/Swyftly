import { Routes } from '@angular/router';
import { requireRoleGuard } from './auth/auth.guard';

const sellerWorkspaceRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/seller-page.component').then(component => component.SellerPageComponent),
    title: 'Seller | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'products',
    loadComponent: () => import('./pages/seller-products-page.component').then(component => component.SellerProductsPageComponent),
    title: 'Seller products | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'products/new',
    loadComponent: () => import('./pages/seller-product-form-page.component').then(component => component.SellerProductFormPageComponent),
    title: 'New product | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'products/:id/edit',
    loadComponent: () => import('./pages/seller-product-form-page.component').then(component => component.SellerProductFormPageComponent),
    title: 'Edit product | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'inventory',
    loadComponent: () => import('./pages/seller-inventory-page.component').then(component => component.SellerInventoryPageComponent),
    title: 'Seller inventory | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'settings/store',
    loadComponent: () => import('./pages/seller-store-settings-page.component').then(component => component.SellerStoreSettingsPageComponent),
    title: 'Store settings | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'orders',
    loadComponent: () => import('./pages/seller-orders-page.component').then(component => component.SellerOrdersPageComponent),
    title: 'Seller orders | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'orders/:orderId',
    loadComponent: () => import('./pages/seller-order-detail-page.component').then(component => component.SellerOrderDetailPageComponent),
    title: 'Seller order | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'returns',
    loadComponent: () => import('./pages/seller-returns-page.component').then(component => component.SellerReturnsPageComponent),
    title: 'Seller returns | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'returns/:returnRequestId',
    loadComponent: () => import('./pages/seller-return-detail-page.component').then(component => component.SellerReturnDetailPageComponent),
    title: 'Seller return | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'payouts',
    loadComponent: () => import('./pages/seller-payouts-page.component').then(component => component.SellerPayoutsPageComponent),
    title: 'Seller payouts | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'support',
    loadComponent: () => import('./pages/seller-support-page.component').then(component => component.SellerSupportPageComponent),
    title: 'Seller support | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'support/:ticketId',
    loadComponent: () => import('./pages/seller-support-detail-page.component').then(component => component.SellerSupportDetailPageComponent),
    title: 'Seller support ticket | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'notifications',
    loadComponent: () => import('./pages/seller-notifications-page.component').then(component => component.SellerNotificationsPageComponent),
    title: 'Seller notifications | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'ads',
    loadComponent: () => import('./pages/seller-ad-campaigns-page.component').then(component => component.SellerAdCampaignsPageComponent),
    title: 'Seller ads | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'ads/new',
    loadComponent: () => import('./pages/seller-ad-campaign-form-page.component').then(component => component.SellerAdCampaignFormPageComponent),
    title: 'New ad campaign | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'ads/:id',
    loadComponent: () => import('./pages/seller-ad-campaign-detail-page.component').then(component => component.SellerAdCampaignDetailPageComponent),
    title: 'Ad campaign | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  },
  {
    path: 'analytics',
    loadComponent: () => import('./pages/seller-analytics-page.component').then(component => component.SellerAnalyticsPageComponent),
    title: 'Seller analytics | Mabuntle',
    canActivate: [requireRoleGuard(['Seller'])]
  }
];

const sellerCompatibilityRedirects: Routes = [
  { path: 'seller', redirectTo: '', pathMatch: 'full' },
  { path: 'seller/products', redirectTo: 'products', pathMatch: 'full' },
  { path: 'seller/products/new', redirectTo: 'products/new', pathMatch: 'full' },
  { path: 'seller/products/:id/edit', redirectTo: 'products/:id/edit', pathMatch: 'full' },
  { path: 'seller/inventory', redirectTo: 'inventory', pathMatch: 'full' },
  { path: 'seller/settings/store', redirectTo: 'settings/store', pathMatch: 'full' },
  { path: 'seller/orders', redirectTo: 'orders', pathMatch: 'full' },
  { path: 'seller/orders/:orderId', redirectTo: 'orders/:orderId', pathMatch: 'full' },
  { path: 'seller/returns', redirectTo: 'returns', pathMatch: 'full' },
  { path: 'seller/returns/:returnRequestId', redirectTo: 'returns/:returnRequestId', pathMatch: 'full' },
  { path: 'seller/payouts', redirectTo: 'payouts', pathMatch: 'full' },
  { path: 'seller/support', redirectTo: 'support', pathMatch: 'full' },
  { path: 'seller/support/:ticketId', redirectTo: 'support/:ticketId', pathMatch: 'full' },
  { path: 'seller/notifications', redirectTo: 'notifications', pathMatch: 'full' },
  { path: 'seller/ads', redirectTo: 'ads', pathMatch: 'full' },
  { path: 'seller/ads/new', redirectTo: 'ads/new', pathMatch: 'full' },
  { path: 'seller/ads/:id', redirectTo: 'ads/:id', pathMatch: 'full' },
  { path: 'seller/analytics', redirectTo: 'analytics', pathMatch: 'full' }
];

export const sellerRoutes: Routes = [
  ...sellerCompatibilityRedirects,
  {
    path: 'login',
    loadComponent: () => import('./auth/login-page.component').then(component => component.LoginPageComponent),
    title: 'Seller sign in | Mabuntle'
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
  ...sellerWorkspaceRoutes,
  { path: '**', redirectTo: '' }
];

