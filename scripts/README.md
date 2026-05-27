# Swyftly Scripts

## Development environment verification

Run the non-mutating preflight helper before long local QA passes:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify-dev-environment.ps1
```

The helper checks the .NET SDK, NuGet source/connectivity, existing restore assets, PostgreSQL TCP reachability, Node/npm, frontend dependencies, Karma config, and Chrome availability. It exits non-zero when a critical local blocker is found.

Common recovery steps:

| Blocker | Recovery |
| --- | --- |
| `api.nuget.org` is missing or disabled | Run `dotnet nuget list source`. Re-enable or add the public NuGet source without committing machine-specific credentials. |
| NuGet restore fails with socket or network policy errors | Verify network access to `api.nuget.org:443`. In sandboxed Codex sessions, run restore with approved outside-sandbox network access. |
| Stale `obj/project.assets.json` or missing packages | Run `dotnet restore backend\Swyftly.sln`, then rebuild with `dotnet build backend\Swyftly.sln --no-restore`. |
| Backend DLLs are locked | Stop any running `Swyftly.Api`, Visual Studio debug sessions, or background `dotnet` processes before rebuilding. |
| PostgreSQL connection is missing | Set `ConnectionStrings__DefaultConnection`, pass `-ConnectionString` to the preflight helper, or update `backend/src/Swyftly.Api/appsettings.Development.json`. |
| Existing seed accounts use an old password | Re-run the seed with `-ResetPasswords` and the password you want to use for local testing. |

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

The script uses `ConnectionStrings__DefaultConnection` when set, otherwise it falls back to `backend/src/Swyftly.Api/appsettings.Development.json`.

Seed failures now propagate the underlying `dotnet run` exit code, so CI or local scripts can trust a non-zero exit when restore, build, migration, or database work fails.

Seeded accounts:

| Email | Roles and setup |
| --- | --- |
| `admin@swyftly.local` | `SuperAdmin`, `Admin` |
| `finance.operator@swyftly.local` | `FinanceOperator` |
| `finance.approver@swyftly.local` | `FinanceApprover` |
| `support@swyftly.local` | `SupportAgent` |
| `buyer@swyftly.local` | `Buyer` with buyer profile and, when `-SeedSampleProducts` is used, one default saved delivery address |
| `seller@swyftly.local` | `Seller` with verified seller profile, published storefront, payout placeholder approval, seller balance, standard delivery method, and, when `-SeedSampleProducts` is used, eight published sample products |
| `seller.pending@swyftly.local` | `Seller` with completed onboarding and an `UnderReview` seller verification when `-SeedSellerFlowDemo` is used |

`-SeedSellerFlowDemo` also creates one product in `PendingReview` and one ad campaign in `PendingReview` for the verified seller so `/admin/products` and `/admin/ads` can be tested immediately. See `docs/seller-flow-test-runbook.md` for the manual checklist and `docs/seller-flow-qa-results.md` for the latest QA evidence.

Do not use these accounts in production.
