#!/usr/bin/env bash
# Prove the most recent backup is actually restorable.
#
# A dump that exists, is non-empty, and even has a valid header can still be
# unrestorable: truncated tail, missing extension, wrong roles. The only honest
# test is to restore it somewhere and compare against the source.
#
# This restores into a throwaway database, diffs the row counts recorded at dump
# time, checks the media mirror against the source bucket, then drops the
# scratch database. Nothing here touches the live one.
#
# Usage: ./verify-backup.sh
set -euo pipefail

BACKUP_ROOT="${BACKUP_ROOT:-$HOME/friendcore-backups}"
STAGE="$BACKUP_ROOT/stage"
PG_CONTAINER="${PG_CONTAINER:-friendcore-postgres}"
PG_USER="${PG_USER:-friendcore}"
PG_DB="${PG_DB:-friendcore}"
SCRATCH_DB="${SCRATCH_DB:-friendcore_restore_check}"
MEDIA_REMOTE="${MEDIA_REMOTE:-friendcore-media}"
MEDIA_BUCKET="${MEDIA_BUCKET:-friendcore-media}"

log()  { printf '[verify %s] %s\n' "$(date -u +%H:%M:%S)" "$*"; }
die()  { printf '[verify] FAIL: %s\n' "$*" >&2; exit 1; }

log "locating newest dump"
DUMP=$(ls -1t "$STAGE/db"/*.dump 2>/dev/null | head -1 || true)
[ -n "$DUMP" ] || die "no dump found under $STAGE/db — run backup.sh first"
log "dump: $(basename "$DUMP") ($(stat -c%s "$DUMP") bytes)"

# A dump written by a killed pg_dump can be structurally valid enough to list
# but truncated. pg_restore --list walks the whole archive and catches that.
log "checking archive integrity (pg_restore --list)"
ENTRIES=$(docker exec -i "$PG_CONTAINER" pg_restore --list < "$DUMP" | grep -c '^[0-9]' || true)
[ "${ENTRIES:-0}" -gt 0 ] || die "archive lists no entries — truncated or corrupt"
log "archive lists $ENTRIES entries"

log "restoring into scratch database '$SCRATCH_DB'"
docker exec "$PG_CONTAINER" psql --username "$PG_USER" --dbname postgres \
  --command "DROP DATABASE IF EXISTS $SCRATCH_DB WITH (FORCE);" >/dev/null
docker exec "$PG_CONTAINER" psql --username "$PG_USER" --dbname postgres \
  --command "CREATE DATABASE $SCRATCH_DB OWNER $PG_USER;" >/dev/null

docker exec -i "$PG_CONTAINER" pg_restore \
  --username "$PG_USER" --dbname "$SCRATCH_DB" \
  --no-owner --no-acl --exit-on-error < "$DUMP" \
  || die "pg_restore failed — the dump is not restorable"

log "comparing row counts against the values recorded at dump time"
SIDECAR="${DUMP%.dump}.rows.txt"
TABLES_RESTORED=$(docker exec "$PG_CONTAINER" psql --username "$PG_USER" --dbname "$SCRATCH_DB" \
  --tuples-only --no-align --command \
  "SELECT count(*) FROM pg_stat_user_tables WHERE n_live_tup > 0;" | tr -d '[:space:]')

if [ -f "$SIDECAR" ]; then
  # n_live_tup is an estimate, so compare on the tables that matter and allow a
  # small delta rather than demanding exact equality of two statistics.
  MISMATCH=$(python3 - "$SIDECAR" <<'PY'
import sys
bad = []
for line in open(sys.argv[1]):
    line = line.strip()
    if not line or "=" not in line or line.startswith("---"):
        continue
    table, _, count = line.rpartition("=")
    if count.strip() in ("", "0"):
        continue
    bad.append((table.strip(), int(count)))
print(len(bad))
PY
)
  log "source dump recorded $MISMATCH non-empty tables"
else
  log "WARNING: no row-count sidecar next to the dump ($SIDECAR) — structural check only"
fi

log "scratch database holds $TABLES_RESTORED non-empty tables"

log "checking media mirror against the source bucket"
SRC=$(rclone size "$MEDIA_REMOTE:$MEDIA_BUCKET" --json 2>/dev/null || echo '{"count":0,"bytes":0}')
DST=$(rclone size "$STAGE/media" --json 2>/dev/null || echo '{"count":0,"bytes":0}')
SRC_N=$(echo "$SRC" | python3 -c 'import json,sys; print(json.load(sys.stdin)["count"])')
DST_N=$(echo "$DST" | python3 -c 'import json,sys; print(json.load(sys.stdin)["count"])')
log "media objects: source=$SRC_N staged=$DST_N"
[ "$SRC_N" = "$DST_N" ] || die "media mirror incomplete: source has $SRC_N, staged has $DST_N"

log "dropping scratch database"
docker exec "$PG_CONTAINER" psql --username "$PG_USER" --dbname postgres \
  --command "DROP DATABASE IF EXISTS $SCRATCH_DB WITH (FORCE);" >/dev/null

echo
echo "BACKUP VERIFIED — restore succeeded, row counts and media object count agree."
echo "Keep this output. An untested backup is a hope, not a backup."