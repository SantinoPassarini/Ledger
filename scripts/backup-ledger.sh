#!/usr/bin/env bash
#
# Creates a verified backup of the Ledger PostgreSQL database with pg_dump.
#
# The connection string must be accepted by libpq (a postgresql:// URI or a
# keyword string such as "host=... dbname=... user=... sslmode=require").
# Keep it in an environment variable or a protected file; never commit it.
#
# The script is independent from the Ledger API and does not expose any
# endpoint. Off-server copying and scheduling are intentionally left to you.

set -Eeuo pipefail

usage() {
    cat <<'EOF'
Usage: backup-ledger.sh [options]

Options:
  --connection VALUE      PostgreSQL connection string (default: LEDGER_BACKUP_CONNECTION)
  --output DIR            Backup directory (default: LEDGER_BACKUP_DIR or <repo>/backups)
  --retention-days DAYS   Delete local backups older than DAYS (default: 30)
  --pg-dump PATH          pg_dump executable (default: PG_DUMP or pg_dump)
  --pg-restore PATH       pg_restore executable (default: PG_RESTORE or pg_restore)
  --no-verify             Skip the pg_restore --list integrity check
  --keep-on-failure       Keep the partial file if the backup fails
  -h, --help              Show this help

Environment:
  LEDGER_BACKUP_CONNECTION     Connection string (preferred)
  LEDGER_BACKUP_DIR            Backup directory
  LEDGER_BACKUP_RETENTION_DAYS Retention in days
  PGPASSWORD / PGPASSFILE      Password handling for pg_dump
  PGSSLMODE                    Optional libpq SSL mode override
EOF
}

connection="${LEDGER_BACKUP_CONNECTION:-}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
output_dir="${LEDGER_BACKUP_DIR:-$script_dir/../backups}"
retention_days="${LEDGER_BACKUP_RETENTION_DAYS:-30}"
pg_dump_bin="${PG_DUMP:-pg_dump}"
pg_restore_bin="${PG_RESTORE:-pg_restore}"
verify=1
keep_on_failure=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --connection)
            connection="${2:-}"
            shift 2
            ;;
        --output)
            output_dir="${2:-}"
            shift 2
            ;;
        --retention-days)
            retention_days="${2:-}"
            shift 2
            ;;
        --pg-dump)
            pg_dump_bin="${2:-}"
            shift 2
            ;;
        --pg-restore)
            pg_restore_bin="${2:-}"
            shift 2
            ;;
        --no-verify)
            verify=0
            shift
            ;;
        --keep-on-failure)
            keep_on_failure=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

if [[ -z "$connection" ]]; then
    echo "Set LEDGER_BACKUP_CONNECTION or pass --connection. Do not commit it to Git." >&2
    exit 1
fi

if [[ ! "$retention_days" =~ ^[0-9]+$ ]] || (( retention_days < 1 )); then
    echo "Retention days must be a positive integer." >&2
    exit 1
fi

if ! command -v "$pg_dump_bin" >/dev/null 2>&1; then
    echo "pg_dump was not found: $pg_dump_bin" >&2
    exit 1
fi

if (( verify )) && ! command -v "$pg_restore_bin" >/dev/null 2>&1; then
    echo "pg_restore was not found: $pg_restore_bin" >&2
    exit 1
fi

umask 077
mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
timestamp="$(date -u +%Y%m%d-%H%M%S)"
file="$output_dir/ledger-$timestamp.dump"

cleanup_on_error() {
    if (( keep_on_failure == 0 )); then
        rm -f "$file"
    fi
}

trap cleanup_on_error ERR

echo "Dumping database to $file ..."
"$pg_dump_bin" \
    --format=custom \
    --no-owner \
    --no-privileges \
    --file "$file" \
    --dbname "$connection"

if [[ ! -s "$file" ]]; then
    echo "pg_dump produced an empty file." >&2
    false
fi

if (( verify )); then
    "$pg_restore_bin" --list "$file" >/dev/null
fi

trap - ERR

if find "$output_dir" -maxdepth 1 -type f -name 'ledger-*.dump' \
    -mtime +"$retention_days" -print -quit | grep -q .; then
    find "$output_dir" -maxdepth 1 -type f -name 'ledger-*.dump' \
        -mtime +"$retention_days" -print -delete
fi

size="$(du -h "$file" | cut -f1)"
echo "Backup completed: $file ($size)."
echo "Copy it off-server and keep it outside this repository."
