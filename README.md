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
├─ Ledger.sln
├─ src/
│  ├─ Ledger.Api/          ASP.NET Core Web API
│  └─ Ledger.Desktop/      Windows Forms client
└─ tests/                  Added when the first tested behavior exists
```

## Prerequisites

- .NET 8 SDK
- A PostgreSQL database (Neon) for the API
- Windows to run the desktop client

## Building

```powershell
dotnet build Ledger.sln
```

## Running the API

```powershell
dotnet run --project src/Ledger.Api
```

Swagger UI is available at `http://localhost:5100/swagger` in the Development environment.

## Running the desktop client

Start the API first, then run:

```powershell
dotnet run --project src/Ledger.Desktop
```

The client reads `src/Ledger.Desktop/appsettings.json` for `ApiBaseUrl` (default `http://localhost:5100/`). Set the `LEDGER_API_BASE_URL` environment variable to override it.

The desktop client has three tabs:

- **Accounts**: list balances, create, edit and archive accounts; optionally show archived accounts.
- **Categories**: filter by kind, create, edit and archive categories.
- **Transactions**: filter and search transactions, page through results, create, edit and archive income or expense transactions.

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

The migrations are applied to the configured database. A new database is seeded with a default set of categories.

## API endpoints

All routes are under `/api/v1`.

| Method | Route | Notes |
|---|---|---|
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
| GET | `/health` | Database connectivity check |

`DELETE` archives records (`IsArchived = true`); it never performs physical deletes. Archived records remain visible with `includeArchived=true`.

## Data separation

Development and real financial data must not share a database once Ledger is used daily. Until a dedicated development database (for example a separate Neon branch) is configured, development and testing must not use real financial information.

## Roadmap

- **CP0**: foundation (solution structure, projects, secrets, build) — done
- **CP1**: domain model, EF Core, initial migration, health check — done
- **CP2**: accounts and categories API — done
- **CP3**: transactions API and balances — done
- **CP4**: WinForms client — done
- **CP5**: transfers — next
- Later: dashboard, budgets, authentication, deployment
