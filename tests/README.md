# Tests

All test code and test tooling live in this folder. Test artifacts are written to `artifacts/` at the repository root and are ignored by Git.

## API integration tests

`Ledger.Api.Tests` uses `WebApplicationFactory` against a real PostgreSQL database.

Each test run:

- creates a unique PostgreSQL schema,
- applies the EF Core migrations to that schema,
- drops the schema when the run finishes.

The `public` schema and any existing data are never modified. Tests create their own `[TEST]` records, so they can run against the same test database used by the desktop app without deleting existing records.

The test factory generates a random JWT signing key and random passwords for every run. It provisions an owner account and a second account through the same `Ledger:Owner` / `Ledger:Users` configuration used by the API, inside the isolated schema. It also enables self-registration with a test invite code. `ApiTestBase` signs in by default and attaches the Bearer token to every request. Authentication tests verify login by username and by email, invalid credentials, missing or invalid tokens, password changes and isolation between two users. Registration tests verify account creation, automatic login, generated `@ledger.local` addresses, default categories, invalid input, invalid invite codes, duplicate usernames and emails, and data isolation for self-registered users. No credentials are hardcoded in the test project.

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

`Ledger.Desktop.Smoke` starts the real WinForms controls, reads accounts, categories, transactions, budgets and the dashboard from a running API, and verifies the grids, the monthly bar chart and the layout. It also creates a transfer between two test accounts, verifies both legs and the dashboard totals, checks that an over-balance transfer is rejected with `409 Conflict`, exercises budget creation and update, and archives everything it created.

Start the API first. The target database must have the latest migrations applied (`dotnet ef database update --project src/Ledger.Api --startup-project src/Ledger.Api`).

```powershell
dotnet run --project src/Ledger.Api
```

Then run:

```powershell
dotnet run --project tests/Ledger.Desktop.Smoke -- --identifier <username-or-email> --password <password>
```

The account must be provisioned in the API through `Ledger:Owner` or `Ledger:Users`. You can also set `LEDGER_SMOKE_IDENTIFIER` and `LEDGER_SMOKE_PASSWORD`; `--email` is accepted as an alias for `--identifier`. The smoke test has no built-in credentials.

Or use the helper script, which starts and stops the API for you:

```powershell
.\tests\run-desktop-smoke.ps1 -Identifier <username-or-email> -Password <password>
```

The script does not override `Ledger:*` or `Jwt:*`. The API reads them from user secrets or the current environment, and the same credentials are passed to the smoke test through environment variables.

If PowerShell blocks local scripts, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-desktop-smoke.ps1
```

Optional arguments:

```powershell
dotnet run --project tests/Ledger.Desktop.Smoke -- --api http://localhost:5100/ --identifier <username-or-email> --password <password> --screenshots
```

The smoke test first verifies that an unauthenticated request is rejected with `401`, then signs in, exercises the API, constructs all dialogs including `LoginForm`, `RegisterForm` and `ChangePasswordDialog`, and verifies that `MainForm` shows the signed-in user.

Screenshots are written to `artifacts/smoke/` and are ignored by Git.

The smoke test creates uniquely named `[TEST]` records and archives them when it finishes. It does not delete user data.

## CI

`.github/workflows/ci.yml` runs:

- API integration tests on Ubuntu with PostgreSQL,
- the full solution build on Windows,
- gitleaks secret scanning,
- NuGet vulnerability audit.
