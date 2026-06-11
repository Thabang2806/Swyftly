# Seller Flow QA Results

Date: 2026-05-27

## Scope

Phase 10Q completed the seeded desktop/mobile seller lifecycle browser sign-off after Phase 10P restored the local verification pipeline. This pass was QA-led: no seller workflow, public API, Angular route, payment, payout, carrier-provider, or payout-bank-storage behavior was added.

## Tooling And Defects Fixed

- `scripts/seed-dev-users.ps1` propagates failed `dotnet run` exit codes instead of returning success after a failed build/seed.
- `scripts/verify-dev-environment.ps1` checks .NET, NuGet source/connectivity, restore assets, PostgreSQL TCP reachability, Node/npm, frontend dependencies, Karma config, and Chrome availability before long QA runs.
- Fixed a backend compile error in the published variant-revision CSV source resolver caused by local variable shadowing.
- Fixed seller product integration test setup so verified test sellers publish their storefront before public product visibility assertions.
- Fixed the variant-revision CSV header test to assert the quoted CSV format emitted by the export/template writer.
- Fixed checkout/order creation in the seeded browser flow by making `InventoryMovementRecorder.LoadSnapshotAsync` use an EF-translatable variant filter before projecting the inventory movement snapshot.
- Fixed mobile overflow on `/admin/sellers` and `/admin/products` by adding final mobile overrides for admin review layouts and moderation rows in the lazy admin luxury stylesheet.

## Seed And API Smoke

Seed command run twice successfully:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo
```

The browser QA pass used deterministic local passwords:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo
```

Authenticated API smoke results with the local API running on `http://localhost:5240`:

| Check | Result |
| --- | --- |
| Pending sellers | 1 row from `/api/admin/sellers/pending`. |
| Pending products | 1 row from `/api/admin/products/pending-review`. |
| Pending ad campaigns | 1 row from `/api/admin/ad-campaigns/pending`. |
| Verified seller products | 9 rows from `/api/seller/products`. |
| Seller dashboard summary | Returned `generatedAtUtc`. |
| Pending seller onboarding | `UnderReview` from `/api/seller/onboarding`. |
| Buyer checkout/order smoke | Created an order, initiated fake payment, posted the signed fake webhook, advanced fulfilment to delivered, and created a buyer return request for seller-return QA routes. |

## Browser QA Evidence

Headless Chrome was run against the local API and Angular dev server at desktop `1440px` and mobile `430px`. The sweep covered 48 route/viewport combinations across public seller entry, seller onboarding, seller products, product editor, inventory, orders, returns, ads, analytics, settings, notifications, and admin seller/product/ad review routes.

Evidence files were written under the local temp folder:

- `%TEMP%\mabuntle-10q-browser-qa\qa-summary.json`
- `%TEMP%\mabuntle-10q-browser-qa\qa-summary.md`
- `%TEMP%\mabuntle-10q-browser-qa\*.png`

Final sweep summary:

| Viewport | Route checks | Max overflow | HTTP errors | Console errors | Login redirects |
| --- | ---: | ---: | ---: | ---: | ---: |
| Desktop 1440px | 24 | 0 | 0 | 0 | 0 |
| Mobile 430px | 24 | 0 | 0 | 0 | 0 |

## QA Checklist

| Area | Routes | Status | Notes |
| --- | --- | --- | --- |
| Seller acquisition | `/sell`, `/register/seller` | Pass | Public seller entry and seller registration loaded on desktop/mobile. |
| Seller onboarding | `/seller` | Pass | Pending seller state is visible and mobile-safe. |
| Seller settings | `/seller/settings/store` | Pass | Store profile, delivery methods, policies, payout change request, and notification preference surfaces loaded without route failure. |
| Seller products | `/seller/products`, `/seller/products/new`, `/seller/products/:id/edit` | Pass | Seeded product/editor routes loaded; listing revision, variant revision, and bulk CSV staging remain clearly admin-reviewed. |
| Seller inventory | `/seller/inventory` | Pass | Inventory adjustment, CSV import/export, scanner-style search, and stock-ledger panels loaded without overflow. |
| Seller ads | `/seller/ads`, `/seller/ads/new`, `/seller/ads/:id` | Pass | Seeded pending campaign is visible and seller ad routes loaded. |
| Seller operations | `/seller/orders`, `/seller/orders/:orderId`, `/seller/returns`, `/seller/returns/:returnRequestId` | Pass | Fulfilment, carrier context, policy context, return restock, and stock-ledger panels loaded against a real delivered order/return. |
| Seller notifications | `/seller/notifications` | Pass | Notification list/read/read-all route and realtime-aware surfaces loaded. |
| Seller analytics | `/seller/analytics` | Pass | Analytics performance, funnel/source reporting, exports, and scheduled report UI loaded. |
| Admin approval queues | `/admin/sellers`, `/admin/sellers/:sellerId`, `/admin/products`, `/admin/products/:productId`, `/admin/ads`, `/admin/ads/:adId` | Pass | Seeded pending seller/product/ad rows were reachable; mobile overflow on seller/product queues was fixed and rechecked. |

## Verification Evidence

Commands run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify-dev-environment.ps1
dotnet restore backend\Mabuntle.sln
dotnet build backend\Mabuntle.sln --no-restore
dotnet test backend\Mabuntle.sln --no-build
dotnet dotnet-ef migrations has-pending-model-changes --project backend\src\Mabuntle.Infrastructure --startup-project backend\src\Mabuntle.Api --context MabuntleDbContext --no-build
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo
cd frontend\mabuntle-web
cmd /c npm run build
cmd /c npm run test:ci
```

Results:

| Check | Result |
| --- | --- |
| Preflight | Passed all checks. |
| Restore | Failed inside sandbox with `NU1301`; passed with approved outside-sandbox network access. |
| Backend build | Passed: 0 warnings, 0 errors. |
| Backend tests | Passed: 210 unit tests, 231 integration tests, 3 opt-in PostgreSQL tests skipped. |
| EF pending model check | Passed: no model changes since the last migration. |
| Dev seed | Passed with `-ResetPasswords -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo`. |
| Browser QA | Passed 48 desktop/mobile route checks after the two small fixes. |
| Angular build | Passed. Prerendered 52 static routes. Initial bundle total is `650.84 kB`, over the unchanged `650.00 kB` budget by 842 bytes. |
| Angular tests | Passed: 290 specs in Chrome Headless. |
| Frontend source hygiene | Mojibake scan returned no matches in `frontend/mabuntle-web/src`. |

## Deferred Follow-Ups

- Admin queues remain pending-review focused rather than all-state operational lists.
- Hardware barcode scanner SDK integration remains future work.
- Deeper campaign attribution modelling, session-path analysis, and historical funnel backfill remain future work.
- Real carrier-provider integration and sensitive payout-bank storage remain future work.
- The existing Angular initial-bundle warning remains at `650.84 kB` and was not materially worsened by this QA pass.
