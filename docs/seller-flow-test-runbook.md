# Seller Flow Test Runbook

Use this runbook to test the seller lifecycle locally from onboarding through admin approvals, product publishing, and ad campaign approval.

For the latest verification evidence and deferred QA notes, see `docs/seller-flow-qa-results.md`.

## Phase 10Q QA Note

Phase 10Q completed the seeded desktop/mobile seller lifecycle browser sign-off after Phase 10P restored the local verification path. The preflight helper, backend restore, build, tests, EF model-drift check, seeded seller-flow data, API smoke checks, Angular build, Angular specs, source hygiene scan, and 48 route/viewport browser checks passed. In this Codex environment, `dotnet restore` still needs approved outside-sandbox network access to reach `api.nuget.org`.

## Seed Data

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo
```

The command is idempotent for repeated local setup runs. It creates the standard dev accounts, published sample catalog data, and seller-flow review records.

If your local database already has the seed users with a different password, run the same command with `-ResetPasswords` so the accounts match the password used for QA:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo
```

## Accounts

| Email | Purpose |
| --- | --- |
| `admin@mabuntle.local` | SuperAdmin/Admin review account for seller, product, and ad approvals. |
| `seller@mabuntle.local` | Verified seller with a published storefront, delivery method, sample products, pending product review, and pending ad review. |
| `seller.pending@mabuntle.local` | Seller with completed onboarding submitted for admin verification. |

Use the password passed to the seed command for all accounts.

## Seller Registration And Onboarding

1. Open `/sell` and confirm the public seller entry page explains the review-led selling path, readiness checklist, and deferred provider integrations.
2. Follow the create-account CTA to `/register/seller` and create a new seller account if you want to test the registration path manually.
3. Login as the new seller or `seller.pending@mabuntle.local`.
4. Open `/seller`.
5. Confirm the onboarding sections are complete for the seeded pending seller.
6. Upload an optional verification evidence file, such as a PDF or image, and confirm it appears in the supporting-documents list with download/remove actions.
7. Confirm removing evidence soft-removes it from the active list and does not affect existing submit-verification eligibility.
8. Confirm the under-review status panel explains that product drafts can be prepared, but product submission, publishing, and ads require seller verification.
9. If testing a rejected seller, confirm the rejection reason appears on `/seller`, the onboarding wizard is editable for resubmission, and optional evidence can be updated.

Expected seeded state: `seller.pending@mabuntle.local` appears in `/admin/sellers` with `UnderReview`.

## Admin Seller Approval

1. Login as `admin@mabuntle.local`.
2. Open `/admin/sellers` in the default `Needs attention` view.
3. Find `Demo Pending Seller` / `Demo Pending Atelier`.
4. Open the detail page and review profile, storefront, address, payout placeholder data, store policy, and optional verification evidence.
5. Download any attached evidence from the admin evidence panel and confirm the file is served through the API rather than a public URL.
6. Approve or reject the seller.

Expected approval result: the seller profile becomes `Verified`; rejection stores the review reason and keeps the seller out of verified public catalog eligibility.

## Product Creation And Review

1. Login as `seller@mabuntle.local`.
2. Open `/seller/products`.
3. Confirm the seeded product `Demo Review Satin Slip Skirt` is visible with `PendingReview`.
4. Open `/seller/products/new` to test creating a new draft manually.
5. Add required listing details, attributes, image, and variants, then submit for review.

Expected seeded state: `Demo Review Satin Slip Skirt` appears in `/admin/products` and can be approved, rejected, or sent back for changes.

## Admin Product Approval

1. Login as `admin@mabuntle.local`.
2. Open `/admin/products` in the default `Needs attention` view.
3. Find `Demo Review Satin Slip Skirt`.
4. Open the detail page and review listing copy, images, attributes, variants, seller context, and moderation flags.
5. Approve, reject, or request changes.

Expected approval result: approved products become `Published` and become eligible for buyer catalog visibility and ad campaign selection.

## Ad Campaign Creation And Review

1. Login as `seller@mabuntle.local`.
2. Open `/seller/ads`.
3. Confirm the seeded `Demo Review Campaign - Rose Linen Dress` is visible with `PendingReview`.
4. Open `/seller/ads/new` to test creating a campaign manually.
5. Select only published, in-stock products and save the campaign draft.
6. Submit the campaign for review.

Expected seeded state: the seeded campaign appears in `/admin/ads` and is eligibility-valid.

## Admin Ad Approval

1. Login as `admin@mabuntle.local`.
2. Open `/admin/ads` in the default `Needs attention` view.
3. Find `Demo Review Campaign - Rose Linen Dress`.
4. Open the detail page and review seller verification, attached products, budget, eligibility, and audit trail.
5. Approve or reject the campaign.

Expected approval result: approved campaigns become `Active`; rejected campaigns return to the seller with a rejection reason.

## Stock Ledger Traceability

1. Login as `buyer@mabuntle.local` and checkout a seeded product through the real cart and checkout flow.
2. Login as `seller@mabuntle.local`, open `/seller/inventory`, and load movement history for the purchased SKU or barcode.
3. Confirm reservation-created and payment-confirmed movements show reserved deltas while stock quantity remains unchanged.
4. Open `/seller/orders/{orderId}` for the order and confirm the Stock ledger panel lists order-related reservation/payment events.
5. After a delivered-order return request is approved, open `/seller/returns/{returnRequestId}` and record a restock decision for each returned item. Use `quantityRestocked: 0` for damaged/non-sellable items or a positive quantity after inspection.
6. Confirm positive restock decisions add `ReturnRestocked` rows to the return and inventory stock ledger panels and increase live variant stock. Confirm zero-restock decisions record inspection context without adding stock.
7. After refund completion, confirm refund movements remain contextual and do not trigger additional automatic stock changes.

Expected result: stock ledger rows explain checkout holds, webhook settlement/failure release, return requests, seller inspection/restock decisions, and refunds. Returns/refunds still do not automatically restock; only explicit seller restock decisions or `/seller/inventory` adjustments change physical stock.

Historical backfill check:

1. Login as an admin and run `POST /api/admin/inventory-ledger/backfill` with `dryRun: true` to preview missing historical reservation, return, and refund stock-ledger rows.
2. Review `createdMovementCount`, `skippedExistingCount`, `skippedAmbiguousCount`, and `warnings`.
3. Run the same request with `dryRun: false` only when the preview is acceptable.
4. Re-run the apply request and confirm it is idempotent: new rows are not duplicated and existing rows are reported as skipped.

Expected result: backfill writes seller-visible traceability rows and an audit log only. It does not change stock, reserved quantities, orders, payments, returns, refunds, payouts, carts, or financial ledger entries.

## Seller Analytics And Exports

1. Login as `seller@mabuntle.local`.
2. Open `/seller/analytics`.
3. Apply a date range that includes seeded or manually-created order/ad/return activity and switch between daily and weekly buckets.
4. Browse a seller storefront/product detail as a buyer with and without UTM query strings, add a product to cart, start checkout, and complete the local fake-payment flow so first-party funnel and source-attribution events can populate.
5. Confirm the page shows sales trend rows, product performance, inventory/barcode stock signals, ad performance, customer-care summary cards, storefront conversion funnel cards/tables, source breakdowns, and source-category funnel filtering without exposing buyer personal details.
6. Use the Sales, Products, Inventory, Ads, Returns, and Funnel CSV export links and confirm each download is scoped to the seller account. Funnel CSV should include product-level source columns.
7. In the Scheduled reports panel, enable a weekly `Last30Days` digest for Monday 08:00 in `Africa/Johannesburg`, save it, and confirm a next-send timestamp appears.
8. Click Send test digest and confirm `/seller/notifications` receives a seller analytics digest notification. If email is enabled, confirm the local email outbox/log-only provider processes the transactional digest without attachments.

Expected result: analytics remain read-only. Exports and scheduled digests provide operational evidence but do not mutate orders, payments, payouts, ads, inventory, returns, refunds, or support tickets.

## Known Gaps

- Phase 10Q signed off the seeded desktop/mobile browser walkthrough. Future QA should rerun the same checklist after larger seller/admin UI changes.
- Admin seller, product, and ad queues now default to `Needs attention` and also expose all-state filters, status counts, pagination, assignment/priority/note/SLA filters, saved views, SLA age chips, claim/unclaim, internal notes, bulk claim/high-priority triage, and read-only workload summaries through Phase 10R/10S/10T operational endpoints.
- Admin support operations now expose support-specific saved views, CSV export, workload metrics, and safe customer context on support/admin ticket detail pages. Support saved views remain per user and internal only.
- Hardware barcode scanner SDK integration remains future work.
- Deeper campaign attribution modelling, session-path analysis, and historical funnel backfill remain future work.
- Real carrier-provider integration and sensitive payout-bank storage remain future work.
