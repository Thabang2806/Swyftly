(function () {
  const routes = [
    '', 'shop', 'seller/products', 'seller/inventory', 'seller/settings/store', 'seller/products/new',
    'seller/products/:id/edit', 'seller/orders', 'seller/orders/:orderId', 'seller/returns',
    'seller/returns/:returnRequestId', 'seller/payouts', 'seller/support', 'seller/support/:ticketId',
    'seller/ads', 'seller/ads/new', 'seller/ads/:id', 'seller/analytics', 'seller', 'admin',
    'admin/sellers', 'admin/sellers/:sellerId', 'admin/products', 'admin/products/revisions/:revisionId',
    'admin/products/:productId', 'admin/reviews', 'admin/audit-logs', 'admin/reports', 'admin/ai-usage',
    'admin/orders', 'admin/orders/:orderId', 'admin/payments', 'admin/payments/:paymentId',
    'admin/refunds', 'admin/disputes', 'admin/payouts', 'admin/payout-profile-changes', 'admin/support',
    'admin/support/:ticketId', 'admin/categories', 'admin/pickup-points', 'admin/ads', 'admin/ads/:id',
    'account', 'account/orders', 'account/orders/:orderId', 'account/returns',
    'account/returns/:returnRequestId', 'account/wishlist', 'account/reviews', 'account/notifications',
    'account/settings', 'account/disputes', 'account/support', 'account/support/:ticketId', 'cart',
    'assistant', 'visual-search', 'checkout', 'checkout/success', 'checkout/failed', 'login',
    'register/buyer', 'register/seller', 'access-denied', 'category/:slug', 'product/:slug',
    'seller/:storeSlug'
  ];

  const route = new URLSearchParams(window.location.search).get('route') || '';
  const app = document.getElementById('app');

  const categories = ['New In', 'Designer Bags', 'Jewellery', 'Beauty', 'Occasionwear', 'Accessories'];
  const productNames = [
    ['Rose silk column dress', 'R 3,950'],
    ['Structured ivory blazer', 'R 2,480'],
    ['Mini leather shoulder bag', 'R 4,200'],
    ['Gold vermeil hoop set', 'R 950'],
    ['Cashmere wrap coat', 'R 5,750'],
    ['Satin mule heel', 'R 1,850'],
    ['Hydrating skin edit', 'R 720'],
    ['Black crepe evening dress', 'R 4,650']
  ];

  const screenSpecs = {
    'seller': spec('seller', 'Seller dashboard', 'Operational overview for a verified seller: setup quality, urgent orders, stock risk, returns, payouts, and growth prompts.', ['Products', 'Orders', 'Stock risk', 'Payouts'], ['42', '8', '5', 'R 18k'], ['Today task', 'Status', 'Area', 'Due', 'Action'], [
      row('Ship order SWY-1042', '2 items awaiting courier booking', 'Ready', 'success', 'Orders', 'Today', 'Fulfil'),
      row('Low stock: black crepe dress', '2 available, 1 reserved', 'Low stock', 'warning', 'Inventory', 'Today', 'Adjust'),
      row('Return request RET-88', 'Buyer requested size exchange', 'Review', 'warning', 'Returns', '1 day', 'Respond')
    ], 'Workspace priorities', 'Focus on fulfilment exceptions and stock integrity before growth work.', ['Open orders', 'Review inventory']),
    'seller/products': spec('seller', 'Seller products', 'Listing workspace for drafts, published products, rejected products, and revision-ready published listings.', ['Published', 'Drafts', 'Changes', 'Rejected'], ['28', '6', '3', '1'], ['Product', 'Status', 'Category', 'Updated', 'Action'], [
      row('Rose silk column dress', 'Slug: rose-silk-column-dress', 'Published', 'success', 'Dresses', '2h ago', 'Edit'),
      row('Pearl trim blazer', 'Revision staged', 'Changes', 'warning', 'Outerwear', 'Today', 'Open'),
      row('Gold hoop set', 'Needs clearer product images', 'Rejected', 'danger', 'Jewellery', 'Yesterday', 'Fix')
    ], 'Listing actions', 'Create drafts, revise locked listings through moderation, and manage live stock in Inventory.', ['New product', 'Open revision']),
    'seller/inventory': spec('seller', 'Seller inventory', 'Stock-control screen for published and draft variants, single adjustments, CSV export, and bulk stocktake imports.', ['Variants', 'Low stock', 'Reserved', 'Import rows'], ['86', '9', '14', '500 max'], ['Variant', 'Available', 'Reserved', 'Status', 'Action'], [
      row('DRS-BLK-M', 'Black dress, size M', '2 units', 'warning', '1 reserved', 'Active', 'Adjust'),
      row('BAG-IVY-OS', 'Mini bag, one size', '0 units', 'danger', '0 reserved', 'OutOfStock', 'Adjust'),
      row('JEW-GLD-HOOP', 'Hoop set', '15 units', 'success', '3 reserved', 'Active', 'Adjust')
    ], 'Bulk stocktake', 'Export the CSV template, preview changes, then apply valid stock and status updates in one transaction.', ['Export CSV', 'Preview import']),
    'seller/settings/store': spec('seller', 'Store settings', 'Manage storefront profile, fulfilment address, delivery methods, pickup opt-in, and payout-profile change requests.', ['Profile', 'Delivery', 'Pickup', 'Payout change'], ['Live', '4 methods', 'Opt-in', 'None'], ['Setting', 'State', 'Coverage', 'Updated', 'Action'], [
      row('Storefront profile', 'Maison Rose Boutique', 'Published', 'success', 'Public', 'Today', 'Edit'),
      row('Standard courier', 'Country-wide delivery method', 'Active', 'success', 'ZA', '2d ago', 'Edit'),
      row('Payout reference', 'Verified profile locked', 'Approved', 'success', 'Finance', 'May 20', 'Request change')
    ], 'Store controls', 'Payout-bank changes stay behind finance re-verification; delivery methods are seller-managed.', ['Preview storefront', 'Add method']),
    'seller/products/new': spec('seller', 'New product', 'Draft listing workflow with basic details, category attributes, images, variants, AI suggestions, and review readiness.', ['Required steps', 'Images', 'Variants', 'Readiness'], ['6', '0', '0', 'Draft'], ['Step', 'State', 'Owner', 'Guidance', 'Action'], [
      row('Basic details', 'Title, slug, brand, descriptions', 'Incomplete', 'warning', 'Seller', 'Required', 'Edit'),
      row('Category attributes', 'Choose category before dynamic fields', 'Blocked', 'warning', 'Catalog', 'Required', 'Select'),
      row('Images', 'Upload JPEG, PNG, or WebP', 'Empty', 'danger', 'Media', 'Required', 'Upload')
    ], 'Draft guidance', 'No published buyer surface changes occur until the product passes admin review.', ['Save draft', 'Generate AI suggestion']),
    'seller/products/:id/edit': spec('seller', 'Edit product', 'Operational editor for owned products with read-only states for published listings and revision proposal controls.', ['Status', 'Images', 'Variants', 'Revision'], ['Changes', '5', '8', 'Draft'], ['Section', 'State', 'Current value', 'Risk', 'Action'], [
      row('Listing content', 'Title and description edits staged', 'Changed', 'warning', 'Moderation', 'Medium', 'Review'),
      row('Gallery', 'Primary image selected', 'Ready', 'success', 'Media', 'Low', 'Update'),
      row('Variants', 'Stock managed in Inventory', 'Locked', 'warning', 'Inventory', 'Low', 'Open inventory')
    ], 'Revision mode', 'Published product changes are proposed separately and leave the live listing visible until approval.', ['Submit revision', 'Cancel draft']),
    'seller/orders': spec('seller', 'Seller orders', 'Fulfilment queue with paid orders, delivery snapshots, carrier state, shipment exceptions, and support handoff.', ['Ready', 'Shipped', 'Exceptions', 'Delivered'], ['5', '9', '2', '31'], ['Order', 'Fulfilment', 'Delivery', 'Payment', 'Action'], [
      row('SWY-1042', '2 items, Rosebank delivery', 'ReadyToShip', 'warning', 'Courier', 'Paid', 'Book carrier'),
      row('SWY-1037', 'Tracking added', 'InTransit', 'success', 'Standard', 'Paid', 'Sync'),
      row('SWY-1029', 'Courier could not reach buyer', 'Exception', 'danger', 'Failed', 'Paid', 'Support')
    ], 'Fulfilment control', 'Manual actions remain available; carrier automation does not change finance state.', ['Mark ready', 'Book carrier']),
    'seller/orders/:orderId': spec('seller', 'Seller order detail', 'Single-order fulfilment view with buyer delivery snapshot, selected method, carrier panel, timeline, and exception actions.', ['Items', 'Shipment events', 'Carrier sync', 'Support'], ['2', '6', 'Fake', 'Open'], ['Timeline item', 'State', 'Actor', 'Time', 'Action'], [
      row('Payment confirmed', 'Webhook settled order', 'Complete', 'success', 'System', '09:30', 'View'),
      row('Ready for courier', 'Seller confirmed package', 'Ready', 'warning', 'Seller', '10:10', 'Book'),
      row('Delivery instructions', 'Leave at reception if unavailable', 'Snapshot', 'success', 'Buyer', 'Checkout', 'Copy')
    ], 'Order actions', 'Seller can book carrier, mark shipped, record delivery failure, or mark delivered when valid.', ['Book carrier', 'Add tracking']),
    'seller/returns': spec('seller', 'Seller returns', 'Return queue for seller responses, buyer messages, eligible disputes, and refund handoff visibility.', ['Open', 'Approved', 'Rejected', 'Disputed'], ['3', '8', '1', '1'], ['Return', 'Status', 'Reason', 'Buyer', 'Action'], [
      row('RET-88', 'Dress size did not fit', 'Open', 'warning', 'Size', 'Buyer A', 'Respond'),
      row('RET-72', 'Item approved for return', 'Approved', 'success', 'Quality', 'Buyer B', 'View'),
      row('RET-64', 'Rejected and disputed', 'Disputed', 'danger', 'Policy', 'Buyer C', 'Open')
    ], 'Return policy', 'Seller decisions are recorded but refunds remain in admin finance workflows.', ['Approve', 'Reject']),
    'seller/returns/:returnRequestId': spec('seller', 'Seller return detail', 'Return decision detail with item context, buyer messages, seller response form, and dispute escalation history.', ['Items', 'Messages', 'Decision', 'Dispute'], ['1', '4', 'Pending', 'None'], ['Evidence', 'Status', 'Source', 'Time', 'Action'], [
      row('Returned item', 'Rose silk dress, size M', 'Selected', 'warning', 'Order item', 'Checkout', 'Inspect'),
      row('Buyer message', 'Size was smaller than expected', 'Open', 'warning', 'Buyer', 'Today', 'Reply'),
      row('Policy window', 'Delivered order is return-eligible', 'Valid', 'success', 'System', 'Now', 'Approve')
    ], 'Seller response', 'Record a clear approve or reject reason; notification delivery is non-blocking.', ['Approve return', 'Reject return']),
    'seller/payouts': spec('seller', 'Seller payouts', 'Read-only finance view of seller balance buckets, payout history, adjustments, and provider status.', ['Pending', 'Available', 'Processing', 'Paid out'], ['R 8k', 'R 4k', 'R 0', 'R 21k'], ['Payout', 'Status', 'Gross', 'Adjustments', 'Action'], [
      row('PAY-2026-05', 'May settlement batch', 'Available', 'success', 'R 4,250', 'R 0', 'View'),
      row('PAY-2026-04', 'Provider reference fake-payout-88', 'PaidOut', 'success', 'R 9,800', 'R 120', 'Open'),
      row('PAY-HOLD-12', 'Finance hold reason visible', 'OnHold', 'warning', 'R 1,100', 'R 0', 'View')
    ], 'Payout visibility', 'Sellers can inspect balances; admin finance controls payout movement.', ['View balance', 'Open history']),
    'seller/support': spec('seller', 'Seller support', 'Support-ticket list and create form for product, order, return, payout, and operational issues.', ['Open', 'Waiting', 'Resolved', 'New draft'], ['4', '2', '11', '1'], ['Ticket', 'Category', 'Status', 'Updated', 'Action'], [
      row('SUP-441', 'Carrier label investigation', 'Open', 'warning', 'Orders', '2h ago', 'Open'),
      row('SUP-402', 'Payout reference review', 'Waiting', 'warning', 'Finance', '1d ago', 'Reply'),
      row('SUP-390', 'Product media upload', 'Resolved', 'success', 'Products', 'May 19', 'View')
    ], 'Create ticket', 'Support tickets are marketplace-visible; internal notes remain admin-only.', ['New ticket', 'Attach context']),
    'seller/support/:ticketId': spec('seller', 'Seller support ticket', 'Ticket detail with public message thread, current category/status, and seller reply composer.', ['Messages', 'Internal notes', 'Status', 'Category'], ['5', 'Hidden', 'Open', 'Orders'], ['Message', 'Sender', 'Visibility', 'Time', 'Action'], [
      row('Please confirm collection address', 'Support agent reply', 'Public', 'success', 'Support', 'Today', 'Reply'),
      row('Seller uploaded package details', 'Seller response', 'Public', 'success', 'Seller', 'Today', 'View'),
      row('Carrier reference checked', 'Internal admin note', 'Hidden', 'warning', 'Admin', 'Today', 'None')
    ], 'Reply composer', 'Seller sees only public messages and can add operational context.', ['Send reply', 'Back to support']),
    'seller/ads': spec('seller', 'Seller ads', 'Campaign dashboard with budget, review status, impressions, clicks, attribution, and AI campaign guidance.', ['Draft', 'Pending', 'Live', 'Spend'], ['2', '1', '3', 'R 780'], ['Campaign', 'Status', 'Budget', 'Performance', 'Action'], [
      row('Winter occasionwear boost', 'Dresses and heels', 'Live', 'success', 'R 500/day', '3.4% CTR', 'Open'),
      row('Jewellery launch', 'Awaiting admin approval', 'PendingReview', 'warning', 'R 250/day', 'Not live', 'View'),
      row('Beauty edit', 'Seller draft', 'Draft', 'warning', 'R 150/day', 'Draft', 'Edit')
    ], 'Campaign controls', 'Ads stay seller-managed but admin-approved before serving.', ['New campaign', 'Review spend']),
    'seller/ads/new': spec('seller', 'New ad campaign', 'Campaign creation flow for product selection, audience, budget, schedule, and seller review before submission.', ['Products', 'Budget', 'Creative', 'Readiness'], ['Select', 'R 0', 'Draft', 'Incomplete'], ['Step', 'State', 'Input', 'Validation', 'Action'], [
      row('Product selection', 'Choose eligible published products', 'Required', 'warning', 'Catalog', 'Missing', 'Select'),
      row('Budget', 'Daily and total caps', 'Required', 'warning', 'Finance', 'Missing', 'Set'),
      row('Creative guidance', 'AI-assisted copy optional', 'Optional', 'success', 'AI', 'Ready', 'Generate')
    ], 'Campaign draft', 'No spend starts until campaign is submitted and approved.', ['Save draft', 'Submit review']),
    'seller/ads/:id': spec('seller', 'Ad campaign detail', 'Campaign performance and moderation state with budget progress, product links, and seller actions.', ['Status', 'Spend', 'Clicks', 'Conversions'], ['Live', 'R 420', '186', '12'], ['Signal', 'Value', 'Window', 'Status', 'Action'], [
      row('Budget pacing', 'R 420 of R 1,000', 'Healthy', 'success', '7 days', 'On track', 'Adjust'),
      row('Product attribution', '6 orders influenced', 'Active', 'success', 'Campaign', 'Live', 'View'),
      row('Admin moderation', 'Approved by admin', 'Complete', 'success', 'Review', 'May 21', 'Open')
    ], 'Campaign actions', 'Pause and edit actions depend on current backend campaign status.', ['Pause', 'Duplicate']),
    'seller/analytics': spec('seller', 'Seller analytics', 'Seller performance summary for product views, orders, revenue, campaign lift, and operational quality.', ['Revenue', 'Orders', 'Views', 'Return rate'], ['R 42k', '38', '2.8k', '3.1%'], ['Metric', 'Current', 'Change', 'Driver', 'Action'], [
      row('Top product', 'Black crepe evening dress', '+18%', 'success', 'Product views', 'Campaign lift', 'Open'),
      row('Fulfilment speed', '1.8 days average', 'Stable', 'success', 'Orders', 'Manual', 'View'),
      row('Returns', 'Size issues rising', '+2', 'warning', 'Returns', 'Fit guidance', 'Review')
    ], 'Analytics notes', 'Use as directional seller reporting; deeper marketplace-wide analytics remain admin reports.', ['Export view', 'Open ads']),

    'admin': spec('admin', 'Admin dashboard', 'Marketplace command centre for moderation, finance, support, catalog, ads, reports, and audit queues.', ['Seller reviews', 'Product reviews', 'Finance alerts', 'Support open'], ['7', '14', '5', '9'], ['Queue', 'Status', 'Owner', 'Age', 'Action'], [
      row('Seller onboarding queue', 'Profiles pending review', 'Pending', 'warning', 'Admin', 'Today', 'Review'),
      row('Payment reconciliation', 'Provider evidence due', 'Attention', 'warning', 'Finance', '2h', 'Open'),
      row('Support escalation', 'Buyer dispute evidence', 'Open', 'danger', 'Support', '1h', 'Triage')
    ], 'Admin focus', 'Prioritize risk, money movement, and buyer-impacting queues before catalog maintenance.', ['Open queues', 'View reports']),
    'admin/sellers': spec('admin', 'Seller approvals', 'Pending seller verification queue with profile, storefront, address, payout placeholder, and audit evidence.', ['Pending', 'Under review', 'Rejected', 'Suspended'], ['7', '3', '2', '1'], ['Seller', 'Status', 'Storefront', 'Completeness', 'Action'], [
      row('Maison Rose Boutique', 'seller@example.test', 'UnderReview', 'warning', 'Published', '4 of 4', 'Review'),
      row('Luxe Archive ZA', 'archive@example.test', 'Pending', 'warning', 'Draft', '3 of 4', 'Open'),
      row('Glow Beauty Studio', 'glow@example.test', 'Rejected', 'danger', 'Hidden', '2 of 4', 'Recheck')
    ], 'Review evidence', 'Approve only complete profiles; rejection and suspension require clear reasons and write audit logs.', ['Approve', 'Reject']),
    'admin/sellers/:sellerId': spec('admin', 'Seller review detail', 'Detailed seller review with profile, storefront, address, payout placeholder, completeness indicators, and decision history.', ['Profile', 'Storefront', 'Address', 'Payout'], ['Complete', 'Published', 'Verified', 'Present'], ['Evidence', 'State', 'Source', 'Risk', 'Action'], [
      row('Business identity', 'Contact and seller profile supplied', 'Complete', 'success', 'Seller', 'Low', 'Inspect'),
      row('Storefront', 'Published slug maison-rose', 'Published', 'success', 'Seller', 'Low', 'Preview'),
      row('Payout reference', 'Provider reference placeholder present', 'Review', 'warning', 'Finance', 'Medium', 'Verify')
    ], 'Decision controls', 'Approve, reject, or suspend based on seller evidence. All actions require audit logging.', ['Approve seller', 'Reject with reason']),
    'admin/products': spec('admin', 'Product moderation queue', 'Pending product review queue with listing quality, image evidence, seller status, AI risk flags, and category context.', ['Pending', 'High risk', 'Changes', 'Revisions'], ['14', '3', '5', '4'], ['Product', 'Seller', 'Category', 'Risk', 'Action'], [
      row('Black crepe evening dress', 'Maison Rose Boutique', 'PendingReview', 'warning', 'Dresses', '2 flags', 'Review'),
      row('Gold vermeil hoop set', 'Luxe Archive ZA', 'PendingReview', 'warning', 'Jewellery', '0 flags', 'Review'),
      row('Hydrating skin edit', 'Glow Beauty Studio', 'ChangesRequested', 'danger', 'Beauty', 'Claims', 'Open')
    ], 'Moderation panel', 'Review images, attributes, variants, seller status, and AI risk before approving or requesting changes.', ['Approve', 'Request changes']),
    'admin/products/revisions/:revisionId': spec('admin', 'Product revision review', 'Current-vs-proposed published listing changes with staged images, tags, attributes, and seller revision reason.', ['Current', 'Proposed', 'Images', 'Embedding'], ['Live', 'Pending', '6', 'Refresh'], ['Revision section', 'Current', 'Proposed', 'Risk', 'Action'], [
      row('Title and slug', 'Black crepe dress', 'Evening crepe column dress', 'Medium', 'Listing', 'SEO', 'Compare'),
      row('Image set', '5 live images', '6 proposed images', 'Low', 'Media', 'Primary changed', 'Inspect'),
      row('Attributes', 'Black, M, crepe', 'Black, M/L, crepe', 'Medium', 'Catalog', 'Variant unaffected', 'Review')
    ], 'Revision decision', 'Approval applies staged listing/images and refreshes search/embeddings; rejection leaves live product unchanged.', ['Approve revision', 'Reject']),
    'admin/products/:productId': spec('admin', 'Product review detail', 'Product moderation detail with gallery review, listing data, attributes, variants, seller context, AI risks, and audit trail.', ['Images', 'Variants', 'AI flags', 'Audit'], ['5', '8', '2', '6'], ['Evidence', 'State', 'Owner', 'Risk', 'Action'], [
      row('Primary image', 'Model-free product visual', 'Present', 'success', 'Seller', 'Low', 'Inspect'),
      row('Listing description', 'Material and care supplied', 'Complete', 'success', 'Seller', 'Low', 'Read'),
      row('AI risk flags', 'Potential brand wording', 'Review', 'warning', 'AI', 'Medium', 'Resolve')
    ], 'Moderation actions', 'Approve, reject, or request changes without altering seller payload shape.', ['Approve product', 'Request changes']),
    'admin/reviews': spec('admin', 'Buyer review moderation', 'Pending verified-purchase review queue with buyer, product, seller, order evidence, and approve/reject/remove actions.', ['Pending', 'Published', 'Rejected', 'Removed'], ['9', '182', '6', '3'], ['Review', 'Rating', 'Product', 'Status', 'Action'], [
      row('Beautiful fabric and fast delivery', 'Verified order SWY-1042', '5 stars', 'success', 'Dress', 'Pending', 'Moderate'),
      row('Sizing was smaller than expected', 'Return was opened', '3 stars', 'warning', 'Blazer', 'Pending', 'Review'),
      row('Unsupported claim in body', 'Needs content decision', '2 stars', 'danger', 'Beauty', 'Flagged', 'Open')
    ], 'Review decision', 'Public product pages show only published reviews; buyer edits reset to pending.', ['Approve', 'Reject']),
    'admin/audit-logs': spec('admin', 'Audit logs', 'Immutable operational history for moderation, finance, catalog, support, payment, and seller actions.', ['Events', 'Finance', 'Moderation', 'Failures'], ['1.2k', '88', '340', '4'], ['Audit event', 'Actor', 'Area', 'When', 'Action'], [
      row('Product approved', 'admin@mabuntle.local', 'Complete', 'success', 'Moderation', '10m ago', 'Open'),
      row('Refund approved', 'finance.approver@mabuntle.local', 'Complete', 'success', 'Finance', '1h ago', 'Open'),
      row('Payout processing blocked', 'System policy', 'Warning', 'warning', 'Payouts', '2h ago', 'Inspect')
    ], 'Audit filters', 'Filter by actor, action, entity, and date. Exports remain future work unless implemented separately.', ['Search', 'Clear filters']),
    'admin/reports': spec('admin', 'Marketplace reports', 'Marketplace reporting surface for GMV, orders, seller performance, refunds, payouts, ads, and operational health.', ['GMV', 'Orders', 'Refund rate', 'Active sellers'], ['R 482k', '214', '2.8%', '38'], ['Report section', 'Current', 'Change', 'Signal', 'Action'], [
      row('Marketplace GMV', 'R 482k in selected range', '+12%', 'success', 'Finance', 'Healthy', 'Export'),
      row('Refund ratio', '2.8% of paid orders', '+0.4%', 'warning', 'Returns', 'Watch', 'Review'),
      row('Seller fulfilment', '94% shipped under SLA', '-1%', 'warning', 'Orders', 'Monitor', 'Open')
    ], 'Report tools', 'Reports are read-focused and exportable through the existing CSV endpoint.', ['Export CSV', 'Change range']),
    'admin/ai-usage': spec('admin', 'AI usage analytics', 'AI cost and usage review for listing assistant, buyer assistant, visual search, prompt versions, failures, and risk flags.', ['Requests', 'Cost est.', 'Failures', 'Risk flags'], ['1.8k', 'R 620', '22', '48'], ['AI feature', 'Requests', 'Cost', 'Quality', 'Action'], [
      row('Listing assistant', 'Seller product suggestions', '860', 'success', 'R 320', '91%', 'Inspect'),
      row('Buyer style finder', 'Assistant searches', '740', 'success', 'R 210', '88%', 'Open'),
      row('Visual search', 'Image attribute extraction', '205', 'warning', 'R 90', '72%', 'Review')
    ], 'AI governance', 'Track usage and risk without exposing provider secrets or raw unsafe prompts.', ['Filter feature', 'View logs']),
    'admin/orders': spec('admin', 'Admin orders', 'Read-only order investigation screen with buyer, seller, totals, address snapshot, payment, shipment, and return context.', ['Paid', 'Pending', 'Cancelled', 'Delivered'], ['112', '8', '3', '76'], ['Order', 'Buyer/Seller', 'Payment', 'Fulfilment', 'Action'], [
      row('SWY-1042', 'Buyer A / Maison Rose', 'Paid', 'success', 'R 4,025', 'ReadyToShip', 'Open'),
      row('SWY-1037', 'Buyer B / Luxe Archive', 'Paid', 'success', 'R 1,850', 'InTransit', 'Open'),
      row('SWY-1029', 'Buyer C / Glow Studio', 'Cancelled', 'danger', 'R 720', 'No shipment', 'Inspect')
    ], 'Read-only scope', 'Admin order screens investigate state and link to finance/support; they do not manually mutate orders.', ['Open detail', 'View payments']),
    'admin/orders/:orderId': spec('admin', 'Admin order detail', 'Order investigation detail with parties, totals, delivery snapshot, shipment timeline, payment events, returns, and support links.', ['Items', 'Payments', 'Shipments', 'Events'], ['2', '1', '1', '9'], ['Timeline', 'Status', 'Actor', 'Time', 'Action'], [
      row('Order created from cart', 'Address and method snapshotted', 'Complete', 'success', 'Buyer', 'Checkout', 'View'),
      row('Payment webhook settled', 'Provider event accepted', 'Paid', 'success', 'System', '09:30', 'Open payment'),
      row('Carrier booking pending', 'Seller has not booked courier', 'Pending', 'warning', 'Seller', 'Now', 'Support')
    ], 'Investigation panel', 'Use related finance, support, and audit links. Keep order state changes out of admin UI.', ['View payment', 'Open support']),
    'admin/payments': spec('admin', 'Admin payments', 'Payment operations screen with provider references, webhook event health, stale candidates, and reconciliation review evidence.', ['Paid', 'Pending', 'Failed events', 'Snoozed'], ['104', '8', '3', '2'], ['Payment', 'Provider', 'Status', 'Review', 'Action'], [
      row('PAY-10042', 'PayFast m_payment_id local id', 'Pending', 'warning', 'No review', '30m old', 'Review'),
      row('PAY-10037', 'Fake provider checkout', 'Paid', 'success', 'Matched', 'Today', 'Open'),
      row('PAY-10029', 'Invalid signature event', 'Failed', 'danger', 'Manual recovery', '2h old', 'Inspect')
    ], 'Reconciliation warning', 'Provider-paid evidence is not authority to settle; valid signed ITN remains the payment source of truth.', ['Record review', 'Open payment']),
    'admin/payments/:paymentId': spec('admin', 'Admin payment detail', 'Payment detail with order link, provider reference, webhook event list, ledger status, reconciliation reviews, and no manual settlement button.', ['Events', 'Ledger', 'Reviews', 'Provider'], ['4', 'Settled', '1', 'PayFast'], ['Payment event', 'Result', 'Source', 'When', 'Action'], [
      row('ITN COMPLETE', 'Signature valid and remote validation passed', 'Processed', 'success', 'PayFast', '09:31', 'View'),
      row('Duplicate ITN', 'Unique event id already processed', 'Idempotent', 'success', 'PayFast', '09:32', 'Open'),
      row('Finance review', 'Dashboard matched no action', 'Recorded', 'success', 'Finance', '10:00', 'View')
    ], 'Payment policy', 'Do not expose manual paid-state mutation. Use reconciliation reviews for evidence only.', ['View order', 'Record review']),
    'admin/refunds': spec('admin', 'Admin refunds', 'Refund operations queue with order/return refund creation, approval, manual provider confirmation, and payout adjustment visibility.', ['Requested', 'Processing', 'Refunded', 'Recovery'], ['6', '2', '18', '1'], ['Refund', 'Source', 'Status', 'Amount', 'Action'], [
      row('REF-88', 'Return RET-88', 'Requested', 'warning', 'R 1,950', 'Buyer return', 'Approve'),
      row('REF-72', 'Order SWY-1037', 'Processing', 'warning', 'R 950', 'PayFast manual', 'Confirm'),
      row('REF-64', 'Order SWY-1011', 'Refunded', 'success', 'R 4,200', 'Fake provider', 'View')
    ], 'Finance controls', 'Operate and approve roles are separated; PayFast refunds require dashboard completion before confirmation.', ['Create refund', 'Approve']),
    'admin/disputes': spec('admin', 'Admin disputes', 'Dispute queue with buyer/seller evidence, messages, return context, and buyer-favoured or seller-favoured resolution actions.', ['Open', 'Evidence due', 'Resolved', 'High risk'], ['5', '2', '14', '1'], ['Dispute', 'Status', 'Evidence', 'Age', 'Action'], [
      row('DSP-14', 'Return rejection challenged', 'Open', 'warning', 'Buyer and seller', '2d', 'Resolve'),
      row('DSP-11', 'Delivery failure evidence', 'Evidence due', 'warning', 'Courier timeline', '1d', 'Request'),
      row('DSP-09', 'Item condition dispute', 'High risk', 'danger', 'Photos attached', '4h', 'Review')
    ], 'Resolution panel', 'Resolution records an outcome and reason; broader money movement remains policy-led.', ['Buyer favoured', 'Seller favoured']),
    'admin/payouts': spec('admin', 'Admin payouts', 'Finance payout queue for pending, on-hold, available, processing, failed, and paid-out seller payouts.', ['Pending', 'Available', 'On hold', 'Blocked'], ['9', '4', '2', '1'], ['Payout', 'Seller', 'Status', 'Net amount', 'Action'], [
      row('PO-2026-05-01', 'Maison Rose Boutique', 'Available', 'success', 'R 4,250', 'No block', 'Process'),
      row('PO-2026-05-02', 'Luxe Archive ZA', 'OnHold', 'warning', 'R 2,100', 'Release review', 'Release'),
      row('PO-2026-05-03', 'Glow Beauty Studio', 'Blocked', 'danger', 'R 1,880', 'Payout profile change', 'Inspect')
    ], 'Dual control', 'Processing is disabled when payout-profile changes are pending or the same actor staged the movement.', ['Make available', 'Process']),
    'admin/payout-profile-changes': spec('admin', 'Payout profile changes', 'Finance queue for seller payout-provider reference changes requiring re-verification and dual-control review.', ['Pending', 'Draft', 'Approved', 'Rejected'], ['3', '2', '9', '1'], ['Request', 'Seller', 'Status', 'Requester', 'Action'], [
      row('PCR-15', 'Maison Rose Boutique', 'PendingReview', 'warning', 'seller@example.test', 'Finance', 'Review'),
      row('PCR-12', 'Luxe Archive ZA', 'Draft', 'warning', 'archive@example.test', 'Seller', 'View'),
      row('PCR-09', 'Glow Beauty Studio', 'Rejected', 'danger', 'glow@example.test', 'Finance', 'Open')
    ], 'Re-verification', 'Requester cannot approve their own payout change, including SuperAdmin.', ['Approve', 'Reject']),
    'admin/support': spec('admin', 'Admin support', 'Support ticket queue for buyer and seller issues with assignment, public replies, internal notes, resolve, and close actions.', ['Open', 'Waiting', 'Resolved', 'Internal notes'], ['12', '4', '30', '88'], ['Ticket', 'Audience', 'Status', 'Category', 'Action'], [
      row('SUP-441', 'Seller carrier booking issue', 'Open', 'warning', 'Seller', 'Orders', 'Reply'),
      row('SUP-438', 'Buyer delivery failure', 'Waiting', 'warning', 'Buyer', 'Delivery', 'Open'),
      row('SUP-420', 'Refund status question', 'Open', 'danger', 'Buyer', 'Finance', 'Triage')
    ], 'Support tools', 'Public replies notify users; internal notes stay admin/support-only.', ['Reply', 'Add internal note']),
    'admin/support/:ticketId': spec('admin', 'Admin support ticket', 'Support ticket detail with public thread, internal notes, linked order/return/payment, and resolve/close controls.', ['Public msgs', 'Internal notes', 'Links', 'Status'], ['6', '3', '4', 'Open'], ['Message', 'Visibility', 'Sender', 'Time', 'Action'], [
      row('Buyer cannot see tracking update', 'Public', 'Open', 'warning', 'Buyer', 'Today', 'Reply'),
      row('Order timeline checked', 'Internal', 'Note', 'success', 'Support', 'Today', 'Keep'),
      row('Seller asked for courier sync', 'Public', 'Open', 'warning', 'Seller', 'Today', 'Respond')
    ], 'Ticket controls', 'Use public replies for user-facing updates and internal notes for investigation.', ['Send reply', 'Resolve']),
    'admin/categories': spec('admin', 'Catalog categories', 'Catalog management workspace for active/inactive categories, parent hierarchy, product counts, and attribute definitions.', ['Categories', 'Active', 'Attributes', 'Products'], ['18', '15', '42', '316'], ['Category', 'Status', 'Products', 'Attributes', 'Action'], [
      row('Dresses', 'Parent: Clothing', 'Active', 'success', '84 products', '7 attrs', 'Edit'),
      row('Designer Bags', 'Top-level category', 'Active', 'success', '42 products', '5 attrs', 'Manage'),
      row('Cleansers', 'Parent: Beauty', 'Inactive', 'warning', '0 products', '4 attrs', 'Activate')
    ], 'Category and attributes', 'No hard delete. Deactivation hides selectors but existing products retain historical values.', ['Create category', 'Add attribute']),
    'admin/pickup-points': spec('admin', 'Pickup points', 'Platform-managed pickup point catalog for admin create, update, activate, deactivate, and checkout snapshot review.', ['Active', 'Inactive', 'Provinces', 'Pickup orders'], ['12', '2', '5', '18'], ['Pickup point', 'Province', 'Status', 'Hours', 'Action'], [
      row('Rosebank Pickup Counter', 'JHB-ROSEBANK-001', 'Active', 'success', 'Gauteng', 'Mon-Fri', 'Edit'),
      row('Cape Town CBD Desk', 'CPT-CBD-002', 'Active', 'success', 'Western Cape', 'Sat open', 'Edit'),
      row('Durban North Counter', 'DBN-N-003', 'Inactive', 'warning', 'KwaZulu-Natal', 'Closed', 'Activate')
    ], 'Pickup controls', 'Pickup points define locations only; pricing still comes from seller delivery methods.', ['Create point', 'Deactivate']),
    'admin/ads': spec('admin', 'Ad campaign review queue', 'Pending ad campaign moderation queue with seller, product eligibility, budget, schedule, copy, and decision controls.', ['Pending', 'Approved', 'Rejected', 'Live'], ['5', '28', '3', '11'], ['Campaign', 'Seller', 'Budget', 'Status', 'Action'], [
      row('Jewellery launch', 'Luxe Archive ZA', 'R 250/day', 'warning', 'PendingReview', '3 products', 'Review'),
      row('Winter occasionwear boost', 'Maison Rose Boutique', 'R 500/day', 'success', 'Approved', 'Live', 'Open'),
      row('Beauty claim campaign', 'Glow Beauty Studio', 'R 150/day', 'danger', 'Rejected', 'Claims risk', 'View')
    ], 'Ad decision', 'Approve or reject campaign serving; do not alter seller product data.', ['Approve', 'Reject']),
    'admin/ads/:id': spec('admin', 'Ad campaign review detail', 'Ad campaign detail with seller, budget, product cards, copy review, AI suggestions, and moderation decision history.', ['Products', 'Budget', 'Copy risks', 'History'], ['3', 'R 250/day', '1', '4'], ['Evidence', 'Value', 'Status', 'Risk', 'Action'], [
      row('Campaign copy', 'New jewellery edit for evening wear', 'Pending', 'warning', 'Low', 'Copy', 'Review'),
      row('Product eligibility', '3 published products selected', 'Valid', 'success', 'Low', 'Catalog', 'Open'),
      row('Budget cap', 'R 1,500 total', 'Valid', 'success', 'Low', 'Finance', 'Approve')
    ], 'Moderation decision', 'Ad approval controls campaign visibility only, not product publication.', ['Approve campaign', 'Reject'])
  };

  function spec(area, title, description, metricLabels, metricValues, columns, rows, panelTitle, panelBody, panelActions) {
    return {
      area,
      title,
      description,
      metrics: metricLabels.map((label, index) => ({
        label,
        value: metricValues[index],
        tone: index === 0 ? 'success' : index === 1 ? 'warning' : 'neutral'
      })),
      columns,
      rows,
      panel: { title: panelTitle, body: panelBody, actions: panelActions }
    };
  }

  function row(primary, meta, status, tone, owner, updated, action) {
    return { primary, meta, status, tone, owner, updated, action };
  }

  function esc(value) {
    return String(value ?? '').replace(/[&<>"']/g, char => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    })[char]);
  }

  function titleFromRoute(path) {
    if (path === '') return 'Marketplace Home';
    return path
      .replace(/:/g, '')
      .split('/')
      .map(part => part.replace(/-/g, ' '))
      .map(part => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' / ');
  }

  function header() {
    return `
      <header class="luxury-header">
        <div class="utility-bar">
          <span>South African luxury marketplace</span>
          <span>Verified sellers - secure checkout - curated fashion</span>
        </div>
        <div class="main-nav">
          <button class="mobile-menu" aria-label="Open menu">Menu</button>
          <nav class="nav-links">
            <a>New In</a><a>Women</a><a>Beauty</a><a>Jewellery</a><a>Pre-loved Luxe</a>
          </nav>
          <a class="brand">
            <strong>Mabuntle</strong>
            <span>Fashion marketplace</span>
          </a>
          <nav class="nav-actions">
            <a>Search</a><a>Account</a><a>Wishlist</a><a>Bag</a>
          </nav>
        </div>
      </header>`;
  }

  function mobileBottom() {
    return `<nav class="mobile-bottom"><a>Shop</a><a>Search</a><a>Saved</a><a>Account</a></nav>`;
  }

  function productGrid(count = 4) {
    return `<div class="product-grid">
      ${productNames.slice(0, count).map((item, index) => `
        <article class="product-card">
          <div class="product-media tone-${index}"></div>
          <div class="product-copy">
            <span>${index % 2 ? 'Emerging seller' : 'Verified boutique'}</span>
            <strong>${item[0]}</strong>
            <div class="price">${item[1]}</div>
          </div>
        </article>`).join('')}
    </div>`;
  }

  function categoryCards() {
    return `<div class="category-grid">
      ${categories.slice(0, 3).map(category => `
        <article class="category-card">
          <h3>${category}</h3>
          <span>Curated edits for refined everyday dressing</span>
        </article>`).join('')}
    </div>`;
  }

  function publicHero(model) {
    return `
      ${header()}
      <main class="page-shell">
        <section class="editorial-hero">
          <div>
            <span class="eyebrow">${esc(model.kicker)}</span>
            <h1>${esc(model.title)}</h1>
            <p class="lead">${esc(model.description)}</p>
            <div class="hero-actions">
              <a class="button">${esc(model.primaryAction)}</a>
              <a class="ghost-button">${esc(model.secondaryAction)}</a>
            </div>
            <div class="section-heading">
              <h2>Curated now</h2>
              <a class="text-button">View all</a>
            </div>
            ${productGrid(4)}
          </div>
          <div class="hero-media">
            <div class="media-caption">
              <div><strong>${esc(model.mediaTitle)}</strong><span>${esc(model.mediaMeta)}</span></div>
              <span class="badge dark">${esc(model.badge)}</span>
            </div>
          </div>
        </section>
        <section class="section-heading"><h2>Shop by mood</h2><span class="muted">Editorial categories, not copied storefronts.</span></section>
        ${categoryCards()}
      </main>
      ${mobileBottom()}`;
  }

  function shopScreen(kind) {
    const isCategory = kind === 'category';
    return `
      ${header()}
      <main class="page-shell">
        <span class="eyebrow">${isCategory ? 'Category edit' : 'Search results'}</span>
        <h1>${isCategory ? 'The refined dress edit' : 'Find fashion, beauty, and accessories'}</h1>
        <p class="lead">A restrained luxury browse experience with editorial merchandising, clear filters, and premium product presentation.</p>
        <div class="pill-row">
          ${categories.map(category => `<span class="pill">${category}</span>`).join('')}
        </div>
        <div class="shop-layout">
          <aside class="filter-rail panel">
            <h3>Refine</h3>
            ${['Search', 'Category', 'Availability', 'Min price', 'Max price', 'Size', 'Colour', 'Material'].map(label => `
              <div class="field"><label>${label}</label><div class="input">${label === 'Category' ? 'All categories' : ''}</div></div>
            `).join('')}
          </aside>
          <section>
            <div class="section-heading"><h2>${isCategory ? 'Available pieces' : 'Published catalog'}</h2><span class="badge">${isCategory ? '12 pieces' : '48 pieces'}</span></div>
            ${productGrid(8)}
          </section>
        </div>
      </main>
      ${mobileBottom()}`;
  }

  function productDetail() {
    return `
      ${header()}
      <main class="page-shell">
        <div class="detail-layout">
          <section class="gallery">
            <div class="thumbs">${[1,2,3,4].map(() => '<div class="thumb"></div>').join('')}</div>
            <div class="hero-media large-product"><div class="media-caption"><div><strong>Editorial product view</strong><span>Detail gallery fallback</span></div></div></div>
          </section>
          <aside class="purchase-panel panel">
            <span class="eyebrow">Verified seller</span>
            <h1>Black crepe evening dress</h1>
            <p class="lead">A refined product detail with gallery, seller trust, variant clarity, delivery expectations, and review confidence.</p>
            <div class="price">R 4,650</div>
            <div class="pill-row"><span class="pill">Size S</span><span class="pill">Size M</span><span class="pill">Size L</span></div>
            <a class="button">Add to bag</a>
            <div class="card-grid" style="margin-top:24px; grid-template-columns:1fr;">
              <div class="panel"><h3>Delivery and returns</h3><p class="muted">Seller-managed delivery with tracked fulfilment and marketplace support.</p></div>
              <div class="panel"><h3>Seller assurance</h3><p class="muted">Published storefront, verified seller, and moderated reviews.</p></div>
            </div>
          </aside>
        </div>
      </main>
      ${mobileBottom()}`;
  }

  function authScreen(type) {
    const isLogin = type === 'login';
    const isSeller = type === 'seller';
    return `
      ${header()}
      <main class="auth-screen">
        <section class="auth-card">
          <span class="eyebrow">${isLogin ? 'Account' : isSeller ? 'Seller' : 'Buyer'}</span>
          <h1>${isLogin ? 'Sign in' : isSeller ? 'Create seller account' : 'Create buyer account'}</h1>
          <p class="lead">${isLogin ? 'Access your Mabuntle account.' : 'Create a secure marketplace account with a polished onboarding handoff.'}</p>
          <div class="field"><label>Email</label><div class="input">you@example.com</div></div>
          <div class="field"><label>Password</label><div class="input">************</div></div>
          ${isLogin ? '' : '<div class="field"><label>Confirm password</label><div class="input">************</div></div>'}
          <div class="action-row"><a class="button">${isLogin ? 'Sign in' : 'Create account'}</a><a class="text-button">${isLogin ? 'Create account' : 'Already have an account'}</a></div>
        </section>
      </main>`;
  }

  function checkoutScreen(state) {
    const heading = state === 'success' ? 'Checkout started' : state === 'failed' ? 'Checkout needs attention' : 'Complete secure checkout';
    return `
      ${header()}
      <main class="page-shell">
        <span class="eyebrow">Checkout</span>
        <h1>${heading}</h1>
        <p class="lead">Provider-neutral payment flow with address, delivery method, order summary, and clear payment state.</p>
        <div class="checkout-layout">
          <section class="step-list">
            ${['Delivery address', 'Shipping method', 'Payment handoff', 'Review order'].map((step, index) => `
              <article class="step">
                <span class="badge ${index === 2 && state === 'failed' ? 'warning' : 'success'}">${index + 1}</span>
                <h3>${step}</h3>
                <p class="muted">${state === 'failed' && index === 2 ? 'Payment can be retried for pending orders.' : 'Saved details are snapshotted for fulfilment and support.'}</p>
              </article>
            `).join('')}
          </section>
          <aside class="summary panel">
            <h2>Order summary</h2>
            <p class="muted">Rose silk column dress</p>
            <p class="muted">Standard courier - 2-5 days</p>
            <hr>
            <h3>Total R 4,025</h3>
            <a class="button">${state === 'failed' ? 'Retry payment' : 'Continue to payment'}</a>
          </aside>
        </div>
      </main>
      ${mobileBottom()}`;
  }

  function accountScreen(path) {
    return `
      ${header()}
      <main class="page-shell">
        <span class="eyebrow">Buyer workspace</span>
        <h1>${esc(titleFromRoute(path))}</h1>
        <p class="lead">Account operations presented as a calm luxury-service area with orders, returns, support, saved items, reviews, notifications, and settings.</p>
        <div class="metric-grid">
          ${['Orders', 'Returns', 'Saved', 'Unread'].map((label, index) => `<article class="metric"><small>${label}</small><strong>${[4,1,9,3][index]}</strong><span class="badge ${index === 3 ? 'warning' : 'success'}">Current</span></article>`).join('')}
        </div>
        <div class="split">
          ${tableCard(path.includes('notifications') ? 'Notification' : path.includes('support') ? 'Support ticket' : 'Buyer activity')}
          <aside class="panel"><h2>Concierge context</h2><p class="muted">Status, delivery, return, support, and review actions remain backed by existing APIs.</p><div class="pill-row"><span class="pill">Verified purchases</span><span class="pill">In-app alerts</span><span class="pill">Support ready</span></div></aside>
        </div>
      </main>
      ${mobileBottom()}`;
  }

  function tableCard(label) {
    const rows = ['Rose silk dress', 'Delivery method update', 'Seller response', 'Review awaiting moderation'];
    return `<section class="table-card table">
      <div class="table-header"><span>${label}</span><span>Status</span><span>Owner</span><span>Updated</span><span>Action</span></div>
      ${rows.map((row, index) => `
        <div class="table-row">
          <div><strong>${row}</strong><small>Reference SWY-${1240 + index}</small></div>
          <div><span class="badge ${index === 1 ? 'warning' : 'success'}">${index === 1 ? 'Pending' : 'Open'}</span></div>
          <div>${index % 2 ? 'Marketplace' : 'Mabuntle'}</div>
          <div><small>${index + 1}h ago</small></div>
          <div><a class="text-button">Open</a></div>
        </div>`).join('')}
    </section>`;
  }

  function dashboardNav(area, activePath) {
    const sellerSections = [
      ['Workspace', [['Dashboard','seller'], ['Products','seller/products'], ['Inventory','seller/inventory'], ['Orders','seller/orders']]],
      ['Operations', [['Returns','seller/returns'], ['Payouts','seller/payouts'], ['Support','seller/support']]],
      ['Growth', [['Ads','seller/ads'], ['Analytics','seller/analytics'], ['Store settings','seller/settings/store']]]
    ];
    const adminSections = [
      ['Review', [['Dashboard','admin'], ['Sellers','admin/sellers'], ['Products','admin/products'], ['Reviews','admin/reviews']]],
      ['Operations', [['Orders','admin/orders'], ['Payments','admin/payments'], ['Refunds','admin/refunds'], ['Payouts','admin/payouts'], ['Disputes','admin/disputes']]],
      ['Platform', [['Support','admin/support'], ['Categories','admin/categories'], ['Pickup points','admin/pickup-points'], ['Reports','admin/reports'], ['Audit logs','admin/audit-logs']]]
    ];
    const sections = area === 'admin' ? adminSections : sellerSections;
    return `<aside class="side-nav">
      <div class="side-brand"><div class="side-logo">S</div><div><strong>Mabuntle</strong><small>${area === 'admin' ? 'Admin console' : 'Seller workspace'}</small></div></div>
      ${sections.map(section => `
        <div class="nav-section-title">${section[0]}</div>
        ${section[1].map(item => `<a class="${activePath.startsWith(item[1]) ? 'active' : ''}"><span>${item[0]}</span><span>&gt;</span></a>`).join('')}
      `).join('')}
    </aside>`;
  }

  function dashboardScreen(area, path) {
    const specModel = screenSpecs[path] ?? fallbackDashboardSpec(area, path);
    const isAdmin = specModel.area === 'admin';
    return `<main class="dashboard">
      ${dashboardNav(area, path)}
      <section class="workspace">
        <div class="workspace-topbar">
          <div><strong>${isAdmin ? 'Operational command centre' : 'Seller operations'}</strong><div class="muted">${esc(path || area)}</div></div>
          <div class="pill-row" style="margin:0"><span class="badge success">${isAdmin ? 'Policy-led' : 'Verified'}</span><span class="badge">Route-specific</span></div>
        </div>
        <div class="workspace-content">
          <section class="workspace-hero">
            <div>
              <span class="eyebrow">${isAdmin ? 'Admin' : 'Seller'}</span>
              <h1>${esc(specModel.title)}</h1>
              <p class="lead">${esc(specModel.description)}</p>
            </div>
            <div class="panel"><h3>${esc(specModel.panel.title)}</h3><p class="muted">${esc(specModel.panel.body)}</p><a class="button">${esc(specModel.panel.actions[0] ?? 'Open')}</a></div>
          </section>
          ${renderMetrics(specModel.metrics)}
          <div class="split">
            ${renderRouteTable(specModel)}
            <aside class="panel">
              <h2>${esc(specModel.panel.title)}</h2>
              <p class="muted">${esc(specModel.panel.body)}</p>
              <div class="panel-list">
                ${specModel.rows.slice(0, 3).map(item => `<div><strong>${esc(item.primary)}</strong><span>${esc(item.meta)}</span></div>`).join('')}
              </div>
              <div class="field"><label>Decision note</label><div class="input">${isAdmin ? 'Record policy or evidence notes' : 'Record seller action notes'}</div></div>
              <div class="action-row">${specModel.panel.actions.map((action, index) => `<a class="${index === 0 ? 'button' : 'ghost-button'}">${esc(action)}</a>`).join('')}</div>
            </aside>
          </div>
        </div>
      </section>
    </main>`;
  }

  function fallbackDashboardSpec(area, path) {
    return spec(area, titleFromRoute(path), area === 'admin' ? 'Route-specific admin content is pending in the mockup registry.' : 'Route-specific seller content is pending in the mockup registry.', ['Queue', 'Attention', 'Records', 'Updated'], ['0', '0', '0', 'Now'], ['Item', 'Status', 'Area', 'Updated', 'Action'], [
      row('Route content pending', path, 'Draft', 'warning', area, 'Now', 'Define')
    ], 'Registry gap', 'Add this route to the mockup screenSpecs registry before implementation.', ['Define content']);
  }

  function renderMetrics(metrics) {
    return `<div class="metric-grid">
      ${metrics.map(metric => `<article class="metric"><small>${esc(metric.label)}</small><strong>${esc(metric.value)}</strong><span class="badge ${metric.tone === 'warning' ? 'warning' : metric.tone === 'danger' ? 'danger' : 'success'}">${metric.tone === 'warning' ? 'Review' : metric.tone === 'danger' ? 'Risk' : 'Current'}</span></article>`).join('')}
    </div>`;
  }

  function renderRouteTable(specModel) {
    const columns = specModel.columns.length === 5 ? specModel.columns : ['Item', 'Status', 'Owner', 'Updated', 'Action'];
    return `<section class="table-card table">
      <div class="table-header">${columns.map(column => `<span>${esc(column)}</span>`).join('')}</div>
      ${specModel.rows.map(item => `
        <div class="table-row">
          <div><strong>${esc(item.primary)}</strong><small>${esc(item.meta)}</small></div>
          <div><span class="badge ${esc(item.tone)}">${esc(item.status)}</span></div>
          <div>${esc(item.owner)}</div>
          <div><small>${esc(item.updated)}</small></div>
          <div><a class="text-button">${esc(item.action)}</a></div>
        </div>`).join('')}
    </section>`;
  }

  function assistantScreen(type) {
    return `
      ${header()}
      <main class="page-shell">
        <span class="eyebrow">${type === 'visual' ? 'Visual discovery' : 'AI style finder'}</span>
        <h1>${type === 'visual' ? 'Search by image or description' : 'Describe the occasion, get a refined edit'}</h1>
        <p class="lead">Luxury discovery tools stay buyer-readable: clear intent extraction, honest confidence, and published product matches only.</p>
        <div class="split">
          <section class="panel">
            <h2>${type === 'visual' ? 'Upload reference' : 'Style prompt'}</h2>
            <div class="field"><label>${type === 'visual' ? 'Image or URL' : 'Prompt'}</label><div class="input">${type === 'visual' ? 'black satin dress reference' : 'Wedding guest outfit under R5000'}</div></div>
            <div class="pill-row"><span class="pill">Occasion</span><span class="pill">Budget</span><span class="pill">Colour</span></div>
            <a class="button">Find pieces</a>
          </section>
          <aside class="panel"><h2>Extracted intent</h2><p class="muted">Category: dresses. Colour: black. Fit: refined. Budget: mid luxury. Safety: no restricted claims.</p></aside>
        </div>
        <section class="section-heading"><h2>Matched products</h2><span class="badge success">6 matches</span></section>
        ${productGrid(4)}
      </main>
      ${mobileBottom()}`;
  }

  function cartScreen() {
    return `
      ${header()}
      <main class="page-shell">
        <span class="eyebrow">Shopping bag</span>
        <h1>Review your selected pieces</h1>
        <div class="checkout-layout">
          ${tableCard('Bag item')}
          <aside class="summary panel"><h2>Bag summary</h2><p class="muted">Single-seller checkout with delivery-rate handoff.</p><h3>Total R 6,850</h3><a class="button">Checkout</a></aside>
        </div>
      </main>
      ${mobileBottom()}`;
  }

  function errorScreen() {
    return `${header()}<main class="auth-screen"><section class="auth-card empty-state"><span class="eyebrow">Access</span><h1>Access denied</h1><p class="lead">The route keeps a polished refusal state without exposing implementation details.</p><a class="button">Return home</a></section></main>`;
  }

  function storeFront() {
    return `${header()}<main class="page-shell"><section class="editorial-hero"><div><span class="eyebrow">Verified storefront</span><h1>Maison Rose Boutique</h1><p class="lead">A premium seller storefront with curated story, policies, product count, and trust cues.</p><div class="hero-actions"><a class="button">Shop storefront</a><a class="ghost-button">Contact support</a></div></div><div class="hero-media"><div class="media-caption"><div><strong>Seller edit</strong><span>36 published pieces</span></div><span class="badge dark">Verified</span></div></div></section><section class="section-heading"><h2>Published pieces</h2><span class="badge">36 items</span></section>${productGrid(4)}</main>${mobileBottom()}`;
  }

  function render() {
    if (route === 'login') return authScreen('login');
    if (route === 'register/buyer') return authScreen('buyer');
    if (route === 'register/seller') return authScreen('seller');
    if (route === 'access-denied') return errorScreen();
    if (route === 'shop') return shopScreen('shop');
    if (route === 'category/:slug') return shopScreen('category');
    if (route === 'product/:slug') return productDetail();
    if (route === 'seller/:storeSlug') return storeFront();
    if (route === 'cart') return cartScreen();
    if (route === 'assistant') return assistantScreen('assistant');
    if (route === 'visual-search') return assistantScreen('visual');
    if (route === 'checkout') return checkoutScreen('default');
    if (route === 'checkout/success') return checkoutScreen('success');
    if (route === 'checkout/failed') return checkoutScreen('failed');
    if (route.startsWith('account')) return accountScreen(route);
    if (route.startsWith('seller')) return dashboardScreen('seller', route);
    if (route.startsWith('admin')) return dashboardScreen('admin', route);
    return publicHero({
      kicker: 'Luxury marketplace',
      title: 'Discover fashion with an editorial eye',
      description: 'A premium marketplace direction for Mabuntle, blending curated South African seller inventory with refined shopping confidence.',
      primaryAction: 'Shop new arrivals',
      secondaryAction: 'Explore sellers',
      mediaTitle: 'The Mabuntle edit',
      mediaMeta: 'Fashion, jewellery, beauty, accessories',
      badge: 'Curated'
    });
  }

  app.innerHTML = `<div class="screen" data-route="${esc(route)}">${render()}</div>`;
  window.__MABUNTLE_ROUTES__ = routes;
})();
