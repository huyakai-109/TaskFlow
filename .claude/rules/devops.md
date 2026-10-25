---
paths:
  - "BE/TaskFlow.Docker/**"
  - "**/Dockerfile*"
  - "**/*.dockerfile"
  - "**/.dockerignore"
  - "**/docker-compose*.{yml,yaml}"
  - "**/compose*.{yml,yaml}"
  - "**/.github/workflows/**"
  - "**/.gitlab-ci.yml"
  - "**/azure-pipelines*.yml"
  - "**/.env.example"
  - "**/deploy/**"
  - "**/*.{nginx,conf}"
  - "**/Caddyfile"
---

# DevOps Rules

Containers, CI/CD, environment configuration, deployment and operations. Background, sources and the CI hardening detail: `.claude/references/deployment-standards.md`. The release procedure itself is the `ship` skill.

**Writing an infrastructure file is allowed when the task needs it. Executing against a real environment is not** — deploy, push, publish, registry login, remote shell, migration against a server, provisioning: each needs explicit approval (`workflow.md` §3).

## 1. Scale to the project

- **[M]** One deployment target by default. No staging/production split, environment selector, release branches or promotion pipeline unless the user asks.
- **[M]** No Kubernetes, Helm, Terraform, service mesh, message broker or second datastore unless the project already has it or the user asks. A Dockerfile, a compose file, one CI workflow and a reverse proxy with TLS cover most projects.
- **[M]** Inspect before proposing: existing Dockerfile/compose, CI platform, how configuration is supplied, where it deploys today, what the build produces. Never assume a cloud provider or CI platform.

## 2. Configuration and secrets

- **[M]** Configuration comes from the environment (env vars, `appsettings.{Environment}.json` without secrets); secrets from the platform's secret store or env vars set on the host.
- **[M]** Secrets never appear in the repository, an image layer, a build argument, CI logs, `CLAUDE.md` or your reports. Report a missing secret by **name and purpose** only.
- **[M]** `.env.example` lists every key the app reads, with empty or dummy values; `.env` is git-ignored.
- **[M]** A new setting has a safe default or fails fast at startup with a clear message, and is listed in `.env.example` and in the handoff as a deployment requirement.

## 3. Containers

- **[M]** Multi-stage build; the final stage carries only the runtime (the `mcr.microsoft.com/dotnet/aspnet` image for `net10.0`; static files served by a web server for an SPA).
- **[M]** Base images pinned to an exact patch version (`aspnet:10.0.<patch>`) or, better, a digest; never `latest` or a floating `10.0`.
- **[M]** Run as a non-root user; `.dockerignore` excludes `.git`, `bin/`, `obj/`, `node_modules/`, `.env*` (but not `.env.example`).
- **[M]** A health check that reflects readiness (dependencies reachable), not only "process is up".
- **[R]** Restore/install layers before copying source so dependency layers cache.

## 4. CI

- **[M]** On every push / PR: restore, build, test — failing tests fail the pipeline. Never mark tests `continue-on-error` to get green.
- **[M]** Third-party actions pinned to a full commit SHA; `permissions:` minimal (`contents: read` by default); no `pull_request_target` checking out untrusted code.
- **[M]** A deploy job (CI mode only) runs from the main branch, behind the platform's environment approval where available, using scoped secrets or OIDC — never long-lived keys in the repository.

## 5. Deploy and rollback

- **[M]** Every release is identifiable: an immutable image tag (commit SHA or date) or artifact version. The previous one stays available.
- **[M]** The rollback command is known **before** the first deploy and recorded in `CLAUDE.md` → Deployment.
- **[M]** Schema changes are compatible with the version that is running during the deploy (`database.md` §5). Backup and restore commands are recorded in `CLAUDE.md` → Deployment; back up before any risky or destructive migration and confirm the backup exists.
- **[M]** The first release has no previous version: its rollback is "stop the app, restore the pre-release backup" — say so in the approval block.
- **[M]** A deploy is verified on the target: health check, the recorded smoke checks, error logs since deploy.

## 6. Observability — the minimum that answers "is it up, and why did it fail"

- **[M]** Health endpoint(s) wired to the container/host health check (ASP.NET Core `AddHealthChecks` + a DB check where there is a database).
- **[M]** Structured logs to stdout/stderr with a request/correlation id; no secrets or personal data; log volume and disk use bounded (rotation or retention).
- **[R]** Unhandled errors visible somewhere you will look (log query, error tracker, alert on health failure). Metrics and tracing (OpenTelemetry) only when there is a question they answer.

## 7. Verification

| Change | Evidence |
|---|---|
| Dockerfile | `docker build` → image tag and size; `docker run` + health check output |
| Compose | `docker compose config` (valid); `up` locally if feasible |
| CI workflow | Syntax validated (`actionlint` if available) or run observed; otherwise `NOT VERIFIED — not executed` |
| Deployment config | `BLOCKED` unless the user approved running it against the target |

A file that has been written but never executed is `NOT VERIFIED`. Say so.
