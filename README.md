# Mabuntle

Mabuntle is a transactional marketplace foundation for fashion, beauty, jewellery, and accessories.

This repository is a monorepo with:

- `backend/`: ASP.NET Core Web API, worker, domain/application/infrastructure projects, and tests.
- `frontend/mabuntle-web/`: Angular SSR app shell.
- `database/`: migration and seed placeholders.
- `docs/`: concise engineering docs derived from the larger source references in `Documentation/`.

Prompt execution progress is tracked in `docs/codex-prompt-progress.md`.

Deployment notes for Cloudflare Pages, AWS Lightsail Docker Compose, and Lightsail managed PostgreSQL live in `docs/deployment-cloudflare-lightsail.md`.

## Prerequisites

- .NET SDK 9
- Node.js 22 and npm
- Angular CLI 19
- Docker Desktop, for local PostgreSQL

On this Windows machine, PowerShell blocks npm shim scripts. Use `cmd /c npm ...` for frontend commands unless the execution policy is changed.

## Backend

```powershell
dotnet tool restore
dotnet restore backend\Mabuntle.sln
dotnet build backend\Mabuntle.sln
dotnet test backend\Mabuntle.sln
dotnet run --project backend\src\Mabuntle.Api\Mabuntle.Api.csproj
```

Health check:

```text
GET https://localhost:<port>/health
```

Readiness check, including PostgreSQL:

```text
GET https://localhost:<port>/health/ready
```

Swagger UI is available in Development:

```text
https://localhost:<port>/swagger
```

Auth foundation endpoints:

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
GET  /api/auth/me
```

Public registration is limited to `Buyer` and `Seller`. Admin, support, and finance roles are seeded as roles only and are not self-assignable. Login and refresh return a short-lived access token; refresh tokens are set as HttpOnly cookies and refresh/logout require the `X-Mabuntle-CSRF` header from the non-HttpOnly CSRF cookie.

## Frontend

```powershell
cd frontend\mabuntle-web
cmd /c npm install
cmd /c npm run build
cmd /c npm start
```

Default local route:

```text
http://localhost:4200
```

Cookie-based refresh uses Secure, SameSite=Lax cookies by default. Cookie path/domain/SameSite/Secure settings live under `AuthCookies`; production startup rejects insecure cookie settings. For browser testing of session restore/logout cookies, run the Angular dev server over HTTPS or use a same-site HTTPS frontend origin.

## Database

```powershell
docker compose up -d
```

The compose file starts PostgreSQL with pgvector support. Runtime verification requires Docker to be installed locally.

EF Core migrations live in `backend/src/Mabuntle.Infrastructure/Persistence/Migrations`.

```powershell
dotnet dotnet-ef migrations add MigrationName --project backend\src\Mabuntle.Infrastructure --startup-project backend\src\Mabuntle.Api --context MabuntleDbContext --output-dir Persistence\Migrations
dotnet dotnet-ef database update --project backend\src\Mabuntle.Infrastructure --startup-project backend\src\Mabuntle.Api --context MabuntleDbContext
```

PostgreSQL integration tests are opt-in so normal test runs do not fail when Docker or PostgreSQL is unavailable.

```powershell
$env:MABUNTLE_RUN_POSTGRES_TESTS='true'
$env:MABUNTLE_TEST_POSTGRES_CONNECTION='Host=localhost;Port=5432;Database=mabuntle_integration_tests;Username=mabuntle;Password=change_me'
dotnet test backend\tests\Mabuntle.IntegrationTests\Mabuntle.IntegrationTests.csproj --filter PostgreSql
```

## Configuration

Use `.env.example` as the reference for local variables. Do not commit `.env` files or real secrets.

JWT configuration keys:

```text
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
Jwt__AccessTokenMinutes
Jwt__RefreshTokenDays
```

Use user-secrets or environment variables for real signing keys, webhook secrets, and database passwords. The root `appsettings.json` intentionally contains no usable production secrets. In `Production`, API startup fails when the JWT signing key, payment webhook signing secret, or PostgreSQL connection string is missing, weak, or a known local placeholder.

Payment webhook payload retention is controlled by `PaymentWebhookPayloadRetention__Enabled`, `PaymentWebhookPayloadRetention__RetentionDays`, and `PaymentWebhookPayloadRetention__BatchSize`. The worker redacts expired raw webhook payload JSON while keeping event metadata for finance reconciliation.

## CI

GitHub Actions runs backend restore/build/tests, PostgreSQL integration tests, and frontend install/build/tests on pull requests and pushes to `main`.
