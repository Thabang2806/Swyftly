# Frontend Cross-Domain QA Results

Date: 2026-06-09

## Scope

This pass verifies the split Cloudflare frontend outputs after the client, seller, and admin apps moved to Tailwind-based primitives:

- `mabuntle.com`: public marketplace and buyer account.
- `seller.mabuntle.com`: seller workspace.
- `admin.mabuntle.com`: admin, support, finance, and reporting console.

This was a QA and deployment-smoke pass only. No backend APIs, Angular route contracts, database schema, payment, carrier, payout, provider, seller workflow, buyer workflow, or admin workflow behavior were changed.

## Local Verification

All commands were run from `frontend/mabuntle-web` with `MABUNTLE_API_BASE_URL=https://api.mabuntle.com`.

| Check | Result | Evidence |
|---|---|---|
| Client Cloudflare build | Pass | `cmd /c npm run build:client:cloudflare`; output `dist/mabuntle-client/browser`; initial browser total `464.23 kB`; copied client `_redirects`. |
| Seller Cloudflare build | Pass | `cmd /c npm run build:seller:cloudflare`; output `dist/mabuntle-seller/browser`; initial browser total `429.51 kB`; copied seller `_redirects`. |
| Admin Cloudflare build | Pass | `cmd /c npm run build:admin:cloudflare`; output `dist/mabuntle-admin/browser`; initial browser total `366.30 kB`; copied admin `_redirects`. |
| Angular specs | Pass | `cmd /c npm run test:ci`; `349 SUCCESS`. |
| Mojibake scan | Pass | `rg -n "Â|Ã|â|ð|�" src`; no matches. |
| Angular Material package/source scan | Pass with expected false positive note | Broad scan reported only `package-lock.json:7655` because the package name `date-format` contains `mat-`. Follow-up scans for `@angular/material`, `@angular/cdk`, `Mat[A-Z]`, `mat[A-Z]`, and source `mat-` usage returned no matches. |

## Redirect Output

| App | Redirect rules verified in build output |
|---|---|
| Client | `/* /index.html 200` |
| Seller | `/seller / 301`; `/seller/* /:splat 301`; `/* /index.html 200` |
| Admin | `/admin / 301`; `/admin/* /:splat 301`; `/* /index.html 200` |

## Deployed Smoke

| URL | Result | Notes |
|---|---|---|
| `https://mabuntle.com` | Pass | Returned `200` and Cloudflare Pages HTML. |
| `https://mabuntle.com/shop` | Pass after redirect | Returned `308` to `/shop/`, then `200`. |
| `https://mabuntle.com/cart` | Pass after redirect | Returned `308` to `/cart/`, then `200`. |
| `https://mabuntle.com/assistant` | Pass after redirect | Returned `308` to `/assistant/`, then `200`. |
| `https://api.mabuntle.com/health` | Blocked | Cloudflare returned repeated `308 Permanent Redirect` to the same URL. This looks like an external Cloudflare/API TLS or proxy configuration loop rather than an app source defect. |
| `https://api.mabuntle.com/health/ready` | Blocked | Same repeated `308 Permanent Redirect` loop as `/health`. |
| `https://seller.mabuntle.com` | Blocked | DNS does not resolve: `DNS name does not exist`. |
| `https://seller.mabuntle.com/products` | Blocked | DNS does not resolve. |
| `https://seller.mabuntle.com/seller/products` | Blocked | DNS does not resolve, so compatibility redirect cannot be verified deployed yet. |
| `https://admin.mabuntle.com` | Blocked | DNS does not resolve: `DNS name does not exist`. |
| `https://admin.mabuntle.com/support` | Blocked | DNS does not resolve. |
| `https://admin.mabuntle.com/admin/support` | Blocked | DNS does not resolve, so compatibility redirect cannot be verified deployed yet. |

## Route Checklist

Local build/test evidence covers the route tables and generated outputs. A full human desktop/mobile visual pass still needs the deployed seller/admin DNS and API health loop fixed first.

| Surface | Desktop route pass | Mobile route pass | Current note |
|---|---|---|---|
| Client public and buyer routes | Partial | Partial | Build/specs pass. Deployed root and representative deep links load. Authenticated buyer workflows were not manually exercised against production because API health is blocked. |
| Seller workspace routes | Blocked | Blocked | Local build/specs pass and redirect output is present. Deployed DNS for `seller.mabuntle.com` does not resolve. |
| Admin/support/finance routes | Blocked | Blocked | Local build/specs pass and redirect output is present. Deployed DNS for `admin.mabuntle.com` does not resolve. |

## Defects Fixed

- No Angular source defects were found during this pass.
- Documentation was updated to remove stale Angular Material guidance now that the app uses Tailwind/native UI primitives.

## External Follow-Ups

1. Add Cloudflare Pages custom domains/DNS for `seller.mabuntle.com` and `admin.mabuntle.com`.
2. Fix `api.mabuntle.com` so `/health` and `/health/ready` return the backend responses instead of a self-redirecting `308` loop. Check Cloudflare SSL/TLS mode, proxy status, and origin mapping to the Lightsail/Caddy host.
3. After those are fixed, rerun the deployed browser pass at desktop `1440px` and mobile `390px` or `430px` across the client, seller, and admin route lists.
4. Once the API health endpoint is reachable, smoke CORS/auth/login and SignalR from the deployed frontends.

## Deployment Phase 6 Recheck

Date: 2026-06-09

Repo-side deployment configuration was inspected:

- The frontend GitHub Actions workflow already deploys a matrix for `mabuntle`, `mabuntle-seller`, and `mabuntle-admin`.
- The backend deployment renders `API_DOMAIN=api.mabuntle.com`, all three CORS frontend origins, and Caddy reverse-proxies `api.mabuntle.com` to the API container.
- The API maps `/health` and `/health/ready` directly and does not call `UseHttpsRedirection`; the repeated `308` loop is therefore most consistent with external Cloudflare SSL/DNS/origin configuration.

Live checks still show:

| Check | Result |
|---|---|
| `mabuntle.com` DNS and root HTTP | Pass |
| `mabuntle.com/shop` | Pass after Cloudflare trailing-slash redirect |
| `seller.mabuntle.com` DNS | Fail, DNS name does not exist |
| `admin.mabuntle.com` DNS | Fail, DNS name does not exist |
| `https://api.mabuntle.com/health` | Fail, repeated `308 Permanent Redirect` to the same URL |
| `https://api.mabuntle.com/health/ready` | Fail, repeated `308 Permanent Redirect` to the same URL |

Added `scripts/check-deployment-smoke.ps1` so the deployed DNS/API/frontend checks can be rerun after Cloudflare and Lightsail settings are corrected.
