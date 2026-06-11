# Phase 10X Buyer Flow Gap Audit Results

Date: 2026-05-27

## Scope

This pass focused on the seeded buyer journey from browse to wishlist, cart, checkout, and account surfaces. It was intentionally audit-led: small existing-flow defects could be fixed, while larger product gaps were documented as follow-up phases.

Out of scope: PayFast/payment-provider changes, real carrier-provider integration, external address verification, SMS/push, marketing automation, and hidden shortcuts for paid/delivered order states.

## Seed Baseline

Command used:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords -ApplyMigrations -SeedSampleProducts
```

Seeded accounts:

| Account | Role | Purpose |
|---|---|---|
| `buyer@mabuntle.local` | Buyer | Buyer flow smoke testing with default saved delivery address. |
| `seller@mabuntle.local` | Seller | Verified seller with published sample products and delivery method. |
| `admin@mabuntle.local` | Admin/SuperAdmin | Optional post-purchase/admin workflow setup. |

## Smoke Coverage

Public route DOM smoke checked these routes and confirmed they rendered nonempty page content:

| Route | Result | Notes |
|---|---|---|
| `/` | Pass | Home route rendered. |
| `/shop` | Pass | Catalog route rendered. |
| `/category/dresses` | Pass | Category route rendered. |
| `/product/rose-linen-midi-dress` | Pass | Seeded product route rendered. |
| `/seller/mabuntle-dev-store` | Pass | Seeded seller storefront route rendered. |
| `/cart` | Pass | Empty/unauthenticated cart surface rendered. |
| `/checkout` | Pass | Checkout route rendered and guards/API state remained non-fatal. |
| `/login` | Pass | Auth route rendered. |
| `/register/buyer` | Pass | Buyer registration route rendered. |
| `/assistant` | Pass | Buyer growth route rendered. |
| `/visual-search` | Pass | Buyer growth route rendered. |

Authenticated buyer API smoke after the fix:

| Step | Result | Evidence |
|---|---|---|
| Login as `buyer@mabuntle.local` | Pass | JWT session created. |
| Load seeded product detail | Pass | `rose-linen-midi-dress` returned product and variant ids. |
| Add product to wishlist | Pass | Wishlist item returned. |
| Move wishlist item to cart | Pass after fix | Returned cart with one item. |
| Load default buyer address | Pass | Seeded Johannesburg address returned. |
| Quote shipping options | Pass | Standard seller delivery method returned. |
| Create order from cart | Pass after fix | Pending-payment order created with server-computed total. |
| Read cart after order creation | Pass after fix | No active cart returned, because the order-linked cart is now checked out. |
| Add another item after checkout | Pass after fix | A new active cart was created for the buyer. |

## Fixed Defects

| Severity | Area | Finding | Fix |
|---|---|---|---|
| Blocker | Cart/order lifecycle | `POST /api/orders/from-cart` left the source cart `Active`, so completed checkout attempts could leave order-linked active carts behind. Later `DELETE /api/cart` failed on the order foreign key, and buyers could be blocked from a clean new cart. | Added `CheckedOut` cart status, mark source carts checked out during order creation, and migrated existing order-linked active carts to `CheckedOut`. |
| Blocker | Cart item persistence | Adding a new item to an existing active cart could be treated as an EF update for a missing row because cart/cart-item ids are client-generated GUIDs but were not explicitly configured as `ValueGeneratedNever`. | Configured `Cart.Id` and `CartItem.Id` as non-generated keys. |
| High | Cart uniqueness | The old cart uniqueness model included status but did not clearly enforce the one-active-cart rule after checkout. | Replaced it with a filtered unique buyer index for `Status = 'Active'`. |
| Medium | Order idempotency | Repeating order creation for a cart that had just become checked out needed to keep returning the existing pending order. | Order creation now checks for an existing pending order by requested `cartId` before requiring an active cart. |
| Medium | Checkout failed copy | Cancelled payment-order copy implied the old cart would always still contain the original items. | Updated failed-checkout and buyer order-detail copy to tell buyers to review the cart or add the items again if the cart is empty. |

## Recommendations

### High: Buyer Catalog Discovery And Product Confidence

Improve `/shop`, category, product detail, and storefront discovery:

- mobile filter drawer and better filter chip review;
- stronger category facets and visual category merchandising;
- richer product image gallery and image fallback consistency;
- review/rating prominence and similar product rails;
- clearer seller trust, policy, delivery, and return snippets on product detail.

Phase 10Z implementation note: the catalog-confidence pass stayed frontend/documentation-led and used the public APIs that already exist: product search/detail, category route data, seller storefront detail, wishlist state, cart add, and public review summary/list reads. It improved confidence through removable filter chips, category-scoped filters, storefront client-side filtering, product-card tag metadata, product trust/policy copy, out-of-stock add-to-cart copy, and a same-category related-products rail derived from existing product search.

Defer backend expansion until the existing buyer surfaces are signed off:

- server-computed facet/count metadata beyond the current search filters;
- paged seller storefront product results beyond the current storefront response shape;
- first-class seller rating/review-summary fields where they are not already returned;
- a dedicated similar-products/recommendations endpoint.

### High: Checkout Conversion And Payment-State Clarity

Phase 10Y implemented the first confidence pass for checkout and payment-state clarity:

- `/checkout` progress now reflects address readiness, quoted delivery, selected delivery, and payment start state;
- editing one-off manual address fields after quoting clears the delivery quote and requires a fresh quote;
- buyer order reads expose a safe latest payment summary when one exists;
- success, failed, order-list, and order-detail pages show payment status context and refresh actions;
- success/failed result pages poll pending-payment orders briefly without blocking the buyer;
- failed-payment navigation preserves a short sanitized reason through the `paymentError` query param;
- delivered-order review copy now says reviews are submitted for moderation before appearing publicly.

Remaining follow-up: create a paid/delivered seeded buyer scenario through existing webhook and fulfilment paths so paid, delivered, return, review, refund, and dispute states can be visually signed off in one run.

### Medium: Post-Purchase Demo State And Return/Review QA

The buyer seed intentionally does not create orders, payments, returns, reviews, or refunds. A dedicated buyer QA phase should create paid/delivered order context through existing webhook/admin/seller flows and then verify:

- buyer order detail after paid/delivered transitions;
- return request and return detail;
- verified-purchase review creation/editing;
- refund/dispute visibility and messaging;
- notification/email/realtime buyer signals.

Phase 11A implementation note: `scripts/create-buyer-post-purchase-demo.ps1` now creates this state through the real local API path. It logs in as the seeded buyer, checks out a seeded product, initiates a `Fake` payment, posts a signed fake paid webhook, then logs in as the seeded seller and fulfils the order through delivered state. QA evidence and route checks are tracked in `docs/buyer-post-purchase-qa-results.md`.

Phase 11B implementation note: post-purchase clarity polish added contextual order/seller support links, support ticket prefill from query params, clearer dispute empty-state wording, delivered-return quantity cap guidance, and focused `/account/returns` component coverage. A human desktop/mobile browser pass remains the next sign-off step.

Phase 11E implementation note: buyer refund/dispute outcome visibility is now represented through buyer-safe refund read APIs, `/account/refunds`, refund panels on order/return detail, account next-action prompts, refund notification routing, and clearer dispute copy. Refund creation, approval, provider confirmation, and ledger effects remain admin/finance workflows.

### Medium: Buyer Account Settings And Trust Polish

Settings, addresses, notifications, wishlist, support, and account dashboard exist. Next polish should focus on:

- address verification feedback language;
- notification preference save feedback;
- clearer empty states for orders, returns, reviews, disputes, support, and notifications;
- account dashboard next actions after a pending-payment order exists.

### Later: Growth Feature Depth

Assistant and visual search remain useful secondary flows but should not outrank catalog and checkout confidence.

Phase 11G implementation note: buyer growth depth for `/assistant` and `/visual-search` is intentionally browser-local only. Recent assistant prompts and visual-search text references are kept in `localStorage` for session continuity, capped at six entries, and can be cleared by the buyer. Uploaded image data, previews, product results, and sensitive data are not stored in the recent-history affordances. Confidence explanations stay buyer-facing and derived from the current response context, such as matched category, attributes, style cues, product count, or image/reference interpretation. Shop handoff uses query parameters so buyers can continue discovery in `/shop` without adding a new recommendation backend.

Phase 11H implementation note: buyer AI discovery now records first-party, buyer-authenticated telemetry for search submitted, product opened, shop handoff, and structured usefulness feedback events. The telemetry stores only sanitized event type/source tool, product id where relevant, result count, confidence band, derived category/colour/material context, source route, timestamp, and a reason-code feedback value. It does not persist raw assistant prompts, uploaded images, previews, base64 content, provider payloads, full AI responses, or free-text feedback. Admin reporting is aggregate-only under `/admin/reports` through `GET /api/admin/reports/buyer-growth`.

Phase 11I implementation note: buyer AI discovery history is now opt-in and disabled by default. `/account/settings` exposes privacy controls and clear-all behavior, while `/account/ai-history` lists safe server-side summaries across assistant and visual search. Stored rows contain only source tool, derived category/colour/material, confidence band, result count, returned product ids, source route, and timestamp. Raw prompts, uploaded images, previews, base64 content, provider payloads, full AI responses, and free-text feedback remain excluded. Browser-local recent prompts/references from Phase 11G remain separate and clearable only from their page-level controls.

Phase 11J implementation note: buyer AI discovery outcome attribution now joins sanitized assistant/visual-search activity to later successful shopping outcomes for aggregate reporting only. The attribution service records product-open, add-to-cart, checkout-start, order-created, and signed-webhook-paid outcomes when recent buyer AI activity is available within the documented 7-day window. Outcome writes are best-effort and idempotent; cart, checkout, order, and payment workflows continue even when attribution is unavailable. Reports remain aggregate-only and do not expose buyer identity or raw AI content.

Phase 11K implementation note: `scripts/create-buyer-ai-attribution-demo.ps1` now provides a repeatable API-only QA path for attribution reporting. It uses the seeded buyer and product catalog, records assistant and/or visual-search telemetry through the existing buyer growth endpoint, completes cart, checkout, order creation, fake payment initiation, and signed fake paid webhook through existing APIs, then reads the aggregate admin buyer-growth report. The admin report outcome trend now labels attributed product opens distinctly from raw product-open telemetry. Manual desktop/mobile `/admin/reports` visual evidence remains the final sign-off step when the local API and Angular app are running.

Phase 11L implementation note: buyer AI discovery personalization is opt-in and disabled by default. When enabled, assistant and visual-search responses can lightly reorder already-eligible published/in-stock result cards and show buyer-safe `why recommended` reasons derived only from wishlist, recent cart/order product/category interest, and enabled AI history summaries. Personalization does not change public catalog search, checkout, payment, cart, or AI provider calls, and does not store raw prompts, uploaded images, provider payloads, support/dispute text, payment/refund data, full AI responses, or free-text feedback.

Deferred buyer growth gaps:

- richer personalization experiments beyond the current explicit opt-in AI discovery boundary;
- recommendation loops and broader ranking experiments beyond current published in-stock product matching;
- richer visual-search confidence scoring and similarity diagnostics;
- wishlist folders/sharing;
- SMS/push notifications;

## Deferred Boundaries

- Paid settlement still depends on the signed webhook path.
- Delivered-order returns and reviews require a real order to progress through payment and fulfilment states.
- Refund outcomes require existing admin/finance flows.
- Manual desktop/mobile visual QA was not fully automated in this pass; representative route smoke and authenticated API smoke were completed, and a human browser pass remains recommended before a buyer demo.
