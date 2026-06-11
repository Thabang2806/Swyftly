# Cloudflare Pages And AWS Lightsail Deployment

This deployment path hosts three Angular builds as Cloudflare Pages outputs, runs the API and Worker on an AWS Lightsail Ubuntu instance with Docker Compose, and uses a Lightsail managed PostgreSQL database.

## Frontend: Cloudflare Pages

Cloudflare Pages projects:

| Domain | Pages project | Build command | Build output directory |
|---|---|---|---|
| `mabuntle.com` | `mabuntle` | `npm ci && npm run build:client:cloudflare` | `dist/mabuntle-client/browser` |
| `seller.mabuntle.com` | `mabuntle-seller` | `npm ci && npm run build:seller:cloudflare` | `dist/mabuntle-seller/browser` |
| `admin.mabuntle.com` | `mabuntle-admin` | `npm ci && npm run build:admin:cloudflare` | `dist/mabuntle-admin/browser` |

Use root directory `frontend/mabuntle-web` and Node version `22` for all three projects.

Required Cloudflare/GitHub configuration:

| Name | Type | Purpose |
|---|---|---|
| `CLOUDFLARE_API_TOKEN` | GitHub secret | Token with Cloudflare Pages deploy access. |
| `CLOUDFLARE_ACCOUNT_ID` | GitHub secret | Cloudflare account id. |
| `CLOUDFLARE_PAGES_PROJECT_CLIENT` | GitHub environment variable | Client Pages project, `mabuntle`. |
| `CLOUDFLARE_PAGES_PROJECT_SELLER` | GitHub environment variable | Seller Pages project, `mabuntle-seller`. |
| `CLOUDFLARE_PAGES_PROJECT_ADMIN` | GitHub environment variable | Admin Pages project, `mabuntle-admin`. |
| `MABUNTLE_API_BASE_URL` | GitHub environment variable | External API origin, `https://api.mabuntle.com`. |

The Cloudflare build uses `scripts/write-production-api-url.mjs` to write the production Angular API origin at build time. App-specific `_redirects` files provide SPA fallback routing and compatibility redirects for old `/seller/*` and `/admin/*` URLs.

If a Cloudflare build fails with `Could not detect a directory containing static files` and shows `Deploy command: npx wrangler deploy`, the project was configured like a Worker deployment or is running from the repository root without building Angular first. Fix the Cloudflare project settings to use the values above, especially:

- Root directory: `frontend/mabuntle-web`
- Build command: one of the three commands listed above.
- Build output directory: the matching `dist/mabuntle-*/browser` directory.
- Environment variables: `NODE_VERSION=22`, `MABUNTLE_API_BASE_URL=https://api.mabuntle.com`

Do not use `npx wrangler deploy` for the Angular static site. Cloudflare Pages should upload the generated `dist/mabuntle-*/browser` directory, or GitHub Actions should run `wrangler pages deploy` for each project.

### Local Frontend Development

The split Cloudflare apps also have local live-reload serve targets. Start the API first:

```powershell
dotnet run --project backend\src\Mabuntle.Api
```

Then run each frontend from `frontend/mabuntle-web`:

```powershell
cmd /c npm run serve:client
cmd /c npm run serve:seller
cmd /c npm run serve:admin
```

Local URLs:

| App | URL | API config |
|---|---|---|
| Client marketplace | `http://localhost:4200` | `src/environments/environment.development.ts` |
| Seller workspace | `http://localhost:4201` | `src/environments/environment.development.ts` |
| Admin console | `http://localhost:4202` | `src/environments/environment.development.ts` |

These commands use the local API origin, currently `https://localhost:7268`, and do not run the Cloudflare production build pipeline.

### Custom Domains

Each frontend surface is a separate Pages project and needs its own custom domain:

| Pages project | Custom domain |
|---|---|
| `mabuntle` | `mabuntle.com` |
| `mabuntle-seller` | `seller.mabuntle.com` |
| `mabuntle-admin` | `admin.mabuntle.com` |

Add these under `Workers & Pages -> Pages project -> Custom domains`. Cloudflare usually creates the DNS records automatically. If it does not, add CNAME records:

```text
seller  CNAME  mabuntle-seller.pages.dev
admin   CNAME  mabuntle-admin.pages.dev
```

Do not point `api.mabuntle.com` at a Pages project. The API hostname must point to the Lightsail static IP, either directly as a DNS-only A record or through Cloudflare proxy with the record content set to the Lightsail static IP.

## Backend: Lightsail Docker Compose

The backend deployment uses:

- `backend/src/Mabuntle.Api/Dockerfile`
- `backend/src/Mabuntle.Worker/Dockerfile`
- `deploy/lightsail/docker-compose.yml`
- `deploy/lightsail/Caddyfile`
- `.github/workflows/deploy-backend.yml`

The GitHub workflow builds and pushes `mabuntle-api` and `mabuntle-worker` images to GHCR, builds an EF migration bundle, copies deployment assets to the Lightsail host, applies migrations, pulls the new images, restarts Docker Compose, and smoke-tests `/health` and `/health/ready`.

Minimum Lightsail host setup:

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker ubuntu
sudo mkdir -p /opt/mabuntle
sudo chown -R ubuntu:ubuntu /opt/mabuntle
```

Open inbound ports `80` and `443` on the Lightsail instance. Point the API DNS record, `api.mabuntle.com`, to the Lightsail public IP before the first Caddy start so TLS can issue. Point `mabuntle.com`, `seller.mabuntle.com`, and `admin.mabuntle.com` at their Cloudflare Pages projects through the Pages custom domain setup.

If `https://api.mabuntle.com/health` returns a repeated `308 Permanent Redirect` to the same URL, check Cloudflare before changing application code:

- Cloudflare SSL/TLS mode must be `Full` or `Full (strict)`, not `Flexible`.
- The `api` DNS record content must be the Lightsail static IP, not a Pages target.
- Lightsail firewall ports `80` and `443` must be open.
- Caddy must be running with `API_DOMAIN=api.mabuntle.com` in `.env.production`.
- The backend app already uses forwarded headers; a self-redirect loop is normally caused by Cloudflare connecting to the origin over plain HTTP while the origin redirects to HTTPS.

## GitHub Production Environment

Create a GitHub Environment named `production` and require manual approval before deploys. Add these variables:

| Variable | Example |
|---|---|
| `API_DOMAIN` | `api.mabuntle.com` |
| `FRONTEND_ORIGIN` | `https://mabuntle.com` |
| `SELLER_FRONTEND_ORIGIN` | `https://seller.mabuntle.com` |
| `ADMIN_FRONTEND_ORIGIN` | `https://admin.mabuntle.com` |
| `AUTH_COOKIE_DOMAIN` | `.mabuntle.com` |
| `ACME_EMAIL` | `ops@mabuntle.com` |
| `LIGHTSAIL_DEPLOY_PATH` | `/opt/mabuntle` |
| `LIGHTSAIL_SSH_PORT` | `22` |
| `EMAIL_FROM_ADDRESS` | `no-reply@mail.mabuntle.com` |
| `SMTP_PORT` | `587` |

Add these secrets:

| Secret | Purpose |
|---|---|
| `LIGHTSAIL_HOST` | Lightsail public host/IP. |
| `LIGHTSAIL_SSH_USER` | SSH user, usually `ubuntu`. |
| `LIGHTSAIL_SSH_PRIVATE_KEY` | Private key for deployment SSH. |
| `PROD_DB_CONNECTION_STRING` | Lightsail managed PostgreSQL connection string. |
| `PROD_JWT_SIGNING_KEY` | Strong production JWT signing key. |
| `GHCR_USERNAME` | GHCR username for the Lightsail host to pull images. |
| `GHCR_TOKEN` | GHCR token with `read:packages`. |
| `SMTP_HOST` | `smtp.resend.com` for Resend SMTP. |
| `SMTP_USERNAME` | `resend` for Resend SMTP. |
| `SMTP_PASSWORD` | Resend API key. |

The API validates production configuration at startup. Production rejects placeholder JWT/database values, fake payment provider mode, log-only email delivery, wildcard/local CORS origins, insecure auth cookies, and fake carrier mode. This first deployment uses `PaymentProvider__ProviderName=Disabled`, so buyer payment initiation returns a clear unavailable response until PayFast is configured.

## Resend SMTP

Mabuntle uses the existing SMTP email provider for Resend. No Resend-specific backend provider is required.

Before deploying email delivery:

1. Verify `mail.mabuntle.com` in Resend.
2. Add the Resend DNS records in Cloudflare for `mail.mabuntle.com`.
3. Create a Resend API key.
4. Set GitHub production values:

```text
EMAIL_FROM_ADDRESS=no-reply@mail.mabuntle.com
SMTP_HOST=smtp.resend.com
SMTP_PORT=587
SMTP_USERNAME=resend
SMTP_PASSWORD=<Resend API key>
```

After redeploying the backend, `/health/ready` should report the `email-delivery` check as healthy. Transactional email remains worker-driven through the persisted notification email outbox.

## Database

Use Lightsail managed PostgreSQL and verify `pgvector` before first deployment:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
SELECT extname FROM pg_extension WHERE extname = 'vector';
```

Enable automated backups in Lightsail. Keep the connection string in GitHub Secrets only. The backend deployment workflow applies EF migrations with a generated migration bundle before restarting containers.

## Smoke Checks

After deployment:

```bash
curl -fsS https://api.mabuntle.com/health
curl -fsS https://api.mabuntle.com/health/ready
```

Or run the repository helper:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\check-deployment-smoke.ps1
```

Then verify from the browser:

- `https://mabuntle.com` loads the client Cloudflare Pages app;
- `https://seller.mabuntle.com` loads the seller Cloudflare Pages app and deep links such as `/products` refresh correctly;
- `https://admin.mabuntle.com` loads the admin Cloudflare Pages app and deep links such as `/support` refresh correctly;
- public shop/product routes load API data;
- login/register work;
- cart and checkout start;
- authenticated buyer account route loads;
- SignalR notification hub connects after login.

## Operational Notes

- Static Cloudflare Pages hosting is the default. Runtime SSR/functions are intentionally not configured.
- API-managed product media and seller evidence use the Docker volume `mabuntle-storage` unless S3-compatible image storage is configured later.
- The Worker must run with the API because it processes email outbox, scheduled reports, reservation expiry, media cleanup, webhook payload retention, and carrier tracking sync.
- Payments are intentionally disabled in the first Lightsail deployment. When PayFast sandbox/live values are ready, change the backend workflow from `PaymentProvider__ProviderName=Disabled` to `PayFast`, restore the PayFast environment values, and use the final API domain for callback URLs before payment verification.
