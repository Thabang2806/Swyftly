# Payment Provider Implementation Notes

Last updated: 2026-05-21

Phase 8C implements PayFast as the first real provider adapter foundation. `PaymentProvider:ProviderName` still defaults to `Fake` outside explicit configuration, and PayU South Africa remains deferred until merchant technical docs and sandbox credentials are available.

## Shared Swyftly Constraints

- Keep Angular provider-neutral. The frontend should only receive and navigate to the backend `checkoutUrl`.
- Keep signed provider webhooks as the only settlement source of truth.
- Continue storing every provider event in `payment_events` and enforcing duplicate-event idempotency.
- Reject settlement when provider amount, currency, or local provider reference does not match the local payment.
- Keep local ledger, seller balance, refund, and payout records authoritative for Swyftly operations.
- Do not add real provider secrets to source. Use environment variables or a secret store.

## PayFast Adapter Status

Implemented in Phase 8C:

- `PayFastPaymentProvider` is registered behind `IPaymentProvider` when `PaymentProvider:ProviderName=PayFast`.
- `POST /api/payments/initiate` creates the local payment and returns a backend checkout bridge URL.
- `GET /api/payments/payfast/checkout/{providerReference}` renders an anonymous auto-submit HTML form to `PayFast:ProcessUrl`.
- The form uses the local payment id as `m_payment_id`, includes `notify_url`, amount, item details, order/payment custom fields, and a PayFast MD5 signature.
- `POST /api/payments/webhook/payfast` accepts `application/x-www-form-urlencoded` ITNs.
- Signature verification excludes `signature`, trims values, URL-encodes with `+` spaces and uppercase percent-hex, appends `passphrase` when configured, and compares lowercase MD5 values.
- Remote ITN validation posts the raw payload to `PayFast:ValidateUrl` when `PayFast:RequireRemoteValidation=true`. Production configuration rejects disabling it.
- `COMPLETE` settles paid payments only when provider reference, amount, and currency match the local payment.
- Unknown/intermediate statuses are stored without settlement. Failed/cancelled statuses affect only eligible pending payments.
- Refunds remain manual: PayFast refund approval records provider action required, and finance finalizes accounting through `POST /api/admin/refunds/{refundId}/confirm-manual-provider-refund` after the dashboard refund exists.
- `GET /health/ready` includes a `payment-provider` check that validates selected-provider configuration.
- `GET /api/admin/payments/reconciliation-candidates` gives finance a read-only queue for stale pending/authorized payments and failed webhook events. This is operational triage only; it does not query PayFast or mutate local settlement state.
- Phase 9B added `payment_reconciliation_reviews` and `POST /api/admin/payments/{paymentId}/reconciliation-reviews` for `FinanceApprove` users to record provider-dashboard evidence, outcome, reason, and optional snooze timestamps. These reviews are evidence only and never settle payments or change orders/ledger/cart/reservation/refund/payout state.
- `/admin/payments` now shows latest reconciliation review state and warns that `ProviderPaidMissingWebhook` requires provider investigation or valid ITN replay, not manual settlement.
- `/admin/refunds` now makes PayFast `Processing` refunds explicit: complete the dashboard refund first, then confirm the manual provider refund reference in Swyftly.
- Sandbox verification steps live in `docs/payfast-sandbox-runbook.md`.

Remaining PayFast hardening:

- End-to-end sandbox testing with real PayFast sandbox credentials and callback URLs.
- Provider status query and settlement automation once exact API behavior is confirmed.
- Automatic PayFast refunds if/when a safe API contract is available.
- End-to-end sandbox evidence for the manual refund and reconciliation review runbooks.

## PayU South Africa Notes

Do not implement a field-level PayU South Africa adapter until the project has merchant technical docs or sandbox credentials. The public PayU South Africa site confirms local payment gateway support, and PaymentsOS public docs describe PayU South Africa region support, ZAR, payment-page methods, refunds, and setup credentials. However, PayU regional products differ, and the PayU Latam confirmation URL docs must not be assumed to be the South Africa webhook contract.

Required inputs before implementation:

- PayU South Africa sandbox credentials.
- Exact hosted checkout or payment-page initiation contract.
- Exact webhook/notification payload and signature algorithm for the selected PayU South Africa integration path.
- Status query/reconciliation endpoint details.
- Refund API behavior, including partial refunds, asynchronous statuses, and webhook updates.
- Confirmation of whether PaymentsOS is the intended Swyftly integration path or whether PayU South Africa provides a direct merchant integration path.

## Config Placeholders

Use these names for runtime configuration. Keep values in environment variables or a secret store; do not commit merchant credentials.

```text
PaymentProvider__ProviderName=Fake|PayFast|PayU

PayFast__MerchantId=
PayFast__MerchantKey=
PayFast__Passphrase=
PayFast__ProcessUrl=
PayFast__ValidateUrl=
PayFast__NotifyUrl=
PayFast__CheckoutBridgeBaseUrl=
PayFast__RequireRemoteValidation=true

PayU__Region=SouthAfrica
PayU__Username=
PayU__Password=
PayU__Safekey=
PayU__PaymentPageUrl=
PayU__WebhookSigningKey=
```

## Follow-Up Implementation Default

Keep PayFast as the first real provider path. The next provider work should be sandbox verification and reconciliation/refund hardening, not adding PayU in parallel.
