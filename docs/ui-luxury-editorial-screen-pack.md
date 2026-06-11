# Luxury Editorial Screen Pack

## Summary

The active high-fidelity visual target is the deterministic screen pack at `Documentation/UI UX/luxury-editorial-screen-pack`. It covers every current Angular route with desktop and mobile PNGs generated from static HTML/CSS, rather than raw AI UI images, so text, spacing, and route coverage stay consistent.

## Direction

The direction is a Mabuntle-specific luxury editorial hybrid inspired by the broad commerce language of Luxity, Apsley, NET-A-PORTER, and Mytheresa. The mockups use warm ivory surfaces, restrained black typography, champagne rules, plum dashboard navigation, rose accents, editorial product panels, dense marketplace cards, and operational seller/admin workspaces.

No third-party product imagery, brand assets, page layouts, or copy are copied. CSS-generated editorial placeholders are used where live imagery is unavailable.

## Coverage

| Group | Routes | Output |
|---|---:|---|
| Buyer public and catalog | 8 | Desktop/mobile PNGs plus contact sheets |
| Auth | 4 | Desktop/mobile PNGs plus contact sheets |
| Buyer account | 12 | Desktop/mobile PNGs plus contact sheets |
| Checkout | 3 | Desktop/mobile PNGs plus contact sheets |
| Seller workspace | 17 | Desktop/mobile PNGs plus contact sheets |
| Admin workspace | 24 | Desktop/mobile PNGs plus contact sheets |

Route-level coverage is tracked in `Documentation/UI UX/luxury-editorial-screen-pack/route-index.json`.

## Implementation Notes

- Seller and admin screens use a persistent desktop side navigation and mobile stacked workspace navigation.
- Buyer public screens use an editorial commerce header, premium product-grid visuals, and category/search merchandising.
- Checkout, account, seller, and admin screens show representative operational states without inventing unsupported backend workflows.
- Admin and seller screens use a route-specific `screenSpecs` registry so each route shows its own realistic metrics, rows, statuses, and action panel instead of generic dashboard content.
- This phase is design-output only; Angular implementation should follow route group by route group.

## Route Content Matrix

| Route group | Mockup content now shown |
|---|---|
| `/admin` | Marketplace command centre with seller review, product review, finance alert, and support queues. |
| `/admin/sellers`, `/admin/sellers/:sellerId` | Seller verification queue/detail with storefront, contact, completeness, payout placeholder, and approve/reject context. |
| `/admin/products`, `/admin/products/:productId`, `/admin/products/revisions/:revisionId` | Product moderation and revision review with image/listing evidence, seller status, categories, variants, AI risk, and approval actions. |
| `/admin/reviews` | Verified-buyer review moderation with product/order evidence and approve/reject/remove controls. |
| `/admin/orders`, `/admin/orders/:orderId` | Read-only order investigation with buyer/seller, payment, fulfilment, delivery snapshot, and timeline context. |
| `/admin/payments`, `/admin/payments/:paymentId` | Payment reconciliation and webhook-event investigation with provider references, latest review, and no manual settlement path. |
| `/admin/refunds`, `/admin/payouts`, `/admin/payout-profile-changes`, `/admin/disputes` | Finance/dispute queues with role-aware action panels, provider/manual-review messaging, payout blocks, and dual-control reminders. |
| `/admin/support`, `/admin/support/:ticketId` | Support ticket queue/detail with buyer/seller audience, public replies, internal notes, status, and linked operational context. |
| `/admin/categories`, `/admin/pickup-points` | Catalog and pickup-point management with active state, counts, address/province coverage, create/edit panels, and no hard-delete affordances. |
| `/admin/reports`, `/admin/ai-usage`, `/admin/audit-logs`, `/admin/ads`, `/admin/ads/:id` | Route-specific analytics, AI usage, audit event, and ad review content tied to existing admin API surfaces. |
| `/seller` | Verified seller workspace priorities for fulfilment, stock risk, returns, payouts, and growth. |
| `/seller/products`, `/seller/products/new`, `/seller/products/:id/edit` | Product list, draft creation, and edit/revision workflows with listing state, image, variant, readiness, and moderation context. |
| `/seller/inventory` | Stock/reserved/available rows, low/out-of-stock states, CSV export/template/import-preview/bulk-adjust controls. |
| `/seller/orders`, `/seller/orders/:orderId` | Fulfilment queues and detail timelines with delivery snapshots, carrier booking, tracking, exceptions, and seller actions. |
| `/seller/returns`, `/seller/returns/:returnRequestId` | Return queue/detail with buyer reason, item evidence, seller response, dispute handoff, and refund caveat. |
| `/seller/payouts`, `/seller/settings/store` | Seller balance/payout history plus storefront, delivery method, pickup opt-in, and payout-profile change request surfaces. |
| `/seller/support`, `/seller/support/:ticketId`, `/seller/ads`, `/seller/ads/new`, `/seller/ads/:id`, `/seller/analytics` | Route-specific support, campaign, campaign creation/detail, and analytics content instead of generic workspace rows. |

## Verification

Expected generated output:

- `68` desktop route screenshots.
- `68` mobile route screenshots.
- `12` grouped contact-sheet PNGs for buyer public, auth, account, checkout, seller, and admin routes.

Manual review should check:

- no mojibake symbols;
- no copied reference-site assets;
- seller/admin navigation is side-oriented on desktop;
- mobile screenshots remain readable at `390px`;
- route-specific screens do not imply fake backend capabilities;
- admin and seller routes do not share identical generic dashboard content.
