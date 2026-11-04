#!/usr/bin/env bash
# Push a local KEY=VALUE env file to a Heroku app in one shot.
#
#   deploy/push-preview-config.sh [env-file] [app]
#
# Defaults: ~/.config/friendcore/preview.env and friend-core (override the app
# with APP=... ). The file is the same shape as deploy/api.env.example.
#
# Only key names are printed, never values, so this is safe to run in a shared
# terminal. It refuses to push while any value still contains CHANGE_ME — that
# prevents a half-filled template from replacing working config with placeholders.

set -euo pipefail

ENV_FILE="${1:-$HOME/.config/friendcore/preview.env}"
APP="${2:-${APP:-friend-core}}"
HEROKU_API="${HEROKU_API:-https://friend-core-f2ac29d531f0.herokuapp.com}"

[[ -r "$ENV_FILE" ]] || { echo "error: cannot read $ENV_FILE" >&2; exit 1; }
mode="$(stat -c %a "$ENV_FILE")"
[[ "$mode" == "600" ]] || echo "warning: $ENV_FILE is mode $mode, expected 600" >&2

pairs=()
keys=()
lineno=0
while IFS= read -r line || [[ -n "$line" ]]; do
    lineno=$((lineno + 1))
    line="${line%$'\r'}"
    [[ -z "${line//[[:space:]]/}" || "$line" == \#* ]] && continue
    if [[ "$line" != *=* ]]; then
        echo "error: $ENV_FILE:$lineno is not KEY=VALUE" >&2
        exit 1
    fi
    key="${line%%=*}"
    value="${line#*=}"
    if [[ ! "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]]; then
        echo "error: $ENV_FILE:$lineno: '$key' is not a valid config key" >&2
        exit 1
    fi
    # Empty values are meaningful here (ConnectionStrings__Redis= disables Redis),
    # so only the placeholder is rejected, not emptiness.
    if [[ "$value" == *CHANGE_ME* ]]; then
        echo "error: $ENV_FILE:$lineno: $key still contains CHANGE_ME" >&2
        exit 1
    fi
    pairs+=("$key=$value")
    keys+=("$key")
done <"$ENV_FILE"

(( ${#pairs[@]} )) || { echo "error: $ENV_FILE defines nothing" >&2; exit 1; }

echo "Pushing ${#pairs[@]} keys to $APP: ${keys[*]}"
HEROKU_DISABLE_AUTOUPDATE=1 heroku config:set -a "$APP" "${pairs[@]}" >/dev/null

# config:set restarts the dyno. Give it time to boot, then prove the app is up.
echo -n "Waiting for $HEROKU_API/health "
for _ in $(seq 1 30); do
    sleep 3
    echo -n "."
    status="$(curl -s -o /dev/null -w '%{http_code}' "$HEROKU_API/health" || true)"
    if [[ "$status" == "200" ]]; then
        echo " ok"
        exit 0
    fi
done

echo " failed (last status ${status:-none})" >&2
echo "Check: heroku logs -a $APP -n 100" >&2
exit 1
