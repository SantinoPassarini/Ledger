#!/usr/bin/env bash
#
# Creates a verified Ledger backup with Docker. pg_dump and pg_restore run inside
# the official postgres:17-alpine image, so the host does not need PostgreSQL
# client tools.
#
# Accepts a libpq URI (postgresql://...) or an Npgsql-style connection string
# (Host=...;Database=...;Username=...;Password=...;SSL Mode=Require). Keep the
# connection string in an environment variable; never commit it.

set -Eeuo pipefail

usage() {
    cat <<'EOF'
Usage: backup-ledger-docker.sh [options]

Options:
  --connection VALUE      Connection string (default: LEDGER_BACKUP_CONNECTION)
  --output DIR            Backup directory (default: LEDGER_BACKUP_DIR or <repo>/backups)
  --retention-days DAYS   Delete local backups older than DAYS (default: 30)
  --image IMAGE           Docker image (default: postgres:17-alpine)
  --no-verify             Skip the pg_restore --list integrity check
  --keep-on-failure       Keep the partial file if the backup fails
  -h, --help              Show this help

Environment:
  LEDGER_BACKUP_CONNECTION     Connection string (preferred)
  LEDGER_BACKUP_DIR            Backup directory
  LEDGER_BACKUP_RETENTION_DAYS Retention in days
EOF
}

connection="${LEDGER_BACKUP_CONNECTION:-}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
output_dir="${LEDGER_BACKUP_DIR:-$script_dir/../backups}"
retention_days="${LEDGER_BACKUP_RETENTION_DAYS:-30}"
image="postgres:17-alpine"
verify=1
keep_on_failure=0
backup_db=""
host=""
port=""
dbname=""
user=""
password=""
sslmode=""

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
        --image)
            image="${2:-}"
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

if ! command -v docker >/dev/null 2>&1; then
    echo "docker was not found on PATH." >&2
    exit 1
fi

trim() {
    local value="$1"
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"
    printf '%s' "$value"
}

strip_quotes() {
    local value="$1"
    if [[ "$value" == \"*\" && "$value" == *\" && ${#value} -ge 2 ]]; then
        value="${value:1:${#value}-2}"
    elif [[ "$value" == \'*\' && "$value" == *\' && ${#value} -ge 2 ]]; then
        value="${value:1:${#value}-2}"
    fi
    printf '%s' "$value"
}

docker_env=()

if [[ "$connection" =~ ^[[:space:]]*postgres(ql)?:// ]]; then
    # libpq understands the URI directly and performs its own percent-decoding.
    export BACKUP_DB="$connection"
    docker_env+=(-e BACKUP_DB)
else
    IFS=';' read -r -a parts <<< "$connection"

    for part in "${parts[@]}"; do
        [[ -z "$(trim "$part")" ]] && continue
        key="$(trim "${part%%=*}")"
        value="$(strip_quotes "$(trim "${part#*=}")")"

        case "${key,,}" in
            host|server|hostaddr) host="$value" ;;
            port) port="$value" ;;
            database|dbname|"initial catalog") dbname="$value" ;;
            username|"user id"|user) user="$value" ;;
            password|pwd) password="$value" ;;
            "ssl mode"|sslmode) sslmode="$value" ;;
        esac
    done

    if [[ -z "$dbname" ]]; then
        echo "The connection string does not contain a database name." >&2
        exit 1
    fi

    if [[ -z "$user" ]]; then
        echo "The connection string does not contain a user name." >&2
        exit 1
    fi

    export BACKUP_DB="$dbname"

    if [[ -n "$host" ]]; then
        export PGHOST="$host"
        docker_env+=(-e PGHOST)
    fi

    if [[ -n "$port" ]]; then
        export PGPORT="$port"
        docker_env+=(-e PGPORT)
    fi

    export PGUSER="$user"
    docker_env+=(-e PGUSER)

    if [[ -n "$password" ]]; then
        export PGPASSWORD="$password"
        docker_env+=(-e PGPASSWORD)
    fi

    if [[ -n "$sslmode" ]]; then
        export PGSSLMODE="${sslmode,,}"
        docker_env+=(-e PGSSLMODE)
    fi
fi

umask 077
mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
timestamp="$(date -u +%Y%m%d-%H%M%S)"
filename="ledger-$timestamp.dump"
host_file="$output_dir/$filename"
container_file="/backup/$filename"

command="pg_dump --format=custom --no-owner --no-privileges --file \"\$BACKUP_FILE\" --dbname \"\$BACKUP_DB\""

if (( verify )); then
    command+=" && pg_restore --list \"\$BACKUP_FILE\" > /dev/null"
fi

echo "Dumping database to $host_file ..."

if ! docker run --rm \
    --user "$(id -u):$(id -g)" \
    --env "BACKUP_FILE=$container_file" \
    "${docker_env[@]}" \
    --mount "type=bind,source=$output_dir,target=/backup" \
    "$image" sh -c "$command"; then
    if (( keep_on_failure == 0 )); then
        rm -f "$host_file"
    fi
    echo "The Docker backup command failed." >&2
    exit 1
fi

if [[ ! -s "$host_file" ]]; then
    rm -f "$host_file"
    echo "pg_dump produced an empty file." >&2
    exit 1
fi

if find "$output_dir" -maxdepth 1 -type f -name 'ledger-*.dump' \
    -mtime +"$retention_days" -print -quit | grep -q .; then
    find "$output_dir" -maxdepth 1 -type f -name 'ledger-*.dump' \
        -mtime +"$retention_days" -print -delete
fi

size="$(du -h "$host_file" | cut -f1)"
echo "Backup completed: $host_file ($size)."
echo "Copy it off-server and keep it outside this repository."
