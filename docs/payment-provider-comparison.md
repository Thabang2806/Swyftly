# Payment Provider Comparison

Last updated: 2026-05-19

This spike compared PayFast and PayU South Africa for Mabuntle production payments. Phase 8C selected PayFast for the first adapter foundation. `Fake` remains the default provider outside explicit configuration, while PayFast can now be enabled with `PaymentProvider:ProviderName=PayFast` and non-secret PayFast configuration plus merchant credentials supplied outside source control.

## Decision Summary

PayFast is the lower-risk first implementation candidate because public sandbox guidance and integration troubleshooting are easier to verify. PayU South Africa is still a credible South African/ZAR candidate, but its public South Africa material is mostly commercial and PaymentsOS-oriented; a production implementation should wait for merchant technical docs or sandbox credentials.

## Comparison Matrix

| Area | PayFast | PayU South Africa | Mabuntle impact |
|---|---|---|---|
| South Africa/ZAR fit | Strong. PayFast is South Africa-focused and has public sandbox guidance. | Strong. PayU South Africa advertises local payment gateway support and ZAR support through PaymentsOS docs. | Both fit the market; provider choice should depend on merchant onboarding, fees, docs, and operations support. |
| Hosted checkout | Public docs indicate standard redirect/form checkout and sandbox testing. | PayU SA supports redirection/direct integration commercially; PaymentsOS docs describe a payment page flow. | Mabuntle should keep the existing backend-returned `checkoutUrl` abstraction for both. |
| Webhook/ITN format | PayFast uses ITN-style provider notifications. Public troubleshooting notes emphasize exact signature field ordering and encoding. | PayU confirmation URL references use form posts and signature validation, but the accessible technical reference is PayU Latam, not guaranteed South Africa-compatible. | Both likely require `application/x-www-form-urlencoded` webhook support in addition to the current fake JSON webhook path. |
| Signature validation | Public PayFast support material identifies MD5 signature sensitivity, passphrase matching, field order, trimming, and URL encoding as common failure points. | PayU Latam docs describe signature verification with provider-sent fields and amount-format rules; PayU SA details need merchant docs. | Implementer must write provider-specific signature tests before settlement logic. |
| Status query/reconciliation | Publicly discoverable status/reconciliation API details are less clear than hosted checkout/ITN. Dashboard reconciliation is available operationally. | PaymentsOS has transaction/reporting concepts and PayU SA docs mention custom `mark_off` reconciliation support, but account setup is required. | First implementation should add admin audit-only reconciliation only after exact provider status APIs are confirmed. |
| Refunds | PayFast public FAQ describes dashboard refund steps. Public refund API details were not sufficient for a safe first adapter. | PaymentsOS docs list refunds as supported for PayU SA payment-page/card flows, but exact merchant API details and credentials are needed. | Keep Mabuntle ledger/refund approval internal; provider refund execution should be manual until exact refund API behavior is verified. |
| Marketplace/split payments | PayFast has split-payment material, but this should be treated as a later provider-payout decision. | PaymentsOS PayU SA docs list multi-seller payments as not supported. | Mabuntle should keep internal ledger and admin payout controls. Do not rely on provider split settlement for MVP. |
| Sandbox availability | PayFast sandbox setup and sample sandbox credentials are publicly documented. Do not commit sample credentials into appsettings. | PaymentsOS docs say PayU SA test credentials are available through PayU SA setup links or merchant portal flow. | PayFast is easier to spike locally. PayU likely requires account/sandbox access before coding. |
| Implementation risk | Medium. Main risk is signature/ITN edge cases and exact PayFast current-doc behavior. | High until PayU SA merchant technical docs are available. Region-specific PayU docs differ. | Build PayFast first unless PayU SA provides complete merchant docs and sandbox access. |

## Recommendation

Use the next implementation phase to build exactly one production adapter. The current recommendation is:

1. Build PayFast first if fast South African hosted checkout is the priority.
2. Keep PayU South Africa in evaluation until merchant technical docs, sandbox credentials, webhook samples, and refund/status API documentation are available.
3. Continue using Mabuntle's internal ledger, payment events, refund records, and payout controls regardless of provider.

## Source Notes

- PayFast developer docs: https://developers.payfast.co.za/docs
- PayFast sandbox guidance: https://support.payfast.help/portal/en/kb/articles/how-do-i-make-test-payments-in-sandbox-mode-20-9-2022
- PayFast integration/signature FAQ: https://payfast.io/faq/merchant-faqs/
- PayU South Africa payment gateway overview: https://southafrica.payu.com/payment-gateway/
- PayU South Africa PaymentsOS region notes: https://developers.paymentsos.com/docs/connect/payu-countries-and-regions/payu-south-africa.html
- PayU confirmation URL technical reference, used only as a non-South-Africa comparison point: https://developers.payulatam.com/latam/en/docs/integrations/confirmation-url.html
