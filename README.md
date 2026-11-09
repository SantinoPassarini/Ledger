# Ledger

Personal finance management application.

Ledger is composed of three parts:

- **Ledger.Desktop**: Windows Forms client. Talks to the API over HTTP/JSON and never connects to the database.
- **Ledger.Api**: ASP.NET Core Web API. Owns business rules, validation and data access through Entity Framework Core.
- **PostgreSQL**: cloud-hosted database (Neon). Only the API connects to it.

```
WinForms -> HTTP/JSON -> ASP.NET Core API -> EF Core -> PostgreSQL
```

## Repository layout

```
Ledger/
├── Ledger.sln
├── src/
│   ├── Ledger.Api/          ASP.NET Core Web API
│   └── Ledger.Desktop/      Windows Forms client
├── scripts/                 Standalone backup scripts and restore notes
├── tests/
│   ├── Ledger.Api.Tests/    API integration tests
│   └── Ledger.Desktop.Smoke/
└── .github/workflows/       CI
```

## Prerequisites

- .NET 8 SDK
- A PostgreSQL database (Neon) for the API
- PostgreSQL client tools (`pg_dump` and `pg_restore`) for backups
- Windows to run the desktop client

## Building

```powershell
dotnet build Ledger.sln
```

## Testing

API integration tests run against PostgreSQL and create a unique schema, so they do not modify existing data:

```powershell
dotnet test tests/Ledger.Api.Tests/Ledger.Api.Tests.csproj
```

They read `LEDGER_TEST_CONNECTION` or the `ConnectionStrings:Ledger` value from `Ledger.Api` user secrets. Never point tests at production data.

The desktop smoke test requires a running API and a provisioned account:

```powershell
dotnet run --project tests/Ledger.Desktop.Smoke -- --identifier <username-or-email> --password <password>
```

Or start the API automatically. The script reads `Ledger:*` credentials from user secrets or environment variables and does not override them:

```powershell
.\tests\run-desktop-smoke.ps1 -Identifier <username-or-email> -Password <password>
```

You can also set `LEDGER_SMOKE_IDENTIFIER` and `LEDGER_SMOKE_PASSWORD`. Test artifacts and screenshots are written to `artifacts/` and are ignored by Git. See `tests/README.md` for details.

## Running the API

Before starting the API, configure the owner account and the JWT signing key:

```powershell
dotnet user-secrets set "Ledger:Owner:Username" "owner" --project src/Ledger.Api
dotnet user-secrets set "Ledger:Owner:Email" "owner@example.com" --project src/Ledger.Api
dotnet user-secrets set "Ledger:Owner:Password" "a-strong-password" --project src/Ledger.Api
dotnet user-secrets set "Ledger:Owner:DisplayName" "Owner" --project src/Ledger.Api
dotnet user-secrets set "Jwt:SigningKey" "at-least-32-characters-long-signing-key" --project src/Ledger.Api
```

Additional accounts can be provisioned from the `Ledger:Users` section. The dictionary key is the username:

```powershell
dotnet user-secrets set "Ledger:Users:second:Email" "second@example.com" --project src/Ledger.Api
dotnet user-secrets set "Ledger:Users:second:Password" "another-strong-password" --project src/Ledger.Api
dotnet user-secrets set "Ledger:Users:second:DisplayName" "Second User" --project src/Ledger.Api
```

If `Email` is omitted, the API uses `<username>@ledger.local`. Every configured account gets its own default categories. Then:

```powershell
dotnet run --project src/Ledger.Api
```

On startup the API creates or updates the configured owner and additional users and assigns their default categories. Sign in with either the username or the email. Swagger UI is available at `http://localhost:5100/swagger` in the Development environment; use the **Authorize** button with the token returned by `POST /api/v1/auth/login`.

### Self-registration

Registration is disabled by default. To let the people you know create their own account, enable it and set a shared invite code:

```powershell
dotnet user-secrets set "Ledger:Registration:Enabled" "true" --project src/Ledger.Api
dotnet user-secrets set "Ledger:Registration:InviteCode" "a-long-random-invite-code" --project src/Ledger.Api
```

The invite code is a secret: store it in user secrets or an environment variable, never in Git. If registration is enabled but the invite code is empty, the API logs an error and rejects every registration request. This is intentional fail-closed behavior.

Registration requires a username, a password with at least 12 characters and the invite code. Email is optional and is not verified. When it is blank, the API generates `<username>@ledger.local` and tells the client that the address cannot receive email. A new account receives its own default categories and an access token, so the desktop client signs in automatically.

The username and email of self-registered users share the same unique namespace as the accounts provisioned from `Ledger:Owner` and `Ledger:Users`. Do not add a configured user with a username or email that a self-registered user already uses.

Optional JWT settings:

| Setting | Default | Notes |
|---|---|---|
| `Jwt:Issuer` | `Ledger.Api` | Token issuer |
| `Jwt:Audience` | `Ledger.Desktop` | Token audience |
| `Jwt:SigningKey` | empty | Required, at least 32 characters |
| `Jwt:AccessTokenMinutes` | `480` | Access token lifetime |

If `Ledger:Owner:Username`/`Email`, `Ledger:Owner:Password` or `Jwt:SigningKey` are missing, the API still starts and `/health` responds, but sign-in is disabled for the affected account and an error is logged. Configured passwords must have at least 8 characters; self-registration and password changes require at least 12.

## Running the desktop client

Start the API first, then run:

```powershell
dotnet run --project src/Ledger.Desktop
```

The client shows a sign-in window. Enter your username or email and password, or use **Create account** to register with an invite code. The access token is kept only in memory for the lifetime of the sign-in session; there is no refresh token, no "remember me" option and no token persisted to disk. When the token expires or the API rejects it, the client returns to the sign-in window. **Sign out** in the header also returns to the sign-in window.

**Change password** in the header requires the current password and a new password with at least 12 characters. The new password must differ from the current one. After a successful change the client closes the session and returns to the sign-in window. Tokens already issued to other devices stay valid until they expire; there is no server-side token revocation yet.

The client reads `src/Ledger.Desktop/appsettings.json` for `ApiBaseUrl` (default `http://localhost:5100/`). Set the `LEDGER_API_BASE_URL` environment variable to override it.

The desktop client has five tabs:

- **Dashboard**: total balance, income and expenses for a selected month, expenses by category, a monthly income/expense bar chart, a six-month trend and recent transactions.
- **Accounts**: list balances, create, edit and archive accounts; optionally show archived accounts.
- **Categories**: filter by kind, create, edit and archive categories.
- **Budgets**: browse monthly expense budgets, create, edit and archive them, and track spent, remaining and percent used.
- **Transactions**: filter and search transactions, page through results, create, edit and archive income or expense transactions, and create and archive transfers.

## Configuration and secrets

Connection strings and credentials are **never** committed to Git. The API reads configuration from:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (development)
4. Environment variables (deployment)

Set the development connection string with user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:Ledger" "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" --project src/Ledger.Api
```

## Database migrations

```powershell
# Create a migration (does not connect to the database)
dotnet ef migrations add <Name> --project src/Ledger.Api --startup-project src/Ledger.Api --output-dir Data/Migrations

# Apply pending migrations (requires a configured connection string)
dotnet ef database update --project src/Ledger.Api --startup-project src/Ledger.Api
```

The migrations are applied to the configured database. A new database is seeded with a default set of categories owned by the provisioned owner. The `AddUsersAndAuthentication` migration creates the `Users` table, assigns all pre-existing accounts, categories, transactions and budgets to the owner user, and then makes `UserId` mandatory. The `AddUsernames` migration adds unique usernames and backfills them from the local part of existing emails. Review both migrations before applying them to a database with real data.

## API endpoints

All routes are under `/api/v1`. Every endpoint requires a Bearer access token except `/health`, `POST /auth/login` and, when enabled, `POST /auth/register`.

| Method | Route | Notes |
|---|---|---|
| POST | `/auth/login` | Sign in with username or email and password; returns an access token and the current user (anonymous) |
| POST | `/auth/register` | Register with an invite code; returns an access token, the new user and whether the email was generated (anonymous, disabled by default) |
| GET | `/auth/me` | Current authenticated user |
| POST | `/auth/change-password` | Change the current user's password; requires at least 12 characters and a new value |
| GET | `/accounts?includeArchived=false` | List accounts with computed balances |
| GET | `/accounts/{id}` | Account detail with balance |
| POST | `/accounts` | Create account |
| PUT | `/accounts/{id}` | Update name and type |
| DELETE | `/accounts/{id}` | Archive account |
| GET | `/categories?kind=&includeArchived=false` | List categories |
| GET | `/categories/{id}` | Category detail |
| POST | `/categories` | Create category |
| PUT | `/categories/{id}` | Update name and kind |
| DELETE | `/categories/{id}` | Archive category |
| GET | `/transactions?from=&to=&accountId=&categoryId=&type=&search=&includeArchived=false&page=1&pageSize=50` | Paged transaction history |
| GET | `/transactions/{id}` | Transaction detail |
| POST | `/transactions` | Create income or expense |
| PUT | `/transactions/{id}` | Update income or expense |
| DELETE | `/transactions/{id}` | Archive transaction |
| POST | `/transfers` | Create a transfer between two accounts; rejects amounts above the source balance with `409 Conflict` |
| GET | `/transfers/{transferGroupId}` | Transfer detail with both legs |
| DELETE | `/transfers/{transferGroupId}` | Archive both transfer legs |
| GET | `/budgets?month=yyyy-MM-dd&includeArchived=false` | List monthly budgets with spent, remaining and percent used |
| GET | `/budgets/{id}` | Budget detail |
| POST | `/budgets` | Create a monthly budget for an expense category |
| PUT | `/budgets/{id}` | Update a budget amount |
| DELETE | `/budgets/{id}` | Archive a budget |
| GET | `/dashboard?month=yyyy-MM-dd` | Monthly dashboard: total balance, income, expenses, categories, trend and recent transactions |
| GET | `/health` | Database connectivity check |

`DELETE` archives records (`IsArchived = true`); it never performs physical deletes. Archived records remain visible with `includeArchived=true`. Archiving a transfer archives both linked transactions.

## Authentication and data isolation

The API uses JWT Bearer authentication. `POST /api/v1/auth/login` returns a short-lived access token; the desktop client attaches it as `Authorization: Bearer <token>` on every request. Tokens are stateless: signing out on the client only removes the token from memory, and the token remains valid on the server until it expires.

Every `Account`, `Category`, `Transaction` and `Budget` row stores a `UserId`. EF Core global query filters restrict reads to the authenticated user, and create operations assign the authenticated `UserId` explicitly. A second user cannot see or modify another user's rows, even when the GUID is known.

There is no open registration. The owner account is provisioned from `Ledger:Owner` configuration at API startup, and every account in `Ledger:Users` is provisioned the same way. Self-registration is opt-in through `Ledger:Registration:Enabled` and the shared `Ledger:Registration:InviteCode`; it is disabled when either setting is missing. Each account gets its own default categories. Sign in with either the username or the email. To change a password, use **Change password** in the desktop client or call `POST /api/v1/auth/change-password` with the current and new password while authenticated. If the account was provisioned from configuration, also update the corresponding `Ledger:Owner:Password` or `Ledger:Users:<username>:Password` value so the provisioning service does not reset it on the next startup.

## Transfers

A transfer moves money between two active accounts. Creating one writes two linked transactions, `TransferOut` and `TransferIn`, with the same `TransferGroupId` and no category. The desktop client archives both legs together.

The API validates funds inside a serializable database transaction: the source account balance is summed from non-archived transactions, and a transfer above that balance returns `409 Conflict` with the title `Insufficient funds`. No transfer legs are written in that case. The transfer dialog also shows the source account's available balance and rejects an excessive amount before calling the API.

## Budgets

Budgets are monthly spending limits for expense categories. Each budget has a category, a month normalized to its first day, and a positive amount with at most two decimals. Only one active budget can exist per category and month.

`GET /api/v1/budgets?month=yyyy-MM-dd` returns the budgets for the selected month (current UTC month when omitted) with:

- `Amount`: the configured limit,
- `Spent`: expenses in that category and month,
- `Remaining`: `Amount - Spent`,
- `PercentUsed`: `Spent / Amount * 100`.

Spent amounts use the same rules as the dashboard: only non-archived expenses on non-archived accounts count. Transfers and income are excluded. `DELETE` archives a budget; recreating the same category and month restores the archived row with the same ID and the new amount. Updates only change the amount, because category and month are fixed.

## Dashboard

`GET /api/v1/dashboard?month=yyyy-MM-dd` returns an aggregated view for the selected month. When `month` is omitted, the API uses the current UTC month and normalizes it to the first day. The response contains:

- total balance across active accounts,
- income and expenses for the selected month,
- balance per active account,
- expenses grouped by category,
- a six-month trend ending at the selected month,
- the eight most recent transactions.

The desktop dashboard renders the trend as a grouped vertical bar chart, with income and expenses per month. Account balances remain in the API response and in the Accounts tab, but the dashboard no longer shows a separate Accounts panel.

Archived transactions and transactions belonging to archived accounts are excluded. Transfers affect account balances but do not count as income or expenses.

## Backups

Backups are produced by standalone scripts in `scripts/`, not by the API. They use `pg_dump --format=custom`, verify the archive with `pg_restore --list` and apply a retention policy:

```powershell
$env:LEDGER_BACKUP_CONNECTION = 'postgresql://user:password@host/db?sslmode=require'
.\scripts\backup-ledger.ps1 -OutputDirectory D:\Backups\Ledger
```

```bash
export LEDGER_BACKUP_CONNECTION='postgresql://user:password@host/db?sslmode=require'
./scripts/backup-ledger.sh --output /srv/backups/ledger
```

The scripts only create local files. Copy them off-server on your own schedule with `rclone` or `scp`, and encrypt them with `age` if the destination is not fully trusted. `scripts/README.md` covers restore commands, off-server copy examples and scheduling. Test a restore periodically; a backup that has never been restored is only a hypothesis.

## Deployment

The guides in `docs/deployment/` are hands-on checklists for taking Ledger to a private production server:

- `01-secretos-produccion.md`: production secrets and environment variables.
- `02-https.md`: TLS, reverse proxy, certificates and redirects.
- `03-empaquetado.md`: publishing the API and the desktop client.
- `04-backups-programados.md`: scheduling, off-server copies, monitoring and restore drills.

Implement each step on your server, then review the result before exposing the API. The guides also list what to verify and which points are reviewed.

## Data separation

Development and real financial data must not share a database once Ledger is used daily. Until a dedicated development database (for example a separate Neon branch) is configured, development and testing must not use real financial information.

## Roadmap

- **CP0**: foundation (solution structure, projects, secrets, build) — done
- **CP1**: domain model, EF Core, initial migration, health check — done
- **CP2**: accounts and categories API — done
- **CP3**: transactions API and balances — done
- **CP4**: WinForms client — done
- **CP5**: transfers — done
- **CP6**: dashboard — done
- **CP7**: budgets — done
- **CP8**: authentication, JWT Bearer and per-user data isolation — done
- **CP9**: self-registration, password change and standalone backups — done
- **CP10**: deployment — guides ready in `docs/deployment/`; implementation and review in progress
