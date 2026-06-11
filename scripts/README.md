# Mabuntle Scripts

## Development environment verification

Run the non-mutating preflight helper before long local QA passes:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify-dev-environment.ps1
```

The helper checks the .NET SDK, NuGet source/connectivity, existing restore assets, PostgreSQL TCP reachability, Node/npm, frontend dependencies, Karma config, and Chrome availability. It exits non-zero when a critical local blocker is found.

## Local frontend development

Start the local API first:

```powershell
dotnet run --project backend\src\Mabuntle.Api
```

Then run the split Angular apps from `frontend/mabuntle-web`:

```powershell
cmd /c npm run serve:client
cmd /c npm run serve:seller
cmd /c npm run serve:admin
```

The local apps run at:

| App | URL |
| --- | --- |
| Client marketplace | `http://localhost:4200` |
| Seller workspace | `http://localhost:4201` |
| Admin console | `http://localhost:4202` |

All three serve targets use `src/environments/environment.development.ts`, so API calls target the local API origin, currently `https://localhost:7268`. Cloudflare production build scripts remain separate.

Common recovery steps:

| Blocker | Recovery |
| --- | --- |
| `api.nuget.org` is missing or disabled | Run `dotnet nuget list source`. Re-enable or add the public NuGet source without committing machine-specific credentials. |
| NuGet restore fails with socket or network policy errors | Verify network access to `api.nuget.org:443`. In sandboxed Codex sessions, run restore with approved outside-sandbox network access. |
| Stale `obj/project.assets.json` or missing packages | Run `dotnet restore backend\Mabuntle.sln`, then rebuild with `dotnet build backend\Mabuntle.sln --no-restore`. |
| Backend DLLs are locked | Stop any running `Mabuntle.Api`, Visual Studio debug sessions, or background `dotnet` processes before rebuilding. |
| PostgreSQL connection is missing | Set `ConnectionStrings__DefaultConnection`, pass `-ConnectionString` to the preflight helper, or update `backend/src/Mabuntle.Api/appsettings.Development.json`. |
| Existing seed accounts use an old password | Re-run the seed with `-ResetPasswords` and the password you want to use for local testing. |

## Deployment smoke verification

After Cloudflare Pages custom domains and the Lightsail API DNS are configured, run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\check-deployment-smoke.ps1
```

The helper checks DNS and HTTP status for:

- `mabuntle.com`
- `seller.mabuntle.com`
- `admin.mabuntle.com`
- `api.mabuntle.com`

It exits non-zero when a required DNS or HTTP check fails. API health endpoints must return `200`; frontend deep links may return a Cloudflare trailing-slash redirect as long as the redirect is not a loop.

## Development user seed

Use `seed-dev-users.ps1` to create local test users in the configured PostgreSQL database. The script uses ASP.NET Identity and EF Core, so password hashes, roles, and seller/buyer profile records are created through the same mappings as the API.

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!"
```

If the database has not been migrated yet:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations
```

To reset passwords for already-created seed users:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ResetPasswords
```

To seed the buyer-flow demo catalog as well:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations -SeedSampleProducts
```

To seed the seller approval flow demo records as well:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\seed-dev-users.ps1 -Password "UseYourOwnDevPassword1!" -ApplyMigrations -SeedSampleProducts -SeedSellerFlowDemo
```

The script uses `ConnectionStrings__DefaultConnection` when set, otherwise it falls back to `backend/src/Mabuntle.Api/appsettings.Development.json`.

Seed failures now propagate the underlying `dotnet run` exit code, so CI or local scripts can trust a non-zero exit when restore, build, migration, or database work fails.

Seeded accounts:

| Email | Roles and setup |
| --- | --- |
| `admin@mabuntle.local` | `SuperAdmin`, `Admin` |
| `finance.operator@mabuntle.local` | `FinanceOperator` |
| `finance.approver@mabuntle.local` | `FinanceApprover` |
| `support@mabuntle.local` | `SupportAgent` |
| `buyer@mabuntle.local` | `Buyer` with buyer profile and, when `-SeedSampleProducts` is used, one default saved delivery address |
| `seller@mabuntle.local` | `Seller` with verified seller profile, published storefront, payout placeholder approval, seller balance, standard delivery method, and, when `-SeedSampleProducts` is used, eight published sample products |
| `seller.pending@mabuntle.local` | `Seller` with completed onboarding and an `UnderReview` seller verification when `-SeedSellerFlowDemo` is used |

`-SeedSellerFlowDemo` also creates one product in `PendingReview` and one ad campaign in `PendingReview` for the verified seller so `/admin/products` and `/admin/ads` can be tested immediately. See `docs/seller-flow-test-runbook.md` for the manual checklist and `docs/seller-flow-qa-results.md` for the latest QA evidence.

## Buyer post-purchase demo helper

After seeding sample products, use `create-buyer-post-purchase-demo.ps1` to create a delivered buyer order through the real local APIs:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create-buyer-post-purchase-demo.ps1 -Password "UseYourOwnDevPassword1!"
```

The helper logs in as `buyer@mabuntle.local`, adds `rose-linen-midi-dress` to cart, quotes shipping, creates an order, initiates a `Fake` payment, posts a signed fake paid webhook, logs in as `seller@mabuntle.local`, and marks the order delivered through seller fulfilment endpoints.

Useful options:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create-buyer-post-purchase-demo.ps1 `
  -Password "UseYourOwnDevPassword1!" `
  -ApiBaseUrl "https://localhost:7268" `
  -FrontendBaseUrl "http://localhost:4200" `
  -ProductSlug "rose-linen-midi-dress" `
  -SkipCertificateCheck
```

Prerequisites:

- API must be running with `PaymentProvider:ProviderName=Fake`.
- `PaymentProvider:WebhookSigningSecret` must match the script secret. The default matches `appsettings.Development.json`.
- Sample products must already be seeded with `-SeedSampleProducts`.
- The script is API-only and fails non-zero when auth, checkout, webhook, or fulfilment fails.

Do not use these accounts in production.

## Buyer AI attribution demo helper

After seeding sample products, use `create-buyer-ai-attribution-demo.ps1` to validate the buyer AI discovery attribution path through the real local APIs:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create-buyer-ai-attribution-demo.ps1 -Password "UseYourOwnDevPassword1!" -SkipCertificateCheck
```

The helper logs in as `buyer@mabuntle.local`, calls the existing assistant and visual-search endpoints, records sanitized growth telemetry through `/api/buyer/growth-events`, adds the attributed product to cart, quotes shipping, creates an order, initiates a `Fake` payment, posts a signed fake paid webhook, then reads the aggregate admin buyer-growth report as `admin@mabuntle.local`.

Useful options:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create-buyer-ai-attribution-demo.ps1 `
  -Password "UseYourOwnDevPassword1!" `
  -ApiBaseUrl "https://localhost:7268" `
  -FrontendBaseUrl "http://localhost:4200" `
  -ProductSlug "rose-linen-midi-dress" `
  -AssistantOnly `
  -SkipCertificateCheck
```

The script is API-only, uses existing contracts, does not write attribution rows directly, and fails non-zero when auth, AI, telemetry, cart, checkout, webhook, or report reads fail. Because outcome attribution uses a 7-day buyer/tool window, run negative-control checks with a clean database or a buyer without recent AI telemetry.
