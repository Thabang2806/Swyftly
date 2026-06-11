# Observability

Last updated: 2026-05-19

## Correlation IDs

The API accepts `X-Correlation-ID` on inbound requests. If the caller does not provide one, the API generates a compact GUID value.

Every response includes `X-Correlation-ID`, and request logs include the same value in the logging scope as `CorrelationId`.

## Request And Error Logging

The API logs each completed HTTP request with:

- method
- path
- status code
- elapsed milliseconds
- correlation id scope

Unhandled request exceptions are logged with the exception and increment the placeholder error metric before the exception is rethrown to the normal ASP.NET Core error pipeline.

## Health Checks

`GET /health` remains a lightweight liveness endpoint.

`GET /health/ready` reports readiness checks tagged as `ready`, currently:

- `postgresql`
- `search-placeholder`
- `storage-placeholder`
- `payment-provider`

The payment-provider check reports the configured provider. `Fake` is healthy for local development. `PayFast` is healthy only when the required PayFast checkout, callback, and validation configuration is present; remote validation can be disabled for local adapter testing, but that is not production evidence.

## Metrics Placeholders

The API exposes a `Mabuntle.Api` meter with placeholder counters:

- `mabuntle.payments.events`
- `mabuntle.ai.requests`
- `mabuntle.orders.created`
- `mabuntle.errors`

Only the error counter is incremented by the foundation middleware today. Future payment, AI, and order workflows should increment the corresponding counters where business events are committed.

No vendor-specific monitoring package is configured yet.
