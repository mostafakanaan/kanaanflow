# KanaanFlow

KanaanFlow is an offline-first, cross-platform finance management application
for small businesses.

It helps track income, expenses, loans, and receivables and generates
daily financial reports (PDF and Excel).  
The app syncs data to a central cloud store when a connection is available.

Built with .NET MAUI, EF Core, SQLite, and Supabase.

⚠️ Source-available. Commercial use requires permission.

---

## Architecture Overview

KanaanFlow follows a clean, layered architecture with clear separation
between UI, domain logic, persistence, and synchronization.

## Project Structure

**KanaanFlow.App:**

├─ MAUI UI

├─ Navigation

├─ ViewModels

└─ Views

**KanaanFlow.Core:**

 ├─ Domain Models

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