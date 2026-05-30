# Cloudflare Pages And AWS Lightsail Deployment

This deployment path hosts the Angular app as static/prerendered Cloudflare Pages output, runs the API and Worker on an AWS Lightsail Ubuntu instance with Docker Compose, and uses a Lightsail managed PostgreSQL database.

## Frontend: Cloudflare Pages

Cloudflare Pages settings:

| Setting | Value |
|---|---|
| Root directory | `frontend/swyftly-web` |
| Build command | `npm ci && npm run build:cloudflare` |
| Build output directory | `dist/swyftly-web/browser` |
| Node version | `22` |

Required Cloudflare/GitHub configuration:

| Name | Type | Purpose |
|---|---|---|
| `CLOUDFLARE_API_TOKEN` | GitHub secret | Token with Cloudflare Pages deploy access. |
| `CLOUDFLARE_ACCOUNT_ID` | GitHub secret | Cloudflare account id. |
| `CLOUDFLARE_PAGES_PROJECT_NAME` | GitHub environment variable | Pages project name. |
| `SWYFTLY_API_BASE_URL` | GitHub environment variable | External API origin, `https://api.swyftly.co.za`. |

The Cloudflare build uses `scripts/write-production-api-url.mjs` to write the production Angular API origin at build time. The public `_redirects` file provides SPA fallback routing for routes that were not prerendered.

If a Cloudflare build fails with `Could not detect a directory containing static files` and shows `Deploy command: npx wrangler deploy`, the project was configured like a Worker deployment or is running from the repository root without building Angular first. Fix the Cloudflare project settings to use the values above, especially:

- Root directory: `frontend/swyftly-web`
- Build command: `npm ci && npm run build:cloudflare`
- Build output directory: `dist/swyftly-web/browser`
- Environment variables: `NODE_VERSION=22`, `SWYFTLY_API_BASE_URL=https://api.swyftly.co.za`

Do not use `npx wrangler deploy` for the Angular static site. Cloudflare Pages should upload the generated `dist/swyftly-web/browser` directory.

## Backend: Lightsail Docker Compose

The backend deployment uses:

- `backend/src/Swyftly.Api/Dockerfile`
- `backend/src/Swyftly.Worker/Dockerfile`
- `deploy/lightsail/docker-compose.yml`
- `deploy/lightsail/Caddyfile`
- `.github/workflows/deploy-backend.yml`

The GitHub workflow builds and pushes `swyftly-api` and `swyftly-worker` images to GHCR, builds an EF migration bundle, copies deployment assets to the Lightsail host, applies migrations, pulls the new images, restarts Docker Compose, and smoke-tests `/health` and `/health/ready`.

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
sudo mkdir -p /opt/swyftly
sudo chown -R ubuntu:ubuntu /opt/swyftly
```

Open inbound ports `80` and `443` on the Lightsail instance. Point the API DNS record, `api.swyftly.co.za`, to the Lightsail public IP before the first Caddy start so TLS can issue. Point the public frontend domain, `swyftly.co.za`, at Cloudflare Pages through the Pages custom domain setup.

## GitHub Production Environment

Create a GitHub Environment named `production` and require manual approval before deploys. Add these variables:

| Variable | Example |
|---|---|
| `API_DOMAIN` | `api.swyftly.co.za` |
| `FRONTEND_ORIGIN` | `https://swyftly.co.za` |
| `AUTH_COOKIE_DOMAIN` | `.swyftly.co.za` |
| `ACME_EMAIL` | `ops@swyftly.co.za` |
| `LIGHTSAIL_DEPLOY_PATH` | `/opt/swyftly` |
| `LIGHTSAIL_SSH_PORT` | `22` |
| `EMAIL_FROM_ADDRESS` | `no-reply@mail.swyftly.co.za` |
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

Swyftly uses the existing SMTP email provider for Resend. No Resend-specific backend provider is required.

Before deploying email delivery:

1. Verify `mail.swyftly.co.za` in Resend.
2. Add the Resend DNS records in Cloudflare for `mail.swyftly.co.za`.
3. Create a Resend API key.
4. Set GitHub production values:

```text
EMAIL_FROM_ADDRESS=no-reply@mail.swyftly.co.za
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
curl -fsS https://api.swyftly.co.za/health
curl -fsS https://api.swyftly.co.za/health/ready
```

Then verify from the browser:

- frontend home page loads from Cloudflare Pages;
- public shop/product routes load API data;
- login/register work;
- cart and checkout start;
- authenticated buyer account route loads;
- SignalR notification hub connects after login.

## Operational Notes

- Static Cloudflare Pages hosting is the default. Runtime SSR/functions are intentionally not configured.
- API-managed product media and seller evidence use the Docker volume `swyftly-storage` unless S3-compatible image storage is configured later.
- The Worker must run with the API because it processes email outbox, scheduled reports, reservation expiry, media cleanup, webhook payload retention, and carrier tracking sync.
- Payments are intentionally disabled in the first Lightsail deployment. When PayFast sandbox/live values are ready, change the backend workflow from `PaymentProvider__ProviderName=Disabled` to `PayFast`, restore the PayFast environment values, and use the final API domain for callback URLs before payment verification.
