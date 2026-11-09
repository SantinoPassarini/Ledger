# Ledger backups

Ledger backups are produced by standalone scripts. The API does not expose a backup endpoint and does not need to be running while the backup runs.

| Script | Platform | Output |
|---|---|---|
| `backup-ledger.ps1` | Windows PowerShell 7+ | `backups/ledger-YYYYMMDD-HHmmss.dump` |
| `backup-ledger.sh` | Linux/macOS Bash | `backups/ledger-YYYYMMDD-HHmmss.dump` |

Both scripts:

- run `pg_dump --format=custom --no-owner --no-privileges`,
- verify the archive with `pg_restore --list`,
- remove local backups older than the retention period,
- never write a password into the repository.

## Requirements

- PostgreSQL client tools (`pg_dump` and `pg_restore`) on `PATH`. Use a client version equal to or newer than the PostgreSQL server.
- A PostgreSQL connection string for the Ledger database. Keep it out of Git.

## Configure the connection

PowerShell:

```powershell
$env:LEDGER_BACKUP_CONNECTION = 'postgresql://user:password@host/db?sslmode=require'
```

Bash:

```bash
export LEDGER_BACKUP_CONNECTION='postgresql://user:password@host/db?sslmode=require'
```

`backup-ledger.ps1` also accepts Npgsql-style strings such as `Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`. The Bash script expects a libpq-compatible URI or keyword string.

For scheduled jobs, prefer `PGPASSFILE` or a protected environment file over putting the password directly in the command line. The PowerShell script passes the password through `PGPASSWORD`; the Bash script relies on libpq, so `PGPASSWORD`/`PGPASSFILE` also work there.

## Run a backup

PowerShell:

```powershell
.\scripts\backup-ledger.ps1
.\scripts\backup-ledger.ps1 -OutputDirectory D:\Backups\Ledger -RetentionDays 14
```

Bash:

```bash
./scripts/backup-ledger.sh
./scripts/backup-ledger.sh --output /srv/backups/ledger --retention-days 14
```

Optional flags: `--no-verify` / `-SkipVerification` skips the integrity check; `--keep-on-failure` / `-KeepOnFailure` keeps a partial file for inspection.

## Restore a backup

Restoring replaces data. Take a fresh backup first, or restore into an empty database.

```powershell
# Windows
pg_restore --clean --if-exists --no-owner --no-privileges `
  --dbname $env:LEDGER_BACKUP_CONNECTION `
  .\backups\ledger-20261108-030000.dump
```

```bash
# Linux/macOS
pg_restore --clean --if-exists --no-owner --no-privileges \
  --dbname "$LEDGER_BACKUP_CONNECTION" \
  backups/ledger-20261108-030000.dump
```

`--clean --if-exists` drops existing objects in the target database before restoring. Never run it against a database you have not backed up.

After restoring:

1. Start the API and check `GET /health`.
2. Sign in and verify accounts, transactions, budgets and the dashboard.
3. Keep the restored database private until you confirm the data is correct.

## Copy backups off-server

The scripts only create local files. Copy them to a different machine or storage account on your own schedule.

`rclone`:

```powershell
rclone copy .\backups\ledger-20261108-030000.dump remote:ledger-backups/
```

`scp`:

```bash
scp backups/ledger-20261108-030000.dump backup@backup-host:/srv/ledger-backups/
```

Encrypt before copying when the destination is not fully trusted. Example with `age`:

```bash
age -r age1examplepublickey -o backups/ledger-20261108-030000.dump.age \
  backups/ledger-20261108-030000.dump
scp backups/ledger-20261108-030000.dump.age backup@backup-host:/srv/ledger-backups/
```

Decrypt only on the machine where you will restore:

```bash
age -d -i /secure/ledger-age-key.txt -o restored.dump \
  backups/ledger-20261108-030000.dump.age
```

## Schedule backups

Windows Task Scheduler example action:

```text
Program:   pwsh.exe
Arguments: -NoProfile -File C:\path\to\Ledger\scripts\backup-ledger.ps1 -OutputDirectory D:\Backups\Ledger
```

cron example:

```cron
15 3 * * * . /etc/ledger/backup.env && /path/to/Ledger/scripts/backup-ledger.sh >> /var/log/ledger-backup.log 2>&1
```

Protect `/etc/ledger/backup.env` with permissions such as `chmod 600`, or use `.pgpass`.

## Verify backups regularly

`pg_restore --list` proves the archive is readable, not that the data is complete. Periodically restore a backup into a separate database and run the smoke test or a manual sign-in check. Keep at least one copy outside the server that runs the API.
