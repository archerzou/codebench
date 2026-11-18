# Configuration, API keys and secrets

You need accounts with two third-party services to run the MVP (Clerk and GitHub), one more for payments later (Paddle or Stripe), and an optional one for public access (Cloudflare). Everything else is a password you generate yourself. Secrets live only in git-ignored files on your machine and in GitHub Actions secrets.

## Third-party accounts and keys to obtain

| Service | Needed from | Key / value | Format (example) | Secret? | Where to find it | Used by |
| --- | --- | --- | --- | --- | --- | --- |
| Clerk | Phase 1 | Publishable key | `pk_test_...` | No (safe in the browser) | Dashboard → API keys | Web app |
| Clerk | Phase 1 | Issuer (Frontend API URL) | `https://<name>.clerk.accounts.dev` | No | Dashboard → API keys ("Frontend API URL") | API (JWT validation, JWKS) |
| Clerk | Phase 1 | Secret key | `sk_test_...` | **Yes** | Dashboard → API keys | API (only if calling Clerk's Backend API), E2E testing tokens |
| Clerk | Phase 1 | Webhook signing secret | `whsec_...` | **Yes** | Dashboard → Webhooks → your endpoint | API (`/webhooks/clerk` verification) |
| GitHub | Phase 3 | `GITHUB_TOKEN` | automatic | Yes | Provided to every workflow run | Actions: push images to GHCR |
| GitHub | Phase 3 | Personal access token (fine-grained or classic), `read:packages` only | `ghp_...` / `github_pat_...` | **Yes** | Settings → Developer settings → Tokens | Your machine and the worker: pull private GHCR images |
| Paddle (option A) | Phase 7 | API key (sandbox) | starts with your sandbox prefix | **Yes** | Sandbox dashboard → Developer tools → Authentication | API: create checkouts, read subscriptions |
| Paddle (option A) | Phase 7 | Client-side token (sandbox) | `test_...` | No | Same page | Web app (Paddle.js checkout) |
| Paddle (option A) | Phase 7 | Notification (webhook) secret key | `pdl_ntfset_...` | **Yes** | Developer tools → Notifications → destination | API (`/webhooks/paddle` verification) |
| Paddle (option A) | Phase 7 | Price IDs for Pro / Pro+ | `pri_...` | No | Catalog → Prices | API + web app |
| Stripe (option B) | Phase 7 | Publishable key | `pk_test_...` | No | Dashboard → Developers → API keys | Web app |
| Stripe (option B) | Phase 7 | Secret key | `sk_test_...` | **Yes** | Same page | API |
| Stripe (option B) | Phase 7 | Webhook signing secret | `whsec_...` | **Yes** | Developers → Webhooks, or `stripe listen` locally | API (`/webhooks/stripe`) |
| Cloudflare (optional) | Any time | Tunnel token | long opaque string | **Yes** | Zero Trust → Networks → Tunnels | `cloudflared` container, for public access or receiving webhooks locally |

Pick one payment provider, not both. Use only sandbox / test-mode keys until you deliberately go live. Formats shown are typical prefixes to help you recognise each value; copy the exact value from the dashboard.

**Getting webhooks to a local machine:** Clerk, Paddle and Stripe can't reach `api.codebench.localhost`. Use a free Cloudflare quick tunnel (`cloudflared tunnel --url http://localhost:80`), Stripe's CLI (`stripe listen --forward-to ...`), or each dashboard's "send test event" feature, and register the tunnel URL as the webhook endpoint. Each new endpoint gets its own signing secret.

## Values you generate yourself

| Variable | Used by | How to generate |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | postgres, api, db-backup | `openssl rand -base64 24` |
| `REDIS_PASSWORD` (hardening) | redis (`--requirepass`), api | `openssl rand -base64 24` |
| `NATS_USER`, `NATS_PASSWORD` (hardening) | nats (`--user`/`--pass`), api, worker | `openssl rand -base64 24` for the password |
| `GRAFANA_ADMIN_PASSWORD` | grafana | `openssl rand -base64 18` |
| `ADMIN_API_KEY` (optional) | api: protects `--seed` / admin slices if exposed over HTTP | `openssl rand -hex 32` |

On Windows without OpenSSL, `[Convert]::ToBase64String((1..24 | % { Get-Random -Max 256 }))` in PowerShell works for local use.

## Complete deploy/.env.example

This replaces the shorter version in the Docker local stack tab. Commit `.env.example` with empty secrets; keep the real `deploy/.env` git-ignored.

```dotenv
# ---------- Compose / images ----------
COMPOSE_PROJECT_NAME=codebench
GHCR_OWNER=archerzou                 # lowercase
TAG=latest                           # or a commit SHA to pin/roll back
GHCR_USERNAME=archerzou              # only for private images
GHCR_READ_TOKEN=                     # SECRET: PAT with read:packages only

# ---------- Public URLs ----------
MARKETING_URL=http://codebench.localhost
APP_URL=http://app.codebench.localhost
API_URL=http://api.codebench.localhost

# ---------- Database / cache / queue ----------
POSTGRES_USER=codebench
POSTGRES_DB=codebench
POSTGRES_PASSWORD=                   # SECRET
REDIS_PASSWORD=                      # SECRET (hardening)
NATS_USER=codebench
NATS_PASSWORD=                       # SECRET (hardening)

# ---------- Clerk ----------
CLERK_ISSUER=https://YOUR-INSTANCE.clerk.accounts.dev
CLERK_PUBLISHABLE_KEY=pk_test_
CLERK_SECRET_KEY=                    # SECRET (only if the API calls Clerk's Backend API)
CLERK_WEBHOOK_SECRET=                # SECRET whsec_...

# ---------- Payments (Phase 7, choose one) ----------
PAYMENTS_PROVIDER=none               # none | paddle | stripe
PADDLE_ENVIRONMENT=sandbox
PADDLE_API_KEY=                      # SECRET
PADDLE_CLIENT_TOKEN=                 # public, test_...
PADDLE_WEBHOOK_SECRET=               # SECRET
PADDLE_PRICE_PRO=
PADDLE_PRICE_PRO_PLUS=
STRIPE_PUBLISHABLE_KEY=              # public, pk_test_...
STRIPE_SECRET_KEY=                   # SECRET sk_test_...
STRIPE_WEBHOOK_SECRET=               # SECRET whsec_...

# ---------- Sandbox ----------
SANDBOX_RUNTIME=runc                 # runsc with gVisor on Linux
SANDBOX_MAX_PARALLEL=4

# ---------- Observability ----------
GRAFANA_ADMIN_PASSWORD=              # SECRET
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317

# ---------- Optional ----------
CLOUDFLARE_TUNNEL_TOKEN=             # SECRET
ADMIN_API_KEY=                       # SECRET
```

## Environment variables per service

.NET reads nested configuration from environment variables with a double underscore (`Clerk__Issuer` = `Clerk:Issuer`), and arrays with an index (`Cors__AllowedOrigins__0`). Compose maps the `.env` values above onto these names.

**api**

| Variable | Value / source | Secret? |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` in Compose, `Development` locally | No |
| `ASPNETCORE_HTTP_PORTS` | `8080` | No |
| `ConnectionStrings__Database` | `Host=postgres;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}` | Yes |
| `ConnectionStrings__Redis` | `redis:6379,password=${REDIS_PASSWORD}` | Yes |
| `ConnectionStrings__Nats` | `nats://${NATS_USER}:${NATS_PASSWORD}@nats:4222` | Yes |
| `Clerk__Issuer` | `${CLERK_ISSUER}` | No |
| `Clerk__AuthorizedParties__0` | `${APP_URL}` | No |
| `Clerk__SecretKey` | `${CLERK_SECRET_KEY}` | Yes |
| `Clerk__WebhookSecret` | `${CLERK_WEBHOOK_SECRET}` | Yes |
| `Cors__AllowedOrigins__0` / `__1` | `${APP_URL}`, `${MARKETING_URL}` | No |
| `Payments__Provider` | `${PAYMENTS_PROVIDER}` | No |
| `Paddle__ApiKey`, `Paddle__WebhookSecret`, `Paddle__Environment`, `Paddle__Prices__Pro` | from `.env` | Keys yes |
| `Stripe__SecretKey`, `Stripe__WebhookSecret` | from `.env` | Yes |
| `RateLimiting__Global__PermitLimit` etc. | template defaults | No |
| `Admin__ApiKey` | `${ADMIN_API_KEY}` | Yes |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME` | collector URL, `codebench-api` | No |

**worker** (deliberately no database, Clerk or payment values)

| Variable | Value / source | Secret? |
| --- | --- | --- |
| `ConnectionStrings__Nats` | `nats://${NATS_USER}:${NATS_PASSWORD}@nats:4222` | Yes |
| `DOCKER_HOST` | `tcp://sandbox-daemon:2375` | No |
| `Sandbox__Runtime` | `${SANDBOX_RUNTIME}` | No |
| `Sandbox__MaxParallel` | `${SANDBOX_MAX_PARALLEL}` | No |
| `Sandbox__Images__algorithms` / `__database` | `ghcr.io/${GHCR_OWNER}/codebench-sandbox-<kind>:${TAG}` | No |
| `Sandbox__Registry__Username`, `Sandbox__Registry__Password` | `${GHCR_USERNAME}`, `${GHCR_READ_TOKEN}` (private images only) | Password yes |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME` | collector URL, `codebench-worker` | No |

**web** (runtime `config.js`; everything here is public by design)

| Variable | Value | Notes |
| --- | --- | --- |
| `API_URL` | `${API_URL}` | |
| `MARKETING_URL` | `${MARKETING_URL}` | |
| `CLERK_PUBLISHABLE_KEY` | `${CLERK_PUBLISHABLE_KEY}` | Publishable, not secret |
| `PADDLE_CLIENT_TOKEN` or `STRIPE_PUBLISHABLE_KEY` | from `.env` | Phase 7; public tokens only |

Never add a variable whose name contains `SECRET`, `API_KEY` (server keys) or `PASSWORD` to the web container: anything in `config.js` is readable by every visitor.

**marketing** (build-time arguments)

| Variable | Value |
| --- | --- |
| `SITE_URL` | `${MARKETING_URL}` |
| `PUBLIC_APP_URL` | `${APP_URL}` |

**infrastructure containers**

| Service | Variables |
| --- | --- |
| postgres | `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` |
| redis | command `redis-server --requirepass ${REDIS_PASSWORD}` |
| nats | command adds `--user ${NATS_USER} --pass ${NATS_PASSWORD}` |
| sandbox-daemon | `DOCKER_TLS_CERTDIR=""` |
| grafana | `GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD}` |
| db-backup | `PGPASSWORD=${POSTGRES_PASSWORD}` |
| cloudflared (optional) | command `tunnel run --token ${CLOUDFLARE_TUNNEL_TOKEN}` |

## Local development without Docker for your code

When you run the API and worker with `dotnet watch` and the frontends with `npm run dev`, configuration comes from these files instead of `deploy/.env`:

```bash
# API: .NET user-secrets (stored outside the repo)
cd src/Web.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=15432;Database=codebench;Username=codebench;Password=<pw>"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:16379,password=<pw>"
dotnet user-secrets set "ConnectionStrings:Nats" "nats://codebench:<pw>@localhost:4222"
dotnet user-secrets set "Clerk:Issuer" "https://YOUR-INSTANCE.clerk.accounts.dev"
dotnet user-secrets set "Clerk:WebhookSecret" "whsec_..."
dotnet user-secrets set "Clerk:AuthorizedParties:0" "http://localhost:5173"

# Worker
cd ../Worker
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Nats" "nats://codebench:<pw>@localhost:4222"
dotnet user-secrets set "Sandbox:Images:algorithms" "codebench-sandbox-algorithms:dev"
# DOCKER_HOST=tcp://127.0.0.1:2375 as an environment variable (dev override publishes it on localhost only)
```

```dotenv
# apps/web/.env.local  (git-ignored by Vite's default .gitignore)
VITE_API_URL=http://localhost:5000
VITE_MARKETING_URL=http://localhost:4321
VITE_CLERK_PUBLISHABLE_KEY=pk_test_...
```

```dotenv
# apps/marketing/.env  (git-ignored)
SITE_URL=http://localhost:4321
PUBLIC_APP_URL=http://localhost:5173
```

Non-secret defaults (rate limits, sandbox limits, CORS for localhost) belong in `appsettings.json` / `appsettings.Development.json`, which are committed.

## GitHub Actions secrets and variables

| Name | Type | Used by | Needed when |
| --- | --- | --- | --- |
| `GITHUB_TOKEN` | Automatic | `images.yml`, `sandbox-images.yml` | Always |
| `CLERK_TEST_PUBLISHABLE_KEY` | Secret | `e2e.yml` | E2E tests |
| `CLERK_TEST_SECRET_KEY` | Secret | `e2e.yml` (Clerk testing tokens for signed-in flows) | Signed-in E2E tests |
| `E2E_POSTGRES_PASSWORD` | Secret (any random value) | `e2e.yml` | E2E tests |
| `GHCR_OWNER` | Variable | workflows that tag images | If the owner differs from the repo owner |

Add them in the repository under Settings → Secrets and variables → Actions. Use a separate Clerk development instance for CI so test users never mix with yours.

## Handling rules

- [ ] `.gitignore` contains `deploy/.env`, `**/.env.local`, `apps/marketing/.env`, `backups/`, `captures/` (REA HAR files hold session cookies).
- [ ] Only `.env.example` files are committed, with secret values empty.
- [ ] Secret scanning and push protection are on in the GitHub repository.
- [ ] Publishable keys (Clerk `pk_`, Stripe `pk_`, Paddle client tokens) may reach the browser; secret keys, webhook secrets, passwords and tokens never do. Verify with the REA bundle audit before each release.
- [ ] The worker receives no database, Clerk or payment secrets.
- [ ] Rotate a key immediately if it is ever pasted into a commit, issue, chat or screenshot; then rewrite the value everywhere it is used.
- [ ] Test-mode keys only until you intentionally go live; live keys go in a separate `.env.production` that never leaves the machine that runs production.
