# Swyftly API Contracts

## Health

Responses include an `X-Correlation-ID` header. Callers may provide the same header on the request; otherwise the API generates one.

```http
GET /health
```

Response:

```json
{
  "status": "Healthy",
  "applicationName": "Swyftly.Api",
  "timestampUtc": "2026-05-18T10:00:00.0000000+00:00"
}
```

## Readiness

```http
GET /health/ready
```

Response when healthy:

```json
{
  "status": "Healthy",
  "applicationName": "Swyftly.Api",
  "timestampUtc": "2026-05-18T10:00:00.0000000+00:00",
  "totalDurationMilliseconds": 12.34,
  "checks": {
    "postgresql": {
      "status": "Healthy",
      "description": null,
      "error": null,
      "durationMilliseconds": 12.34
    }
  }
}
```

The readiness endpoint returns HTTP `503` with the same response shape when PostgreSQL or a required dependency check is unavailable. It includes a search placeholder plus real `image-storage`, `payment-provider`, and `email-delivery` configuration checks. The payment check is healthy for the local fake provider and validates required PayFast configuration when `PaymentProvider__ProviderName=PayFast`. The image-storage check validates local storage or S3-compatible storage configuration and enforces the configured production media-scanner policy. The email check is healthy for local `LogOnly` delivery outside production and validates SMTP configuration when `EmailDelivery__ProviderName=Smtp`; production must not use `LogOnly`.

## Authentication

Auth endpoints are under:

```http
/api/auth
```

Public registration supports only `Buyer` and `Seller`. `Admin`, `SuperAdmin`, and `SupportAgent` are reserved roles and cannot be self-assigned.

```http
POST /api/auth/register
```

Request:

```json
{
  "email": "buyer@example.com",
  "password": "Password123",
  "role": "Buyer"
}
```

Response:

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "email": "buyer@example.com",
  "role": "Buyer",
  "sellerVerificationStatus": null,
  "emailVerificationRequired": false
}
```

Seller registration returns `"sellerVerificationStatus": "PendingVerification"`.

```http
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
GET /api/auth/me
```

Login and refresh return a JWT access token and user role data. Refresh tokens are not returned in JSON; the API sets `swyftly_rt` as an HttpOnly cookie scoped to `/api/auth`, plus a non-HttpOnly `swyftly_csrf` cookie for refresh/logout CSRF validation. Cookie path/domain/SameSite/Secure attributes are controlled by the `AuthCookies` configuration section; production validation requires secure cookies, `SameSite=Lax` or `Strict`, and a refresh-token path scoped to `/api/auth`.

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "email": "buyer@example.com",
  "roles": ["Buyer"],
  "accessToken": "<jwt>",
  "accessTokenExpiresAtUtc": "2026-05-18T10:30:00+00:00"
}
```

The API never returns password hashes. Refresh tokens are stored server-side as hashes with a token-family id. Refresh rotation keeps the same family id; replaying an already-revoked refresh token revokes any active replacement token in that family and returns `401`. `POST /api/auth/refresh` and `POST /api/auth/logout` read the refresh token from the cookie and require `X-Swyftly-CSRF` to match the `swyftly_csrf` cookie. Angular stores the access token in memory only and calls refresh with credentials on startup.

Scaffold-only policy check endpoints exist for test coverage:

```http
GET /api/auth/policy-checks/admin
GET /api/auth/policy-checks/seller
```

## Seller Onboarding

Seller onboarding endpoints require a seller JWT role and always operate on the authenticated seller profile.

```http
GET /api/seller/onboarding
PUT /api/seller/onboarding/profile
PUT /api/seller/onboarding/storefront
PUT /api/seller/onboarding/address
PUT /api/seller/onboarding/payout
POST /api/seller/onboarding/submit-verification
```

`PUT /api/seller/onboarding/payout` stores only a payout provider reference placeholder. It does not store bank details or integrate a payment provider.

`POST /api/seller/onboarding/submit-verification` returns `400` until profile, storefront, address, and payout placeholder fields are complete. A successful submission changes the seller verification status to `UnderReview`; admin approval is not part of this endpoint set.

## Admin Seller Approval

Admin seller approval endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/sellers/pending
GET /api/admin/sellers/{sellerId}
POST /api/admin/sellers/{sellerId}/approve
POST /api/admin/sellers/{sellerId}/reject
POST /api/admin/sellers/{sellerId}/suspend
```

`POST /approve` verifies a seller when required onboarding data and the payout placeholder exist. `POST /reject` and `POST /suspend` require a JSON body:

```json
{
  "reason": "Documents are not clear."
}
```

Admin actions write audit-log entries and seller detail responses include `auditTrail`.

Angular admin seller routes `/admin/sellers` and `/admin/sellers/{sellerId}` use these endpoints unchanged. The queue applies client-side search/status/storefront filters over the loaded pending-seller response, while the detail screen presents profile, storefront, address, payout setup, completeness indicators, review actions, and audit trail without changing request payloads.

## Admin Audit Logs

Admin audit-log endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/audit-logs
```

Supported query filters:

- `actionType`
- `entityType`
- `entityId`
- `actorUserId`
- `fromUtc`
- `toUtc`
- `pageNumber`
- `pageSize`, capped at `100`

Response:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "actorUserId": "00000000-0000-0000-0000-000000000000",
      "actorRole": "Admin",
      "actionType": "ProductApproved",
      "entityType": "Product",
      "entityId": "00000000-0000-0000-0000-000000000000",
      "previousValueJson": "{\"status\":\"PendingReview\"}",
      "newValueJson": "{\"status\":\"Published\"}",
      "reason": "Manual review complete.",
      "ipAddress": "127.0.0.1",
      "createdAtUtc": "2026-05-18T10:00:00+00:00"
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 1
}
```

Seller approval/rejection/suspension, product approval/rejection/change-request, payout hold/release, refund approval, dispute resolution, and ad-campaign approval/rejection workflows write audit logs through the shared audit logging service. Future role-change and sensitive admin actions should use the same service.

Angular admin route `/admin/audit-logs` keeps the same query filters and API usage. The UI now uses shared admin navigation, page header, empty/error states, filter submission, and clear behavior.

## Admin Dashboard

Admin dashboard endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/dashboard/summary
```

Response:

```json
{
  "pendingSellerApprovals": 2,
  "pendingProductReviews": 4,
  "newOrdersToday": 6,
  "openDisputes": 1,
  "pendingRefunds": 3,
  "pendingPayouts": 5,
  "totalGrossSalesPlaceholder": 0,
  "platformCommissionPlaceholder": 0
}
```

## Admin Marketplace Reports

Admin marketplace report endpoints require an `Admin` or `SuperAdmin` JWT role. Reports use `fromUtc` and `toUtc` query filters; when omitted, the API defaults to the last 30 days.

```http
GET /api/admin/reports/marketplace?fromUtc=2026-05-01T00:00:00.000Z&toUtc=2026-05-19T00:00:00.000Z
GET /api/admin/reports/marketplace/export.csv?fromUtc=2026-05-01T00:00:00.000Z&toUtc=2026-05-19T00:00:00.000Z
```

Response:

```json
{
  "fromUtc": "2026-05-01T00:00:00+00:00",
  "toUtc": "2026-05-19T00:00:00+00:00",
  "generatedAtUtc": "2026-05-19T10:00:00+00:00",
  "currency": "ZAR",
  "finance": {
    "grossMerchandiseValue": 1200.00,
    "platformCommissionEarned": 120.00,
    "paymentProcessingFees": 36.00,
    "refunds": 150.00,
    "sellerPendingBalances": 300.00,
    "sellerAvailableBalances": 900.00,
    "sellerHeldBalances": 50.00,
    "payoutsProcessed": 500.00,
    "failedPayouts": 100.00
  },
  "operations": {
    "orderCount": 4,
    "refundCount": 1,
    "payoutsProcessedCount": 2,
    "failedPayoutCount": 1,
    "disputeCount": 1,
    "activeDisputeCount": 1
  },
  "topSellers": [
    {
      "sellerId": "00000000-0000-0000-0000-000000000000",
      "sellerDisplayName": "Seller Store",
      "orderCount": 2,
      "grossMerchandiseValue": 700.00,
      "itemsSold": 3
    }
  ],
  "topCategories": [
    {
      "categoryId": "00000000-0000-0000-0000-000000000000",
      "categoryName": "Dresses",
      "quantitySold": 3,
      "revenue": 700.00
    }
  ],
  "csvExportUrl": "/api/admin/reports/marketplace/export.csv?fromUtc=..."
}
```

`grossMerchandiseValue` is derived from paid-or-later order item subtotals created inside the range, excluding shipping, discounts, and platform fee adjustments. Platform commission and payment processing fees are derived from ledger entries inside the range. Seller pending, available, and held balances are current balance snapshots, not historical balance snapshots. Processed and failed payouts are filtered by `UpdatedAtUtc` because the payout aggregate does not yet have dedicated terminal timestamps. The CSV export contains aggregate summary rows only and does not expose buyer-level or raw ledger rows.

## Admin AI Usage Analytics

Admin AI usage analytics require an `Admin` or `SuperAdmin` JWT role. Filters are optional: `fromUtc`, `toUtc`, `featureName`, and `sellerId`. When dates are omitted, the API defaults to the last 30 days.

```http
GET /api/admin/analytics/ai-usage?fromUtc=2026-05-01T00:00:00.000Z&toUtc=2026-05-19T00:00:00.000Z&featureName=ListingAssistant&sellerId=00000000-0000-0000-0000-000000000000
```

Response:

```json
{
  "fromUtc": "2026-05-01T00:00:00+00:00",
  "toUtc": "2026-05-19T00:00:00+00:00",
  "generatedAtUtc": "2026-05-19T10:00:00+00:00",
  "featureName": "ListingAssistant",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "totals": {
    "requests": 3,
    "successfulRequests": 2,
    "failedRequests": 1,
    "failureRate": 0.3333,
    "inputTokens": 250,
    "outputTokens": 210,
    "estimatedCost": 0.04,
    "averageLatencyMs": 150
  },
  "suggestions": {
    "productSuggestionsGenerated": 2,
    "productSuggestionsAccepted": 1,
    "suggestionAcceptanceRate": 0.5,
    "productSuggestionsApplied": 1,
    "productsTouchedByAi": 2,
    "productsImprovedWithAi": 1,
    "averageListingQualityScore": 70,
    "averageQualityScoreImprovement": null,
    "qualityScoreImprovementNote": "Pre-AI baseline quality scores are not captured yet; improvement is unavailable until baseline capture is added.",
    "fieldAuditCount": 2,
    "fieldValuesAccepted": 1,
    "fieldValuesEdited": 1
  },
  "moderation": {
    "moderationChecks": 1,
    "adminReviewFlags": 1,
    "lowRiskFlags": 0,
    "mediumRiskFlags": 0,
    "highRiskFlags": 1
  },
  "featureUsage": [],
  "modelUsage": [],
  "topSellers": []
}
```

The endpoint aggregates existing `AiUsageLog`, `AiProductSuggestion`, `AiSuggestionFieldAudit`, and `AiModerationResult` data only. It does not call an AI provider. `averageQualityScoreImprovement` is intentionally nullable because Swyftly does not yet persist a pre-AI baseline listing quality score.

## Buyer AI Shopping Intent

Prompt 61 added backend intent extraction contracts. Prompt 62 adds the buyer-only recommendation endpoint that uses those contracts and returns real Swyftly products only.

Application contracts:

- `IAiShoppingIntentService`
- `IAiShoppingIntentProvider`
- `ShoppingIntentExtractionRequest`
- `ShoppingIntent`

The fake provider extracts structured intent fields from buyer text, including category, subcategory, budget, size, colour, occasion, style, material, brand, beauty skin type, beauty concern, and search text. Vague requests return `isVague: true` with a clarification prompt instead of inventing products.

Buyer assistant endpoint:

```http
POST /api/buyer/ai/shopping-assistant
```

Request:

```json
{
  "message": "Show me a black dress in size medium under R1,500."
}
```

Response:

```json
{
  "intent": {
    "category": "Dresses",
    "subcategory": null,
    "budgetMax": 1500,
    "budgetMin": null,
    "size": "M",
    "colour": "Black",
    "occasion": null,
    "style": null,
    "material": null,
    "brand": null,
    "beautySkinType": null,
    "beautyConcern": null,
    "searchText": "Show me a black dress in size medium under R1,500.",
    "isVague": false,
    "clarificationPrompt": null
  },
  "products": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "title": "Black Wedding Dress",
      "slug": "black-wedding-dress",
      "sellerDisplayName": "Assistant Seller",
      "imageUrl": "https://example.test/black-dress.jpg",
      "price": 999,
      "currency": "ZAR",
      "matchReasons": ["Available in Black.", "Available in size M."]
    }
  ],
  "summary": "These matches come only from published Swyftly products returned by the backend search.",
  "safetyNote": null
}
```

The endpoint requires a `Buyer` JWT role. It searches published products with active sellable stock and returns only product ids found by backend queries. It does not invent products, prices, sellers, delivery promises, or stock availability. Beauty requests include a product-discovery safety note and avoid medical advice.

Angular buyer route:

```http
/assistant
```

Angular status: Phase 6B keeps this route buyer-guarded and uses the same request/response contract. The screen now shows example prompt chips, extracted intent details, clarification/safety messages, loading/error/empty states, and product result cards without adding wishlist, review, checkout, or ordering behavior.

## Buyer AI Visual Search

Visual search requires a `Buyer` JWT role. The MVP accepts either an image reference or base64 image data from an upload. Uploaded image data is processed only for the request and is not persisted by the API.

```http
POST /api/buyer/ai/visual-search
```

Request:

```json
{
  "imageReference": "black formal maxi dress flatlay",
  "imageDataBase64": null,
  "fileName": "black-dress.jpg",
  "contentType": "image/jpeg"
}
```

Response:

```json
{
  "attributes": {
    "category": "Dresses",
    "colour": "Black",
    "style": "Formal",
    "shape": "Maxi",
    "pattern": null,
    "materialGuess": null,
    "materialConfidence": null,
    "confidence": 0.72,
    "searchText": "Dresses Black Formal Maxi",
    "warnings": [
      "Material and brand are not inferred unless visible context is explicit."
    ]
  },
  "products": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "title": "Black Formal Maxi Dress",
      "slug": "black-formal-maxi-dress",
      "sellerDisplayName": "Visual Seller",
      "imageUrl": "https://example.test/black-formal-maxi-dress.jpg",
      "price": 999,
      "currency": "ZAR",
      "matchReasons": ["Matches visual category Dresses.", "Available in Black."]
    }
  ],
  "summary": "These matches use extracted visual attributes against published Swyftly products only.",
  "imageRetentionNote": "Uploaded image data is processed for this request only and is not persisted by the visual search MVP."
}
```

The fake vision provider is deterministic for local development and tests. It extracts category, colour, style, shape, pattern, and low-confidence material guesses from image references or file names. The endpoint searches only published products with active sellable stock and returns only product ids found by backend queries. It does not infer exact brand or verified material from an image.

Angular buyer route:

```http
/visual-search
```

Angular status: Phase 6B keeps this route buyer-guarded and uses the same request/response contract. The screen now validates supported image uploads client-side, shows selected image preview/filename, extracted visual attributes, confidence, warnings, retention notes, loading/error/empty states, and product result cards without changing API payloads.

The admin dashboard landing page returns aggregate operational counts only. It intentionally does not expose buyer or seller detail records on the landing page. Dedicated finance and AI analytics are exposed through the admin reports routes above.

Angular admin routes now include:

```http
/admin
/admin/sellers
/admin/products
/admin/orders
/admin/orders/{orderId}
/admin/payments
/admin/payments/{paymentId}
/admin/reports
/admin/ai-usage
/admin/refunds
/admin/disputes
/admin/payouts
/admin/support
/admin/support/{ticketId}
/admin/categories
/admin/ads
/admin/ads/:id
```

Some admin routes are intentionally read-only where backend write workflows are not exposed yet.

Admin finance UI status: `/admin/orders`, `/admin/orders/{orderId}`, `/admin/payments`, `/admin/payments/{paymentId}`, `/admin/refunds`, `/admin/payouts`, and `/admin/disputes` are API-backed frontend screens. Order/payment screens are read-only investigation surfaces; payment mutation remains with buyer payment initiation, provider webhooks, refund workflows, and finance payout/refund actions.

Admin support UI status: `/admin/support` and `/admin/support/{ticketId}` are API-backed frontend screens for support agents, admins, and super admins. Admin catalog UI status: `/admin/categories` is an API-backed catalog management workspace for category and attribute create/edit/activate/deactivate workflows. The catalog UI intentionally has no hard-delete controls.

Admin moderation UI status: `/admin/sellers`, `/admin/sellers/{sellerId}`, `/admin/products`, `/admin/products/{productId}`, and `/admin/audit-logs` are API-backed frontend screens using existing contracts unchanged. Phase 5C added shared admin workspace navigation, client-side queue filters, denser triage rows, seller completeness indicators, product image review/fallbacks, AI risk display polish, and shared loading/empty/error states.

## Admin Order And Payment Reads

Admin order/payment read endpoints require `FinanceRead` (`Admin`, `SuperAdmin`, `FinanceOperator`, or `FinanceApprover`). They are read-only endpoints for support, finance, refund, dispute, and webhook investigation. They do not expose order mutation, payment capture, manual settlement, or provider-dashboard replacement workflows.

```http
GET /api/admin/orders?status=Paid
GET /api/admin/orders/{orderId}
GET /api/admin/payments?status=Paid&orderId={orderId}
GET /api/admin/payments/reconciliation-candidates?olderThanMinutes=30&includeSnoozed=false
POST /api/admin/payments/{paymentId}/reconciliation-reviews
GET /api/admin/payments/{paymentId}
```

Order summaries include order ids, buyer/seller ids, seller display name when available, item count, totals, latest payment status, latest shipment status, and created/updated timestamps. Order detail adds the delivery-address/instruction snapshot, items, status history, shipments with shipment events, and related payment summaries.

Payment summaries include payment id, order id, buyer id, provider, provider reference, amount, currency, status, paid/failed timestamps, and created/updated timestamps. Payment detail adds a compact related-order summary and webhook event metadata. Raw webhook payloads are intentionally not returned.

`GET /api/admin/payments/reconciliation-candidates` is a read-only finance operations queue. It returns stale `Pending`/`Authorized` payments and payments with failed webhook events so finance can check the provider dashboard or support logs. Candidate responses include the latest reconciliation review when one exists. Candidates whose latest review has a future `nextReviewAfterUtc` are hidden unless `includeSnoozed=true`.

`POST /api/admin/payments/{paymentId}/reconciliation-reviews` requires `FinanceApprove` (`FinanceApprover` or `SuperAdmin`). It records provider-dashboard evidence only. It does not call PayFast/PayU, mark payments paid/failed, mutate orders, create ledger entries, clear carts, change reservations, or alter refund/payout state.

Review request:

```json
{
  "observedProviderStatus": "COMPLETE",
  "observedAmount": 499.99,
  "observedCurrency": "ZAR",
  "outcome": "ProviderPaidMissingWebhook",
  "reason": "Provider dashboard shows COMPLETE but no valid ITN was received.",
  "nextReviewAfterUtc": "2026-05-22T10:00:00Z"
}
```

Supported review outcomes are `ProviderPending`, `MatchedNoAction`, `ProviderPaidMissingWebhook`, `ProviderFailedMissingWebhook`, and `ManualRecoveryRequired`. `ProviderPaidMissingWebhook` is an investigation signal: finance should trigger provider/dashboard investigation or ITN replay, not manually settle the order from the admin screen.

## Admin Categories

Category metadata and write endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/categories
POST /api/admin/categories
PUT /api/admin/categories/{categoryId}
POST /api/admin/categories/{categoryId}/activate
POST /api/admin/categories/{categoryId}/deactivate
POST /api/admin/categories/{categoryId}/attributes
PUT /api/admin/categories/{categoryId}/attributes/{attributeId}
POST /api/admin/categories/{categoryId}/attributes/{attributeId}/activate
POST /api/admin/categories/{categoryId}/attributes/{attributeId}/deactivate
```

`GET /api/admin/categories` returns a flat category list with parent ids, operational counts, and attribute definitions:

```json
[
  {
    "categoryId": "20000000-0000-0000-0000-000000000003",
    "parentCategoryId": "20000000-0000-0000-0000-000000000002",
    "name": "Dresses",
    "slug": "women-clothing-dresses",
    "displayOrder": 10,
    "isActive": true,
    "productCount": 12,
    "childCount": 0,
    "attributes": [
      {
        "attributeId": "30000000-0000-0000-0000-000000000001",
        "name": "Size",
        "key": "size",
        "dataType": "Select",
        "isRequired": true,
        "allowedValues": ["XS", "S", "M", "L", "XL"],
        "displayOrder": 10,
        "isActive": true
      }
    ]
  }
]
```

Category create/update request:

```json
{
  "parentCategoryId": null,
  "name": "Dresses",
  "slug": "women-dresses",
  "displayOrder": 10
}
```

Attribute create/update request:

```json
{
  "name": "Size",
  "key": "size",
  "dataType": "Select",
  "isRequired": true,
  "allowedValues": ["XS", "S", "M", "L", "XL"],
  "displayOrder": 10
}
```

Supported attribute data types are `Text`, `Number`, `Decimal`, `Boolean`, `Select`, `MultiSelect`, and `Date`. Select and multiselect attributes require allowed values; non-select types reject allowed values. Deactivation hides inactive categories and attributes from active-only public and seller catalog selectors without deleting historical taxonomy data or rewriting existing products.

Write guards reject duplicate category slugs, category parent cycles, duplicate attribute keys within a category, attribute key/type changes when product attribute values already exist, removal of select/multiselect values already used by products, and making an optional attribute required when existing products in that category lack the value. Every category/attribute create, update, activate, and deactivate action writes an audit-log entry.

Angular admin route `/admin/categories` uses these endpoints for a catalog management workspace with category metadata, create/edit forms, activate/deactivate actions, selected-category attribute management, safety messaging, and backend ProblemDetails display. Hard delete, bulk import, drag-and-drop reordering, and taxonomy versioning remain future work.

## Public Catalog And Product Search

Public catalog endpoints do not require authentication.

```http
GET /api/products/search
GET /api/products/{slug}
GET /api/products/{slug}/reviews
GET /api/products/{slug}/review-summary
GET /api/categories
GET /api/sellers/{storeSlug}
```

`GET /api/products/search` uses PostgreSQL as the first search backend and returns only `Published` products. Supported query filters:

- `query`
- `categoryId`
- `categorySlug`
- `sellerId`
- `minPrice`
- `maxPrice`
- `size`
- `colour`
- `brandId`
- `material`
- `inStock`
- `sort`: `newest`, `price_asc`, `price_desc`, `relevance`
- `page`
- `pageSize`, capped at `60`

Response:

```json
{
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "sellerId": "00000000-0000-0000-0000-000000000000",
      "sellerStoreName": "Seller Store",
      "sellerStoreSlug": "seller-store",
      "categoryId": "20000000-0000-0000-0000-000000000003",
      "categoryPath": "Women > Clothing > Dresses",
      "brandId": null,
      "title": "Summer Dress",
      "slug": "summer-dress",
      "shortDescription": "A lightweight summer dress.",
      "primaryImageUrl": "https://example.test/summer-dress.jpg",
      "primaryImageAltText": "Summer dress",
      "priceMin": 499.99,
      "compareAtPriceMin": 699.99,
      "inStock": true,
      "tags": ["summer"],
      "publishedAtUtc": "2026-05-18T10:00:00+00:00"
    }
  ],
  "page": 1,
  "pageSize": 24,
  "totalCount": 1,
  "sort": "newest"
}
```

Out-of-stock handling is explicit through `inStock`: by default, published products can appear even if all active variants are unavailable; `inStock=true` restricts results to products with at least one active variant where stock exceeds reserved quantity. Public product search and detail only expose products whose seller is verified and whose storefront is published.

`GET /api/products/{slug}` returns public product detail with images, variants, attributes, and the product card payload. It returns `404` for products from unverified, suspended, rejected, pending, or unpublished-storefront sellers. Current product slugs are seller-scoped in persistence, so duplicate public slugs are still a known future routing issue.

`GET /api/products/{slug}/reviews` and `GET /api/products/{slug}/review-summary` return only published verified-buyer reviews. They do not require authentication and do not change the existing product detail/search response shapes.

`GET /api/sellers/{storeSlug}` returns `404` unless the storefront is published and the seller is verified.

Search indexing is prepared behind `ISearchIndexService` and `IProductSearchIndexer`. Published products are indexed after admin approval into the current local in-memory placeholder. If the search index has no usable data, public search falls back to PostgreSQL.

Product embeddings are prepared behind `IAiEmbeddingService` and `IProductEmbeddingGenerator`. Published products generate or replace a private `product_embeddings` row after admin approval using the current fake embedding provider. No public semantic-search API exists yet.

Inventory reservation support is prepared behind `IInventoryReservationService`. Order creation calls this service when checkout starts. The service creates active reservations from cart items, increments variant reserved quantities inside a database transaction, expires reservations after the configured duration, and releases stock on expiry. PostgreSQL updates are conditional so reserved quantity cannot exceed stock or fall below zero under concurrent reservation/release attempts. No public standalone reservation endpoint exists.

Angular public routes using these endpoints:

```http
/shop
/category/:slug
/product/:slug
/seller/:storeSlug
```

Product detail pages include buyer add-to-cart controls. Unauthenticated or non-buyer users are routed to sign in before adding items.

## Buyer Cart

Cart endpoints require a buyer JWT role. A buyer has at most one active cart, and the MVP cart can contain products from only one seller.

```http
GET /api/cart
POST /api/cart/items
POST /api/cart/shipping-options
PUT /api/cart/items/{itemId}
POST /api/cart/items/{itemId}/move-to-wishlist
DELETE /api/cart/items/{itemId}
DELETE /api/cart
```

`POST /api/cart/items` adds to an existing variant quantity when the variant is already in the cart. `PUT /api/cart/items/{itemId}` sets the item quantity. Quantity must be positive and cannot exceed the product variant's available stock, calculated as stock minus reserved quantity. Cart item unit price is captured for display only; final order pricing must be confirmed during checkout.

`POST /api/cart/items/{itemId}/move-to-wishlist` saves the cart item's product to the authenticated buyer's wishlist and removes the cart item only after the wishlist save succeeds. It is idempotent for an already-saved product and returns the updated cart.

Add item request:

```json
{
  "productVariantId": "00000000-0000-0000-0000-000000000000",
  "quantity": 2
}
```

Cart response:

```json
{
  "cartId": "00000000-0000-0000-0000-000000000000",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "sellerStoreName": "Seller Store",
  "items": [
    {
      "cartItemId": "00000000-0000-0000-0000-000000000000",
      "productId": "00000000-0000-0000-0000-000000000000",
      "productVariantId": "00000000-0000-0000-0000-000000000000",
      "productTitle": "Summer Dress",
      "productSlug": "summer-dress",
      "primaryImageUrl": "https://example.test/summer-dress.jpg",
      "primaryImageAltText": "Summer dress",
      "sku": "SKU-1",
      "size": "M",
      "colour": "Black",
      "unitPrice": 499.99,
      "quantity": 2,
      "lineTotal": 999.98
    }
  ],
  "totalQuantity": 2,
  "subtotal": 999.98
}
```

Trying to add a product from a different seller returns a validation problem. `DELETE /api/cart` clears the active cart and returns `204`.

`POST /api/cart/shipping-options` returns active seller-managed delivery methods that match the selected checkout address. It uses the active single-seller cart plus either an owned saved `deliveryAddressId` or an inline `deliveryAddress`; the backend computes each shipping amount and applies any seller free-shipping threshold.

Shipping-options request:

```json
{
  "cartId": "00000000-0000-0000-0000-000000000000",
  "deliveryAddressId": "00000000-0000-0000-0000-000000000000",
  "deliveryAddress": null
}
```

Shipping-options response:

```json
{
  "cartId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "cartSubtotal": 999.98,
  "options": [
    {
      "deliveryMethodId": "00000000-0000-0000-0000-000000000000",
      "name": "Standard courier",
      "description": "Door-to-door delivery within South Africa.",
      "methodType": "Standard",
      "countryCode": "ZA",
      "province": "Gauteng",
      "basePrice": 75.00,
      "freeShippingThreshold": 1000.00,
      "shippingAmount": 0.00,
      "freeShippingApplied": true,
      "estimatedMinDays": 2,
      "estimatedMaxDays": 5,
      "displayOrder": 10
    }
  ]
}
```

If no active method matches the selected address, checkout returns a validation error and the order cannot be created.

Angular buyer cart route:

```http
/cart
```

The cart page shows cart items with product images or fallback visuals, quantities, seller name, single-seller checkout notice, subtotal, quantity update, remove item, save-for-later, product detail links, and checkout navigation.

## Buyer Wishlist, Reviews, And Notifications

Buyer engagement endpoints require a `Buyer` JWT role except the public product review reads documented in the catalog section.

```http
GET /api/buyer/wishlist
GET /api/buyer/wishlist/product-ids
POST /api/buyer/wishlist/{productId}
POST /api/buyer/wishlist/{productId}/move-to-cart
DELETE /api/buyer/wishlist/{productId}
GET /api/buyer/reviews
POST /api/buyer/orders/{orderId}/items/{orderItemId}/review
PUT /api/buyer/reviews/{reviewId}
DELETE /api/buyer/reviews/{reviewId}
GET /api/buyer/profile
PUT /api/buyer/profile
GET /api/buyer/delivery-addresses
POST /api/buyer/delivery-addresses
PUT /api/buyer/delivery-addresses/{addressId}
DELETE /api/buyer/delivery-addresses/{addressId}
POST /api/buyer/delivery-addresses/{addressId}/make-default
GET /api/buyer/notification-preferences
PUT /api/buyer/notification-preferences
GET /api/buyer/notifications
POST /api/buyer/notifications/{notificationId}/read
POST /api/buyer/notifications/read-all
```

Wishlist actions are idempotent per buyer/product and only accept products that are publicly visible: published product, verified seller, published storefront, and sellable active variant data.

`GET /api/buyer/wishlist/product-ids` returns a lightweight hydration response for product cards and product detail pages:

```json
{
  "productIds": ["00000000-0000-0000-0000-000000000000"]
}
```

Wishlist list items include `availableVariants`, which contains active variant options for move-to-cart selection:

```json
{
  "productVariantId": "00000000-0000-0000-0000-000000000000",
  "size": "M",
  "colour": "Black",
  "price": 499.99,
  "compareAtPrice": null,
  "inStock": true,
  "availableQuantity": 5
}
```

`POST /api/buyer/wishlist/{productId}/move-to-cart` atomically adds the selected variant to the buyer's cart and removes the wishlist item only after cart validation succeeds. It preserves the wishlist item when quantity, variant ownership, visibility, stock, or single-seller cart rules reject the move.

Request:

```json
{
  "productVariantId": "00000000-0000-0000-0000-000000000000",
  "quantity": 1
}
```

Review creation is verified-purchase only. The buyer must own the order, the order must be `Delivered`, and the order item can have only one review. Review ratings must be `1` through `5`. Updating or deleting a review is restricted to the owning buyer; delete marks the review removed so it no longer appears in public reads.

New or edited verified-purchase reviews enter `PendingReview` until an admin approves them. Buyer review responses include moderation status plus rejection reason/timestamp metadata where applicable. Public product review reads continue to expose only `Published` reviews.

Buyer profile settings expose the buyer identity email as read-only and allow optional display name and phone number updates only:

```json
{
  "displayName": "Thabo",
  "phoneNumber": "+27110000000"
}
```

Saved delivery addresses are buyer-owned reusable checkout addresses. The first saved address becomes default automatically, creating or marking another address as default clears the previous default, and deleting the default promotes another remaining address. Buyers can store up to 10 addresses.

Saved-address request:

```json
{
  "label": "Home",
  "recipientName": "Thabo",
  "phoneNumber": "+27110000000",
  "addressLine1": "10 Market Street",
  "addressLine2": "Apartment 4",
  "suburb": "Rosebank",
  "city": "Johannesburg",
  "province": "Gauteng",
  "postalCode": "2196",
  "countryCode": "ZA",
  "deliveryInstructions": "Leave at reception if needed.",
  "isDefault": true
}
```

Notifications are persisted records created through backend workflows via `INotificationService`. Buyer notification APIs list in-app-visible records and update read state. Category-level preferences independently control future in-app and email delivery for `Orders`, `Returns`, `Reviews`, and `Support`; missing preference rows default both channels to enabled, and existing visible notifications remain visible. Email delivery uses a durable worker-processed outbox with `LogOnly` as the local default and `Smtp` as the first real provider option. There is no SMS, push, SignalR, marketing automation, seller/admin email workflow, or public send-email endpoint. Current workflow notifications cover review approval/rejection, seller return approval/rejection, support public replies, order tracking updates, ready-to-ship, shipped, delivered, delivery-failed, and returned-to-sender order events.

Notification preference request:

```json
{
  "preferences": [
    { "category": "Orders", "isEnabled": true, "emailEnabled": true },
    { "category": "Returns", "isEnabled": true, "emailEnabled": true },
    { "category": "Reviews", "isEnabled": false, "emailEnabled": true },
    { "category": "Support", "isEnabled": true, "emailEnabled": false }
  ]
}
```

Older clients may omit `emailEnabled` in update requests; the backend preserves an existing email setting when present and defaults new rows to email enabled.

Email delivery configuration keys:

```text
EmailDelivery__ProviderName=LogOnly|Smtp
EmailDelivery__FromAddress=
EmailDelivery__FromName=Swyftly
EmailDelivery__AppBaseUrl=http://localhost:4200
EmailDelivery__BatchSize=25
EmailDelivery__MaxAttempts=5
EmailDelivery__RetryMinutes=15
EmailDelivery__Smtp__Host=
EmailDelivery__Smtp__Port=587
EmailDelivery__Smtp__Username=
EmailDelivery__Smtp__Password=
EmailDelivery__Smtp__EnableSsl=true
```

Angular buyer routes using these endpoints:

```http
/account/wishlist
/account/reviews
/account/notifications
/account/settings
```

Product listing cards and product detail pages hydrate saved state from the lightweight wishlist product-id endpoint, can save/remove products, and redirect unauthenticated buyers to login with `returnUrl`. `/account/wishlist` shows variant/quantity controls and can move saved items to cart. Product detail pages show public review summary/list data. Delivered buyer order detail pages can create verified-purchase reviews for order items, and `/account/reviews` manages existing buyer reviews.

## Admin Review Moderation

Admin review moderation endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/reviews/pending
GET /api/admin/reviews/{reviewId}
POST /api/admin/reviews/{reviewId}/approve
POST /api/admin/reviews/{reviewId}/reject
POST /api/admin/reviews/{reviewId}/remove
```

`GET /pending` returns buyer reviews waiting for moderation with product, seller, buyer, and order context. Detail responses also include review audit-log history.

Reject and remove requests require a reason:

```json
{
  "reason": "Review contains unsupported claims."
}
```

Approve, reject, and remove write admin audit-log entries. Approve and reject also create buyer notifications and queue buyer email when the buyer's review email preference is enabled. Removed reviews and rejected reviews remain hidden from public product review reads.

Angular admin route:

```http
/admin/reviews
```

The screen uses the shared admin workspace navigation, client-side search/status filters, evidence panels, and approve/reject/remove actions without changing backend payloads.

## Orders

Order endpoints use JWT roles. Buyer endpoints operate only on the authenticated buyer's orders. Seller endpoints operate only on orders for the authenticated seller.

```http
POST /api/orders/from-cart
GET /api/orders
GET /api/orders/{orderId}
GET /api/buyer/orders
GET /api/buyer/orders/{orderId}
GET /api/seller/orders
GET /api/seller/orders/{orderId}
POST /api/seller/orders/{orderId}/mark-processing
POST /api/seller/orders/{orderId}/mark-ready-to-ship
POST /api/seller/orders/{orderId}/tracking
POST /api/seller/orders/{orderId}/mark-shipped
POST /api/seller/orders/{orderId}/mark-delivered
POST /api/seller/orders/{orderId}/mark-delivery-failed
POST /api/seller/orders/{orderId}/mark-returned-to-sender
```

`POST /api/orders/from-cart` creates a `PendingPayment` order from the authenticated buyer's active cart and reserves inventory for the cart items. Repeating the request for the same active cart returns the existing pending-payment order instead of creating a duplicate. The cart stays active until a paid webhook confirms payment; successful payment clears the active cart after reservations and ledger processing complete.

Request:

```json
{
  "cartId": null,
  "reservationMinutes": null,
  "deliveryAddressId": "00000000-0000-0000-0000-000000000000",
  "deliveryAddress": null,
  "deliveryMethodId": "00000000-0000-0000-0000-000000000000"
}
```

`cartId` is optional; when omitted, the buyer's active cart is used. `reservationMinutes` is optional and defaults to 15 minutes. New orders require exactly one delivery address source: either an owned saved `deliveryAddressId` or an inline one-off `deliveryAddress`, plus an active seller delivery method that serves that address. The backend recomputes shipping from the selected delivery method and cart subtotal; Angular does not send `shippingAmount`. The selected/inline address, including optional delivery instructions, and the selected delivery method/rate are copied to the order as snapshots; later saved-address or seller delivery-method edits do not change historical orders. Older orders can return `deliveryAddress: null` and nullable delivery-method snapshot fields.

Inline delivery-address request:

```json
{
  "recipientName": "Thabo",
  "phoneNumber": "+27110000000",
  "addressLine1": "10 Market Street",
  "addressLine2": "Apartment 4",
  "suburb": "Rosebank",
  "city": "Johannesburg",
  "province": "Gauteng",
  "postalCode": "2196",
  "countryCode": "ZA",
  "deliveryInstructions": "Leave at reception if needed."
}
```

Response:

```json
{
  "orderId": "00000000-0000-0000-0000-000000000000",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "cartId": "00000000-0000-0000-0000-000000000000",
  "status": "PendingPayment",
  "items": [
    {
      "orderItemId": "00000000-0000-0000-0000-000000000000",
      "productId": "00000000-0000-0000-0000-000000000000",
      "productVariantId": "00000000-0000-0000-0000-000000000000",
      "productTitle": "Summer Dress",
      "sku": "SKU-1",
      "size": "M",
      "colour": "Black",
      "unitPrice": 499.99,
      "quantity": 2,
      "lineTotal": 999.98
    }
  ],
  "itemsSubtotal": 999.98,
  "shippingAmount": 0,
  "platformFeeAmount": 0,
  "discountAmount": 0,
  "totalAmount": 999.98,
  "deliveryMethodId": "00000000-0000-0000-0000-000000000000",
  "deliveryMethodName": "Standard courier",
  "deliveryMethodType": "Standard",
  "deliveryEstimatedMinDays": 2,
  "deliveryEstimatedMaxDays": 5,
  "deliveryAddress": {
    "recipientName": "Thabo",
    "phoneNumber": "+27110000000",
    "addressLine1": "10 Market Street",
    "addressLine2": "Apartment 4",
    "suburb": "Rosebank",
    "city": "Johannesburg",
    "province": "Gauteng",
    "postalCode": "2196",
    "countryCode": "ZA",
    "deliveryInstructions": "Leave at reception if needed."
  },
  "statusHistory": [
    {
      "statusHistoryId": "00000000-0000-0000-0000-000000000000",
      "previousStatus": null,
      "newStatus": "PendingPayment",
      "changedAtUtc": "2026-05-18T10:00:00+00:00",
      "reason": "OrderCreated"
    }
  ],
  "shipments": [
    {
      "shipmentId": "00000000-0000-0000-0000-000000000000",
      "status": "InTransit",
      "carrierName": "Courier One",
      "trackingNumber": "TRACK-123",
      "trackingUrl": "https://tracking.example/TRACK-123",
      "shippedAtUtc": "2026-05-18T10:30:00+00:00",
      "deliveredAtUtc": null,
      "events": [
        {
          "shipmentEventId": "00000000-0000-0000-0000-000000000000",
          "status": "InTransit",
          "eventType": "ShipmentInTransit",
          "message": "Shipment was marked as shipped.",
          "carrierName": "Courier One",
          "trackingNumber": "TRACK-123",
          "occurredAtUtc": "2026-05-18T10:30:00+00:00"
        }
      ]
    }
  ]
}
```

`shippingAmount` is recomputed server-side from the selected seller delivery method. Platform fee and discount values remain placeholders and currently default to zero.

Manual fulfilment starts after payment has moved the order to `Paid`. `POST /mark-processing` changes the order to `Processing` and creates an `AwaitingFulfilment` shipment if one does not exist. `POST /mark-ready-to-ship` moves a paid or processing order to `ReadyToShip` and marks the shipment `ReadyForCourier`. `POST /tracking` accepts:

```json
{
  "carrierName": "Courier One",
  "trackingNumber": "TRACK-123",
  "trackingUrl": "https://tracking.example/TRACK-123",
  "note": "Collected by courier."
}
```

Tracking can be added to paid or fulfilment orders and always writes a shipment event. `POST /mark-shipped` changes the order to `Shipped`, moves the current shipment to `InTransit`, and writes a shipment event. `POST /mark-delivered` is seller-owned and marks a shipped order plus its current in-transit shipment as `Delivered`; repeating it for an already delivered order returns the current order. Other statuses return conflict. Delivery confirmation creates a buyer notification and queues buyer email when enabled, and unlocks existing delivered-order return/review flows. It does not automatically make seller payouts available.

Shipment exception endpoints accept `{ "reason": "Courier could not reach the recipient." }`. `POST /mark-delivery-failed` records `DeliveryFailed` against the current in-transit shipment while leaving the order `Shipped`. `POST /mark-returned-to-sender` records `ReturnedToSender` from an in-transit or failed shipment and also leaves the order `Shipped`. These exception events create buyer notifications and queue buyer email when enabled, but do not mutate payment, refund, payout, ledger, cart, or reservation state.

`GET /api/buyer/orders` and `GET /api/buyer/orders/{orderId}` are buyer-specific aliases for the existing buyer order reads.

Angular buyer account routes using these order reads:

```http
/account
/account/orders
/account/orders/{orderId}
```

`/account/orders/{orderId}` also starts delivered-order return requests through the existing return endpoint below. Pending-payment orders expose a buyer payment retry action through `POST /api/payments/initiate`; cancelled payment-failed orders tell the buyer to restart checkout from the cart instead of reopening the cancelled order.

## Returns

Return endpoints use JWT roles. Buyers can request returns only for their own delivered orders. Sellers can view/respond only to returns for their own orders. Admin return endpoints require `Admin` or `SuperAdmin`.

```http
POST /api/buyer/orders/{orderId}/returns
GET /api/buyer/returns
GET /api/buyer/returns/{returnRequestId}
POST /api/buyer/returns/{returnRequestId}/dispute
GET /api/seller/returns
GET /api/seller/returns/{returnRequestId}
POST /api/seller/returns/{returnRequestId}/approve
POST /api/seller/returns/{returnRequestId}/reject
GET /api/admin/returns/disputed
```

Create return request:

```json
{
  "reason": "DamagedItem",
  "details": "The item arrived damaged.",
  "items": [
    {
      "orderItemId": "00000000-0000-0000-0000-000000000000",
      "quantity": 1,
      "reason": "DamagedItem",
      "isOpenedOrUnsealed": false,
      "note": "Torn seam."
    }
  ]
}
```

Return response:

```json
{
  "returnRequestId": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "status": "AwaitingSellerResponse",
  "reason": "DamagedItem",
  "details": "The item arrived damaged.",
  "requestedAtUtc": "2026-05-18T10:00:00+00:00",
  "sellerRespondedAtUtc": null,
  "sellerResponseReason": null,
  "disputedAtUtc": null,
  "disputeReason": null,
  "items": [],
  "messages": []
}
```

Creating a valid return changes the order to `ReturnRequested` and places linked pending/available seller payouts on hold. The current opened/unsealed rule blocks changed-mind returns for opened or unsealed items; category-specific beauty policy remains a later refinement.

Seller approve/reject requests use:

```json
{
  "message": "Return approved."
}
```

Reject requires a message. Buyers can dispute only rejected returns:

```json
{
  "reason": "Please review the listing photos."
}
```

Angular buyer return routes:

```http
/account/returns
/account/returns/{returnRequestId}
```

The buyer return detail UI uses `POST /api/buyer/returns/{returnRequestId}/dispute` for rejected-return escalation. Backend return eligibility remains authoritative.

Disputing a rejected return changes the return to `Disputed` and the order to `Disputed`. The standalone dispute workflow below should be used when messages, evidence, and admin final decisions are needed.

## Disputes

Dispute endpoints use JWT roles. Buyers can open disputes for their own eligible orders or returns. Sellers can respond only to disputes for their own orders. Admin dispute endpoints require `Admin` or `SuperAdmin`.

```http
POST /api/buyer/orders/{orderId}/disputes
POST /api/buyer/returns/{returnRequestId}/disputes
GET /api/buyer/disputes
POST /api/buyer/disputes/{disputeId}/messages
POST /api/buyer/disputes/{disputeId}/evidence
GET /api/seller/disputes
POST /api/seller/disputes/{disputeId}/messages
POST /api/seller/disputes/{disputeId}/evidence
GET /api/admin/disputes
POST /api/admin/disputes/{disputeId}/resolve
```

Angular buyer dispute route:

```http
/account/disputes
```

The buyer disputes UI lists disputes inline because there is no single-dispute buyer read endpoint. It supports buyer messages and evidence on the selected dispute using the existing endpoints.

Open dispute request:

```json
{
  "reason": "Item appears counterfeit.",
  "evidence": [
    {
      "evidenceType": "Image",
      "storageReference": "uploads/disputes/photo.jpg",
      "description": "Logo mismatch."
    }
  ]
}
```

Message request:

```json
{
  "message": "Supplier certificate attached."
}
```

Evidence request:

```json
{
  "evidenceType": "Document",
  "storageReference": "uploads/disputes/certificate.pdf",
  "description": "Supplier certificate."
}
```

Resolve request:

```json
{
  "outcome": "SellerFavoured",
  "reason": "Seller evidence accepted."
}
```

Supported resolution outcomes are `BuyerFavoured` and `SellerFavoured`. Opening an active dispute changes the order to `Disputed` and holds linked pending/available seller payouts. Seller-favoured resolution releases linked held payouts back to pending. Buyer-favoured resolution keeps payout funds held and creates a requested refund for the remaining refundable payment amount. The refund still requires the normal finance approval/provider flow; PayFast refunds remain manual-provider-confirmed.

Angular admin route `/admin/disputes` lists disputes, shows messages and evidence inline, and resolves disputes with a supported outcome and reason. It remains limited to `Admin` and `SuperAdmin` in the frontend route configuration.

Dispute response:

```json
{
  "disputeId": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "returnRequestId": null,
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "status": "AwaitingSeller",
  "reason": "Item appears counterfeit.",
  "openedAtUtc": "2026-05-18T10:00:00+00:00",
  "resolvedAtUtc": null,
  "resolutionReason": null,
  "messages": [],
  "evidence": []
}
```

## Support Tickets

Support ticket endpoints use JWT roles. Buyers and sellers can create and read their own tickets. Support agents, admins, and super admins can read all tickets, respond publicly, add private internal notes, resolve tickets, and close tickets.

```http
POST /api/buyer/support-tickets
GET /api/buyer/support-tickets
GET /api/buyer/support-tickets/{ticketId}
POST /api/buyer/support-tickets/{ticketId}/messages
POST /api/seller/support-tickets
GET /api/seller/support-tickets
GET /api/seller/support-tickets/{ticketId}
POST /api/seller/support-tickets/{ticketId}/messages
GET /api/support/tickets
GET /api/support/tickets/{ticketId}
POST /api/support/tickets/{ticketId}/messages
POST /api/support/tickets/{ticketId}/internal-notes
POST /api/support/tickets/{ticketId}/resolve
POST /api/support/tickets/{ticketId}/close
```

Create ticket request:

```json
{
  "category": "OrderIssue",
  "subject": "Order arrived damaged",
  "description": "The box arrived damaged.",
  "linkedOrderId": null,
  "linkedProductId": null,
  "linkedSellerId": null,
  "linkedPaymentId": null
}
```

Supported categories are `OrderIssue`, `PaymentIssue`, `ReturnIssue`, `SellerIssue`, `ProductIssue`, `TechnicalIssue`, and `Other`.

Message and internal-note requests use:

```json
{
  "message": "Please upload a photo of the damaged item."
}
```

Ticket response:

```json
{
  "supportTicketId": "00000000-0000-0000-0000-000000000000",
  "createdByUserId": "00000000-0000-0000-0000-000000000000",
  "createdByRole": "Buyer",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": null,
  "category": "OrderIssue",
  "status": "WaitingForCustomer",
  "subject": "Order arrived damaged",
  "description": "The box arrived damaged.",
  "linkedOrderId": null,
  "linkedProductId": null,
  "linkedSellerId": null,
  "linkedPaymentId": null,
  "assignedSupportUserId": "00000000-0000-0000-0000-000000000000",
  "openedAtUtc": "2026-05-19T10:00:00+00:00",
  "resolvedAtUtc": null,
  "closedAtUtc": null,
  "messages": [
    {
      "supportMessageId": "00000000-0000-0000-0000-000000000000",
      "senderUserId": "00000000-0000-0000-0000-000000000000",
      "senderRole": "SupportAgent",
      "message": "Please upload a photo of the damaged item.",
      "isInternal": false,
      "createdAtUtc": "2026-05-19T10:05:00+00:00"
    }
  ]
}
```

Internal notes are included only on `/api/support/tickets` responses. Buyer and seller ticket responses filter out messages where `isInternal` is `true`. Linked order/payment records are ownership-checked for buyer and seller creation; linked product/seller ids are stored as references only and no linked object details are exposed in the support response.

Angular buyer support routes `/account/support` and `/account/support/{ticketId}` use the buyer support-ticket endpoints for list/create/detail/message flows. Internal notes remain hidden from buyer responses.

Angular admin routes `/admin/support` and `/admin/support/{ticketId}` list tickets, show public and internal messages, add public replies, add internal notes, resolve tickets, and close tickets. The frontend role list includes `SupportAgent`, `Admin`, and `SuperAdmin` to match the backend support policy.

## Seller Ad Campaigns

Seller ad campaign endpoints require the `Seller` JWT role and always operate on campaigns owned by the authenticated seller.

```http
POST /api/seller/ad-campaigns
GET /api/seller/ad-campaigns
GET /api/seller/ad-campaigns/{id}
PUT /api/seller/ad-campaigns/{id}
POST /api/seller/ad-campaigns/{id}/submit-review
POST /api/seller/ad-campaigns/{id}/pause
POST /api/seller/ad-campaigns/{id}/resume
POST /api/seller/ad-campaigns/{id}/cancel
```

Create/update request:

```json
{
  "name": "Launch campaign",
  "campaignType": "FeaturedProduct",
  "startsAtUtc": "2026-05-20T00:00:00+00:00",
  "endsAtUtc": "2026-06-03T00:00:00+00:00",
  "productIds": ["00000000-0000-0000-0000-000000000000"],
  "budget": {
    "currency": "ZAR",
    "dailyBudget": 100.00,
    "totalBudget": 1000.00,
    "maxCostPerClick": 5.00
  }
}
```

Supported campaign types are `FeaturedProduct`, `SponsoredSearch`, `FeaturedStorefront`, and `CategorySpotlight`.

Response:

```json
{
  "adCampaignId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "name": "Launch campaign",
  "campaignType": "FeaturedProduct",
  "status": "Draft",
  "startsAtUtc": "2026-05-20T00:00:00+00:00",
  "endsAtUtc": "2026-06-03T00:00:00+00:00",
  "submittedAtUtc": null,
  "approvedAtUtc": null,
  "pausedAtUtc": null,
  "completedAtUtc": null,
  "cancelledAtUtc": null,
  "rejectionReason": null,
  "productIds": ["00000000-0000-0000-0000-000000000000"],
  "budget": {
    "currency": "ZAR",
    "dailyBudget": 100.00,
    "totalBudget": 1000.00,
    "maxCostPerClick": 5.00,
    "spentAmount": 0
  },
  "eligibility": {
    "isEligible": true,
    "sellerReasons": [],
    "products": [
      {
        "productId": "00000000-0000-0000-0000-000000000000",
        "isEligible": true,
        "qualityScore": 100,
        "reasons": []
      }
    ]
  }
}
```

Campaign creation and updates validate seller/product eligibility. The current eligibility rules require a verified seller, no seller suspension, no dispute currently under admin review, seller-owned published products, sellable stock, no unresolved moderation flags, and a deterministic product completeness score of at least `80`. This quality score is a local completeness score until a later prompt introduces a richer advertising quality model.

`POST /submit-review` moves an eligible draft/rejected campaign to `PendingReview`. Campaigns become `Active` only through the admin ad campaign review workflow below. Pause/resume endpoints are present for the campaign state model but only work once a campaign is active/paused.

## Admin Ad Campaign Review

Admin ad campaign review endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/ad-campaigns/pending
GET /api/admin/ad-campaigns/{id}
POST /api/admin/ad-campaigns/{id}/approve
POST /api/admin/ad-campaigns/{id}/reject
```

`GET /pending` returns campaigns in `PendingReview`. Campaign detail responses include seller details, promoted products, budget, current eligibility results, and campaign audit trail entries.

`POST /approve` re-runs campaign eligibility before activation. If the seller or promoted products no longer qualify, the endpoint returns a validation problem and leaves the campaign in `PendingReview`. Successful approval moves the campaign to `Active`, records the approving admin user id, and writes an `AdCampaignApproved` audit-log entry.

`POST /reject` requires a reason:

```json
{
  "reason": "Promoted products do not meet ad policy."
}
```

Rejecting moves the campaign to `Rejected`, stores the rejection reason, and writes an `AdCampaignRejected` audit-log entry.

Angular admin routes:

```http
/admin/ads
/admin/ads/:id
```

## Ad Tracking And Campaign Metrics

Public ad tracking endpoints are anonymous and return `202 Accepted` whether an event is stored or ignored. This avoids making the buyer flow depend on ad-tracking success. Impressions and clicks use separate rate-limit policies.

```http
POST /api/ads/impressions
POST /api/ads/clicks
```

Impression request:

```json
{
  "adCampaignId": "00000000-0000-0000-0000-000000000000",
  "productId": "00000000-0000-0000-0000-000000000000",
  "placement": "shop-grid",
  "anonymousVisitorId": "visitor-session-id"
}
```

Click request:

```json
{
  "adCampaignId": "00000000-0000-0000-0000-000000000000",
  "productId": "00000000-0000-0000-0000-000000000000",
  "anonymousVisitorId": "visitor-session-id"
}
```

Response:

```json
{
  "recorded": true,
  "eventId": "00000000-0000-0000-0000-000000000000",
  "status": "ClickRecorded",
  "reason": null
}
```

The tracking service records events only for active campaigns within their flight window, campaign-linked published products, and products with sellable stock. Repeated impressions are de-duplicated over a short visitor window; repeated clicks are de-duplicated over a shorter buyer/visitor window. When an anonymous tracking request omits `anonymousVisitorId`, the API derives a short hashed request fingerprint from connection metadata so repeated anonymous impressions do not create unlimited writes. Clicks create ad charges using the campaign max CPC and respect daily and total campaign budget limits.

Successful payment webhook processing runs backend-only conversion attribution. It attributes paid order items to the latest buyer ad click for the same product inside the attribution window and stores conversions without exposing buyer personal data in reports.

Seller-owned campaign metrics are available for the future seller ads dashboard:

```http
GET /api/seller/ad-campaigns/{id}/metrics
```

Response:

```json
{
  "adCampaignId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "status": "Active",
  "impressions": 100,
  "clicks": 5,
  "clickThroughRate": 0.05,
  "spend": 25.00,
  "ordersGenerated": 1,
  "revenueGenerated": 499.99,
  "returnOnAdSpend": 19.9996,
  "currency": "ZAR"
}
```

Angular seller advertising routes:

```http
/seller/ads
/seller/ads/new
/seller/ads/:id
```

The seller ads UI lists campaigns, creates draft campaigns, selects products to promote, submits campaigns for admin review, shows eligibility warnings returned by the API, displays campaign metrics, and exposes pause/resume/cancel actions where the current campaign status allows them.

## Seller Analytics

Seller analytics endpoints require the `Seller` JWT role and return only aggregate seller-owned data. Buyer identities and personal details are not included.

```http
GET /api/seller/analytics/summary
```

Response:

```json
{
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "totalSales": 998.00,
  "orderCount": 1,
  "averageOrderValue": 998.00,
  "conversionRatePlaceholder": 0,
  "productsSold": 2,
  "totalRefunded": 100.00,
  "refundRate": 1.0,
  "returnRate": 1.0,
  "topProducts": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "productTitle": "Seller One Product",
      "quantitySold": 2,
      "revenue": 998.00
    }
  ],
  "lowStockProducts": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "title": "Seller One Product",
      "status": "Published",
      "availableQuantity": 3,
      "lowStockVariantCount": 1
    }
  ],
  "adPerformance": {
    "campaignCount": 1,
    "impressions": 100,
    "clicks": 5,
    "clickThroughRate": 0.05,
    "spend": 25.00,
    "ordersGenerated": 1,
    "revenueGenerated": 499.00,
    "topCampaigns": []
  },
  "aiUsage": {
    "requests": 3,
    "successfulRequests": 2,
    "failedRequests": 1,
    "estimatedCost": 0.02,
    "averageLatencyMs": 100
  }
}
```

`totalSales` is gross sales from paid-or-later seller order states, excluding pending-payment and cancelled/refunded orders. Refund and return rates are count-based against seller paid-or-later order count. `conversionRatePlaceholder` remains zero until a dedicated storefront/session conversion model exists.

Angular seller analytics route:

```http
/seller/analytics
```

## Refunds

Refund endpoints use finance policies. Reads require `FinanceRead` (`Admin`, `SuperAdmin`, `FinanceOperator`, or `FinanceApprover`). Creating refund requests requires `FinanceOperate` (`FinanceOperator` or `SuperAdmin`). Approving provider refunds and confirming manual provider refunds require `FinanceApprove` (`FinanceApprover` or `SuperAdmin`). Dual control applies to every role, including `SuperAdmin`: the actor who created a refund request cannot approve the same refund.

```http
POST /api/admin/orders/{orderId}/refunds
POST /api/admin/returns/{returnRequestId}/refunds
GET /api/admin/refunds
POST /api/admin/refunds/{refundId}/approve
POST /api/admin/refunds/{refundId}/confirm-manual-provider-refund
```

Create refund request:

```json
{
  "amount": 500.00,
  "reason": "Approved partial refund."
}
```

Approve refund request:

```json
{
  "reason": "Return approved by admin."
}
```

Manual provider refund confirmation request:

```json
{
  "providerRefundReference": "payfast_dashboard_refund_reference",
  "reason": "Refund completed in provider dashboard."
}
```

Refund response:

```json
{
  "refundId": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "paymentId": "00000000-0000-0000-0000-000000000000",
  "buyerId": "00000000-0000-0000-0000-000000000000",
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "returnRequestId": null,
  "amount": 500.00,
  "currency": "ZAR",
  "status": "Refunded",
  "reason": "Approved partial refund.",
  "providerRefundReference": "fake_refund_reference",
  "failureReason": null,
  "requestedAtUtc": "2026-05-18T10:00:00+00:00",
  "approvedAtUtc": "2026-05-18T10:05:00+00:00",
  "refundedAtUtc": "2026-05-18T10:05:00+00:00",
  "events": []
}
```

Approving a refund calls the configured payment-provider abstraction. Fake-provider refunds complete immediately. PayFast refunds are manual in this phase: approval moves the refund to `Processing` and records a provider-action-required event; finance must complete the refund in the PayFast dashboard and then use `POST /api/admin/refunds/{refundId}/confirm-manual-provider-refund` with the provider refund reference. Completed refunds mark the payment `PartiallyRefunded` or `Refunded`, write `RefundIssued` and `RefundReversal` ledger entries, adjust seller balances proportionally to the original seller-pending amount, and write audit logs. If an unpaid payout item is tied to the refunded order/payment, the payout item `AdjustedAmount` and payout net amount are reduced; zero-net unpaid payouts become `Reversed`. Refunds against `Processing` or `PaidOut` payouts create a recovery-required payout adjustment instead of mutating the provider payout record. If seller balances are already insufficient, the pending balance can go negative as an explicit manual-recovery signal. Full refunds mark the order `Refunded`; partial refunds leave the order in its current status.

Angular admin route `/admin/refunds` lists refund records, creates order or return refund requests, approves selected refunds, and shows a manual provider-reference confirmation flow for `Processing` PayFast refunds. The UI exposes read/operate/approve eligibility, but backend finance policies and dual-control checks remain authoritative.

## Angular Checkout

Angular checkout routes:

```http
/checkout
/checkout/success
/checkout/failed
```

The checkout page displays the current cart summary, lets the buyer choose a saved delivery address or enter a one-off address with optional delivery instructions, loads seller-managed delivery options, requires a selected delivery method, starts checkout by calling `POST /api/orders/from-cart`, then calls `POST /api/payments/initiate` for the pending-payment order. If the API returns `checkoutUrl`, Angular redirects the buyer to that provider URL. `/checkout/success`, `/checkout/failed`, and `/account/orders/{orderId}` can retry payment while the order remains `PendingPayment`.

## Payment Provider Abstraction

Payment provider integration is prepared behind Application-layer contracts:

- `IPaymentProvider`
- `IPaymentInitiationService`
- `PaymentInitiationRequest`
- `PaymentInitiationResult`
- `PaymentVerificationRequest`
- `PaymentVerificationResult`
- `PaymentWebhookEvent`
- `PaymentProviderOptions`

Runtime payment providers are selected with `PaymentProvider__ProviderName`. `FakePaymentProvider` remains the default outside explicit provider configuration. Phase 8C adds `PayFastPaymentProvider` as the first real adapter foundation behind the same abstraction. It returns a Swyftly-hosted checkout bridge URL, renders a signed PayFast hosted-checkout form server-side, verifies PayFast ITN signatures, optionally performs PayFast remote ITN validation, and keeps PayFast refunds as manual provider actions until an automatic refund API is verified.

Configuration keys:

```text
PaymentProvider__ProviderName
PaymentProvider__DefaultCurrency
PaymentProvider__SuccessRedirectUrl
PaymentProvider__FailureRedirectUrl
PaymentProvider__WebhookSigningSecret
PaymentProvider__FakeOutcome
PaymentWebhookPayloadRetention__Enabled
PaymentWebhookPayloadRetention__RetentionDays
PaymentWebhookPayloadRetention__BatchSize
```

`PaymentProvider__WebhookSigningSecret` is used only by the fake provider. `PaymentProvider__FakeOutcome` supports `Success` or `Failure` for development/test simulation. `PaymentWebhookPayloadRetention` controls worker-side redaction of stored raw webhook payload JSON; event metadata remains for reconciliation.

PayFast configuration keys:

```text
PayFast__MerchantId
PayFast__MerchantKey
PayFast__Passphrase
PayFast__ProcessUrl
PayFast__ValidateUrl
PayFast__NotifyUrl
PayFast__CheckoutBridgeBaseUrl
PayFast__RequireRemoteValidation
```

PayU remains deferred until merchant technical docs and sandbox credentials are available:

```text
PayU__Region
PayU__Username
PayU__Password
PayU__Safekey
PayU__PaymentPageUrl
PayU__WebhookSigningKey
```

Provider comparison and implementation notes live in `docs/payment-provider-comparison.md` and `docs/payment-provider-implementation-notes.md`. No provider SDK or payment-provider database migration is active. Production startup rejects `PaymentProvider__ProviderName=Fake` and requires external HTTPS PayFast URLs plus remote validation when `PaymentProvider__ProviderName=PayFast`.

## Payments And Webhooks

Buyer payment initiation requires a buyer JWT role and operates only on the authenticated buyer's pending-payment order.

```http
POST /api/payments/initiate
```

Request:

```json
{
  "orderId": "00000000-0000-0000-0000-000000000000"
}
```

Response:

```json
{
  "paymentId": "00000000-0000-0000-0000-000000000000",
  "orderId": "00000000-0000-0000-0000-000000000000",
  "provider": "Fake",
  "providerReference": "fake_reference",
  "amount": 999.98,
  "currency": "ZAR",
  "status": "Pending",
  "checkoutUrl": "http://localhost:4200/checkout/success?orderId=00000000-0000-0000-0000-000000000000&providerReference=fake_reference"
}
```

Payment initiation creates a local `Payment` row before calling the configured provider abstraction. The provider checkout URL is stored on the payment and returned on duplicate active-payment initiation. If the fake provider is configured to fail, the local payment is marked `Failed`; retrying the same pending-payment order creates a new local payment. Cancelled payment-failed orders are not reopened.

PayFast payment initiation uses the local payment id as `m_payment_id`/provider reference and returns a backend checkout bridge URL:

```http
GET /api/payments/payfast/checkout/{providerReference}
```

The checkout bridge is anonymous and returns `text/html` with an auto-submit form posting to `PayFast__ProcessUrl`. It only serves pending local PayFast payments and includes signed PayFast fields, `notify_url`, return/cancel URLs, order id, payment id, amount, and item summary.

Payment webhooks are anonymous because provider callbacks cannot carry buyer JWTs. They are protected with the `Webhook` rate-limit policy, reject payloads larger than 64 KB, verify the provider route, and require provider signature verification before parsing or persisting any webhook event. The fake provider requires `application/json` or `application/*+json` and expects the `X-Swyftly-Fake-Signature` header to contain the lowercase hex HMAC-SHA256 of the raw request body using `PaymentProvider__WebhookSigningSecret`. PayFast requires `application/x-www-form-urlencoded` at `/api/payments/webhook/payfast`.

```http
POST /api/payments/webhook/{provider}
```

Fake webhook payload:

```json
{
  "eventId": "evt_1",
  "eventType": "payment.paid",
  "providerReference": "fake_reference",
  "status": "Paid",
  "occurredAtUtc": "2026-05-18T12:01:00Z"
}
```

Webhook handling stores a sanitized JSON representation of the provider event in `payment_events` and uses `(Provider, ProviderEventId)` to avoid duplicate processing. JSON webhooks are wrapped as `payloadType=json`; PayFast form ITNs are wrapped as `payloadType=form`. Sensitive fields such as signatures, tokens, secrets, passwords, passphrases, card fields, and CVV/CVC values are redacted before persistence. A worker-side retention cleanup later replaces expired raw payload JSON with a `payloadType=redacted` marker and sets `raw_payload_redacted_at_utc`, while preserving provider event id, event type, received/processed timestamps, processing status, and payment linkage. A successful payment webhook marks the payment and order as paid, confirms active cart reservations, creates successful-payment ledger entries, credits seller pending balance, and clears the buyer's active cart. A failed payment webhook marks payment failed, cancels the order, cancels active reservations, and releases reserved stock without clearing the cart. Authorized, failed, cancelled, or unknown webhook statuses do not clear the cart. Concurrent duplicate webhooks that race on the unique index are treated as idempotent success by reloading the existing event result.

PayFast ITN handling maps `COMPLETE` to paid, maps failed/cancelled statuses only for eligible pending payments, and stores unknown/intermediate statuses without settlement. PayFast paid settlement is rejected when `m_payment_id`, amount, or currency does not match the local payment. Invalid content type returns `415`, oversized payloads return `413`, and invalid signatures or failed remote validation return `401`. These failures do not create `payment_events` rows. Duplicate provider event ids return the existing processing result and do not create duplicate ledger entries. Webhook processing is wrapped in a database transaction before storing the event and applying payment/order/ledger changes.

Current successful-payment ledger entries:

- `BuyerPaymentReceived`
- `PlatformCommissionRecorded`
- `PaymentProviderFeeRecorded`
- `SellerPendingBalanceCredited`

Ledger fee configuration keys:

```text
Ledger__PlatformCommissionRatePercent
Ledger__PaymentProviderFeeRatePercent
Ledger__PaymentProviderFixedFee
```

Provider status-query settlement, automatic PayFast refunds, PayU South Africa, and real payout provider integrations remain future work.

## Seller Balances And Payouts

Seller balance and payout endpoints use JWT roles. Sellers can read only their own balances and payouts. Admin payout reads require `FinanceRead`; hold and make-available require `FinanceOperate`; release, process, and reconcile require `FinanceApprove`. Dual control applies to every role, including `SuperAdmin`.

```http
GET /api/seller/balance
GET /api/seller/payouts
GET /api/admin/payouts/pending
POST /api/admin/payouts/{id}/hold
POST /api/admin/payouts/{id}/release
POST /api/admin/payouts/{id}/make-available
POST /api/admin/payouts/{id}/process
POST /api/admin/payouts/{id}/reconcile
```

`GET /api/seller/balance` returns pending, available, and held balances per currency:

```json
{
  "sellerId": "00000000-0000-0000-0000-000000000000",
  "balances": [
    {
      "currency": "ZAR",
      "pendingBalance": 875.00,
      "availableBalance": 0,
      "heldBalance": 0
    }
  ]
}
```

Successful payment ledger processing creates a `Pending` seller payout and a payout item linked to the seller-pending ledger entry. Seller payout history intentionally hides internal ledger, order, and payment identifiers; it returns payout totals plus item source type and amount only. Admin finance payout reads retain item, ledger, order, and payment identifiers for reconciliation.

Seller payout item response shape:

```json
{
  "amount": 875.00,
  "currency": "ZAR",
  "createdAtUtc": "2026-05-19T10:00:00Z",
  "sourceType": "Order"
}
```

Admin payout action request:

```json
{
  "reason": "Dispute review required."
}
```

Holding a payout changes it to `OnHold`, preserves whether it was held from `Pending` or `Available`, moves the payout amount into held balance, and writes a `PayoutHeld` audit log. Releasing a held payout returns it to the held-from status and writes a `PayoutReleased` audit log.

`POST /make-available` moves a `Pending` payout into `Available` and moves seller balance from pending to available. `POST /process` starts provider payout processing, moves the payout amount out of available balance, calls the configured payout provider with the payout id as the idempotency key, and marks the payout `PaidOut`, `Processing`, or `Failed` based on provider status. Failed payouts restore available balance for retry. `POST /reconcile` refreshes a `Processing` payout from the provider and finalizes paid/failed status.

Payout provider configuration keys:

```text
PayoutProvider__ProviderName
PayoutProvider__FakeOutcome
```

The current implementation is `FakePayoutProvider`; no real payout provider SDK or bank integration is present.

Angular admin route `/admin/payouts` lists pending/on-hold payouts and calls the hold, release, make-available, process, and reconcile endpoints with required reason input. The UI shows operate/approve eligibility, but backend finance policies and dual-control checks remain authoritative.

## Admin Product Review

Admin product review endpoints require an `Admin` or `SuperAdmin` JWT role.

```http
GET /api/admin/products/pending-review
GET /api/admin/products/{productId}
POST /api/admin/products/{productId}/approve
POST /api/admin/products/{productId}/reject
POST /api/admin/products/{productId}/request-changes
GET /api/admin/products/pending-revisions
GET /api/admin/products/revisions/{revisionId}
POST /api/admin/products/revisions/{revisionId}/approve
POST /api/admin/products/revisions/{revisionId}/reject
```

`GET /pending-review` returns products in `PendingReview` or `NeedsAdminReview`. Product detail responses include seller status, attributes, variants, images, AI moderation results, and product audit trail entries.

`POST /approve` publishes the product only when the seller is verified. Products with unresolved high-risk moderation results require an override reason:

```json
{
  "overrideReason": "Supplier documents reviewed manually."
}
```

`POST /reject` and `POST /request-changes` require a reason:

```json
{
  "reason": "Add clearer size measurements."
}
```

Rejecting moves the product to `Rejected`; requesting changes moves it to `ChangesRequested`, which remains seller-editable so the seller can fix and resubmit the listing. Every approval, rejection, and change request writes an audit-log entry.

Angular admin product routes `/admin/products` and `/admin/products/{productId}` use these endpoints unchanged. The queue applies client-side search/status/seller/risk filters over the pending-review response. The detail screen presents seller context, listing data, image review with thumbnail selection and fallback, attributes, variants, AI moderation flags, unchanged review-action payloads, and audit trail.

Published listing revisions are reviewed separately from initial product submissions. `GET /pending-revisions` returns seller-submitted revisions for live published products. Admin approval applies the staged listing fields, attributes, tags, and proposed final image set to the live product, writes an audit log, and refreshes search indexing and embeddings. Admin rejection stores the rejection reason, writes an audit log, and leaves the live buyer-visible product unchanged.

## Seller Product Drafts

Seller product endpoints require a seller JWT role and always operate on products owned by the authenticated seller. Pending sellers may create and edit drafts, but only verified sellers can submit products for review.

```http
GET /api/seller/catalog/categories
POST /api/seller/products
GET /api/seller/products
GET /api/seller/products/{id}
PUT /api/seller/products/{id}
POST /api/seller/products/{id}/variants
PUT /api/seller/products/{id}/variants/{variantId}
DELETE /api/seller/products/{id}/variants/{variantId}
POST /api/seller/products/{id}/images
POST /api/seller/products/{id}/images/upload
PUT /api/seller/products/{id}/images/{imageId}
DELETE /api/seller/products/{id}/images/{imageId}
POST /api/seller/products/{id}/submit-review
GET /api/seller/products/{id}/revision
PUT /api/seller/products/{id}/revision
POST /api/seller/products/{id}/revision/images/upload
PUT /api/seller/products/{id}/revision/images/{revisionImageId}
DELETE /api/seller/products/{id}/revision/images/{revisionImageId}
POST /api/seller/products/{id}/revision/submit-review
POST /api/seller/products/{id}/revision/cancel
```

Product drafts support category-specific attributes as a JSON object. Stored product image records reference uploaded images by URL/storage key; image binary data is not stored in PostgreSQL. New uploads create `media_assets` metadata plus `thumb`, `card`, and `detail` WebP variants, and product image URLs point at the `detail` variant. The legacy image-reference endpoint remains for compatibility, while the Angular editor uses multipart upload.

`POST /api/seller/products/{id}/images/upload` accepts multipart fields `file`, `altText`, `sortOrder`, and `isPrimary`. It accepts JPEG, PNG, and WebP only, rejects empty, oversized, mismatched, undecodable, over-dimensioned, or scanner-rejected files, requires a seller-owned editable product (`Draft`, `Rejected`, or `ChangesRequested`), and clears existing primary images when a new primary image is uploaded.

`PUT /api/seller/products/{id}/images/{imageId}` updates image metadata only. URL and storage key remain immutable. The product and image must belong to the authenticated seller, the product must be seller-editable (`Draft`, `Rejected`, or `ChangesRequested`), and `sortOrder` must be non-negative. When `isPrimary` is `true`, the API clears the primary flag from the other images on the product and marks the selected image as primary.

Request:

```json
{
  "altText": "Model wearing a black dress",
  "sortOrder": 10,
  "isPrimary": true
}
```

Response: the existing `SellerProductDetailResponse`.

Published product listing changes use the revision endpoints instead of mutating the live product directly. A seller-owned published product can have one active revision where proposed title, slug, category, brand, descriptions, tags, category attributes, and final image set are staged. Uploading revision images uses the same media validation, scanning, storage, and variant generation path as draft uploads. Submitting a revision moves it to admin review; cancelling leaves the published product unchanged.

Media storage configuration is backend-only. `ImageStorage__ProviderName` supports `Local` and `S3`; local storage is served by the API under `ImageStorage__PublicBasePath`, while S3-compatible storage uses bucket/service/public-CDN settings. `MediaScanning__ProviderName=TrustLocalClean` is the local/test scanner. `MediaScanning__RequireExternalScannerInProduction=true` makes readiness fail in production unless a non-local scanner is configured. `MediaCleanup__GracePeriodHours` and `MediaCleanup__BatchSize` control worker cleanup of pending-delete or unreferenced media assets.

Submission requires:

- Verified seller status.
- Category, title, slug, short description, and full description.
- Required category attributes.
- At least one product image.
- At least one active variant with available stock.

Submission runs business-rule moderation before the product enters the admin review queue. Counterfeit-risk wording, risky beauty claims, and missing beauty safety fields are stored in `ai_moderation_results`. Products with high-risk moderation flags move to `NeedsAdminReview`; otherwise they move to `PendingReview`.

## Seller Inventory

Seller inventory endpoints require a seller JWT role and always operate on product variants owned by the authenticated seller. Inventory adjustment is intentionally separate from the product editor so sellers can manage stock for published listings without reopening draft-only product editing.

```http
GET /api/seller/inventory
GET /api/seller/inventory/export.csv
GET /api/seller/inventory/import-template.csv
POST /api/seller/inventory/import/preview
POST /api/seller/inventory/bulk-adjust
POST /api/seller/inventory/{variantId}/adjust
```

`GET /api/seller/inventory` returns flattened variant rows with product title/status/slug, primary image, SKU, size, colour, price, stock quantity, reserved quantity, available quantity, variant status, and updated timestamp.

CSV export/template columns:

```text
variantId,sku,productTitle,productSlug,size,colour,price,reservedQuantity,availableQuantity,stockQuantity,status,updatedAtUtc
```

Adjustment request:

```json
{
  "stockQuantity": 12,
  "status": "Active",
  "reason": "Cycle count correction"
}
```

Supported `status` values are `Active`, `Inactive`, and `OutOfStock`. The API rejects empty reasons, variants owned by another seller, invalid statuses, negative stock, and stock quantities below the current reserved quantity. Each successful adjustment writes a `SellerInventoryAdjusted` audit-log entry for the product variant.

Bulk inventory import is stocktake-only. The preview endpoint accepts multipart `file` CSV upload and does not mutate inventory. The apply endpoint accepts JSON:

```json
{
  "reason": "Monthly stocktake import",
  "items": [
    {
      "variantId": "00000000-0000-0000-0000-000000000000",
      "sku": "DRESS-M-BLACK",
      "stockQuantity": 12,
      "status": "Active"
    }
  ]
}
```

Rows match by `variantId` when present, otherwise by seller-owned `sku`. If both are present, they must refer to the same variant. Preview/apply responses include total, valid, error, changed, and unchanged row counts plus per-row current/proposed values and validation messages. Bulk apply rejects invalid rows and applies valid batches all-or-nothing; each changed variant writes a `SellerInventoryBulkAdjusted` audit-log entry. Batches are capped at 500 rows. Bulk import does not change price, SKU, size, colour, product content, variant structure, or reservations.

Angular seller routes:

```http
/seller/inventory
/seller/settings/store
```

`/seller/inventory` uses these inventory endpoints for searchable/filterable stock operations, CSV export/template download, import preview, and bulk apply. `/seller/settings/store` reuses the existing seller onboarding profile, storefront, and address endpoints for post-verification store settings. Payout-provider/bank details remain read-only in that screen until a secure re-verification workflow exists.

## Seller Delivery Methods

Seller delivery-method endpoints require a seller JWT role and always operate on methods owned by the authenticated seller. They are provider-free rates for the current single-seller checkout flow; there is no carrier API, label purchase, address verification, pickup-point network, payment mutation, or payout mutation.

```http
GET /api/seller/delivery-methods
POST /api/seller/delivery-methods
PUT /api/seller/delivery-methods/{deliveryMethodId}
POST /api/seller/delivery-methods/{deliveryMethodId}/activate
POST /api/seller/delivery-methods/{deliveryMethodId}/deactivate
```

Request:

```json
{
  "name": "Standard courier",
  "description": "Door-to-door delivery within South Africa.",
  "methodType": "Standard",
  "countryCode": "ZA",
  "province": "Gauteng",
  "basePrice": 75.00,
  "freeShippingThreshold": 1000.00,
  "estimatedMinDays": 2,
  "estimatedMaxDays": 5,
  "displayOrder": 10,
  "isActive": true
}
```

Supported `methodType` values are `Standard`, `Express`, and `LocalCourier`. `province` is optional; country-wide and province-specific active methods can both match a checkout address, and the buyer chooses the option. Validation rejects missing names, invalid country codes, negative prices, invalid day ranges, and methods owned by another seller. Create/update/activate/deactivate actions write seller audit-log entries.

## AI Listing Assistant

Prompt 22 added backend schema and Application DTOs for future AI listing suggestions. Prompt 23 added the backend service abstraction, prompt builder, suggestion validator, usage logger, and local fake provider.

Seller AI suggestion generation requires a seller JWT role and always operates on products owned by the authenticated seller. The product must be `Draft` or `Rejected`.

```http
POST /api/seller/products/{productId}/ai-suggestions
```

Request:

```json
{
  "sellerNotes": "Lightweight summer dress with a relaxed fit.",
  "productTypeHint": "Dress",
  "selectedCategoryId": "00000000-0000-0000-0000-000000000000",
  "knownAttributes": {
    "occasion": "summer"
  },
  "imageIds": ["00000000-0000-0000-0000-000000000000"]
}
```

Response:

```json
{
  "suggestionId": "00000000-0000-0000-0000-000000000000",
  "recommendedTitle": "AI-assisted product title",
  "titleSuggestions": ["AI-assisted product title"],
  "shortDescription": "A concise marketplace-ready product summary.",
  "fullDescription": "A draft listing description.",
  "suggestedCategoryId": null,
  "suggestedCategoryPath": null,
  "attributes": {},
  "tags": ["draft", "ai-assisted"],
  "seo": {},
  "imageAltText": {},
  "missingFields": ["brand", "material", "exact sizing"],
  "riskFlags": [],
  "qualityScore": 65
}
```

The AI assistant endpoint does not apply suggestions directly to a product. It persists a draft suggestion and records usage logs for successful, invalid, or failed provider responses. Provider secrets are not stored in the schema and Angular must not call an AI provider directly.

`seo` and `imageAltText` are response placeholders until later prompts expand the AI suggestion schema and apply workflow. A rate-limit placeholder is documented on the endpoint metadata; a concrete rate-limit policy is still future work.

Sellers can apply selected AI suggestion fields after review:

```http
POST /api/seller/products/{productId}/ai-suggestions/{suggestionId}/apply
```

Request:

```json
{
  "fieldsToApply": ["title", "shortDescription", "attributes", "tags", "imageAltText"],
  "editedValues": {
    "title": "Seller reviewed title",
    "attributes": {
      "size": "M",
      "colour": "Black"
    },
    "tags": ["summer", "reviewed"],
    "imageAltText": {
      "00000000-0000-0000-0000-000000000000": "Model wearing a black summer dress"
    }
  },
  "confirmRiskFlags": false
}
```

Supported `fieldsToApply` values are `title`, `shortDescription`, `fullDescription`, `category`, `attributes`, `tags`, and `imageAltText`. The endpoint validates product ownership, suggestion ownership, product editability, category existence, category attribute values, and product image ownership. If a suggestion has risk flags, `confirmRiskFlags` must be `true` before applying fields. Each applied field writes an `ai_suggestion_field_audits` row showing the AI value and seller-final value.

## Current Scope

Health/readiness, identity foundation with HttpOnly refresh cookies, seller onboarding, seller inventory adjustment and bulk CSV import/export, seller delivery-method/rate management, seller store settings UI, admin seller approval and Angular moderation polish, admin product review and Angular moderation polish, admin buyer-review moderation, admin audit-log UI polish, admin dashboard summary, admin category/attribute catalog management, admin marketplace finance reports, admin order/payment read APIs and Angular read screens, public product search, public verified-buyer product review reads, buyer-facing shop/category/product/seller/cart/checkout/assistant/visual-search pages, buyer account order/return/dispute/support/wishlist/review/notification/settings Angular routes, buyer profile settings, saved delivery addresses, delivery instructions, order delivery-address and delivery-method snapshots, fulfilment exception tracking, in-app notification preferences, and buyer transactional email notification outbox delivery, seller product draft endpoints with production-hardened media uploads and image metadata updates, S3-compatible image storage configuration, media scanning abstraction, WebP image variants, media cleanup, moderation-aware published listing revisions, polished seller product editor UX, buyer cart endpoints with saved-for-later wishlist moves, product image metadata, and seller shipping-option quotes, buyer wishlist/review/notification backend APIs with saved-state hydration and wishlist-to-cart moves, inventory reservation services, order creation from cart, payment provider abstractions with fake and PayFast providers, local payment persistence with retryable checkout URLs, idempotent payment webhook handling including duplicate race fallback and paid-cart cleanup, payment reconciliation review evidence, seller delivery confirmation, successful-payment ledger entries, seller balance/payout read APIs, admin payout hold/release/make-available/process/reconcile with a fake payout provider, refund workflow with ledger reversals, payout adjustments, and manual PayFast refund confirmation, finance dual-control policies, dispute workflow with evidence/messages/admin resolution, admin finance Angular routes for refunds/payouts/disputes, support ticket workflow with private internal notes and Angular support queue/detail routes, seller ad campaign draft/submission API and Angular dashboard, seller analytics dashboard, admin ad campaign review, ad event tracking and seller campaign metrics, product moderation, AI suggestion persistence/DTOs, the backend AI listing assistant service abstraction, seller AI suggestion generation/apply endpoints, the Angular seller AI assistant UI, buyer AI shopping intent extraction/recommendations, buyer visual search with a fake vision provider, and private product embedding generation exist. PayFast sandbox verification, payment-provider status-query settlement, automatic PayFast refunds, real payout provider integration, carrier integration, provider/carrier rate calculation, address verification, carrier selection, variant/pricing revision workflows, production vision AI, buyer-favoured dispute money movement, admin order/payment mutation workflows, SMS/push notification delivery, seller/admin email workflows, payout-bank re-verification workflows, hard-delete taxonomy operations, bulk category import, and taxonomy versioning are intentionally not implemented yet.

## API Rules

- Keep endpoints thin.
- Validate ownership and roles at the API/application boundary.
- Do not expose provider secrets or internal ledger implementation details.
- Use idempotency for webhooks and other external event handlers.

## Rate Limiting

Swyftly uses ASP.NET Core fixed-window rate limiting with named policies configured under `SwyftlyRateLimits`.

Current policies:

- `Auth`: login, public registration, refresh, and logout.
- `Ai`: seller AI listing suggestion generation, stricter than normal browsing.
- `ProductWrite`: seller product creation.
- `Payment`: buyer payment initiation.
- `Webhook`: anonymous payment webhooks.
- `AdImpression`: anonymous ad impression tracking.
- `AdClick`: anonymous ad click tracking.
- `Search`: public product search.

When a policy is exceeded, the API returns HTTP `429` with a small ProblemDetails-style JSON response:

```json
{
  "title": "RateLimit.Exceeded",
  "status": 429,
  "detail": "Too many requests. Please wait before trying again."
}
```

Development settings keep limits relaxed enough for local work. Tests verify the `Search` and `AdImpression` policies return `429` when their configured limits are exceeded.
