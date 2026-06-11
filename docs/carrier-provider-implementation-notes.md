# Carrier Provider Implementation Notes

Last updated: 2026-05-21

## Current Mabuntle Baseline

Mabuntle already has provider-neutral carrier booking/tracking with `Manual` and `Fake` providers. Sellers can mark orders ready to ship, book a configured provider, sync tracking, add manual tracking, and handle delivery exceptions. Seller-managed delivery methods still determine checkout shipping price.

This document is for provider adapter planning only. It does not enable a real carrier.

## Preferred Path: Bob Go Adapter

Bob Go is the preferred first candidate because its public product page aligns with Mabuntle's intended marketplace path:

- live rates at checkout;
- order sync and shipment generation;
- tracking;
- multi-courier options;
- sandbox environment.

Implementation should not start until field-level technical docs or sandbox access are available. The public API documentation entry point is JavaScript-rendered, so the implementation needs a browser-verified export, OpenAPI document, Postman collection, or account-accessible docs before coding request/response contracts.

Minimum adapter shape:

- Add `BobGoCarrierProvider` behind `ICarrierProvider`.
- Add non-secret config under `CarrierProvider:BobGo` for base URLs, API key, sandbox mode, timeout, and optional webhook secret if Bob Go supports webhooks.
- Support booking from Mabuntle `ReadyToShip` orders using existing package dimensions and seller fulfilment address.
- Store provider shipment reference, tracking URL, label URL, service code, provider status, provider timestamps, and provider errors in existing shipment fields.
- Use tracking polling first unless Bob Go webhook contracts are verified.
- Keep checkout rates seller-managed initially; add live provider rates only in a separate phase after pricing, margin, and seller display rules are defined.

Blocking questions before implementation:

- Authentication scheme and API-key header names.
- Sandbox base URL and production base URL.
- Shipment booking payload and required address/package fields.
- Label generation endpoint and file format.
- Tracking status vocabulary and whether stale/duplicate statuses can occur.
- Cancellation and return-shipment endpoints.
- Rate quote payload, tax/insurance/remote-area surcharge behavior, and expiry rules.
- Webhook support and signature validation, if available.

## Fallback Path: PUDO Adapter

The Courier Guy / PUDO Locker API has the most inspectable public endpoint documentation today. It is a good fallback if Bob Go docs or credentials are blocked.

Useful public capabilities to map:

- rates for door-to-door and door/locker combinations;
- shipment creation for D2D, D2L, L2D, and L2L;
- shipment return and cancellation;
- waybill PDF and sticker labels;
- tracking by parcel, shipment, waybill, status, and barcode;
- POD image retrieval;
- locker data import.

Recommended scope if PUDO is selected:

- Start with booking/tracking/label generation for seller-ready orders.
- Add locker import into existing platform-managed pickup points only after confirming data licensing and update cadence.
- Keep seller-managed checkout delivery rates for the first adapter pass; use provider rates as admin/seller evidence before exposing them to buyers.
- Poll tracking through the worker. Do not invent webhook behavior unless official docs confirm it.

## Deferred Providers

### Shiplogic / The Courier Guy Direct

Do not implement first from public docs alone. Public pages confirm the platform/API exists, but endpoint-level docs were not available through static verification. Merchant docs, sandbox access, or support-provided integration files are required.

### Pargo

Pargo is attractive for pickup points, click-and-collect, returns, and broader Africa/Middle East logistics, but field-level API docs were not publicly available during this spike. Treat it as a later pickup-network candidate after merchant onboarding and API documentation are available.

## Integration Rules For Any Real Provider

- Do not call carrier APIs from Angular.
- Keep `Manual` as a production-safe fallback.
- Keep `Fake` local/test only and rejected in production.
- Do not mutate payments, refunds, payouts, ledger, cart, or reservations from carrier events.
- Map provider statuses conservatively into existing shipment/order states.
- Store raw provider secrets nowhere in orders, shipments, audit logs, or frontend models.
- Treat labels and tracking URLs as operational fulfilment metadata, not payment evidence.
- Add tests with a deterministic fake HTTP handler before any sandbox calls.
