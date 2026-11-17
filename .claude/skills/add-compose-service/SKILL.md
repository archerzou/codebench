---
name: add-compose-service
description: Add or change a container in CodeBench's Docker Compose stack (deploy/docker-compose.yml) with the right networks, profile, env vars from deploy/.env, .env.example entries, Traefik labels, healthcheck, and dev-override ports. Use when the user wants a new service, container, or infrastructure piece in the local stack.
argument-hint: <service and purpose, e.g. "cloudflared tunnel for webhooks">
---

# Add a Compose Service

Guide 05 (service map, compose file) and guide 08 (config & secrets); rules AR-02, SB-02, SC-01.

## Decide first

| Question | Rule |
|---|---|
| Does the browser reach it? | Only then join `edge` **and** add Traefik labels (`Host(\`<name>.codebench.localhost\`)`, `loadbalancer.server.port`). Never publish host ports in the main file. |
| Does it need Postgres/Redis/NATS? | Join `backend`. |
| Does it run or talk to sandboxes? | Only the worker and `sandbox-daemon` may join `exec`. |
| Does it need the internet? | Only via `egress` (currently just `sandbox-daemon`). |
| Is it needed every day? | No profile = always on (infra only). Otherwise use a profile: `app`, `grading`, `backup`, or a new one. |

The **worker** must never receive database, Clerk or payment variables *[AR-02]*. Nothing but Traefik
mounts `/var/run/docker.sock` *[SB-02]*.

## Workflow

1. **Service block** in `deploy/docker-compose.yml`, in the right section: pinned image tag (no `latest`
   once it works), `networks`, `profiles`, `depends_on` with `condition: service_healthy` where a
   healthcheck exists, named volume for state (add it to `volumes:`), OTel env via `<<: *otel-env` for
   .NET services.
2. **Config** — every value comes from `deploy/.env` as `${VAR}`. Add each new variable to
   `deploy/.env.example` (secrets empty, with a `# SECRET` comment) and to guide 08's per-service table if
   it's a new kind of value. .NET settings use `Section__Key` names.
3. **Healthcheck** for anything others depend on.
4. **Dev override** — if host-run code (`dotnet watch`, `npm run dev`) needs it, add a port in
   `deploy/docker-compose.dev.yml` bound to `127.0.0.1` only.
5. **Docs** — add a row to the service map in `docs/guide/05-docker-local-stack.md`; update the profile list
   in the compose header comment and in `CLAUDE.md` if you add a profile.
6. **Verify**
   ```powershell
   docker compose -f deploy/docker-compose.yml --env-file deploy/.env.example --profile <p> config --quiet
   docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml --env-file deploy/.env --profile <p> up -d <service>
   docker compose -f deploy/docker-compose.yml --env-file deploy/.env ps
   powershell -File scripts/check-rules.ps1
   ```

## Done when

`config --quiet` passes with the example env, the service is healthy, only Traefik publishes a port in the
main file, and no secret value is committed.
