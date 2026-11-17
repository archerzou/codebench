# CI/CD with GitHub Actions

GitHub Actions tests every pull request, builds and pushes versioned images to GitHub Container Registry (GHCR) on every merge to `main`, and your machine deploys by pulling those images. No paid hosting is involved.

## Pipeline at a glance

| Workflow | Trigger | Jobs | Output |
| --- | --- | --- | --- |
| `ci.yml` | Pull request, push to `main` | .NET build + unit/integration/architecture tests; frontend lint, test, build | Pass/fail status checks |
| `images.yml` | Push to `main`, tags `v*` | Build and push `api`, `worker`, `web`, `marketing` images | `ghcr.io/<owner>/codebench-*:<sha>` and `:latest` |
| `sandbox-images.yml` | Changes under `src/Sandbox.Runner`, `sandbox-images`, `problems` | Build sandbox images, run the hostile-submission suite against them, push | `codebench-sandbox-*` images |
| `e2e.yml` | Nightly + manual | Compose up the full stack on the runner, Playwright smoke test | Test report artifact |
| Deploy | Manual on your machine (or self-hosted runner) | `compose pull && up -d` | Running stack |

**Cost:** public repositories get free Actions minutes and free GHCR storage. Private repositories get a monthly free allowance of minutes and package storage on the Free plan; check GitHub's current limits and keep image layers small.

## .github/workflows/ci.yml

```yaml
name: ci
on:
  pull_request:
  push:
    branches: [main]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  dotnet:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore CodeBench.slnx
      - run: dotnet build CodeBench.slnx -c Release --no-restore   # TreatWarningsAsErrors is on in the template
      - name: Test (unit, integration via Testcontainers, architecture)
        run: dotnet test CodeBench.slnx -c Release --no-build --logger trx --results-directory TestResults
      - uses: actions/upload-artifact@v4
        if: always()
        with: { name: dotnet-test-results, path: TestResults }

  frontend:
    runs-on: ubuntu-latest
    strategy:
      matrix: { app: [web, marketing] }
    defaults:
      run: { working-directory: apps/${{ matrix.app }} }
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 24
          cache: npm
          cache-dependency-path: apps/${{ matrix.app }}/package-lock.json
      - run: npm ci
      - run: npm run lint --if-present
      - run: npm test --if-present -- --run
      - run: npm run build
```

GitHub's `ubuntu-latest` runners include Docker, so Testcontainers works with no extra setup.

## .github/workflows/images.yml

```yaml
name: images
on:
  push:
    branches: [main]
    tags: ['v*']

permissions:
  contents: read
  packages: write

jobs:
  build-push:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        include:
          - { name: api,       context: .,              file: src/Web.Api/Dockerfile }
          - { name: worker,    context: .,              file: src/Worker/Dockerfile }
          - { name: web,       context: apps/web,       file: apps/web/Dockerfile }
          - { name: marketing, context: apps/marketing, file: apps/marketing/Dockerfile }
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v3
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - id: meta
        uses: docker/metadata-action@v5
        with:
          images: ghcr.io/${{ github.repository_owner }}/codebench-${{ matrix.name }}
          tags: |
            type=sha,format=long
            type=ref,event=tag
            type=raw,value=latest,enable={{is_default_branch}}
      - uses: docker/build-push-action@v6
        with:
          context: ${{ matrix.context }}
          file: ${{ matrix.file }}
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha,scope=${{ matrix.name }}
          cache-to: type=gha,mode=max,scope=${{ matrix.name }}
```

Gate this workflow on CI by making it run only after `ci` succeeds (`on: workflow_run`) or by protecting `main` so nothing merges without green checks. Note that `github.repository_owner` must be lowercase for GHCR; if your owner name has capitals, lowercase it in a step first.

## .github/workflows/sandbox-images.yml

```yaml
name: sandbox-images
on:
  push:
    branches: [main]
    paths: ['src/Sandbox.Runner/**', 'sandbox-images/**', 'problems/**']
  pull_request:
    paths: ['src/Sandbox.Runner/**', 'sandbox-images/**', 'problems/**']

permissions: { contents: read, packages: write }

jobs:
  build-test-push:
    runs-on: ubuntu-latest
    strategy:
      matrix: { kind: [algorithms, database] }
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v3
      - name: Build (load locally for testing)
        uses: docker/build-push-action@v6
        with:
          context: .
          file: sandbox-images/${{ matrix.kind }}.Dockerfile
          load: true
          tags: codebench-sandbox-${{ matrix.kind }}:test
      - name: Hostile submission suite
        run: ./scripts/test-sandbox.sh codebench-sandbox-${{ matrix.kind }}:test tests/hostile-submissions
      - name: Push
        if: github.event_name == 'push'
        run: |
          echo "${{ secrets.GITHUB_TOKEN }}" | docker login ghcr.io -u ${{ github.actor }} --password-stdin
          IMG=ghcr.io/${{ github.repository_owner }}/codebench-sandbox-${{ matrix.kind }}
          docker tag codebench-sandbox-${{ matrix.kind }}:test $IMG:${{ github.sha }}
          docker tag codebench-sandbox-${{ matrix.kind }}:test $IMG:latest
          docker push --all-tags $IMG
```

`scripts/test-sandbox.sh` runs each file in `tests/hostile-submissions/` with the same `docker run` flags the worker uses (`--network none --read-only --memory ... --pids-limit ...`) and compares the verdict with an `expected.json`. Example cases you write yourself:

| Case | Expected verdict |
| --- | --- |
| Opens an `HttpClient` to an external host | Tests fail with a network error; never Accepted |
| `while (true) { }` | Timeout |
| Allocates 2 GB | Memory limit: killed, BudgetExceeded or InternalError |
| Spawns processes in a loop | Pids limit reached; no host impact |
| Writes to `/etc` or `/opt/problems` | Permission denied |
| Prints a fake success JSON | Ignored; real result parsed from the nonce block |
| Correct O(n) Two Sum | Accepted |

## .github/workflows/e2e.yml

```yaml
name: e2e
on:
  schedule: [{ cron: '0 15 * * *' }]   # 04:00 NZDT
  workflow_dispatch:

jobs:
  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Write CI env
        run: |
          cp deploy/.env.ci deploy/.env   # test-only values, committed without secrets
          echo "CLERK_PUBLISHABLE_KEY=${{ secrets.CLERK_TEST_PUBLISHABLE_KEY }}" >> deploy/.env
      - name: Start stack
        run: docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build --wait
      - uses: actions/setup-node@v4
        with: { node-version: 24 }
      - run: npx playwright install --with-deps chromium
      - run: npx playwright test --config e2e/playwright.config.ts
      - if: failure()
        run: docker compose -f deploy/docker-compose.yml logs > compose-logs.txt
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: e2e-report
          path: |
            playwright-report
            compose-logs.txt
```

For signed-in flows in E2E, use Clerk's testing tokens for development instances rather than real credentials; keep the first smoke test anonymous (open a free problem, press Run, see a result).

## Deploying to your own machine

GitHub-hosted runners can't reach your laptop, so pick one of these:

| Option | How | Pros | Cons |
| --- | --- | --- | --- |
| **A. Pull script (start here)** | `./deploy/update.sh` after a merge | Simple, no inbound access, fully under your control | Manual step |
| B. Self-hosted runner | Register a GitHub runner on your machine; a `deploy` job runs `compose pull && up -d` after `images` succeeds | Automatic | The runner executes workflow code on your machine: private repo only, never for PRs from forks |
| C. Watchtower | Container that polls GHCR and restarts services when `:latest` changes | Hands-off | Less control over timing; pin it to your own images only |

`deploy/update.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
export TAG="${1:-latest}"              # pass a commit SHA to roll back or pin
docker compose --env-file .env pull
docker compose --env-file .env run --rm api --migrate
docker compose --env-file .env up -d --remove-orphans
docker image prune -f
```

Rollback = `./deploy/update.sh <previous-sha>`, which is why images are tagged by commit SHA, not only `latest`.

For private GHCR images, log in once on your machine: `echo $PAT | docker login ghcr.io -u <you> --password-stdin` with a fine-grained token that only has `read:packages`.

## Secrets and repository settings

| Name | Where | Used by |
| --- | --- | --- |
| `GITHUB_TOKEN` | Automatic | Pushing images to GHCR |
| `CLERK_TEST_PUBLISHABLE_KEY`, `CLERK_TEST_SECRET_KEY` | Actions secrets | E2E against a Clerk dev/test instance |
| `deploy/.env` | Your machine only, git-ignored | Real local secrets |

- [ ] Protect `main`: require `ci` checks, require PR review (you can self-review, but the gate keeps habits honest).
- [ ] Enable Dependabot for `nuget`, `npm`, `docker` and `github-actions`.
- [ ] Enable secret scanning and push protection.
- [ ] Pin third-party actions to versions (or commit SHAs for stricter supply-chain hygiene).
- [ ] Optional: after `images`, run REA's bundle and assembly audits (see the REA guide tab) and upload the JSON as an artifact.
