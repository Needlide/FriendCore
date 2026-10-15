#!/usr/bin/env bash
# Flip a user's email-verification state, by username, from the server shell.
#
#   ./scripts/set-verified.sh <username> on|off
#   ./scripts/set-verified.sh --list
#
# Why this exists rather than a config flag or a self-service toggle:
#
#   * A config flag (RequireVerificationForPosting) is global. Flipping it makes EVERYONE
#     blocked or EVERYONE unblocked, so you cannot compare the two experiences in one
#     session.
#   * Letting users flip their own status destroys what verification means — the server
#     has no evidence, so the gate becomes a UI toggle anyone bypasses with one request.
#
# This is an operator tool: it needs shell access to the host, writes directly to the
# column, and is deliberately outside the domain's rules. User.MarkEmailVerified refuses
# to unverify and rejects an already-verified account; that one-way invariant protects the
# application's own transitions, not test fixtures. Here we want exactly that freedom.
#
# It is NOT a production capability. There is no endpoint behind it, and nothing a browser
# can reach. If you ever expose verification to users, do it with a real email round-trip.
set -euo pipefail

CONTAINER="${PG_CONTAINER:-friendcore-postgres}"
DB="${PG_DB:-friendcore}"

usage() {
  echo "usage: $(basename "$0") <username> on|off" >&2
  echo "       $(basename "$0") --list" >&2
  exit 2
}

# Run a query inside the container.
# No `-i`: every query runs with -c and supplies its own statement, and leaving
# stdin attached makes docker exec hold the pipe open (a hang, not an error).
sql() { docker exec "$CONTAINER" psql --username friendcore --dbname "$DB" --tuples-only --no-align -c "$1"; }

case "${1:-}" in
  --list)
    printf '%-22s %-10s %s\n' USERNAME VERIFIED CREATED
    # Built as a single field in SQL: psql's unaligned separator is "|", not a space,
    # so selecting three columns would hand `read` one line containing pipes.
    sql "SELECT username_slug || ' ' || is_verified || ' ' || created_at
           FROM users ORDER BY created_at;" \
      | while read -r u v c; do printf '%-22s %-10s %s\n' "$u" "$v" "$c"; done
    exit 0
    ;;
esac

USERNAME="${1:-}"
ACTION="${2:-}"
[ -n "$USERNAME" ] && [ -n "$ACTION" ] || usage
case "$ACTION" in
  on|off) ;;
  *) usage ;;
esac

# Reject anything that is not a plain username. This value is interpolated into SQL
# below, so it is validated rather than escaped — a slug cannot contain quotes,
# semicolons or whitespace, so anything else is a typo or an attempt.
if ! printf '%s' "$USERNAME" | grep -qE '^[A-Za-z0-9_.-]{1,30}$'; then
  echo "not a valid username: $USERNAME" >&2
  exit 2
fi

# on/off is our vocabulary; PostgreSQL wants true/false. Interpolated into SQL below,
# but only ever from this fixed pair — never from user input.
case "$ACTION" in
  on) WANT=true ;;
  off) WANT=false ;;
esac
CURRENT=$(sql "SELECT is_verified FROM users WHERE username_slug = '$USERNAME';")

if [ -z "$CURRENT" ]; then
  echo "no such user: $USERNAME" >&2
  echo "run with --list to see who exists" >&2
  exit 1
fi

# The labels describe the world; $WANT is the SQL spelling. Keep them separate or the
# script cheerfully announces "now unverified" after verifying someone.
if [ "$ACTION" = "on" ]; then STATE="verified"; else STATE="unverified"; fi

if [ "$CURRENT" = "${WANT:0:1}" ]; then
  echo "$USERNAME is already $STATE — nothing to do"
  exit 0
fi

sql "UPDATE users
        SET is_verified = ${WANT},
            updated_at  = now()
      WHERE username_slug = '$USERNAME';" >/dev/null

# Re-read rather than trusting the UPDATE: this is the line someone will trust later.
NOW=$(sql "SELECT is_verified FROM users WHERE username_slug = '$USERNAME';")
if [ "$NOW" != "${WANT:0:1}" ]; then
  echo "FAILED: $USERNAME still reads '$NOW', expected '$WANT'" >&2
  exit 1
fi

if [ "$ACTION" = "on" ]; then
  echo "$USERNAME is now verified — can post."
else
  echo "$USERNAME is now unverified — posting is blocked; browse, comment, like, follow and DM still work."
fi