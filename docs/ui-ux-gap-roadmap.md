# Swyftly UI/UX Gap Roadmap

## Summary

This document maps the UI/UX blueprint to the current Angular app and turns it into a practical implementation sequence. The blueprint remains the direction of travel, but it should not be treated as a literal build list because the application has already moved ahead in AI, ads, analytics, finance, and backend-backed checkout areas.

The main UI issue is not route absence. The app has many useful screens, but they still feel like implementation surfaces rather than a cohesive fashion marketplace. The next UI work should start with navigation, shared visual primitives, marketplace trust, and buyer journey polish before broad feature expansion.

Active high-fidelity implementation progress is tracked in `docs/ui-high-fidelity-progress.md`. The next visual target is the luxury editorial route-complete pack documented in `docs/ui-luxury-editorial-screen-pack.md` and generated under `Documentation/UI UX/luxury-editorial-screen-pack`.

Latest implementation note: Phase 9S split route-specific luxury CSS out of the initial global stylesheet into lazy route-group assets. The Angular build now reports an initial total of `643.13 kB`, below the unchanged `650 kB` warning budget, with global styles reduced from `193.93 kB` to `102.03 kB`.

Latest buyer-flow note: Phase 9T added an opt-in demo catalog seed (`-SeedSampleProducts`), local sample product visuals, a seeded buyer delivery address, buyer settings form polish, clearer cart subtotal/delivery/payment copy, and `docs/buyer-flow-test-runbook.md` for repeatable local buyer-flow testing.

Latest seller-flow note: Phase 9U added an opt-in seller approval-flow seed (`-SeedSellerFlowDemo`) with a pending seller, pending product review, pending ad review, scoped seller guidance copy, and `docs/seller-flow-test-runbook.md` for repeatable local seller/admin approval testing.

Latest seller-notification note: Phase 10A added seller-managed notification preferences for Verification, Products, Revisions, and Ads, and widened the existing SignalR notification hub so sellers get realtime unread badges, toasts, and `/seller/notifications` sync after visible notification persistence. Phase 10J added the Reports preference category for scheduled analytics digests.

Latest seller-onboarding note: Phase 10B polished `/seller` for non-verified sellers with distinct pending, under-review, rejected, and suspended states, additive seller-facing verification review context, readiness cards, and draft-preparation guidance while preserving the verified seller live dashboard.

Latest seller-policy note: Phase 10C added seller-managed store policies in `/seller/settings/store`, order policy snapshots at checkout, policy context on public product/storefront pages, buyer/seller return and order detail pages, and admin seller review detail. Policies are informational context only and do not automate return/refund enforcement.

Latest seller-evidence note: Phase 10D added optional seller verification evidence uploads to `/seller`, private seller/admin download endpoints, evidence metadata on admin seller detail, and audit logs for upload/remove/admin-download actions. Evidence improves review context but does not block or automate seller verification.

Latest seller-stock-ledger note: Phase 10E extended seller inventory history into operational stock-ledger traceability for checkout reservations, payment settlement/failure releases, return requests, and refund completion. `/seller/inventory`, `/seller/orders/:orderId`, and `/seller/returns/:returnRequestId` show stock/reserved deltas and related order/return context without changing checkout stock semantics or auto-restocking behavior.

Latest seller-QA note: Phase 10Q completed the seeded desktop/mobile seller lifecycle browser sign-off after Phase 10P restored the verification path. The pass covered 48 route/viewport checks across seller and admin seller/product/ad review routes, fixed checkout/order creation in the seeded flow, fixed mobile admin queue overflow, and kept the existing `650.84 kB` Angular initial-budget warning tracked.

Latest seller-restock note: Phase 10G added explicit seller restock decisions for approved/returned/refunded/closed returns. `/seller/returns/:returnRequestId` now records one inspection decision per return item, increments stock only when the seller enters a positive restock quantity, and shows `ReturnRestocked` rows in seller inventory/order/return stock-ledger views.

Latest seller-ledger-backfill note: Phase 10I added an admin-only historical stock-ledger backfill for missing derivable reservation, return, and refund inventory movement rows, plus scanner-friendly exact SKU/barcode lookup on `/seller/inventory`. The backfill is traceability-only and does not mutate stock, orders, payments, returns, refunds, payouts, carts, or financial ledger entries.

Latest seller-report note: Phase 10J added opt-in scheduled seller analytics digests with weekly/monthly schedule settings, test digest delivery, worker processing, idempotent run tracking, and seller notification/email preference support. Digests link back to `/seller/analytics` and existing CSV exports; no files are attached.

Latest seller-conversion note: Phase 10L added first-party storefront attribution/source reporting on top of Phase 10K funnel analytics. Public funnel events now carry UTM source/medium/campaign, referrer host, and controlled source categories; `/seller/analytics` shows source breakdowns, funnel source filtering, product-level source context, and Funnel CSV source columns. No third-party analytics script or checkout behavior change was added.

Latest seller-merchandising note: Phase 10M added seller-managed product SEO and merchandising fields, staged those fields through published listing revisions, exposed merchandising/care/disclaimer snippets on public product/storefront surfaces, and polished `/seller/products` plus the product editor with thumbnail, stock, preview, SEO preview, and buyer-facing attribute context.

Latest seller-product note: Phase 10N added bulk CSV tooling to the published variant/pricing revision panel. Sellers can export current variants, download a template, preview Add/Update/Deactivate rows, and bulk-stage changed valid rows into the existing admin-reviewed revision workflow without submitting automatically.

Latest admin-ops note: Phase 10R upgraded `/admin/sellers`, `/admin/products`, and `/admin/ads` from pending-only queues to all-state operational consoles backed by new paged list endpoints, status counts, server-side search/status/seller filters, and existing detail/action routes. Phase 10S added safe queue triage with claim/unclaim, Normal/High/Urgent priority, internal admin notes, assignment/priority filters, and bulk claim/high-priority tools without adding bulk approval or rejection. Phase 10T added per-admin saved queue views, SLA/age chips and filters, and read-only workload/throughput summary context.

Latest seller-dashboard note: Phase 9X added `GET /api/seller/dashboard/summary` and wired `/seller` for verified sellers to show live sales, fulfilment, catalog, inventory, customer-care, finance, ads, notification, alert, and recent-activity data. Unverified sellers keep the existing onboarding/status flow.

Latest seller-inventory note: Phase 9Y added structured inventory movement history for seller single adjustments and bulk CSV imports, SKU/barcode lookup, barcode columns in inventory CSV flows, and `/seller/inventory` movement-history panels.

Latest seller-acquisition note: Phase 9Z added a public `/sell` landing page as the seller acquisition and onboarding entry point, linked from public navigation, mobile navigation, home seller CTAs, the footer, and seller registration context.

## Status Legend

| Status | Meaning |
|---|---|
| Implemented | Route and primary workflow exist at useful MVP depth. |
| Partial | Route exists and does useful work, but important blueprint UX is missing. |
| Shell | Route exists mainly as a placeholder or foundation. |
| Missing | No meaningful UI route exists yet. |
| Later/Beta | Exists or is planned, but should not block MVP UI hardening. |

## Current Frontend Shape

Current Angular routing is concentrated in `src/app/app.routes.ts` with standalone page components under `src/app/pages`, plus feature services under `auth`, `shop`, `cart`, `seller`, `buyer`, and `admin`.

High-level coverage:

| Area | Current state |
|---|---|
| Public buyer | Home, shop, category, product detail, seller storefront, cart, checkout start, checkout success/failure. |
| Auth | Login, buyer registration, seller registration, access denied. |
| Buyer account | Buyer dashboard, order history/detail, payment retry, returns, disputes, support tickets, wishlist, reviews, notifications, saved delivery addresses with local verification, pickup-aware checkout, carrier-aware shipment tracking, and settings exist at useful MVP depth. Buyer transactional email delivery and real-time in-app SignalR updates exist; SMS and push channels remain missing. |
| Seller | Onboarding, verified seller dashboard with live operational summary, products, dedicated inventory, store settings, polished MVP product editor with production-hardened media uploads, moderation-aware published listing revisions and variant/pricing revisions, seller notifications with preferences/realtime unread badge/toast, AI listing assistant, orders, manual fulfilment, provider-neutral carrier booking/tracking, returns, explicit restock decisions, payouts, support, ads, analytics, and stock-ledger traceability for reservations/orders/returns/refunds. Hardware barcode scanner SDK integration, sensitive payout-bank storage, and real carrier-provider integration remain incomplete. |
| Admin | Dashboard, seller queue/detail, product queue/detail, audit logs, reports, AI usage, ad campaign queue/detail, finance refunds, finance payouts, dispute resolution, pickup-point management, and read-only order/payment investigation screens. |
| Shared UI | Product card exists. Most layout and component styling lives in one global stylesheet. |

## Buyer And Public Screen Coverage

| ID | Blueprint screen | Current route/component | Status | Notes |
|---|---|---|---|---|
| B-01 | Home Page | `/`, `HomePageComponent` | Partial | Current page is foundation-oriented. It should become a buyer-first marketplace home with search, category entry points, product imagery, and seller trust cues. |
| B-02 | Category Landing Page | `/category/:slug`, `CategoryPageComponent` | Partial | Loads category products, but lacks category merchandising, subcategory browsing, filters, and featured sellers. |
| B-03 | Product Listing/Search Results | `/shop`, `ShopPageComponent` | Partial | Search, filters, sort, pagination, and product grid exist. Needs mobile filter drawer, category facets, availability, rating, brand/material treatment, and stronger empty/loading states. |
| B-04 | Product Detail Page | `/product/:slug`, `ProductDetailPageComponent` | Partial | Product detail, variant selection, attributes, price, seller link, and add-to-cart exist. Needs true image gallery, trust blocks, return/shipping info, reviews, similar products, and real wishlist behavior. |
| B-05 | Seller Storefront | `/seller/:storeSlug`, `SellerStorefrontPageComponent` | Partial | Basic hero and product grid exist. Needs seller logo/profile, rating, policies, review summary, and stronger visual hierarchy. |
| B-06 | Wishlist | `/account/wishlist`, product cards/detail | Partial | Buyer wishlist route exists, product cards/detail hydrate saved state, save/remove products, and redirect unauthenticated buyers with return URL. Wishlist items show variant/quantity controls and can move to cart. Wishlist sharing, folders, and recommendation loops remain future work. |
| B-07 | Cart | `/cart`, `CartPageComponent` | Partial | Cart item update/remove, single-seller checkout, variant/SKU detail, product image metadata/fallback visuals, price clarity, trust messaging, product detail links, save-for-later, and checkout delivery-rate handoff are present. Richer checkout state remains future work. |
| B-08 | Checkout Address & Delivery | `/checkout`, `CheckoutPageComponent`; `/account/settings`, `BuyerSettingsPageComponent` | Partial | Checkout can use saved delivery addresses or an inline one-off address with optional delivery instructions, runs warning-only local address verification, loads seller-managed delivery methods/rates, supports pickup-point choices for pickup methods, requires a selected delivery option, and orders persist delivery-address verification plus delivery-method/rate/pickup snapshots. External address verification/geocoding and carrier-provided rates remain future work. |
| B-09 | Checkout Delivery | `/checkout`, `CheckoutPageComponent` | Partial | Checkout now presents seller-managed delivery options, pickup choices where applicable, verification warnings, and rate totals. Carrier-provider rate quoting and provider-backed delivery estimates remain future work. |
| B-10 | Checkout Payment | `/checkout`, `CheckoutPageComponent` | Partial | Checkout now creates a pending order, initiates backend payment, and redirects to the provider checkout URL. Real provider SDK/UI and saved payment methods remain future work. |
| B-11 | Payment Failed | `/checkout/failed`, `CheckoutFailedPageComponent` | Partial | Route loads order context when available, supports pending-payment retry, and tells buyers to restart checkout for cancelled orders. Change-payment details await real payment provider UI. |
| B-12 | Order Confirmation | `/checkout/success`, `CheckoutSuccessPageComponent` | Partial | Route loads order context, shows webhook-confirmed order status, and supports pending-payment retry. Rich paid-order confirmation and delivery ETA remain future polish. |
| B-13 | Buyer Account Dashboard | `/account`, `AccountPageComponent` | Partial | Dashboard now summarizes orders, active returns, open disputes, support tickets, wishlist, reviews, and notifications using existing buyer APIs. `/account/settings` manages lightweight profile details, saved delivery addresses with verification previews/status, and separate in-app/email notification preferences. Buyer real-time notification badge/toast exists. SMS and push remain future work. |
| B-14 | Buyer Orders List | `/account/orders`, `BuyerOrdersPageComponent` | Partial | API-backed buyer order history exists with client-side search/status filtering, shipment summaries, and carrier provider status where present. List-level bulk actions and real carrier-provider integration remain future work. |
| B-15 | Buyer Order Detail | `/account/orders/:orderId`, `BuyerOrderDetailPageComponent` | Partial | API-backed detail shows items, totals, delivery-address/instruction/verification, selected delivery-method and pickup-point snapshots, status history, shipment event timelines, carrier status metadata, delivery exception support CTAs, delivered-order return/review actions, and pending-payment retry. Real carrier-provider integration remains future work. |
| B-16 | Return/Refund Request | `/account/returns`, `/account/returns/:returnRequestId` | Partial | API-backed buyer return list/detail and rejected-return dispute escalation exist. Refund status is still handled through existing backend/admin flows. |
| B-17 | Leave Review | `/account/reviews`, `/account/orders/:orderId`, `/product/:slug`, `/admin/reviews` | Partial | Delivered order detail can create verified-purchase reviews, product detail shows public review summary/list data, `/account/reviews` can edit/delete buyer reviews with moderation state, and `/admin/reviews` can approve/reject/remove reviews. Review reporting and richer public review sorting remain future work. |
| B-18 | Notifications | `/account/notifications`, `/account/settings` | Partial | Buyer notification list/read/read-all UI exists for in-app notifications, with workflow notifications for review decisions, returns, support replies, tracking, ready-to-ship, shipped, delivered, delivery-failed, and returned-to-sender orders. `/account/settings` can separately mute future in-app and email notifications by Orders, Returns, Reviews, and Support. Buyer transactional email delivery uses a worker outbox with local log-only and SMTP provider options. SignalR live updates now drive buyer and seller unread badges, latest-notification toasts, and notification page sync. SMS, push, and admin email workflows remain future work. |
| B-19 | AI Shopping Assistant | `/assistant`, `BuyerAiAssistantPageComponent` | Later/Beta | Route exists and calls buyer AI services, but it should remain behind buyer auth and should not block MVP shopping polish. |
| B-20 | Visual Search Upload | `/visual-search`, `BuyerVisualSearchPageComponent` | Later/Beta | Route exists. Useful, but should stay secondary until core catalog UX is stronger. |

## Seller Screen Coverage

| ID | Blueprint screen | Current route/component | Status | Notes |
|---|---|---|---|---|
| S-01 | Sell on Swyftly Landing | `/sell`, `SellOnSwyftlyPageComponent`; `/register/seller` | Implemented | Public seller acquisition page explains the review-led seller path, readiness checklist, seller operations, honest deferred integrations, and routes sellers to registration/sign-in. |
| S-02 | Seller Registration/Login | `/register/seller`, `/login` | Partial | Auth flow exists with seller-specific copy, `/sell` context, and success-to-login behavior. Auto-login and deeper post-registration guidance remain future polish. |
| S-03 | Seller Onboarding Wizard | `/seller`, `SellerPageComponent` | Partial | Multi-step onboarding exists with readiness cards, clearer save/submit states, optional verification evidence upload/list/remove/download, and separate non-verified versus verified dashboard presentation. Evidence is review context only and does not alter submit gating. |
| S-04 | Seller Verification Status | `/seller`, `SellerPageComponent` | Partial | Pending, under-review, rejected, suspended, and verified states are now distinct, with seller-facing rejection/suspension reasons where existing records provide them and optional evidence context for review. Dedicated standalone status routes are not needed for MVP. |
| S-05 | Seller Dashboard | `/seller`, `SellerPageComponent` | Partial | Verified sellers now see an API-backed command center with live 30-day sales, fulfilment, catalog, inventory, customer-care, finance, growth, notification, alert, and recent-activity data. Deeper trend charts remain in `/seller/analytics`. |
| S-06 | Products List | `/seller/products`, `SellerProductsPageComponent` | Partial | Product table, edit action, search, status filtering, status badges, primary thumbnails/fallbacks, merchandising labels, inventory grouping, and published public-preview affordances exist. Bulk product operations remain future polish. |
| S-07 | Product Images | `/seller/products/new`, `/seller/products/:id/edit` | Partial | Product editor now has API-backed image upload, visual gallery, primary/fallback previews, image metadata editing, make-primary/remove actions, and production media-storage hardening behind the upload endpoints. Cloud/CDN operations and image-processing jobs remain future production polish. |
| S-08 | Product Basic Details | Product editor | Partial | Basic details now sit in a clearer stepper with slug guidance, editability messaging, save feedback, seller-managed SEO title/description, merchandising label, product care/disclaimer fields, and a search-result preview. AI-assisted SEO generation remains future work. |
| S-09 | AI Product Listing Assistant | Product editor | Partial | AI suggestion panel exists and is review-first with Luxe Blush visual treatment. Field-level confidence and deeper AI risk scoring remain future work. |
| S-10 | Product Attributes | Product editor | Partial | Dynamic attributes now show selected-category context, required/optional indicators, type hints, no-category guidance, and a buyer-facing attribute preview. Deeper merchandising recommendations remain future work. |
| S-11 | Product Variants & Stock | Product editor | Partial | Variant editing now supports add/edit modes, dense variant cards, stock/reserved/available indicators, and low/out-of-stock badges. Published stock operations remain correctly separated into Inventory. |
| S-12 | Pricing & Shipping | `/seller/settings/store`, checkout | Partial | Pricing exists through variants. Sellers can now manage provider-free delivery methods/rates for checkout, including country/province coverage, estimates, active state, free-shipping thresholds, and pickup-point delivery methods backed by platform pickup points. Carrier booking labels/tracking exist in fulfilment for configured providers. Product-level shipping policies, carrier-provided rates, and return-policy configuration remain future work. |
| S-13 | Product Review & Submit | Product editor | Partial | Review step now includes a buyer-facing preview, readiness checklist, public preview link when published, submit gating, seller-visible moderation history, and variant/pricing revision submission for published products. CSV bulk-staging now feeds the same submit-review path. |
| S-14 | Product Detail/Edit Screen | `/seller/products/:id/edit` | Partial | Edit route now has seller workspace navigation, status header, rejection/change-request banner, read-only messaging for locked statuses, seller-safe moderation/review history, published listing revision mode, and a dedicated published variant/pricing revision panel with export/template/preview/bulk-stage CSV tools. Variant hard-delete remains future work. |
| S-15 | Inventory Management | `/seller/inventory`, `SellerInventoryPageComponent` | Partial | Dedicated inventory route exists with searchable/filterable variant rows, stock-state summaries, SKU/barcode lookup, scanner-friendly exact SKU/barcode entry, single-row adjustment, CSV export/template download including barcode, import preview, bulk stock/status apply, backend audit logging, structured movement history, reservation/order/return/refund/restock stock-ledger events, admin historical stock-ledger backfill, related filters, and all-or-nothing validation. Hardware scanner SDK integration remains future work. |
| S-16 | Seller Orders List | `/seller/orders`, `SellerOrdersPageComponent` | Partial | API-backed order list exists with status, totals, item count, shipment summary, and detail navigation. |
| S-17 | Seller Order Detail/Fulfilment | `/seller/orders/:orderId`, `SellerOrderDetailPageComponent` | Partial | API-backed detail and manual fulfilment actions exist, including selected delivery-method/address/pickup snapshots, ready-to-ship, manual tracking, provider-neutral carrier booking/tracking, shipped, delivered, delivery-failed, returned-to-sender transitions, full event timelines, and compact order stock-ledger context. Real carrier-provider integration remains future work. |
| S-18 | Seller Returns | `/seller/returns`, `/seller/returns/:returnRequestId` | Partial | API-backed return list/detail and seller approve/reject actions exist with checkout-time seller policy context, return/refund stock-ledger context, and explicit per-item restock decisions that can increment stock only after seller inspection. Richer dispute handoff polish remains future work. |
| S-19 | Seller Payouts/Balance | `/seller/payouts`, `SellerPayoutsPageComponent` | Partial | Seller balance and payout history are visible as read-only finance views. Admin payout lifecycle actions remain separate. |
| S-20 | Seller Storefront Settings | `/seller/settings/store`, `SellerStoreSettingsPageComponent` | Partial | Dedicated settings route exists for profile, storefront, fulfilment address, delivery methods including PickupPoint, seller-managed buyer-facing store policies, verified-seller payout-profile change requests, and seller notification preferences. Full sensitive bank-detail storage and external provider verification remain future work. |
| S-21 | Seller Analytics | `/seller/analytics`, `SellerAnalyticsPageComponent` | Partial | Seller analytics now include API-backed date filters, daily/weekly sales trend buckets, product performance, inventory and barcode stock signals, ad performance, customer-care summary metrics, first-party storefront conversion funnel summaries/trends/product rows, source breakdowns, source-category funnel filtering, CSV exports for Sales, Products, Inventory, Ads, Returns, and Funnel, plus opt-in scheduled weekly/monthly digest delivery. Deeper campaign attribution modelling and session-path analysis remain future work. |
| S-22 | Seller Ads Dashboard | `/seller/ads`, `SellerAdCampaignsPageComponent` | Later/Beta | Route exists and should remain secondary to core seller operations. Ad detail now shows rejection reason and review history where available. |
| S-23 | Create Ad Campaign | `/seller/ads/new`, `SellerAdCampaignFormPageComponent` | Later/Beta | Route exists. Should not drive design-system decisions yet. |
| S-24 | Seller Support Tickets | `/seller/support`, `/seller/support/:ticketId` | Partial | Seller ticket list/create/detail/message flows exist. Support assignment and internal-note workflows remain support/admin-only. |
| S-25 | Seller Notifications | `/seller/notifications`, `SellerNotificationsPageComponent`; `/seller/settings/store` | Partial | Seller notification list, unread count badge, mark-read/read-all actions, links back to seller/product/ad/analytics workspaces, realtime SignalR badge/toast/page sync, and seller-managed in-app/email preferences exist for verification, product, revision, ad moderation, and analytics report outcomes. SMS/push and admin notification channels remain future work. |

## Admin And Support Screen Coverage

| ID | Blueprint screen | Current route/component | Status | Notes |
|---|---|---|---|---|
| A-01 | Admin Login | `/login` | Partial | Shared login supports admin roles, but no admin-specific login context. That is acceptable for now. |
| A-02 | Admin Dashboard | `/admin`, `AdminPageComponent` | Partial | Operational metrics and navigation exist. Finance routes now use role-aware links, but richer finance dashboard summaries remain future work. |
| A-03 | Seller Approval Queue | `/admin/sellers`, `AdminSellersPageComponent` | Partial | Queue now uses an all-state operational seller list with Needs Attention/All views, server-side search/status/seller/SLA filters, saved views, pagination, status counts, dense seller rows, status/SLA badges, queue assignment, priority, internal notes, bulk claim/high-priority triage, workload summary context, and shared loading/empty/error states. Bulk moderation decisions remain future work. |
| A-04 | Seller Review Detail | `/admin/sellers/:sellerId`, `AdminSellerDetailPageComponent` | Partial | Review detail now separates profile, storefront, address, payout setup, store policy, optional verification evidence with secure downloads, completeness indicators, actions, and audit trail. Deeper provider verification and richer decision history remain future work. |
| A-05 | Product Moderation Queue | `/admin/products`, `AdminProductsPageComponent` | Partial | Queue now uses a unified all-state moderation list for products, listing revisions, and variant revisions, with Needs Attention/All views, server-side search/status/seller/SLA filters, saved views, pagination, status counts, risk/staged-item context, assignment, priority, internal notes, bulk triage, existing detail routes, workload summary context, and shared loading/empty/error states. Bulk moderation decisions remain future work. |
| A-06 | Product Review Detail | `/admin/products/:productId`, `AdminProductDetailPageComponent` | Partial | Detail review now has a primary image/thumbnail review layout, fallbacks, seller context, attributes, variants, AI risk flags, actions, and audit trail. Side-by-side seller-facing preview remains future work. |
| A-07 | Orders Admin | `/admin/orders`, `AdminOrdersPageComponent` | Partial | API-backed read-only order list exists with finance-read access, status/search filtering, totals, seller/payment/shipment context, and detail links. Admin order mutations and paged server-side search remain future work. |
| A-08 | Order Detail Admin | `/admin/orders/:orderId`, `AdminOrderDetailPageComponent` | Partial | API-backed read-only detail exists with totals, parties, items, status history, shipments, and related payments. Admin order edits/manual state transitions remain out of scope. |
| A-09 | Payment/Ledger Overview | `/admin/payments`, `AdminPaymentsPageComponent`; `/admin/payments/:paymentId`, `AdminPaymentDetailPageComponent`; `/admin/reports` | Partial | API-backed payment list/detail exists with provider references, related order context, webhook event metadata, and a read-only reconciliation-candidate panel for stale/failed-provider-review payments. Raw webhook payloads, provider status queries, and manual payment mutation are intentionally not exposed. |
| A-10 | Seller Payout Queue | `/admin/payouts`, `AdminPayoutsPageComponent` | Partial | API-backed pending/on-hold payout queue exists with hold, release, make-available, process, and reconcile actions plus visible finance role eligibility. |
| A-11 | Refunds Queue | `/admin/refunds`, `AdminRefundsPageComponent` | Partial | API-backed refund list, order/return refund creation, and approval actions exist with visible operate/approve role eligibility. |
| A-12 | Dispute Case Detail | `/admin/disputes`, `AdminDisputesPageComponent` | Partial | API-backed dispute list, inline messages/evidence, and buyer/seller-favoured resolution exist. A dedicated single-case route and richer evidence review remain future work. |
| A-13 | Category Manager | `/admin/categories`, `AdminCategoriesPageComponent` | Partial | API-backed category create/edit/activate/deactivate exists with product, child, and attribute counts. Hard delete, bulk import, drag-and-drop reordering, SEO fields, and taxonomy versioning remain future work. |
| A-14 | Attribute Manager | `/admin/categories`, `AdminCategoriesPageComponent` | Partial | API-backed attribute create/edit/activate/deactivate exists with type, required, allowed-values, display-order, and safety-warning UX. Bulk editing and deeper seller-form preview remain future work. |
| A-15 | AI Moderation Dashboard | `/admin/ai-usage`, `AdminAiUsageAnalyticsPageComponent` | Partial | AI usage analytics exist. Needs moderation/risk review workflows if it is to match blueprint A-15. |
| A-16 | Support Ticket Queue | `/admin/support`, `/admin/support/:ticketId` | Done | API-backed support queue/detail exists with summary cards, saved views, queue CSV export, support quality dashboard/export, workload metrics, customer context, SLA/priority filters, claim/unclaim, escalation context, public replies, internal notes, resolve, and close actions for support/admin roles. SLA signals and quality targets are operational reporting only. |
| A-17 | Ad Campaign Approval Queue | `/admin/ads`, `AdminAdCampaignsPageComponent` | Later/Beta | Queue now uses an all-state operational campaign list with Needs Attention/All views, server-side search/status/seller/SLA filters, saved views, pagination, status counts, campaign budget/context, assignment, priority, internal notes, bulk triage, workload summary context, and existing detail routes. Keep ad hardening lower priority than core admin operations. |
| A-18 | Campaign Review Detail | `/admin/ads/:id`, `AdminAdCampaignDetailPageComponent` | Later/Beta | Exists. Should not take priority over orders/refunds/payouts. |
| A-19 | Reports & Analytics | `/admin/reports`, `AdminMarketplaceReportsPageComponent` | Later/Beta | Exists despite blueprint marking later. Useful for admins, but operational queues need more attention. |
| A-20 | Audit Logs | `/admin/audit-logs`, `AdminAuditLogsPageComponent` | Partial | Route has shared admin navigation, page header, filter form, clear behavior, and shared empty/error states. Export and finer role-based visibility remain future work. |
| A-21 | Platform Settings | None | Missing | No platform settings UI. |
| A-22 | Pickup Point Manager | `/admin/pickup-points`, `AdminPickupPointsPageComponent` | Partial | Admin-managed pickup-point list/create/edit/activate/deactivate exists with provider/code identity, address fields, coordinates/opening-hours metadata, and no hard delete. Bulk import and provider-network sync remain future work. |

## Cross-Cutting UX Gaps

1. **Marketplace identity is weak.** The home page and footer still describe a foundation rather than selling the buyer journey. For a fashion marketplace, product imagery and search should dominate the first viewport.

2. **Navigation still needs deeper role polish, but the main shell issue is addressed.** Seller and admin now use grouped side navigation with a mobile-safe stacked layout. Remaining work is mostly route-level refinement: buyer account menu density, contextual breadcrumbs, and visual review against every generated screen.

3. **Encoding artifacts need cleanup.** Some UI templates contain mojibake, including `Â·`, `Ã—`, and `â™¡`. These should be replaced with ASCII-safe text or proper icon components.

4. **Product visuals are not strong enough.** The app currently has no marketplace image assets beyond the favicon, and product display relies on remote product URLs or text placeholders. The product card, gallery, and storefront hero need stronger visual fallbacks.

5. **Shared UI primitives are improving, but need discipline.** The global stylesheet now keeps tokens, base layout, app shell, Material baseline overrides, and shared primitives, while luxury route CSS lives in lazy route-group assets. Future UI phases should keep route-specific CSS out of `src/styles.scss`.

6. **Operational screens need denser layouts.** Admin and seller screens should be quiet, table-heavy, filterable, and optimized for repeated work. Avoid making these screens look like marketing pages.

7. **Buyer trust is underdeveloped.** Checkout, product detail, seller storefronts, returns, and payment states need visible trust cues: verified seller, secure checkout copy, return policy, delivery expectations, support path, and price/fee clarity.

8. **State design is inconsistent.** Loading, empty, error, permission denied, validation error, and success states exist in several places but are not standardized.

## Blueprint Adjustments Recommended

- Move buyer AI shopping assistant and visual search to **Later/Beta** in delivery priority, even though routes already exist. Core catalog, product detail, cart, and checkout trust should come first.
- Move seller ads and seller analytics to **Later/Beta** for UI polish. The routes exist, but seller orders, fulfilment, returns, and payouts matter more for marketplace operations.
- Treat finance/admin payout/refund/dispute/order/payment UIs as **MVP admin operations** because the backend has already been hardened. Refunds, payouts, disputes, and order/payment read screens are API-backed; write workflows should stay narrow and policy-led.
- Split seller onboarding and seller dashboard. The current `/seller` page mixes setup and workspace entry; verified sellers should land on a dashboard, while incomplete sellers should see onboarding/status.
- Add a lightweight design-system phase before implementing more screen-specific UI. Without it, new screens will amplify the existing global-style sprawl.

## Recommended Implementation Sequence

### Phase 1: UI Foundation

- Clean encoding artifacts in Angular templates.
- Replace manual text symbols with Angular Material icons where applicable.
- Add shared UI primitives for badges, alerts, empty states, page headers, product cards, data tables, dashboard cards, and step panels.
- Split navigation into public, buyer, seller, and admin patterns with mobile-safe behavior.
- Keep the global stylesheet for tokens and base layout only; move component-specific styles closer to shared components over time.

### Phase 2: Buyer Marketplace Journey

- Rework the home page into a buyer-first marketplace entry point with search, categories, product imagery, and seller trust.
- Improve shop filters, sort, mobile filter behavior, product cards, and empty states.
- Improve product detail with gallery, seller trust, return/shipping cues, variant clarity, and wishlist entry point.
- Improve seller storefront with logo/profile, seller verification cues, policies, and product merchandising.

### Phase 3: Cart And Checkout Trust

- Add product imagery and clearer stock/variant details to cart.
- Convert checkout into visible steps: address, delivery, payment, review.
- Add delivery and payment placeholders only where backend support is still pending, but avoid copy that exposes internal prompt history.
- Strengthen success and failure pages with clear next actions.

Status: Implemented as frontend trust polish and later deepened in Phase 9E. The cart now uses product image metadata from the cart API when available, falls back to professional visuals, and supports save-for-later moves into the buyer wishlist. Checkout delivery selection and carrier-aware tracking exist; payment provider UI and real carrier-provider tracking remain separate feature work.

### Phase 4: Seller Operations

- Separate seller onboarding/status from the verified seller dashboard.
- Improve product list and editor with thumbnails, filters, stronger stepper UX, save/submission state, and rejection feedback.
- Build seller order, fulfilment, returns, payout/balance, storefront settings, and support ticket UI before further ads/analytics polish.

Status: Ops-first implementation completed and extended through Phase 10N. Verified sellers now get a dashboard with a live seller-owned operational summary and shared seller workspace navigation. API-backed seller orders, order detail/fulfilment actions, selected delivery-method snapshots, checkout-time seller-policy snapshots, ready-to-ship, manual tracking, provider-neutral carrier booking/tracking, delivery confirmation, delivery exception recording, returns with policy context and explicit seller restock decisions, payout/balance history, support ticket screens, dedicated inventory management with bulk CSV stocktake tooling, SKU/barcode lookup, seller-visible stock movement history and reservation/order/return/refund/restock stock-ledger context, seller-managed delivery methods/rates, buyer-facing store policies, store settings, payout-profile change requests, seller transactional notifications, and product merchandising/SEO fields exist. The product list now includes thumbnails, merchandising labels, inventory summaries, and public previews. The product editor now has clearer status/read-only messaging, step completion, production-hardened media uploads, image metadata/gallery controls, variant edit mode, SEO/search preview, buyer-facing attribute preview, product care/disclaimer guidance, honest shipping guidance, a buyer-facing review preview, published-product listing revisions, published variant/pricing revisions, bulk CSV variant revision staging, and seller-facing moderation history. Real carrier-provider integration, hardware barcode scanning, and full sensitive payout-bank storage remain later work.

### Phase 5: Admin Operations And Finance

- Replace admin placeholders for orders, payments, refunds, disputes, and payouts with real queue/detail surfaces.
- Apply finance-role and dual-control UX to refund/payout actions so users can see why an action is unavailable.
- Improve seller/product review detail screens with evidence panels, risk flags, audit history, and decision clarity.
- Keep admin pages dense and operational, with filters, status tags, action panels, and audit context.

Status: Phase 5A implemented for admin finance operations. `/admin/refunds`, `/admin/payouts`, and `/admin/disputes` now use existing backend APIs. Finance navigation is visible to `Admin`, `SuperAdmin`, `FinanceOperator`, and `FinanceApprover` where appropriate, while dispute routes remain admin-only.

Status: Phase 5B implemented for support and catalog-reference operations. `/admin/support` and `/admin/support/:ticketId` now use the support-ticket APIs for list/detail, public replies, internal notes, resolve, and close.

Status: Phase 5C implemented for moderation review polish. Admin sections now share a lightweight workspace navigation component. Seller and product moderation queues use client-side filters, denser review rows, shared page headers/status badges/alerts/empty states, and clearer action links. Seller detail review has completeness indicators and reorganized profile/storefront/address/payout/audit panels. Product detail review has larger image review, thumbnail selection, fallbacks, variant stock summaries, seller context, AI risk display, and unchanged approve/reject/change-request payloads. Audit logs use the shared admin navigation and improved filter/empty/error presentation.

Status: Phase 5D implemented for admin order/payment investigation. `/admin/orders`, `/admin/orders/:orderId`, `/admin/payments`, and `/admin/payments/:paymentId` now use FinanceRead-protected backend APIs and replace the previous API-gap pages. The screens are intentionally read-only and do not add manual order mutation, payment capture, or raw webhook-payload exposure.

Status: Phase 9C implemented for admin catalog operations. `/admin/categories` now uses admin category and attribute write APIs for create/edit/activate/deactivate flows, shows product/child/attribute counts, selected-category attribute management, safety messaging, and backend validation errors. The backend prevents parent cycles, duplicate slugs/keys, and breaking attribute edits where existing product data would be invalidated. Hard delete, bulk import, drag-and-drop reordering, SEO fields, and taxonomy versioning remain future work.

### Phase 6: Growth Features

- Polish buyer AI assistant and visual search after core shopping conversion paths are reliable.
- Polish seller ads, campaign creation, seller analytics, and admin ad review after seller fulfilment and payout UX is usable.
- Add advanced personalization only after product discovery and checkout are stable.

Status: Phase 6A implemented as buyer account operations before growth polish. `/account` now summarizes buyer orders, returns, disputes, and support. `/account/orders`, `/account/orders/:orderId`, `/account/returns`, `/account/returns/:returnRequestId`, `/account/disputes`, `/account/support`, and `/account/support/:ticketId` use existing buyer APIs. Wishlist, reviews, notifications, and growth-feature redesign remain separate buyer-engagement work.

Status: Phase 6B implemented as buyer AI discovery polish. `/assistant` and `/visual-search` now use shared UI primitives, clearer marketplace copy, improved loading/error/empty states, extracted intent/attribute panels, improved product result cards, assistant example prompts, visual-search upload preview, and client-side image type/size validation. The screens continue to call only the existing backend buyer AI endpoints.

Status: Phase 6C implemented as frontend bundle hygiene. The root app shell no longer imports Angular Material button/toolbar modules, trimming the initial bundle slightly. The initial budget warning was recalibrated from `500kB` to `650kB` because the current SSR Angular app already carries shared Angular runtime chunks and a large global stylesheet.

Status: Phase 9S implemented as style split and bundle hygiene. Route-specific luxury editorial CSS now lives in lazy static route-group assets loaded by public/buyer/seller/admin style carriers and workspace navigation components. The initial bundle warning is cleared without raising budgets: initial total moved from `734.14 kB` to `643.13 kB`, and the global styles bundle moved from `193.93 kB` to `102.03 kB`.

### Phase 7: Buyer Engagement

- Add real buyer-facing UI for wishlist management, verified-purchase reviews, public product review summaries/lists, and in-app notifications.
- Keep recommendation logic and email/push notification delivery separate from the first UI pass.

Status: Phase 7A implemented as backend foundation. Wishlist, verified-buyer product reviews, public review reads, notification persistence, notification read-state APIs, EF migration, and backend tests exist.

Status: Phase 7B implemented as buyer engagement UI. `/account/wishlist`, `/account/reviews`, and `/account/notifications` are buyer-guarded routes. Product cards and product detail can save products to the wishlist. Product detail shows public review summary/list data, and delivered order detail can create verified-purchase reviews. Phase 9E later added wishlist-aware initial card/detail state and saved-for-later/cart bridging. Phase 9M added buyer transactional email delivery through a backend outbox, Phase 9Q added SignalR live notification delivery for buyers, Phase 9V added seller transactional notification/email delivery for moderation outcomes, and Phase 10A added seller notification preferences plus realtime seller delivery. SMS, push, and admin email workflows remain future work.

Status: Phase 7C implemented as review moderation and in-app engagement notifications. New buyer reviews and edited published/rejected reviews now move through `PendingReview`. `/admin/reviews` provides an admin moderation queue with evidence context and approve/reject/remove actions. `/account/reviews` shows pending/rejected/published state and rejection reasons. In-app notifications are created for review decisions, seller return responses, support public replies, order tracking updates, and shipped orders.

Status: Phase 9M implemented buyer email notification delivery. Existing buyer lifecycle notifications now respect separate in-app/email category preferences, can queue hidden email-only notification rows, and are delivered by the worker through local `LogOnly` or configured SMTP providers. `/account/settings` exposes both channel toggles.

Status: Phase 9Q implemented buyer real-time in-app notifications, and Phase 10A widened the same hub to sellers. Persisted in-app-visible buyer and seller notifications publish best-effort SignalR events, read/read-all HTTP mutations publish sync events, the app shell shows unread badges/latest-notification toasts, and notification pages deduplicate live entries against REST state. REST remains the source of truth; SMS, push, and admin notification channels remain future work.

Status: Phase 9V implemented seller transactional notifications and moderation-history polish. Admin seller verification, product moderation, published listing revision, and ad review outcomes now create best-effort seller in-app notifications and optional seller transactional email outbox rows. Phase 10A added seller-managed in-app/email preferences, realtime seller SignalR delivery, app-shell seller toasts, and live `/seller/notifications` sync. SMS/push and admin email workflows remain future work.

Status: Phase 9E implemented as buyer saved-state polish. Wishlist product ids hydrate product-card/detail saved state, `/account/wishlist` supports variant/quantity controlled move-to-cart, `/cart` shows product image metadata and save-for-later actions, and backend bridge endpoints keep cart/wishlist moves atomic.

Status: Phase 9H implemented saved delivery-address/order snapshots, Phase 9J added delivery instructions, Phase 9L added seller-managed checkout delivery rates, Phase 9O added provider-neutral carrier booking/tracking metadata to order screens, Phase 9P added local address verification plus platform-managed pickup points, and Phase 9R documented real carrier-provider comparison. `/account/settings` manages saved delivery addresses with default selection and verification previews, `/checkout` can use a saved address or inline one-off address, show verification warnings, load matching seller delivery methods and pickup choices, and include the selected rate in the order total. Buyer/seller/admin order detail screens show persisted delivery-address verification, delivery-method snapshots, pickup snapshots, and carrier context where present. External address verification/geocoding, pickup-network APIs, carrier-provided rates, and real carrier-provider integrations remain future work.

### Phase 8: Checkout Closure

Status: Phase 8A implemented as checkout lifecycle closure. `/checkout` now creates or reuses a pending order, initiates backend payment, and redirects to the persisted provider checkout URL. `/checkout/success`, `/checkout/failed`, and `/account/orders/:orderId` show order/payment state and retry pending payments. Seller order detail supports manual delivery confirmation, which unlocks existing delivered-order return/review flows without auto-releasing payouts.

## Immediate Next Implementation Candidates

The highest-value next implementation candidates are:

1. Run the manual buyer-flow checklist in `docs/buyer-flow-test-runbook.md` against the seeded demo catalog.
2. Use the completed Phase 10Q seller browser sign-off as the baseline for future seller/admin UI regressions; rerun it after larger seller workflow changes.
3. Implement the next seller depth candidate: seller notification analytics, public seller acquisition analytics/content management, richer storefront attribution/source segmentation, or all-state admin seller/product/ad operational queues.
4. Complete manual desktop/mobile visual spot checks after the Phase 9S style split, especially `/`, `/checkout`, `/account`, `/assistant`, `/seller/products`, `/seller/notifications`, and `/admin/payments`.
5. Run PayFast sandbox verification before production payments when credentials and callback URL are available.
6. Choose a real production media scanner adapter before enabling strict production scanner readiness.
7. Implement the first real carrier adapter after Bob Go API docs/sandbox credentials are available, or use the documented PUDO fallback if Bob Go access is blocked. Carrier-provided rate calculation, external address verification/geocoding, pickup-network APIs, and SMS/push notifications remain separate candidates.
8. Deeper payout hardening can revisit encrypted/tokenized bank-detail storage after a real external payout provider is selected.
9. If stricter performance targets are needed, minify/cache the lazy static route CSS assets or split them further by route cluster; do not raise the initial budget to hide regressions.

This keeps UI work tied to real contracts instead of creating fake surfaces that would need to be unwound later.

## Verification For Future UI Work

For any UI implementation pass:

```powershell
cd frontend\swyftly-web
cmd /c npm run build
cmd /c npm run test:ci
```

For visual work, also run the Angular app locally and check desktop and mobile widths for:

- no overlapping text
- no broken or mojibake symbols
- usable mobile navigation
- visible loading, empty, and error states
- product imagery or professional fallbacks
- checkout and finance copy that does not expose internal implementation notes

## Assumptions

- The blueprint is directional and can be optimized where the codebase has evolved.
- MVP UI priority is buyer trust, product discovery, checkout clarity, seller operations, and admin finance workflows.
- The original audit was documentation-only; later phases have since implemented the tracked Angular UI and bundle-hygiene work.
- A broad frontend folder restructure should still wait until there is a concrete maintenance issue that the current shared primitives and lazy style assets cannot solve.
