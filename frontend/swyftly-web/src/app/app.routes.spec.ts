import { ADMIN_ROLES, FINANCE_READ_ROLES, SUPPORT_ROLES, routes } from './app.routes';

describe('app routes', () => {
  it('uses finance read roles for admin finance routes', () => {
    expect(FINANCE_READ_ROLES).toEqual(['Admin', 'SuperAdmin', 'FinanceOperator', 'FinanceApprover']);

    const financePaths = [
      'admin/orders',
      'admin/orders/:orderId',
      'admin/payments',
      'admin/payments/:paymentId',
      'admin/refunds',
      'admin/payouts'
    ];
    for (const path of financePaths) {
      const route = routes.find(item => item.path === path);
      expect(route?.canActivate?.length).toBeGreaterThan(0);
    }
  });

  it('keeps disputes admin-only', () => {
    expect(ADMIN_ROLES).toEqual(['Admin', 'SuperAdmin']);

    const disputeRoute = routes.find(item => item.path === 'admin/disputes');
    expect(disputeRoute?.canActivate?.length).toBeGreaterThan(0);
  });

  it('uses support roles for admin support routes', () => {
    expect(SUPPORT_ROLES).toEqual(['Admin', 'SuperAdmin', 'SupportAgent']);

    for (const path of ['admin/support', 'admin/support/:ticketId']) {
      const route = routes.find(item => item.path === path);
      expect(route?.canActivate?.length).toBeGreaterThan(0);
    }
  });

  it('keeps category management admin-only', () => {
    const route = routes.find(item => item.path === 'admin/categories');
    expect(route?.canActivate?.length).toBeGreaterThan(0);
  });

  it('keeps buyer review moderation admin-only', () => {
    const route = routes.find(item => item.path === 'admin/reviews');
    expect(route?.canActivate?.length).toBeGreaterThan(0);
  });

  it('protects buyer account operation routes', () => {
    const buyerPaths = [
      'account',
      'account/orders',
      'account/orders/:orderId',
      'account/returns',
      'account/returns/:returnRequestId',
      'account/wishlist',
      'account/reviews',
      'account/notifications',
      'account/settings',
      'account/disputes',
      'account/support',
      'account/support/:ticketId'
    ];

    for (const path of buyerPaths) {
      const route = routes.find(item => item.path === path);
      expect(route?.canActivate?.length).toBeGreaterThan(0);
    }
  });
});
