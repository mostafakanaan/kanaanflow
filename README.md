# KanaanFlow

KanaanFlow is an offline-first, cross-platform finance management application
for small businesses.

It helps track income, expenses, loans, and receivables and generates
daily financial reports (PDF and Excel).  
The app syncs data to a central cloud store when a connection is available.

Built with .NET (Blazor WebAssembly PWA and MAUI), EF Core, SQLite, and Supabase.

## The app (KanaanFlow.Web)

An installable web app (PWA) that runs on Android, iPhone and Windows from one codebase.
Arabic first (right-to-left), English one tap away.

- **Record money** in three steps: Received / Paid › amount › person › type. Undo for 5 seconds.
- **People & companies** with categories, totals per currency, open loans, history and a PDF statement.
- **Loans**: I lent / I borrowed, progress, overdue flag, one-tap repayment (repayments also update cash).
- **Exchange** between SYP, USD, EUR and TRY at a typed or fetched rate; each currency keeps its own balance.
- **Reports** for a day or date range, filtered by people category, entry type or person; export PDF (print) or Excel.
- **Offline first**: everything is stored on the device (IndexedDB) and works with no internet.
- **Sync** (optional): devices share an append-only event log through Supabase, so entries are never overwritten.
  Run [`docs/sync-supabase.sql`](docs/sync-supabase.sql) once, then enter the project URL, anon key and a
  shared book code under *More › Sync*.

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

├─ IndexedDB local store, Supabase sync

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

 ├─ Supabase Integration

 └─ Conflict Resolution



## Dependencies

- KanaanFlow.App

    - depends on Core, Data, Sync

- KanaanFlow.Core

    - no external dependencies

- KanaanFlow.Data

    - depends on Core

- KanaanFlow.Sync

    - depends on Core

###

- `App` depends on all other layers
- `Core` contains no infrastructure dependencies
- `Data` and `Sync` depend only on `Core`
- No circular dependencies

## Key Principles

- Offline-first design
- Clear domain separation
- Minimal external dependencies
- Future-ready for licensing and commercial distribution