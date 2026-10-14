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

# Wait for the dependencies before starting.
#
# Postgres, Redis and the object store are containers, and this is a *user* unit: it
# cannot declare Requires=docker.service, because a user unit cannot depend on a
# system unit. So the user manager starts this the moment the session comes up,
# which can be before Docker has finished starting the containers.
#
# StorageBucketInitializer then does an unhandled S3 call against a port nothing is
# listening on. Connection refused aborts the process (SIGABRT), systemd restarts it,
# it races again, and the result is a crash loop that looks identical to "the website
# doesn't come up" — with no indication that the cause was startup ordering.
#
# Blocking here turns that failure into a few seconds of waiting. The timeout is
# deliberately finite: if a dependency is genuinely down, we want to exit and let
# Restart=always retry, not hang forever holding a wedged process.
DEPENDENCY_TIMEOUT="${DEPENDENCY_TIMEOUT:-120}"

wait_for() {
  local host="$1" port="$2" name="$3" waited=0
  while ! (exec 3<>"/dev/tcp/$host/$port") 2>/dev/null; do
    exec 3<&- 2>/dev/null || true
    if [ "$waited" -ge "$DEPENDENCY_TIMEOUT" ]; then
      echo "run-api: $name ($host:$port) still unreachable after ${DEPENDENCY_TIMEOUT}s" >&2
      echo "          is docker running?  sudo systemctl start docker" >&2
      echo "          are the containers up? docker compose -f docker-compose.dev.yml up -d --wait" >&2
      return 1
    fi
    [ $((waited % 10)) -eq 0 ] && echo "run-api: waiting for $name ($host:$port)… ${waited}s"
    sleep 2; waited=$((waited + 2))
  done
  echo "run-api: $name ready after ${waited}s"
}

wait_for localhost 5432 postgres || exit 1
wait_for localhost 6379 redis    || exit 1
wait_for localhost 9000 storage  || exit 1

# Run the built assembly, not `dotnet run`.
#
# `dotnet run` is a build-and-launch wrapper: it forks the real server as a child
# and exits when that child exits. Under systemd that means the supervisor is
# watching the wrapper, and killing the server makes the wrapper report success —
# so Restart=on-failure never fires and the API stays down. Running the assembly
# directly makes the server itself PID 1 of the unit, so signals, exit codes and
# restarts behave.
#
# Rebuild after changing code:  systemctl --user restart friendcore-api
# (or set FRIENDCORE_BUILD=1 to build on every start, which is slower but handy
# while iterating.)
API_PROJECT="$ROOT/src/FriendCore.API"
API_DLL="$API_PROJECT/bin/Release/net10.0/FriendCore.API.dll"

if [ "${FRIENDCORE_BUILD:-0}" = "1" ] || [ ! -f "$API_DLL" ]; then
  echo "run-api: building Release (this takes a minute)"
  dotnet build "$API_PROJECT" -c Release --nologo
fi

if [ ! -f "$API_DLL" ]; then
  echo "run-api: $API_DLL still missing after build — cannot start" >&2
  exit 1
fi

echo "run-api: exec $API_DLL"
# contentRoot matches what `dotnet run` used to set, so relative paths in config
# and Serilog resolve the same way they did before.
exec dotnet "$API_DLL" --contentRoot "$API_PROJECT"