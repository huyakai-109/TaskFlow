# Deployment Standards

Reference for `ship` and `devops-lead`: background, trade-offs and sources. The binding rules are in `.claude/rules/devops.md`; this file explains them. Load it when setting up a deployment, writing a CI workflow, or reviewing infrastructure — not for an ordinary release.

Sourced from official documentation (links at the end).

**Personal-scale rule:** adopt what removes real risk. Skip the rest and say you skipped it.

---

## 1. Two deployment modes — decide this first

Everything else depends on which one the project uses. `/ship setup` records it.

| | **Local** | **CI** |
|---|---|---|
| Who deploys | You, from your machine | The CI platform, on push or on a trigger |
| `/ship` executes | the deploy command (SSH, `docker compose up`, rsync, CLI) | commit → push → watch the run → approve the gate |
| Credentials live | on your machine | in CI secrets / OIDC |
| Approval gate | the skill's own approval step | the skill's step **plus** the CI environment's required reviewer |
| Good for | a single VPS, a container host you own | anything already on GitHub/GitLab with cloud targets |

Neither is better. Local is fewer moving parts; CI is reproducible and keeps credentials off your laptop.

---

## 2. Containers

Docker's own guidance, applied:

- **Multi-stage build.** The final image carries only what runs — a slimmer base than the build stage.
- **Pin the base image by digest**, not tag: `FROM alpine:3.21@sha256:...`. Tags are mutable, so a tag alone does not give a reproducible build. Keep digests current with Dependabot (`package-ecosystem: "docker"`) or `docker scout recommendations`.
- **Non-root `USER` with an explicit UID/GID** — auto-assigned IDs are not deterministic. No `sudo`.
- **`.dockerignore`** so build context and secrets stay out of layers.
- **Combine `apt-get update` with `apt-get install` in one `RUN`**, and delete `/var/lib/apt/lists/*` in that same step — a separate `update` layer serves stale package lists from cache.
- Secrets passed as build args **persist in image layers**. Use build secrets or runtime env instead.

## 3. Rollback on a single host

**Compose has no built-in rollback.** Plan for that rather than discovering it during an incident.

The workable approach on one host: **tag every deployed image with an immutable identifier** (commit SHA or date, never only `latest`), keep the previous tag available, and roll back by pointing `image:` at it and running `up` again. That command is what `/ship setup` records as the rollback command.

`docker compose up` stops and recreates containers, so this path has a short downtime window. For a personal project that is usually fine — say so rather than implying zero-downtime.

If genuinely zero-downtime is needed: single-node Swarm gives `docker service update --rollback`, start-first ordering, and `--update-failure-action=rollback`. That is more machinery than most personal projects should take on — treat it as a deliberate decision, not a default.

## 4. CI/CD

- **Pin third-party actions to a full commit SHA**, not a tag.
- **Minimal `permissions:`.** Default to `contents: read`; add `id-token: write` only in the job that needs an OIDC token. It grants no write access to resources by itself.
- **Avoid `pull_request_target`**, and never check out untrusted code under it. `workflow_run` gives better privilege separation.
- Reusable workflows that request OIDC tokens need `id-token: write` set explicitly at the **caller** level.
- Keep the pipeline simple for a personal project: build → test → (on the main branch) deploy. Release branching and promotion pipelines are not warranted.

## 5. Approval gates in CI

A CI platform's **environment** is the right place for the deployment gate — it is enforced by the platform, not by convention:

- **Required reviewers** — one approval is enough; "prevent self-review" exists but is pointless for a solo project.
- **Deployment branches/tags** — restrict which refs may deploy.
- **Scoped secrets** — only jobs using that environment can read them, and only after the rules pass.
- **Wait timer** — a delay before the job proceeds.

Note: on GitHub, environments on **private** repositories require a paid plan. On a Free private repo the rules and secrets are ignored — so do not rely on the gate there; keep approval in `/ship` instead.

## 6. Cloud credentials

**Prefer OIDC over stored long-lived keys** where the provider supports it (AWS, Azure, GCP all do). It issues short-lived, narrowly scoped tokens per run.

**Lock the trust policy down.** At least one condition on the `sub` claim, restricting which repository and branch may assume the role — otherwise an untrusted repository can request tokens for your resources.

Where OIDC is not available, store credentials in the CI platform's secret store scoped to the deployment environment. Never in the repository, the image, or `CLAUDE.md`.

## 7. Pre-release security signal

The build usually already tells you. Capture it rather than running a separate audit:

| Stack | Signal |
|---|---|
| .NET | `NU1901`–`NU1904` restore warnings |
| Node | `npm audit` |
| Any | the platform's dependency scanning |

A HIGH or critical advisory in a dependency is a **release finding** — decide knowingly, do not let it scroll past in the build log.

---

## Sources

- [Docker — Dockerfile best practices](https://docs.docker.com/build/building/best-practices/)
- [Docker — Use Compose in production](https://docs.docker.com/compose/production/)
- [Docker — `docker service rollback`](https://docs.docker.com/reference/cli/docker/service/rollback/)
- [GitHub — Manage environments](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments)
- [GitHub — Secure use reference](https://docs.github.com/en/actions/reference/security/secure-use)
- [GitHub — Configuring OpenID Connect in cloud providers](https://docs.github.com/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-cloud-providers)

Verified October 2026. Platform behaviour changes — re-check before relying on a detail.
