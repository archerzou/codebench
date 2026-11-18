# Local Docker stack

One Compose project runs all 15 services on your machine: `docker compose up -d` starts everything, Traefik serves it on port 80 under `*.codebench.localhost`, and nothing except Traefik publishes a port.

## Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| Docker Desktop or Docker Engine | Compose v2 | Windows: enable the WSL2 backend; give Docker ≥ 8 GB RAM |
| .NET SDK | 10.0 | For running tests and `dotnet watch` outside containers |
| Node.js | 24 LTS | Frontends; also meets REA's requirement |
| Git + GitHub account | – | GHCR images and Actions |

`*.localhost` names resolve to 127.0.0.1 in Chrome, Edge and Firefox without any setup. Command-line tools on Windows may not resolve them; if `curl http://api.codebench.localhost` fails, add lines like `127.0.0.1 api.codebench.localhost` to your hosts file.

## Service map

| Service | Image | Networks | URL / port | Purpose |
| --- | --- | --- | --- | --- |
| `traefik` | `traefik:v3` | edge | :80, `traefik.codebench.localhost` | Reverse proxy, the only published port |
| `marketing` | built from `apps/marketing` | edge | `codebench.localhost` | Astro static site |
| `web` | built from `apps/web` | edge | `app.codebench.localhost` | React app |
| `api` | built from `src/Web.Api` | edge, backend | `api.codebench.localhost` | .NET API |
| `worker` | built from `src/Worker` | backend, exec | – | Grading orchestrator |
| `sandbox-daemon` | `docker:dind` | exec, egress | – | Runs sandbox containers only |
| `postgres` | `postgres:17` | backend | – | Application database |
| `redis` | `redis:7-alpine` | backend | – | Cache L2 |
| `nats` | `nats:2` | backend | – | JetStream queue |
| `otel-collector` | `otel/opentelemetry-collector-contrib` | backend | – | Receives OTLP from API and worker |
| `prometheus` | `prom/prometheus` | backend | – | Metrics |
| `loki` | `grafana/loki` | backend | – | Logs |
| `tempo` | `grafana/tempo` | backend | – | Traces |
| `grafana` | `grafana/grafana` | edge, backend | `grafana.codebench.localhost` | Dashboards |
| `db-backup` | `postgres:17` | backend | – | Nightly `pg_dump` to `./backups` |

## deploy/.env.example

```dotenv
# Copy to deploy/.env (git-ignored) and fill in
COMPOSE_PROJECT_NAME=codebench
GHCR_OWNER=archerzou
TAG=latest

POSTGRES_USER=codebench
POSTGRES_PASSWORD=change-me
POSTGRES_DB=codebench

CLERK_ISSUER=https://YOUR-INSTANCE.clerk.accounts.dev
CLERK_PUBLISHABLE_KEY=pk_test_xxx
CLERK_WEBHOOK_SECRET=whsec_xxx

GRAFANA_ADMIN_PASSWORD=change-me
```

## deploy/docker-compose.yml

```yaml
name: codebench

x-otel-env: &otel-env
  OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317
  OTEL_EXPORTER_OTLP_PROTOCOL: grpc

services:
  traefik:
    image: traefik:v3.6          # v3.3 can't talk to Docker Engine 29+ (API 1.24 rejected)
    command:
      - --providers.docker=true
      - --providers.docker.exposedbydefault=false
      - --providers.docker.network=codebench_edge
      - --entrypoints.web.address=:80
      - --api.dashboard=true
    ports: ["80:80"]
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro   # Traefik reads labels only; trusted, local-only
    networks: [edge]
    labels:
      - traefik.enable=true
      - traefik.http.routers.dashboard.rule=Host(`traefik.codebench.localhost`)
      - traefik.http.routers.dashboard.service=api@internal

  marketing:
    image: ghcr.io/${GHCR_OWNER}/codebench-marketing:${TAG}
    build: { context: ../apps/marketing }
    networks: [edge]
    labels:
      - traefik.enable=true
      - traefik.http.routers.marketing.rule=Host(`codebench.localhost`)
      - traefik.http.services.marketing.loadbalancer.server.port=80

  web:
    image: ghcr.io/${GHCR_OWNER}/codebench-web:${TAG}
    build: { context: ../apps/web }
    environment:
      API_URL: http://api.codebench.localhost
      CLERK_PUBLISHABLE_KEY: ${CLERK_PUBLISHABLE_KEY}
      MARKETING_URL: http://codebench.localhost
    networks: [edge]
    labels:
      - traefik.enable=true
      - traefik.http.routers.web.rule=Host(`app.codebench.localhost`)
      - traefik.http.services.web.loadbalancer.server.port=80

  api:
    image: ghcr.io/${GHCR_OWNER}/codebench-api:${TAG}
    build: { context: .., dockerfile: src/Web.Api/Dockerfile }
    environment:
      <<: *otel-env
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: "8080"
      OTEL_SERVICE_NAME: codebench-api
      ConnectionStrings__Database: Host=postgres;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      ConnectionStrings__Redis: redis:6379
      ConnectionStrings__Nats: nats://nats:4222
      Clerk__Issuer: ${CLERK_ISSUER}
      Clerk__WebhookSecret: ${CLERK_WEBHOOK_SECRET}
      Clerk__AuthorizedParties__0: http://app.codebench.localhost
      Cors__AllowedOrigins__0: http://app.codebench.localhost
    depends_on:
      postgres: { condition: service_healthy }
      nats: { condition: service_started }
      redis: { condition: service_started }
    networks: [edge, backend]
    labels:
      - traefik.enable=true
      - traefik.http.routers.api.rule=Host(`api.codebench.localhost`)
      - traefik.http.services.api.loadbalancer.server.port=8080

  worker:
    image: ghcr.io/${GHCR_OWNER}/codebench-worker:${TAG}
    build: { context: .., dockerfile: src/Worker/Dockerfile }
    environment:
      <<: *otel-env
      OTEL_SERVICE_NAME: codebench-worker
      ConnectionStrings__Nats: nats://nats:4222
      DOCKER_HOST: tcp://sandbox-daemon:2375
      Sandbox__Runtime: runc
      Sandbox__Images__algorithms: ghcr.io/${GHCR_OWNER}/codebench-sandbox-algorithms:${TAG}
      Sandbox__Images__database: ghcr.io/${GHCR_OWNER}/codebench-sandbox-database:${TAG}
      # deliberately NO database connection string
    depends_on: [nats, sandbox-daemon]
    networks: [backend, exec]

  sandbox-daemon:
    image: docker:27-dind
    privileged: true                    # required by Docker-in-Docker
    environment:
      DOCKER_TLS_CERTDIR: ""            # plain TCP on the internal exec network
    command: ["--host=tcp://0.0.0.0:2375"]
    volumes: [dind-data:/var/lib/docker]
    networks: [exec, egress]

  postgres:
    image: postgres:17
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 5s
      retries: 10
    networks: [backend]

  redis:
    image: redis:7-alpine
    networks: [backend]

  nats:
    image: nats:2
    command: ["-js", "-sd", "/data", "-m", "8222"]
    volumes: [natsdata:/data]
    networks: [backend]

  # --- observability: reuse the configs from your template under deploy/otel, deploy/grafana ---
  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel/config.yaml"]
    volumes: [./otel/config.yaml:/etc/otel/config.yaml:ro]
    networks: [backend]
  prometheus:
    image: prom/prometheus:latest
    command: ["--config.file=/etc/prometheus/prometheus.yml", "--web.enable-remote-write-receiver"]
    volumes: [./prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro]
    networks: [backend]
  loki:
    image: grafana/loki:latest
    networks: [backend]
  tempo:
    image: grafana/tempo:latest
    command: ["-config.file=/etc/tempo.yaml"]
    volumes: [./tempo/tempo.yaml:/etc/tempo.yaml:ro]
    networks: [backend]
  grafana:
    image: grafana/grafana:latest
    environment:
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_ADMIN_PASSWORD}
    volumes:
      - ./grafana/provisioning:/etc/grafana/provisioning:ro
      - grafanadata:/var/lib/grafana
    networks: [edge, backend]
    labels:
      - traefik.enable=true
      - traefik.http.routers.grafana.rule=Host(`grafana.codebench.localhost`)
      - traefik.http.services.grafana.loadbalancer.server.port=3000

  db-backup:
    image: postgres:17
    environment:
      PGPASSWORD: ${POSTGRES_PASSWORD}
    entrypoint: ["/bin/sh", "-c"]
    command:
      - |
        while true; do
          pg_dump -h postgres -U ${POSTGRES_USER} ${POSTGRES_DB} | gzip > /backups/codebench-$$(date +%F).sql.gz
          find /backups -name '*.sql.gz' -mtime +7 -delete
          sleep 86400
        done
    volumes: [../backups:/backups]
    depends_on: { postgres: { condition: service_healthy } }
    networks: [backend]

networks:
  edge: {}
  backend: {}
  exec: { internal: true }   # no route to the internet; only worker <-> sandbox-daemon
  egress: {}                 # lets the sandbox daemon pull images

volumes: { pgdata: {}, natsdata: {}, dind-data: {}, grafanadata: {} }
```

Pin `latest` tags to specific versions once things work; CI should never depend on a moving tag.

## Dockerfiles for the .NET services

Your template already ships `src/Web.Api/Dockerfile`; check that it builds from the repo root (it needs `Directory.Build.props` and `Directory.Packages.props`). The worker follows the same pattern:

```dockerfile
# src/Worker/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Contracts/ src/Contracts/
COPY src/Worker/ src/Worker/
RUN dotnet publish src/Worker -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN useradd --uid 10001 --no-create-home app
USER 10001
COPY --from=build /out /app
ENTRYPOINT ["dotnet", "/app/CodeBench.Worker.dll"]
```

## Loading sandbox images into the sandbox daemon

The worker asks `sandbox-daemon` to start images, so those images must exist *inside* that daemon. Two ways:

1. **Pull from GHCR (default):** CI pushes `codebench-sandbox-*` images; the worker calls `docker.Images.CreateImageAsync` for each configured image at start-up. For private images, also pass GHCR credentials (a read-only token) to the pull call.
2. **Build locally without a registry:** pipe a local build into the nested daemon:

```bash
docker build -f sandbox-images/algorithms.Dockerfile -t codebench-sandbox-algorithms:dev .
docker save codebench-sandbox-algorithms:dev | docker compose -f deploy/docker-compose.yml exec -T sandbox-daemon docker load
```

Then set `Sandbox__Images__algorithms=codebench-sandbox-algorithms:dev` in a dev override.

## Development overrides (deploy/docker-compose.dev.yml)

During day-to-day work, run infrastructure in Docker and your code with hot reload on the host:

```yaml
services:
  postgres: { ports: ["127.0.0.1:15432:5432"] }   # host 5432 often taken by a native Postgres
  redis:    { ports: ["127.0.0.1:16379:6379"] }   # host 6379 inside Windows' WinNAT excluded range
  nats:     { ports: ["127.0.0.1:4222:4222", "127.0.0.1:8222:8222"] }
  sandbox-daemon: { ports: ["127.0.0.1:2375:2375"] }   # dev only: lets a host-run worker reach it
  otel-collector: { ports: ["127.0.0.1:4317:4317"] }
```

```bash
# infra only
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml \
  up -d postgres redis nats sandbox-daemon otel-collector prometheus loki tempo grafana traefik

# code with hot reload
dotnet watch --project src/Web.Api
dotnet watch --project src/Worker
npm --prefix apps/web run dev          # http://localhost:5173
npm --prefix apps/marketing run dev    # http://localhost:4321
```

Ports are bound to `127.0.0.1` only. Never publish the DinD port on all interfaces.

## Everyday commands

| Task | Command |
| --- | --- |
| Start everything (build locally) | `docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build` |
| Start from CI-built images | `docker compose -f deploy/docker-compose.yml --env-file deploy/.env pull && ... up -d` |
| Tail API and worker logs | `docker compose -f deploy/docker-compose.yml logs -f api worker` |
| Apply EF migrations | Template applies them at startup in Development; for Production, run a one-off: `docker compose ... run --rm api --migrate` (add a flag in `Program.cs`) |
| Seed problems | `docker compose ... run --rm api --seed` |
| Inspect JetStream | `docker run --rm --network codebench_backend natsio/nats-box nats -s nats://nats:4222 stream ls` |
| List live sandboxes | `docker compose ... exec sandbox-daemon docker ps` |
| Restore a backup | `gunzip -c backups/codebench-YYYY-MM-DD.sql.gz \| docker compose ... exec -T postgres psql -U codebench codebench` |
| Reset everything | `docker compose ... down -v` (deletes volumes: data, queues, sandbox image cache) |

## Smoke check after `up`

- [ ] `http://codebench.localhost` shows the marketing site and its "Start free" link opens the app.
- [ ] `http://app.codebench.localhost` loads; sign-in with Clerk works and redirects back.
- [ ] `http://api.codebench.localhost/health` returns healthy (add a health endpoint covering Postgres, Redis and NATS).
- [ ] Running the Two Sum starter code returns a result within a few seconds.
- [ ] `http://grafana.codebench.localhost` shows a trace spanning API → NATS → worker for that run.
- [ ] A `.sql.gz` file appears in `./backups` (or trigger `db-backup` manually).

## Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| 404 from Traefik | Router label typo or container not on `edge` | Check the dashboard at `traefik.codebench.localhost` |
| CORS error in the browser | Origin missing from `Cors__AllowedOrigins` | Add the exact scheme + host |
| 401 on every API call | Wrong `Clerk__Issuer` or `azp` not in `AuthorizedParties` | Decode the token at jwt.io-style tools locally and compare `iss` and `azp` |
| Runs stay Pending | Worker can't reach NATS or sandbox daemon; image not loaded | `logs worker`; `exec sandbox-daemon docker images` |
| Sandbox daemon won't start | Privileged containers disabled, or not enough disk | Enable in Docker settings; prune `dind-data` |
| Timings noisy | Laptop power-saving, other load | Use relative budgets with headroom; median of 7 runs |
