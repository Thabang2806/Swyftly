# Buyer Flow Test Runbook

Use this runbook to test the local buyer journey with realistic development data.

## Seed Data

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords -ApplyMigrations -SeedSampleProducts
```

The seed is idempotent. It reuses the existing development users and matches sample products by seller id plus slug.

## Seeded Accounts

| Email | Role | Notes |
|---|---|---|
| `buyer@mabuntle.local` | Buyer | Includes a default saved delivery address when sample products are seeded. |
| `seller@mabuntle.local` | Seller | Verified seller with published storefront, seller balance, standard delivery method, and sample products. |
| `admin@mabuntle.local` | SuperAdmin, Admin | Available for admin checks if needed. |

Use the password you passed to the seed script.

## Seeded Product Routes

| Product | Route |
|---|---|
| Rose Linen Midi Dress | `/product/rose-linen-midi-dress` |
| Ivory Silk Wrap Blouse | `/product/ivory-silk-wrap-blouse` |
| Black Structured Leather Tote | `/product/black-structured-leather-tote` |
| Champagne Mini Crossbody Bag | `/product/champagne-mini-crossbody-bag` |
| Gold Polished Hoop Earrings | `/product/gold-polished-hoop-earrings` |
| Silver Stacking Ring Set | `/product/silver-stacking-ring-set` |
| Hydrating Cream Cleanser | `/product/hydrating-cream-cleanser` |
| Soft Matte Foundation | `/product/soft-matte-foundation` |

Seller storefront route: `/seller/mabuntle-dev-store`.

## Manual Buyer Flow Checklist

1. Register a new buyer at `/register/buyer`; confirm the success screen links back to `/login`.
2. Login as `buyer@mabuntle.local`.
3. Browse `/`, `/shop`, product detail pages, category pages, and `/seller/mabuntle-dev-store`.
4. Save at least one product to wishlist from a product card or product detail.
5. Open `/account/wishlist`, select a variant, and move a saved product to cart.
6. Open `/cart`, update quantity, remove an item, and save one cart item for later.
7. Start checkout from `/cart`.
8. Confirm the seeded saved address is selected by default.
9. Check delivery options and select the seeded seller delivery method.
10. Start checkout and verify payment initiation redirects through the configured fake/provider checkout URL.
11. Visit `/checkout/success?orderId=...` or `/checkout/failed?orderId=...` and confirm copy states that paid status is webhook-confirmed.
12. On checkout result and order-detail pages, verify the payment summary, refresh action, retry behavior for pending payment, and cancelled/failed guidance.
13. Requote delivery after changing a one-off manual address; the previous delivery total should clear before checkout can start.
14. Visit `/account`, `/account/settings`, `/account/orders`, `/account/wishlist`, and `/account/notifications`.

## Post-Purchase Demo State

Phase 11A adds a local helper that creates a delivered buyer order through existing APIs, not through direct database mutation.

Prerequisites:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords -ApplyMigrations -SeedSampleProducts
```

Start the API with the local `Fake` payment provider and the development webhook signing secret, then run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create-buyer-post-purchase-demo.ps1 -Password "UseYourOwnDevPassword1!"
```

The helper:

1. Logs in as `buyer@mabuntle.local`.
2. Loads `/api/products/rose-linen-midi-dress` and selects an in-stock variant.
3. Adds the variant to the buyer cart.
4. Quotes seller delivery with a Johannesburg address.
5. Creates an order through `/api/orders/from-cart`.
6. Initiates payment through `/api/payments/initiate`.
7. Posts a signed `Fake` paid webhook to `/api/payments/webhook/Fake`.
8. Logs in as `seller@mabuntle.local`.
9. Marks the order processing, ready to ship, tracked, shipped, and delivered through seller order APIs.
10. Prints the order, payment, and browser URLs needed for QA.

Use the printed order id to verify:

| Area | Route | Expected check |
|---|---|---|
| Orders | `/account/orders` | Delivered order appears with paid payment status context. |
| Order detail | `/account/orders/{orderId}` | Payment summary, delivery address, shipment timeline, tracking, return action, and review action are visible. |
| Returns | `/account/returns` and `/account/returns/{returnRequestId}` | Buyer can create and inspect a return after delivery. |
| Reviews | `/account/reviews` | Buyer can submit a verified-purchase review, with moderation-first copy. |
| Notifications | `/account/notifications` | Order/shipment/return/review/support notifications are visible when those actions occur. |
| Support | `/account/support` | Buyer can create a ticket with linked order context. |
| Disputes | `/account/disputes` | Empty or linked dispute states are readable and do not imply automatic refunds. |

The helper is safe to rerun. Each run creates a new cart/order/payment through the same APIs the browser uses.

Phase 11B post-purchase checks:

- On `/account/orders/{orderId}`, use the Contact support action and confirm `/account/support` opens with `orderId` and `sellerId` query parameters.
- On `/account/support`, confirm linked order and seller fields are prefilled from those parameters, with an editable order-support subject.
- On delivered order detail, confirm return quantity guidance shows the selected line-item cap before submitting.
- On `/account/disputes`, confirm the empty state distinguishes formal dispute cases from return escalations that remain on the return detail.

Phase 11C browser sign-off checks:

- Check the post-purchase routes at desktop `1440px` and mobile `390px` or `430px`.
- After opening `/account/support?orderId={orderId}&sellerId={sellerId}`, navigate back to `/account/support` and confirm the query-prefilled context clears instead of sticking to the plain support route.
- If a return detail exists, use Contact support from `/account/returns/{returnRequestId}` and confirm the linked support route includes the return order and seller context.
- Confirm a delivered-order review submits into pending moderation, not directly public status.
- Record any remaining route/action blocker in `docs/buyer-post-purchase-qa-results.md` with route, viewport, account, and order id.

Phase 11D buyer account trust polish/action sign-off:

- Follow the planning-agent workflow before running the final sign-off, and keep the pass scoped to buyer account trust/actions unless a blocker requires a separate implementation task.
- Create a fresh delivered order with `scripts\create-buyer-post-purchase-demo.ps1`, or reuse a known delivered helper order if it is still valid.
- Record the final `orderId`, `paymentId`, `providerReference`, `returnRequestId`, and `supportTicketId` in `docs/buyer-post-purchase-qa-results.md`.
- Create a return from the delivered order and confirm the return detail route shows submitted items, quantity caps, seller response state, and order/seller support handoff.
- Create an order-linked support ticket and confirm the support detail route shows linked order/seller context and public message history.
- Recheck orders, returns, support, notifications, and disputes at desktop and mobile widths for readable trust copy, no horizontal overflow, and no API-error state.

Phase 11D was completed with delivered order `491490cb-39d3-4511-b383-fcfc34eb734b`, return request `e6ea57b0-dd8c-431c-aa9c-f7c9c682acd5`, and support ticket `0fb110e2-41de-4738-af38-ebb949e21592`. Reuse this sequence for future account-trust regression checks.

Phase 11E buyer refund visibility checks:

- Use the delivered-order helper, then create a refund through the existing admin finance screens/APIs when refund state is needed.
- Confirm `/account/refunds` lists only buyer-owned refunds and shows buyer-safe status/timeline copy.
- Confirm `/account/orders/{orderId}` and `/account/returns/{returnRequestId}` show refund panels without exposing finance notes or provider payloads.
- Confirm `/account` surfaces a next-action prompt for requested, approved, processing, or failed refunds.
- Confirm `/account/disputes` explains that buyer-favoured dispute outcomes may create refund requests that still require finance processing.

Phase 11F refund-state sign-off:

- Completed with order `71583d35-cdfc-43f3-88d6-14b3b5c78f5b`, return `5774b963-0aad-4477-8fb7-ec1a723ff3cd`, and refund `6f00614d-fe12-4fdf-a4c7-52da46f33393`.
- Use `finance.operator@mabuntle.local` to create refund requests and `finance.approver@mabuntle.local` to approve them; finance dual control rejects approval by the same actor.
- For return-linked refund QA, create the return from the buyer order detail first, approve it as the seller, then create the refund through `/api/admin/returns/{returnRequestId}/refunds`.
- The latest desktop/mobile evidence is recorded in `docs/buyer-post-purchase-qa-results.md` under Phase 11F.

Phase 11G buyer growth depth checks:

- Login as `buyer@mabuntle.local` before opening `/assistant` and `/visual-search`; both growth routes remain buyer-authenticated secondary flows.
- On `/assistant`, submit a few fashion/beauty/accessory prompts and confirm the recent prompt chips are browser-local only, capped at six entries, and removable with Clear. Refreshing the browser may keep local recents, but there is no backend prompt-history list to verify.
- Confirm assistant confidence copy explains why products matched the current prompt, using buyer-safe signals such as category, attributes, style, colour, material, price, or occasion.
- Use the assistant shop handoff and confirm it routes to `/shop` with query parameters for the current discovery intent instead of requiring a persisted recommendation record.
- On `/visual-search`, upload or reference an image, confirm client-side recent text references are browser-local only, capped at six entries, and removable with Clear. Verify uploaded image data, previews, and base64 content are not stored in local recent history and no persisted backend visual-search history appears in buyer account screens.
- Confirm visual-search confidence copy explains the current reference interpretation and product match reasons without exposing internal prompts, embeddings, or provider payloads.
- Use the visual-search shop handoff and confirm `/shop` opens with query parameters representing the interpreted search context.

## Catalog Confidence Checklist

For Phase 10Z catalog discovery/product confidence, use the existing public APIs and seeded products only:

1. On `/shop`, confirm search, sort, pagination, existing filters, product-card imagery/fallbacks, wishlist state, prices, seller/category labels, and empty/error states.
2. On `/category/dresses`, confirm the page clearly communicates the active category and reuses the same product-card confidence signals.
3. On `/product/rose-linen-midi-dress`, confirm gallery/fallback behavior, variant confidence, add-to-cart/wishlist actions, seller link, public review summary/list, and available policy/delivery/returns snippets.
4. On `/seller/mabuntle-dev-store`, confirm seller verification/trust copy, policy snippets, product grid confidence, and storefront empty/loading/error behavior.
5. Avoid adding backend-only expectations during this pass. Server facet counts, storefront product pagination, aggregate seller ratings, and dedicated similar-products APIs are deferred backend work.

## API Smoke Checks

With the API running locally:

```powershell
curl https://localhost:7268/api/products/search
curl https://localhost:7268/api/products/rose-linen-midi-dress
```

Checkout shipping options require an authenticated buyer session, so verify that path through the Angular UI or Swagger after logging in.

Recommended authenticated smoke path:

1. Login as `buyer@mabuntle.local`.
2. Load `/api/products/rose-linen-midi-dress` and select an active variant.
3. Add the product to the wishlist.
4. Move the wishlist item to cart.
5. Load the seeded default delivery address.
6. Call `/api/cart/shipping-options` with the active cart and saved address.
7. Create an order with `/api/orders/from-cart`.
8. Confirm `/api/cart` returns no active items after order creation.
9. Add another seeded product to cart and confirm a new active cart is created.

The cart-after-order check is important: order creation now marks the source cart as `CheckedOut`, while duplicate order creation for the same cart id still returns the existing pending-payment order.

## Expected Boundaries

- The seed does not create carts, orders, payments, returns, reviews, or wishlists.
- Fake payment remains local/provider-neutral. Paid status still requires the existing signed webhook flow.
- Paid, delivered, return, review, refund, and dispute paths should be created through existing payment webhook, seller fulfilment, buyer return/review, and admin finance/support workflows rather than through hidden seed shortcuts.
- Product images are local static SVG assets under `frontend/mabuntle-web/public/assets/sample-products`.

## Latest Audit Evidence

Phase 10X results are recorded in `docs/buyer-flow-gap-audit-results.md`. That pass fixed a checkout cart lifecycle blocker and confirmed the seeded buyer can move wishlist items to cart, quote shipping, create a pending-payment order, and start a fresh cart afterwards.

Phase 10Y added checkout confidence checks: stateful checkout progress, delivery-quote reset after manual address edits, safe payment summaries on buyer order reads, result-page refresh and short pending-payment polling, and clearer failed/cancelled/payment-pending copy.

Phase 10Z implemented the catalog discovery/product confidence pass using existing public product/category/seller/review APIs. Verify removable `/shop` filter chips, category-scoped filters, seller storefront client-side filtering, product-card tags, product trust/policy copy, out-of-stock add-to-cart copy, and the same-category related-products rail. Backend facets, storefront pagination, seller rating aggregation, and dedicated similar-products endpoints remain deferred.

Phase 11A added `scripts/create-buyer-post-purchase-demo.ps1` for repeatable paid-and-delivered buyer order context. Use it before testing return, review, support, notification, and dispute states that require a delivered order.

Phase 11C completed the desktop/mobile browser route sign-off for the delivered-order post-purchase routes and fixed two small support handoff issues. A final human click-through of return creation and support-ticket creation remains useful because the headless route-action script did not reliably submit those two forms, even though component/API coverage and route rendering passed.

Phase 11D completed buyer account trust polish/action sign-off after following the planning-agent workflow. The final order/return/support identifiers and desktop/mobile route notes are recorded in `docs/buyer-post-purchase-qa-results.md`.

Phase 11F completed refund-state browser sign-off with a real return-linked refund created and approved through existing admin finance APIs. The next buyer phase can move to buyer growth depth for `/assistant` and `/visual-search`.

Phase 11G implemented buyer growth depth for `/assistant` and `/visual-search`: browser-local recent prompts/references only, clear-history/reset controls, confidence explanations on current results, and `/shop` handoff through query parameters. Later phases added sanitized feedback telemetry, opt-in server-side safe history summaries, aggregate outcome attribution, and opt-in explainable AI personalization. Richer visual similarity diagnostics and broader ranking experiments remain deferred.

Phase 11H buyer growth telemetry checks:

- Login as `buyer@mabuntle.local`, open `/assistant`, submit a prompt, open a returned product, use the shop handoff, and submit one structured usefulness reason. These actions should not block navigation if telemetry fails.
- Repeat the same flow on `/visual-search` with a text reference or uploaded test image. Confirm recent text references remain browser-local and uploaded image data/previews are not persisted in local history.
- As `admin@mabuntle.local`, open `/admin/reports` and confirm the buyer AI discovery section shows aggregate search, product-open, shop-handoff, feedback, context, and trend data.
- API spot check: `POST /api/buyer/growth-events` requires a buyer token and rejects admin/seller tokens. `GET /api/admin/reports/buyer-growth?bucket=Day` requires Admin or SuperAdmin and must not return buyer ids, raw prompts, image/base64 content, provider payloads, or full AI responses.

Phase 11I AI discovery history checks:

- Login as `buyer@mabuntle.local`, open `/account/settings`, confirm AI discovery history is off by default, and confirm the copy says prompts, images, previews, base64 content, provider payloads, and full AI responses are not stored.
- Enable AI discovery history in settings, then submit one `/assistant` search and one `/visual-search` search.
- Open `/account/ai-history` and confirm rows show only source tool, derived category/colour/material, confidence, result count, product links where visible, source route, and repeat-in-shop handoff.
- Delete one row, then use Clear server history and confirm browser-local recent prompt/reference chips remain controlled only from `/assistant` and `/visual-search`.
- API spot check: `GET /api/buyer/ai-discovery/history` requires a buyer token and returns only that buyer's rows. It must not expose raw prompts, uploaded image data, previews, provider payloads, or full AI response JSON.

Phase 11J AI discovery outcome-attribution checks:

- Login as `buyer@mabuntle.local`, use `/assistant` or `/visual-search`, open a returned product, add that product to cart, quote shipping in `/checkout`, create an order, initiate fake payment, and post the signed fake paid webhook through the existing local helper path when possible.
- As `admin@mabuntle.local`, open `/admin/reports` and confirm the buyer AI discovery section includes outcome funnel cards and source-tool rows for product opens, cart adds, checkout starts, orders, and paid orders.
- API spot check: `GET /api/admin/reports/buyer-growth?bucket=Day` must remain aggregate-only and must not include buyer ids, raw prompts, uploaded image/base64 data, provider payloads, or full AI responses.
- Attribution is best-effort. Missing rows are acceptable when the buyer did not recently open or hand off from an AI discovery result within the 7-day attribution window.

Phase 11K AI discovery attribution QA helper:

- Seed sample products, start the local API with the `Fake` payment provider, then run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create-buyer-ai-attribution-demo.ps1 -Password "UseYourOwnDevPassword1!" -SkipCertificateCheck
```

- Use `-AssistantOnly` or `-VisualOnly` to isolate one source tool.
- The helper uses only existing APIs: buyer login, assistant/visual-search, buyer growth telemetry, cart add, shipping quote, order creation, fake payment initiation, signed fake paid webhook, and admin buyer-growth report read.
- Confirm the helper output shows product opens, cart adds, checkout starts, orders, and paid orders in the aggregate report summary, with source rows for Assistant and/or VisualSearch.
- Open `/admin/reports` at desktop `1440px` and mobile `390px` or `430px`; confirm buyer-growth outcome cards and source-tool rows are readable and remain aggregate-only.
- For a no-attribution control, use a clean database or a buyer without recent AI telemetry. The 7-day attribution window means an ordinary cart/order by the same buyer can still attribute to a recent AI shop handoff.

Phase 11L AI personalization boundary checks:

- Login as `buyer@mabuntle.local`, open `/account/settings`, and confirm AI discovery history and Personalized AI discovery are separate controls and both default off for buyers without saved preferences.
- With personalization off, use `/assistant` and `/visual-search`; product cards should not show Personalized badges or `why recommended` reason chips.
- Save a product to wishlist or add a product to cart, enable Personalized AI discovery, then repeat an assistant or visual-search query that returns the same product/category.
- Confirm result cards can show only buyer-safe reasons such as `Similar to saved items`, `Matches recent cart interest`, or `Aligned with your enabled AI history`.
- Confirm browser-local recent prompts/references remain controlled only from `/assistant` and `/visual-search`, and clearing server-side AI history does not clear those local chips.
