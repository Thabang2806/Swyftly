# Carrier Provider Comparison

Last updated: 2026-05-21

## Decision

Recommended first adapter candidate: **Bob Go**, provided Mabuntle can obtain API documentation export/sandbox credentials from Bob Go.

Fallback candidate: **The Courier Guy / PUDO Locker API**, if Bob Go field-level API documentation or sandbox credentials are not available. PUDO has the most inspectable public API details today, but it is narrower than Bob Go for a marketplace that needs multi-courier rates, booking, labels, and tracking.

Do not implement Shiplogic, The Courier Guy Direct, or Pargo first until merchant technical docs and sandbox access are available.

## Source Inventory

| Provider | Source | Notes |
|---|---|---|
| Bob Go | https://www.bobgo.co.za/apps-integrations | Public product page says Bob Go has an open API for live checkout rates, order sync, shipment generation, and tracking, plus a dedicated sandbox. |
| Bob Go API docs | https://api-docs.bob.co.za/ | Public entry point is JavaScript-rendered and was not readable through static inspection. Treat as available only after browser/API-doc export review. |
| Shiplogic | https://www.shiplogic.com/api-docs/shipping | Public page confirms an API product, but does not expose enough endpoint detail for adapter implementation. |
| The Courier Guy | https://thecourierguy.co.za/api-docs/ | Public page is mostly navigation/contact content. The integration help article redirected to sign-in during verification. |
| The Courier Guy / PUDO | https://api-pudo.co.za/ | Public docs expose rates, shipment creation, returns, cancellation, labels, tracking/POD, billing, and lockers. |
| Pargo | https://www.pargo.com/pargo | Public product page emphasizes pickup points, unified API, last-mile delivery, returns, and Africa/Middle East coverage, but field-level API docs were not found publicly. |

## Decision Matrix

| Capability | Bob Go | Shiplogic / The Courier Guy | PUDO Locker API | Pargo |
|---|---|---|---|---|
| South Africa ecommerce fit | Strong. Multi-courier shipping platform positioned for online stores. | Strong brand presence, but public adapter docs are not readily inspectable. | Strong for The Courier Guy locker/door flows. | Strong pickup/last-mile network, including South Africa. |
| Door-to-door rates | Public product page says live checkout rates are supported. Field details need docs access. | Likely supported by platform, but public details insufficient. | Public docs expose door-to-door and mixed door/locker rate endpoints. | Likely supported as logistics platform, but public field details unavailable. |
| Booking / shipment creation | Public product page says shipment generation is supported. | Likely supported, but needs docs/access. | Public docs expose shipment creation endpoints for D2D, D2L, L2D, and L2L. | Public field details unavailable. |
| Labels / waybills | Likely supported through shipment workflow; verify in API docs. | Likely supported; verify with merchant docs. | Public docs expose waybill PDF and sticker label endpoints. | Public field details unavailable. |
| Tracking | Public product page says tracking is supported. | Likely supported; public docs insufficient. | Public docs expose multiple tracking lookup endpoints and POD images. | Product page emphasizes proactive tracking, but API shape unavailable. |
| Cancellation | Needs API-doc verification. | Needs API-doc verification. | Public docs expose shipment cancellation. | Needs API-doc verification. |
| Pickup points / lockers | Possible through delivery partners, but details need API-doc verification. | The Courier Guy ecosystem supports lockers. | Public docs expose locker data. | Strongest pickup-point positioning, with a large pickup network. |
| Sandbox | Public product page says dedicated sandbox is available. | The Courier Guy help search result referenced Shiplogic sandbox, but article now requires sign-in. | Public production docs are visible; sandbox/credential path needs confirmation. | Needs merchant onboarding confirmation. |
| Webhooks vs polling | Needs API-doc verification. | Needs API-doc verification. | Public docs show tracking reads; webhook support not evident from static review. | Needs API-doc verification. |
| Returns | Needs API-doc verification. | Needs API-doc verification. | Public docs expose returning a shipment. | Product page mentions returns solutions, but field details unavailable. |
| Pricing ownership fit | Best fit if Mabuntle later replaces seller-managed rates with provider live rates. Also supports current seller-managed phase by using booking/tracking only first. | Unknown until docs are available. | Good technical fallback, but narrower and more The Courier Guy-specific. | Good pickup fit, but likely needs commercial onboarding before API work. |
| Implementation risk | Medium: best product fit, but field-level docs/credentials are required. | High: public docs insufficient. | Medium-low for a narrow adapter because public docs are detailed. | High until merchant technical docs are available. |

## Recommendation

Build the next real adapter as **Bob Go** only after obtaining:

- API key/sandbox account.
- Static/exported endpoint documentation for authentication, rates, shipment creation, labels, tracking, cancellation, and error codes.
- Confirmation of webhook support, or confirmation that polling is the supported tracking model.
- Confirmation of how courier/service choices map to Mabuntle `CarrierProviderShipmentStatus`.

If those are not available, build a narrower **PUDO adapter** first for rate lookup, shipment creation, labels, tracking polling, returns, cancellation, and locker import. That would give Mabuntle a real provider path without inventing undocumented Bob Go behavior.

## Current Runtime Position

No runtime behavior changes are made by this spike.

- `CarrierProvider__ProviderName=Manual` remains the safe default.
- `Fake` remains local/test automation only and is rejected in production.
- No production carrier credentials are configured.
- Existing seller booking and tracking endpoints remain provider-neutral.
