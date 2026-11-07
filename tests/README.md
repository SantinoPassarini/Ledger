# Tests

All test code and test tooling live in this folder. Test artifacts are written to `artifacts/` at the repository root and are ignored by Git.

## API integration tests

`Ledger.Api.Tests` uses `WebApplicationFactory` against a real PostgreSQL database.

Each test run:

- creates a unique PostgreSQL schema,
- applies the EF Core migrations to that schema,
- drops the schema when the run finishes.

The `public` schema and any existing data are never modified. Tests create their own `[TEST]` records, so they can run against the same test database used by the desktop app without deleting existing records.

### Connection string

The tests resolve the database in this order:

1. `LEDGER_TEST_CONNECTION` environment variable.
2. The `ConnectionStrings:Ledger` value in `Ledger.Api` user secrets.

Never point either value at production data. Use a dedicated development/test database.

Run:

```powershell
dotnet test tests/Ledger.Api.Tests/Ledger.Api.Tests.csproj
```

or:

```powershell
.\tests\run-api-tests.ps1
```

CI uses a PostgreSQL service container and sets `LEDGER_TEST_CONNECTION` automatically.

## Desktop smoke test

`Ledger.Desktop.Smoke` starts the real WinForms controls, reads accounts, categories and transactions from a running API, and verifies the grids and layout. It also creates a transfer between two test accounts, verifies both legs and archives the transfer.

Start the API first:

```powershell
dotnet run --project src/Ledger.Api
```

Then run:

```powershell
dotnet run --project tests/Ledger.Desktop.Smoke
```

Or use the helper script, which starts and stops the API for you:

```powershell
.\tests\run-desktop-smoke.ps1
```

If PowerShell blocks local scripts, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-desktop-smoke.ps1
```

Optional arguments:

```powershell
dotnet run --project tests/Ledger.Desktop.Smoke -- --api http://localhost:5100/ --screenshots
```

Screenshots are written to `artifacts/smoke/` and are ignored by Git.

The smoke test creates uniquely named `[TEST]` records and archives them when it finishes. It does not delete user data.

## CI

`.github/workflows/ci.yml` runs:

- API integration tests on Ubuntu with PostgreSQL,
- the full solution build on Windows,
- gitleaks secret scanning,
- NuGet vulnerability audit.
