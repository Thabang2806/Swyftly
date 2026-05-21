# PayFast Sandbox Runbook

Last updated: 2026-05-21

This runbook verifies the PayFast adapter without committing merchant credentials. It is intentionally operational: do not mark PayFast production-ready until these checks pass against a real PayFast sandbox account and a publicly reachable API callback URL.

## Preconditions

- Local PostgreSQL is migrated and the API can start.
- PayFast sandbox merchant id, merchant key, and passphrase are available outside source control.
- The API has a public HTTPS callback URL, for example through a controlled tunnel, with:
  - `GET /api/payments/payfast/checkout/{providerReference}`
  - `POST /api/payments/webhook/payfast`
- Angular uses the existing backend-returned `checkoutUrl`; no frontend provider configuration is required.

## Local Configuration

Set these through environment variables or a local untracked secret source:

```text
PaymentProvider__ProviderName=PayFast
PaymentProvider__DefaultCurrency=ZAR
PaymentProvider__SuccessRedirectUrl=http://localhost:4200/checkout/success
PaymentProvider__FailureRedirectUrl=http://localhost:4200/checkout/failed

PayFast__MerchantId=<sandbox merchant id>
PayFast__MerchantKey=<sandbox merchant key>
PayFast__Passphrase=<sandbox passphrase if configured>
PayFast__ProcessUrl=https://sandbox.payfast.co.za/eng/process
PayFast__ValidateUrl=https://sandbox.payfast.co.za/eng/query/validate
PayFast__NotifyUrl=<public API base>/api/payments/webhook/payfast
PayFast__CheckoutBridgeBaseUrl=<public API base>
PayFast__RequireRemoteValidation=true
```

For local adapter-only testing, `PayFast__RequireRemoteValidation=false` can be used, but that does not validate the provider callback path and is not production evidence.

## Readiness Checks

Run:

```powershell
dotnet build backend\Swyftly.sln --no-restore
dotnet test backend\Swyftly.sln --no-build --filter "PayFast|PaymentProviderHealthCheck|PaymentEndpoint"
```

Start the API with the PayFast environment values and call:

```http
GET /health/ready
```

Expected:

- `payment-provider` is `Healthy`.
- The description mentions PayFast.
- If remote validation is disabled, treat the result as development-only evidence.

## End-To-End Sandbox Flow

1. Sign in as a buyer.
2. Add a published product to cart.
3. Start checkout and create a pending-payment order.
4. Call `POST /api/payments/initiate`.
5. Confirm the response provider is `PayFast` and `checkoutUrl` points to `/api/payments/payfast/checkout/{providerReference}`.
6. Open the checkout URL in a browser and verify the auto-submit form redirects to PayFast sandbox.
7. Complete a sandbox payment.
8. Confirm PayFast posts ITN to `/api/payments/webhook/payfast`.
9. Confirm the local payment and order become `Paid`, reservations are confirmed, ledger entries are written, ad conversion attribution runs, and the active cart is cleared.
10. Replay the same ITN payload and confirm no duplicate ledger entries are created.

## Negative Checks

Run at least one test case for each:

- invalid PayFast signature returns `401`;
- failed remote validation returns `401`;
- amount mismatch stores a failed payment event and does not settle the order;
- unknown `m_payment_id` stores a failed event and does not mutate any payment;
- `FAILED` or `CANCELLED` ITN only cancels eligible pending-payment orders;
- unknown/intermediate payment status stores the event without settlement.

## Manual Refund Flow

PayFast refunds are manual in this phase.

1. Finance operator creates a refund request.
2. Finance approver approves it.
3. Swyftly moves the refund to `Processing` and records `ProviderRefundActionRequired`.
4. Finance completes the refund in the PayFast dashboard.
5. Finance confirms the provider refund reference through:

```http
POST /api/admin/refunds/{refundId}/confirm-manual-provider-refund
```

6. Swyftly then writes refund ledger reversals, payout adjustments, payment refund state, order/return updates, and audit logs.

## Manual Reconciliation Review Flow

Dashboard-observed provider status is evidence, not settlement authority.

1. Finance opens `/admin/payments` and reviews stale pending/authorized payments or payments with failed webhook events.
2. Finance checks the PayFast dashboard, ITN history, and Swyftly support/audit context.
3. Finance records a review through:

```http
POST /api/admin/payments/{paymentId}/reconciliation-reviews
```

4. Use one of:
   - `ProviderPending`
   - `MatchedNoAction`
   - `ProviderPaidMissingWebhook`
   - `ProviderFailedMissingWebhook`
   - `ManualRecoveryRequired`
5. If the provider status is `COMPLETE` but Swyftly has no valid paid ITN, record `ProviderPaidMissingWebhook` and investigate or replay the PayFast ITN. Do not mark the payment or order paid from an admin screen.
6. Set `nextReviewAfterUtc` when the case should be snoozed. Snoozed candidates are hidden from the default queue until that timestamp passes.

## Exit Criteria

PayFast can be considered sandbox-verified only when:

- the hosted checkout redirect works through the public bridge URL;
- PayFast ITNs reach the local API callback URL;
- remote ITN validation is enabled and successful;
- paid, duplicate, failed/cancelled, invalid-signature, remote-validation-failed, and amount-mismatch paths behave as expected;
- no real credentials or sandbox sample credentials are committed.

## Remaining Production Gaps

- Provider status-query settlement is still not implemented because exact PayFast status API behavior must be verified.
- Automatic PayFast refunds are still not implemented; manual dashboard confirmation remains the safe flow.
- PayU South Africa remains deferred until merchant technical docs and sandbox credentials are available.

As of 2026-05-21, sandbox checkout and callback verification remain blocked until real PayFast sandbox credentials and a public callback URL are available.
