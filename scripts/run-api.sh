#!/usr/bin/env bash
# Launch the FriendCore API with its runtime configuration.
#
# All configuration — including the JWT signing key and storage credentials —
# lives in an env file OUTSIDE the repository, default:
#
#     ~/.config/friendcore/api.env      (chmod 600)
#
# That split is deliberate. The values are secrets and the repository is not the
# right place for them, but the *shape* of the configuration is code and belongs
# in version control so it can be reviewed and reproduced. This script is that
# committed half; see deploy/api.env.example for every key it expects.
#
# Why this exists rather than a bare `dotnet run`: the app reads
# ASPNETCORE_ENVIRONMENT, ForwardedHeaders:KnownProxies, Storage:* and Jwt:*
# from the environment, and a restart without them comes up in Development with
# empty storage credentials. Most of that fails quietly — media uploads break,
# while rate limiting silently collapses every tester into one bucket.
set -euo pipefail

ENV_FILE="${FRIENDCORE_ENV_FILE:-$HOME/.config/friendcore/api.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "run-api: missing env file $ENV_FILE" >&2
  echo "          copy deploy/api.env.example and fill it in, then chmod 600" >&2
  exit 1
fi

# Refuse to run on a world-readable or group-readable env file rather than
# printing the path and continuing with a leaked secret.
PERMS=$(stat -c '%a' "$ENV_FILE")
case "$PERMS" in
  600|400) ;;
  *) echo "run-api: $ENV_FILE has mode $PERMS; tighten it with: chmod 600 $ENV_FILE" >&2; exit 1 ;;
esac

# shellcheck disable=SC1090
set -a; . "$ENV_FILE"; set +a

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# ForwardedHeaders:KnownProxies is the one that bites: if it is unset the API
# sees Caddy as the client for every request, so all testers share a rate-limit
# bucket and the admin allowlist checks the wrong address. Refuse rather than
# start a publicly-reachable service that cannot tell its users apart.
if [ -z "${ForwardedHeaders__KnownProxies:-}" ]; then
  echo "run-api: ForwardedHeaders__KnownProxies is unset — refusing to start." >&2
  echo "          set it to the address Caddy connects from (10.7.0.1 for the" >&2
  echo "          thin-client ingress), or every tester shares one rate-limit bucket." >&2
  exit 1
fi

echo "run-api: starting in ${ASPNETCORE_ENVIRONMENT:-unset} on ${ASPNETCORE_URLS:-unset}"
echo "run-api: env from $ENV_FILE"

exec dotnet run --project "$ROOT/src/FriendCore.API" --no-launch-profile