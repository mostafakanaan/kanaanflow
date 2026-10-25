# KanaanFlow

KanaanFlow is an offline-first, cross-platform finance management application
for small businesses.

It helps track income, expenses, loans, and receivables and generates
daily financial reports (PDF and Excel).  
The app syncs data to the team's own server (a Raspberry Pi) when a connection is available.

Built with .NET (Blazor WebAssembly PWA, ASP.NET Core and MAUI), PostgreSQL, EF Core and SQLite.

## The app (KanaanFlow.Web)

An installable web app (PWA) that runs on Android, iPhone and Windows from one codebase.
Arabic first (right-to-left), English one tap away.

- **Record money** in three steps: Received / Paid › amount › person › type. Undo for 5 seconds.
- **People & companies** with categories, totals per currency, open loans, history and a PDF statement.
- **Loans**: I lent / I borrowed, progress, overdue flag, one-tap repayment (repayments also update cash).
- **Exchange** between SYP, USD, EUR and TRY at a typed or fetched rate; each currency keeps its own balance.
- **Reports** for a day or date range, filtered by people category, entry type or person; export PDF (print) or Excel.
- **Offline first**: everything is stored on the device (IndexedDB) and works with no internet.
- **Team sync**: devices share an append-only event log through the KanaanFlow sync service (ASP.NET Core +
  PostgreSQL on the Pi), so entries are never overwritten. Each team member signs in with a name and password,
  and the server enforces their role (Owner, Staff, View only). See [`docs/hosting-pi.md`](docs/hosting-pi.md).

Run locally:

```bash
dotnet run --project KanaanFlow.Web
```

Deploy: self-hosted on a Raspberry Pi behind a Cloudflare Tunnel; every push to `main` builds and deploys it
(see [`docs/hosting-pi.md`](docs/hosting-pi.md) for the one-time setup).
On a phone, open the site and choose *Install app* (Android/Windows) or *Share › Add to Home Screen* (iPhone).

⚠️ Source-available. Commercial use requires permission.

---

## Architecture Overview

KanaanFlow follows a clean, layered architecture with clear separation
between UI, domain logic, persistence, and synchronization.

## Project Structure

**KanaanFlow.Web:**

├─ Blazor WebAssembly PWA (phone, tablet, desktop)

├─ IndexedDB local store, team sync client

└─ Excel / PDF export

**KanaanFlow.App:**

├─ MAUI UI

├─ Navigation

├─ ViewModels

└─ Views

**KanaanFlow.Core:**

 ├─ Domain Models

 ├─ Finance: event log, book projection, balances, loans, reports

 ├─ Enums

 ├─ Business Rules
 
 └─ Interfaces

**KanaanFlow.Data:**

 ├─ EF Core DbContext

 ├─ SQLite Persistence

 ├─ Migrations

 └─ Repositories

**KanaanFlow.Sync:**

 ├─ Sync Services

 ├─ ASP.NET Core sync API (sign-in, roles, event log)

 ├─ PostgreSQL (Docker on the Pi)

 └─ Conflict Resolution



## Dependencies

- KanaanFlow.App

    - depends on Core, Data

- KanaanFlow.Core

    - no external dependencies

- KanaanFlow.Data

    - depends on Core

- KanaanFlow.Sync

    - depends on Core

###

- `App` and `Web` depend on Core; `Sync` is a separate server that also depends only on Core
- `Core` contains no infrastructure dependencies
- `Data` and `Sync` depend only on `Core`
- No circular dependencies

## Key Principles

- Offline-first design
- Clear domain separation
- Minimal external dependencies
- Future-ready for licensing and commercial distribution