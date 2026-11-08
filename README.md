# FriendCore — API

The backend for **FriendCore**, a privacy-first social platform built around a
follow-only chronological feed, interest-based discovery, and real-time direct
messages. It is a complete, deployed REST API — not a scaffold — with
authentication, media processing, moderation, notifications, and admin tooling.

[Web client](https://github.com/Needlide/FriendCoreClient) · [Live demo](https://friend-core-client-ecb9d732c1d1.herokuapp.com/)

> The demo runs on free-tier infrastructure with a seeded set of fictional
> accounts. Credentials are available on request.

## What it is

FriendCore is a solo-built MVP of a social network that deliberately rejects
algorithmic feeds and engagement-driven UX. The home feed contains only posts
from people you follow, in strict chronological order; discovery is based on
shared interest tags rather than popularity; and the product avoids dark
patterns such as infinite scroll, autoplay, and notification bait.

This repository is the server. It exposes a versioned REST API (`/api/v1`),
two SignalR hubs for push updates, and optionally serves the built Angular SPA.

## At a glance

| | |
|---|---|
| **Stack** | .NET 10, ASP.NET Core, EF Core 10, PostgreSQL |
| **Architecture** | Modular monolith with Clean Architecture + CQRS (MediatR) |
| **API surface** | 15 controllers, JWT bearer auth, versioned routes |
| **Realtime** | SignalR — notifications and messaging hubs |
| **Media** | S3-compatible storage, ImageSharp WebP variants, background processing |
| **Moderation** | Reports, enforcement ladder, appeals, audit log, admin metrics |
| **Tests** | 522 unit · 375 integration · 8 architecture (905 total) |
| **Deployment** | Heroku preview today; Docker/Caddy/systemd files included for self-hosting |

## Feature map

| Area | What is implemented |
|---|---|
| **Authentication** | Register, login, email verification, refresh-token rotation with theft detection, logout, forgot/reset password, resend verification. Only an HMAC of the email address is stored. |
| **Profiles & social graph** | Public profiles, profile editing, follow/unfollow, block, mute. Blocks are enforced by EF Core global query filters, so blocked content disappears from every query without handler-level checks. Blocked profiles return 404 rather than revealing the block. |
| **Posts** | Text + image posts, up to 12 tags, sensitivity level, delete. Cursor-paginated follow-only home feed. |
| **Comments & likes** | Comments on post detail, delete own; idempotent like toggle with correct counts; deletion cascades to comments and likes. |
| **Discovery** | Tag-overlap matching for people and posts, tag autocomplete with debounced ranked suggestions, onboarding interest selection. No trending or global feed. |
| **Media pipeline** | Presigned upload, magic-byte validation, asynchronous processing, four WebP variants (100/300/800/1400 px), processing sweep for stuck jobs, consent-gated display of sensitive media. |
| **Messaging** | 1:1 DMs and a support channel, replies, reactions, edit/delete, per-view hide, message reports, in-thread search with context, read receipts, unread badges, date dividers, SignalR delivery. |
| **Notifications** | Exactly-once rows for follows, likes, comments, and messages; bell badge; mark-all-read; per-type mute; snooze. |
| **Moderation & admin** | Nine report categories across posts/users/messages/media; snapshot copy at report time; removal → ban → legal enforcement ladder; appeals reviewed by a different moderator; audit log with IP; metrics dashboard; IP-allowlisted admin surface. |
| **Settings** | Interests, profile, notification preferences, keyword filters, media-safety preferences, appeals view, account/session management. |

## Architecture

```
FriendCore.Domain          entities, value objects, domain events, repository contracts
      ▲
FriendCore.Application     use cases: MediatR commands/queries, validation, ErrorOr results
      ▲
FriendCore.Infrastructure  EF Core, PostgreSQL, Redis, S3, email, JWT, image processing
      ▲
FriendCore.API             controllers, auth policies, rate limits, SignalR hubs, health
```

Dependency direction is one-way and checked by architecture tests: the API knows
Application, Application knows Domain, and Infrastructure implements interfaces
defined inward. Controllers contain no business logic; command and query handlers
are the unit of work.

Patterns used throughout:

- **CQRS via MediatR** — writes and reads are separate objects with separate handlers.
- **Result pattern via ErrorOr** — expected failures are values, not exceptions.
- **Domain events via MediatR** — e.g. likes, comments, and follows emit notifications.
- **Repositories** — defined in Domain, implemented in Infrastructure; handlers never touch EF Core.
- **Soft deletes + global query filters** — deletion is a timestamp, and blocked content is filtered automatically.
- **Thin controllers** — HTTP mapping only; validation lives with the request.

## Security and privacy decisions

- **Refresh tokens in an `HttpOnly`, `SameSite=Strict` cookie**, rotated on every use. Replaying a revoked token revokes the user's entire token family.
- **Email addresses are never stored in plaintext** — only a keyed HMAC, used for lookups and duplicate detection.
- **Passwords hashed with BCrypt**; no plaintext or reversible storage.
- **Rate limiting per client IP**: auth 10 requests/min, general API 120/min, messaging a token bucket of 10 burst / 1 per 5 s.
- **Admin surface is IP-allowlisted** (`AdminAccess`) and role-gated (`Moderator` / `SeniorModerator` / `Admin`).
- **Baseline security headers** (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, HSTS in production).
- **Media safety** — flagged media is withheld until the viewer explicitly reveals it; refused consent returns `409`.
- **Blind password reset** — responses do not reveal whether an address exists.

## Testing

```bash
dotnet test tests/FriendCore.UnitTests
dotnet test tests/FriendCore.ArchitectureTests
dotnet test tests/FriendCore.IntegrationTests   # requires Docker
```

| Suite | Count | Notes |
|---|---:|---|
| Unit | 522 | Handlers, validators, domain rules, services |
| Integration | 375 | Real PostgreSQL and MinIO via Testcontainers, `WebApplicationFactory`, SignalR client |
| Architecture | 8 | Enforces layer boundaries and naming rules |

CI (`.github/workflows/ci.yml`) runs architecture + unit tests on every push and
pull request, and the full integration suite on `main`. Tests are the main safety
net for a codebase of this size — they cover auth edge cases, query filters,
cursor pagination, media states, messaging semantics, and moderation flows.

## Run locally

Prerequisites: **.NET 10 SDK**, **Docker**, and (optionally) the .NET user-secrets
tool that ships with the SDK.

```bash
# 1. Postgres, Redis, and an S3-compatible RustFS instance
docker compose -f docker-compose.dev.yml up -d

# 2. Local configuration (dev credentials for the containers are in the example)
cp .env.example .env

# 3. A real JWT signing key (at least 32 characters)
dotnet user-secrets --project src/FriendCore.API set "Jwt:SecretKey" "dev-only-signing-key-change-me-32chars"

# 4. Run — Development applies migrations on startup
dotnet run --project src/FriendCore.API --launch-profile http
```

The API listens on `http://localhost:5024`; the Angular client expects that
origin. Health is at `/health`; OpenAPI JSON is available in Development.

Optional demo data (9 fictional users, posts, tags, follows, likes, comments,
and conversations) can be loaded once with:

```bash
dotnet run --project src/FriendCore.API -- --seed-demo
```

For a local demo without a configured mail provider, set
`Email__AutoVerifyNewUsers=true` so new registrations are marked verified.

## Configuration

Configuration follows normal ASP.NET Core precedence; nested keys use `__`
(e.g. `Storage__Bucket` → `Storage:Bucket`). Secrets belong in user-secrets or
environment variables, never in the repository.

| Key | Purpose |
|---|---|
| `ConnectionStrings:Database` | PostgreSQL connection string |
| `ConnectionStrings:Redis` | Optional cache; leave empty to run without Redis |
| `Jwt:SecretKey` | Signing key (required, ≥32 chars) |
| `Storage:*` | S3 endpoint, bucket, keys, region, public base URL |
| `Email:*` | Resend API key, sender, verification base URL, auto-verify flag |
| `Database:MigrateOnStartup` | Opt-in migrations outside Development |
| `Seeding:DemoData` / `Seeding:AdminEmails` | Demo data and initial admin accounts |
| `AdminAccess:AllowedIps` / `AllowedNetworks` | Admin surface allowlist |
| `Cors:AllowedOrigins` | Explicit client origins |
| `Sentry:Dsn` | Error tracking (optional) |

`deploy/api.env.example` documents the production-shaped environment, and
`deploy/push-preview-config.sh` pushes a local env file to Heroku without
printing secret values.

## Deployment

The public preview runs the API on Heroku with:

- **PostgreSQL on Supabase** via the IPv4 session pooler (the direct host is
  IPv6-only and unreachable from Heroku);
- **Media on Supabase Storage** through the S3-compatible endpoint;
- **Redis disabled** on the single dyno (`/health` still returns 200);
- migrations and demo seeding executed as one-off commands, not on every boot —
  `Database__MigrateOnStartup=false`, `Seeding__DemoData=false`.

The repository also includes a `Caddyfile`, a systemd unit, and an environment
template for a conventional Linux host; the API can serve the built SPA from
`Spa:Path`, which keeps client and API on one origin and avoids cross-site
cookie problems.

### Operational recovery notes

- **Keep more than one admin on a long-lived instance.** Moderation is
  role-gated and there is no UI that grants roles, so losing the only `Admin`
  account leaves no in-app recovery path. Promote a replacement with SQL before
  that happens (or right after, if you still have database access):

  ```sql
  UPDATE users SET role = 'Admin' WHERE username = 'someone';
  ```

  Prefer a second admin account from the start on anything that will outlive a
  demo.
- **Migrations do not run automatically outside Development.** A deploy that
  adds a migration needs an explicit one-off
  `dotnet ef database update --startup-project src/FriendCore.API` (the preview
  uses `--migrate`) before code depends on the new column. The closed beta hit
  the stale-schema version of this twice; treat the migration step as part of
  the deploy checklist, not as something startup will fix.

## Scope and intentional gaps

Built and tested: everything in the feature map above. Deferred by design:
blogging, custom profile CSS and pages, end-to-end encrypted DMs, creator
monetisation, theme marketplace, and the separate mobile app. Post and comment
editing, global user search, account deletion, and English/Ukrainian runtime
language switching are implemented; deletion is a soft delete, and media objects
are retained after soft deletion.

These are scope decisions for this milestone rather than accidental omissions.
The web client's README links to the same feature map and scope note.

## Repository layout

```
src/
  FriendCore.API/             controllers, contracts, auth policies, hubs, health
  FriendCore.Application/     commands, queries, validators, domain-event handlers
  FriendCore.Infrastructure/  EF Core, storage, media, email, auth, caching, jobs
  FriendCore.Domain/          entities, value objects, events, repository contracts
tests/
  FriendCore.UnitTests/
  FriendCore.IntegrationTests/
  FriendCore.ArchitectureTests/
deploy/                       production templates and preview config helper
docker-compose.dev.yml        local Postgres + Redis + RustFS
```

---

Built by [Max Lytvyniuk](https://github.com/Needlide). The Angular client lives at
[Needlide/FriendCoreClient](https://github.com/Needlide/FriendCoreClient).
