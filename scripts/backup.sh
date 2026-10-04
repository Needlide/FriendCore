#!/usr/bin/env bash
# FriendCore backup — Postgres logical dump + media mirror, staged locally then
# pushed off-machine.
#
# Usage:
#   ./backup.sh                 # full backup
#   ./backup.sh --db-only
#   ./backup.sh --media-only
#
# Credentials never live here. Postgres creds come from the compose file; the
# media endpoint comes from the environment; the off-machine destination is an
# rclone remote in ~/.config/rclone/rclone.conf.
#
# Why this shape:
#   * pg_dump -Fc (custom format) rather than plain SQL: compressed, and
#     pg_restore can pull a single table back without rewriting the whole dump.
#   * Media is mirrored at the OBJECT level through rclone/S3, never by tarring
#     the Docker volume. The volume holds RustFS's internal layout, which is
#     version-coupled and would not move to R2 later. Objects will.
#   * Local staging first, so a restore does not depend on the remote being up.
#   * The remote is an rclone remote, so swapping the thin client's SFTP remote
#     for an R2 S3 remote later is a config change, not a code change.
set -euo pipefail

BACKUP_ROOT="${BACKUP_ROOT:-$HOME/friendcore-backups}"
STAGE="$BACKUP_ROOT/stage"
KEEP_DAILY="${KEEP_DAILY:-7}"
KEEP_WEEKLY="${KEEP_WEEKLY:-4}"

PG_CONTAINER="${PG_CONTAINER:-friendcore-postgres}"
PG_USER="${PG_USER:-friendcore}"
PG_DB="${PG_DB:-friendcore}"

OFFSITE_HOST="${OFFSITE_HOST:-needlide@192.168.0.112}"
OFFSITE_PATH="${OFFSITE_PATH:-friendcore-backups}"
MIN_FREE_MB="${MIN_FREE_MB:-2000}"
REMOTE_KEEP_DAYS="${REMOTE_KEEP_DAYS:-35}"

MEDIA_REMOTE="${MEDIA_REMOTE:-friendcore-media}"   # rclone remote, S3-compatible
MEDIA_BUCKET="${MEDIA_BUCKET:-friendcore-media}"

DO_DB=1; DO_MEDIA=1
case "${1:-}" in
  --db-only) DO_MEDIA=0 ;;
  --media-only) DO_DB=0 ;;
  "") ;;
  *) echo "unknown option: $1" >&2; exit 2 ;;
esac

log() { printf '[backup %s] %s\n' "$(date -u +%H:%M:%S)" "$*"; }
die() { printf '[backup] FATAL: %s\n' "$*" >&2; exit 1; }

need() { command -v "$1" >/dev/null 2>&1 || die "missing required tool: $1"; }
need docker; need rclone

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DAY="$(date -u +%Y%m%d)"

mkdir -p "$STAGE/db" "$STAGE/media"
STARTED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# ---------------------------------------------------------------- database ---
if [ "$DO_DB" -eq 1 ]; then
  log "dumping postgres ($PG_DB) -> $STAGE/db/friendcore-$STAMP.dump"
  docker exec "$PG_CONTAINER" pg_restore --version >/dev/null 2>&1 || true
  docker exec "$PG_CONTAINER" pg_dump \
    --username "$PG_USER" --dbname "$PG_DB" \
    --format custom --compress 6 \
    --no-owner --no-acl \
    > "$STAGE/db/friendcore-$STAMP.dump" \
    || die "pg_dump failed"

  # An empty dump is the classic silent failure: pg_dump exits 0 against an empty
  # or wrong database, and you only discover it during a restore.
  SIZE=$(stat -c%s "$STAGE/db/friendcore-$STAMP.dump")
  [ "$SIZE" -gt 1024 ] || die "dump is only $SIZE bytes — refusing to treat that as a backup"
  log "dump is $((SIZE / 1024)) KiB"

  # Row counts, so verify.sh can prove the restore matches the source rather than
  # merely proving pg_restore exited 0.
  {
    echo "--- table row counts at $STAMP ---"
    docker exec "$PG_CONTAINER" psql --username "$PG_USER" --dbname "$PG_DB" --tuples-only \
      --no-align --command \
      "SELECT relname || '=' || n_live_tup FROM pg_stat_user_tables ORDER BY relname;"
  } > "$STAGE/db/friendcore-$STAMP.rows.txt" 2>/dev/null || true
fi

# ------------------------------------------------------------------- media ---
if [ "$DO_MEDIA" -eq 1 ]; then
  log "mirroring media bucket s3://$MEDIA_BUCKET -> $STAGE/media"
  # --transfers 4 keeps the Pi's uplink and the laptop's disk from thrashing;
  # --checksum makes rclone verify object integrity rather than trusting size+mtime.
  rclone sync "$MEDIA_REMOTE:$MEDIA_BUCKET" "$STAGE/media" \
    --transfers 4 --checksum --create-empty-src-dirs \
    || die "media mirror failed"

  OBJECTS=$(rclone size "$MEDIA_REMOTE:$MEDIA_BUCKET" --json 2>/dev/null \
            | python3 -c 'import json,sys; print(json.load(sys.stdin)["count"])' 2>/dev/null || echo "?")
  BYTES=$(rclone size "$MEDIA_REMOTE:$MEDIA_BUCKET" --json 2>/dev/null \
            | python3 -c 'import json,sys; print(json.load(sys.stdin)["bytes"])' 2>/dev/null || echo "?")
  log "media: $OBJECTS objects, $((BYTES / 1024)) KiB"
  echo "objects=$OBJECTS bytes=$BYTES" > "$STAGE/media.count"
fi

offsite_free_mb() {
  ssh -o BatchMode=yes "$OFFSITE_HOST" \
    "df -Pm ~ | awk 'NR==2 {print \$4}'" 2>/dev/null || echo "-1"
}
FREE=$(offsite_free_mb)
[ "$FREE" -ge 0 ] 2>/dev/null || die "cannot read free space on $OFFSITE_HOST — refusing to push blind"

# ----------------------------------------------------------------- manifest ---
# Written before the offsite push so the remote copy carries it.
cat > "$STAGE/MANIFEST.txt" <<EOF
FriendCore backup manifest
  completed_at : $(date -u +%Y-%m-%dT%H:%M:%SZ)
  started_at   : $STARTED_AT
  database     : $PG_DB ($PG_CONTAINER)
  media_bucket : $MEDIA_BUCKET
  media        : ${OBJECTS:-skipped} objects / ${BYTES:-skipped} bytes
  offsite_free : ${FREE} MiB at dump time (floor ${MIN_FREE_MB} MiB)
  newest_dump  : $(ls -1t "$STAGE/db"/*.dump 2>/dev/null | head -1 | xargs -r basename)
  offsite      : $OFFSITE_HOST:~$OFFSITE_PATH
EOF

# ----------------------------------------------------------------- offsite ---
# tar over plain ssh rather than rclone's SFTP backend or rsync.
#
# rclone's SFTP backend fails to authenticate against this sshd ("attempted
# methods [none publickey], no supported methods remain") even though plain ssh
# using the same key works, and rsync is not installed on the laptop. tar-over-ssh
# needs nothing beyond the ssh client we already depend on.
#
# The cost is that tar has no delta transfer, so media is only pushed when its
# object count or total size has actually changed — otherwise every night would
# re-send the entire bucket. Database dumps are small and change every run, so
# they always go.
#
# When the offsite target becomes Cloudflare R2, add an s3 remote to
# rclone.conf and replace this block with `rclone copy "$STAGE" <remote>:friendcore`,
# which is incremental and removes the need for the change-detection below.
MEDIA_FINGERPRINT="$STAGE/.media-fingerprint"

# The offsite target is a 14GB disk already running the OS, AdGuard, Caddy and
# WireGuard. A backup that fills it takes down DNS, HTTPS and the tunnel the whole
# beta depends on, so headroom is a hard gate, not a warning. The database is
# small and always worth keeping; media is the only thing that grows without bound
# and is the only thing we will refuse to push.
log "offsite headroom: ${FREE} MiB free (floor ${MIN_FREE_MB} MiB)"
log "pushing database + manifest to $OFFSITE_HOST"
tar -C "$STAGE" -czf - db MANIFEST.txt \
  | ssh -o BatchMode=yes "$OFFSITE_HOST" \
      "mkdir -p ~/$OFFSITE_PATH && tar -C ~/$OFFSITE_PATH -xzf -" \
  || die "offsite push failed (db)"

FINGERPRINT="objects=${OBJECTS:-?} bytes=${BYTES:-?}"
if [ "$DO_MEDIA" -eq 1 ]; then
  PREV=$(cat "$MEDIA_FINGERPRINT" 2>/dev/null || echo "none")
  if [ "$FINGERPRINT" = "$PREV" ]; then
    log "media unchanged since last push ($FINGERPRINT) — skipping"
  elif [ "$FREE" -lt "$MIN_FREE_MB" ]; then
    # Loud, and non-fatal: the database copy succeeded, and losing media copies is
    # strictly better than taking the tunnel down mid-beta.
    log "WARNING: only ${FREE} MiB free on $OFFSITE_HOST — SKIPPING media push"
    log "         database backup completed; media copy deferred until space frees up"
  else
    log "media changed ($PREV -> $FINGERPRINT) — pushing bucket"
    tar -C "$STAGE" -czf - media \
      | ssh -o BatchMode=yes "$OFFSITE_HOST" \
          "mkdir -p ~/$OFFSITE_PATH && tar -C ~/$OFFSITE_PATH -xzf -" \
      || die "offsite push failed (media)"
    printf '%s' "$FINGERPRINT" > "$MEDIA_FINGERPRINT"
  fi
fi

# Remote rotation. tar only ever adds and overwrites, so without this the offsite
# accumulates every dump ever taken and eventually fills the disk. Age-based
# pruning rather than count-based, because the box may be off for days at a time
# and a count would then delete the weekly keepers.
log "pruning offsite dumps older than ${REMOTE_KEEP_DAYS} days"
ssh -o BatchMode=yes "$OFFSITE_HOST" \
  "find ~/$OFFSITE_PATH/db -name '*.dump' -mtime +$REMOTE_KEEP_DAYS -delete 2>/dev/null; df -Pm ~ | awk 'NR==2 {print \$4}'" \
  | { read -r AFTER; log "offsite headroom after rotation: ${AFTER:-?} MiB"; }

# ---------------------------------------------------------------- rotation ---
# Keep the newest KEEP_DAILY dumps, PLUS the first dump of each of the last
# KEEP_WEEKLY ISO weeks. Naive "delete anything older than 7 days" quietly
# destroys every backup when the laptop is off for a few days.
log "rotating (keep $KEEP_DAILY daily + $KEEP_WEEKLY weekly)"
prune() {
  local dir="$1" keep_daily="$2" keep_weekly="$3"
  python3 - "$dir" "$keep_daily" "$keep_weekly" <<'PY'
import sys, os, re, glob, datetime
d, keep_daily, keep_weekly = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
pat = re.compile(r"friendcore-(\d{8})T\d{6}Z\.dump$")
files = []
for f in glob.glob(os.path.join(d, "*.dump")):
    m = pat.search(os.path.basename(f))
    if m:
        files.append((datetime.datetime.strptime(m.group(1), "%Y%m%d"), f))
files.sort(reverse=True)
keep = {f for _, f in files[:keep_daily]}
weeks, this_week = [], None
for day, f in files:
    iso = day.isocalendar()[:2]
    if iso != this_week:
        this_week = iso
        weeks.append((iso, f))
    if len(weeks) > keep_weekly:
        break
for iso, f in weeks[-keep_weekly:]:
    keep.add(f)
removed = 0
for day, f in files:
    if f not in keep:
        os.remove(f); removed += 1
print(f"  kept {len(keep)}, removed {removed}")
PY
}
prune "$STAGE/db" "$KEEP_DAILY" "$KEEP_WEEKLY"

# Row-count sidecars follow their dump, otherwise they accumulate forever.
find "$STAGE/db" -name '*.rows.txt' -mtime +14 -delete 2>/dev/null || true

log "done"