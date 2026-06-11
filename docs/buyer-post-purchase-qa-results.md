# Phase 11A-11F Buyer Post-Purchase QA Results

Date: 2026-05-29

## Scope

This pass adds repeatable local setup for buyer post-purchase QA. It intentionally uses the same checkout, payment webhook, and seller fulfilment APIs that production workflows use.

Out of scope: PayFast/live payment-provider changes, carrier-provider integrations, direct database state mutation, hidden paid-order seed shortcuts, refund automation, payout movement, SMS/push, and marketing automation.

## Demo Helper

Seed first:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords -ApplyMigrations -SeedSampleProducts
```

Create a delivered demo order:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create-buyer-post-purchase-demo.ps1 -Password "UseYourOwnDevPassword1!"
```

Expected output includes:

- `orderId`
- `paymentId`
- `providerReference`
- buyer route URLs
- seller order-detail URL

Latest helper smoke:

| Field | Value |
|---|---|
| Result | Pass |
| Order ID | `339fe53e-5a26-450a-9ecb-df4373db2e3f` |
| Payment ID | `d804fc62-a6c8-40af-b2df-693674aefccd` |
| Provider reference | `fake_339fe53e5a26450a9ecbdf4373db2e3f_1780046892242` |
| Final status | `Delivered` |
| Product / variant | Rose Linen Midi Dress, `L / Rose` |

## Phase 11C Browser Sign-Off

The Phase 11C pass used the delivered-order helper against the local API and Angular app. Screenshots and the raw browser result JSON were written under:

`C:\Users\thaba\AppData\Local\Temp\mabuntle-phase11c-qa-1780047758345`

Latest browser sign-off order:

| Field | Value |
|---|---|
| Result | Pass with minor polish fixes |
| Order ID | `b6cc1186-4812-4232-82af-6b5a340d126e` |
| Payment ID | `1f0e7b4f-b346-4a63-b949-7bc5fc2d84b1` |
| Provider reference | `fake_b6cc11864812423282af6b5a340d126e_1780047388806` |
| Final status | `Delivered` |
| Product / variant | Rose Linen Midi Dress, `L / Rose` |

All route checks below rendered authenticated account content with no API-error state and no horizontal overflow at desktop `1440px` or mobile `430px`.

| Route | Desktop | Mobile | Notes |
|---|---|---|---|
| `/account/orders` | Pass | Pass | Delivered order and payment context are readable. |
| `/account/orders/{orderId}` | Pass | Pass | Payment summary, shipment timeline, tracking, return quantity cap guidance, review eligibility, and contextual support handoff are visible. |
| `/account/support?orderId={orderId}&sellerId={sellerId}` | Pass | Pass | Query-prefilled support context renders without overflow. |
| `/account/support` | Pass after fix | Pass after fix | Query-prefilled order/seller context now clears when navigating back to the plain support route. |
| `/account/returns` | Pass | Pass | List route renders readable empty/populated states without overflow. |
| `/account/returns/{returnRequestId}` | Component-covered; browser route requires a created return | Component-covered; browser route requires a created return | Return detail support handoff now carries order/seller context. |
| `/account/reviews` | Pass | Pass | Browser QA successfully submitted a delivered-order review and confirmed it appears as pending moderation. |
| `/account/notifications` | Pass | Pass | Notification rows render cleanly; richer open-action links remain a later enhancement. |
| `/account/support/{ticketId}` | Component/API-covered; browser route requires a created ticket | Component/API-covered; browser route requires a created ticket | Support creation remains covered by the support-page spec; full browser ticket creation is a follow-up manual check. |
| `/account/disputes` | Pass | Pass | Empty copy distinguishes formal dispute cases from return escalations. |

## Phase 11D Buyer Account Trust Polish And Action Sign-Off

Phase 11D completed buyer account trust polish/action sign-off. The planning-agent workflow was followed before implementation, then the account trust UI and final return/support click-through were completed against a delivered helper order.

Screenshots and raw browser result JSON were written under:

`C:\Users\thaba\AppData\Local\Temp\mabuntle-phase11d-qa-1780049753231`

| Field | Value |
|---|---|
| Result | Pass with buyer account trust polish |
| Order ID | `491490cb-39d3-4511-b383-fcfc34eb734b` |
| Payment ID | `8e6b7747-b4f9-4272-bd1f-1035ded3b0ae` |
| Provider reference | `fake_491490cb39d34511b383fcfc34eb734b_1780049561193` |
| Return request ID | `e6ea57b0-dd8c-431c-aa9c-f7c9c682acd5` |
| Support ticket ID | `0fb110e2-41de-4738-af38-ebb949e21592` |
| Final status | `Delivered` before return/support actions |
| Product / variant | Rose Linen Midi Dress, `L / Rose` |

Action checks:

- Created a delivered-order return through the browser and confirmed `/account/returns/e6ea57b0-dd8c-431c-aa9c-f7c9c682acd5` renders the submitted item state and `AwaitingSellerResponse` context.
- Created an order-linked support ticket through `/account/support?orderId=491490cb-39d3-4511-b383-fcfc34eb734b&sellerId=912b8c72-398a-49d1-b92c-b68b2be12685` and confirmed `/account/support/0fb110e2-41de-4738-af38-ebb949e21592` shows linked order/seller context.
- Rechecked account, settings, wishlist, notifications, support, returns, reviews, and disputes at desktop `1440px` and mobile `430px`; the route sweep found no login redirects, no visible API-error states, and no horizontal overflow.
- Kept refund/dispute financial resolution out of this pass.

## Phase 11E Buyer Refund Visibility

Phase 11E added buyer-safe refund outcome visibility. Browser sign-off for real refund states still depends on creating a refund through existing admin finance workflows, but the read-only buyer surfaces and focused tests are in place.

Implementation notes:

- Added `/account/refunds` with list, empty, error, timeline, order-link, and return-link states.
- Added refund panels to `/account/orders/{orderId}` and `/account/returns/{returnRequestId}`.
- Added account next-action prompts for requested, approved, processing, and failed refunds.
- Added refund-related notification Open routing to `/account/refunds`.
- Updated `/account/disputes` copy to explain that buyer-favoured dispute outcomes may create refund requests that still require finance processing.

Deferred evidence:

- Create a buyer-owned refund through admin finance after a delivered-order return/dispute and record the refund id, status, desktop/mobile route notes, and any UI defects found.

## Phase 11F Buyer Refund-State Sign-Off

Phase 11F completed the deferred refund-state browser evidence using only existing workflows: sample-product seed, delivered-order helper, buyer return creation, seller return approval, finance-operator refund request, finance-approver approval, and buyer refund reads.

Screenshots and raw browser result JSON were written under:

`C:\Users\thaba\AppData\Local\Temp\mabuntle-phase11f-qa-1780054746662`

| Field | Value |
|---|---|
| Result | Pass |
| Order ID | `71583d35-cdfc-43f3-88d6-14b3b5c78f5b` |
| Payment ID | `1fcb5477-f8ba-4fa9-b2f1-a797f83482e2` |
| Provider reference | `fake_71583d35cdfc43f388d614b3b5c78f5b_1780054237501` |
| Return request ID | `5774b963-0aad-4477-8fb7-ec1a723ff3cd` |
| Refund ID | `6f00614d-fe12-4fdf-a4c7-52da46f33393` |
| Refund status | `Refunded` after fake-provider finance approval |
| Product / variant | Rose Linen Midi Dress, `L / Rose` |

Refund workflow evidence:

- Created the delivered order through `scripts/create-buyer-post-purchase-demo.ps1 -SkipCertificateCheck`.
- Created a buyer return through `POST /api/buyer/orders/{orderId}/returns`.
- Approved the return through `POST /api/seller/returns/{returnRequestId}/approve`.
- Created a return-linked refund as `finance.operator@mabuntle.local` through `POST /api/admin/returns/{returnRequestId}/refunds`.
- Approved the refund as `finance.approver@mabuntle.local` through `POST /api/admin/refunds/{refundId}/approve`; fake-provider refund completion moved the buyer-visible status to `Refunded`.

Route checks:

| Route | Desktop | Mobile | Notes |
|---|---|---|---|
| `/account` | Pass | Pass | Account dashboard rendered authenticated content and refund continuity without stale prompts. |
| `/account/refunds` | Pass | Pass | Refund row showed refund id, order id, return id, amount, `Refunded` status, buyer-safe status message, and timeline. |
| `/account/orders/{orderId}` | Pass | Pass | Refund panel linked back to the refund/return context and did not expose finance notes or provider payloads. |
| `/account/returns/{returnRequestId}` | Pass | Pass | Refund outcome panel rendered the linked refund context and retained dispute guidance. |
| `/account/disputes` | Pass | Pass | Copy remained honest that buyer-favoured outcomes can create refund requests that still need finance processing. |
| `/account/notifications` | Pass | Pass | Notification route rendered authenticated content with no broken Open actions found in this refund-state pass. |

The desktop `1440px` and mobile `430px` sweep found no horizontal overflow, no visible API-error states, and no missing refund/order/return context on the checked buyer routes. The headless harness used in-app SPA navigation after login because a fresh local Chrome profile cannot reliably restore the in-memory access token on hard reloads against the local HTTPS API certificate; this is a local QA harness constraint, not a buyer route defect found during the authenticated sweep.

## Phase 11B Fixed Defects

- `/account/orders/{orderId}` now links to `/account/support` with `orderId` and `sellerId` query params so support tickets can carry order context.
- `/account/support` reads `orderId` and `sellerId` query params, prefills linked fields, and sets an order-support subject without removing manual ticket creation.
- `/account/disputes` empty-state copy no longer implies every return escalation becomes a standalone dispute.
- Delivered-order return requests show the selected line-item quantity cap before submission.
- `/account/returns` now has focused component tests for loading, empty, error, and populated list states.

## Phase 11C Fixed Defects

- `/account/support` now subscribes to query-param changes and clears previously prefilled order/seller context when the buyer navigates back to the plain support route in the same SPA session.
- `/account/returns/{returnRequestId}` now links Contact support to `/account/support` with the return's `orderId` and `sellerId` query parameters.
- Focused specs now cover support-query clearing and return-detail support handoff query params.

## Phase 11D Fixed Defects

- `/account/notifications` now shows Open actions for supported related entities: orders, return requests, and support tickets. Unsupported related entities remain read-only metadata.
- `/account` now surfaces next-action prompts for pending payments, delivered orders, open support tickets, and unread notifications using existing account data.
- `/account/settings` now uses buyer-safe copy for local address checks, saved addresses, notification preferences, and unavailable channels.
- `/account/support` now presents linked order/seller query-param context as intentional support context while preserving editable linked-id fields and the existing create-ticket payload.
- `/account/wishlist` now gives clearer variant/quantity/availability guidance before move-to-cart attempts.

## Findings

The Phase 11A API helper path passed locally after the sample-product seed and a local API start. Phase 11C completed the desktop/mobile route sign-off for the delivered-order post-purchase pages. Phase 11D completed the final action sign-off that Phase 11C left open: delivered-order return creation and order-linked support-ticket creation now have concrete browser evidence and IDs above. Phase 11F completed refund-state browser sign-off with a buyer-owned return-linked refund created and approved through existing finance workflows.

The latest authenticated SPA browser sweeps found no route-level overflow and no visible API-error states across the checked buyer account trust and refund routes.

## Deferred Follow-Ups

- Buyer growth feature depth for `/assistant` and `/visual-search` is now the next buyer candidate after refund-state sign-off.
- SMS/push notification channels remain future work.
