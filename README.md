# KanaanFlow

An offline-first personal finance management app built with .NET MAUI.

## Architecture

Clean layered architecture with clear dependency boundaries:

```
KanaanFlow.Core      — No dependencies (domain models, interfaces, enums)
KanaanFlow.Data      — Depends on Core (EF Core + SQLite repositories)
KanaanFlow.Sync      — Depends on Core (sync placeholder)
KanaanFlow.App       — Depends on Core, Data, Sync (MAUI UI, ViewModels, Services)
KanaanFlow.Tests     — Depends on Core (xUnit tests)
```

## Features

### Domain Models
- **Transaction** — income/expense tracking with category, date, amount
- **Category** — named categories with icon and color
- **Loan** — given/received loans with status tracking (Active, PaidOff, Overdue)
- **Receivable** — money owed to you
- **DailyReport** — computed daily financial summary

### Pages
| Page | Description |
|------|-------------|
| Dashboard | Today's income/expense/balance + 5 recent transactions |
| Transactions | Full list with date filter, swipe-to-delete |
| Add Transaction | Form with amount, category, type toggle, date |
| Loans | Grouped by status (Active / Overdue / Paid Off) |
| Add Loan | Create new loan with contact, amount, direction |
| Reports | Daily/Weekly/Monthly summaries |
| Categories | Manage categories with add/delete |
| Settings | License info and app version |

### MVVM Stack
- All ViewModels extend `BaseViewModel` (via `ObservableObject` from CommunityToolkit.Mvvm)
- Commands use `[RelayCommand]` source generator
- Lists use `ObservableCollection<T>`
- DI wired in `MauiProgram.cs`

### Data Layer
- SQLite via EF Core
- 4 repositories: Transaction, Category, Loan, Receivable
- Auto-seeded default categories on first launch
- `DbInitializer` runs `EnsureCreatedAsync` + seeds categories

### Sync
- `ISyncService` interface defined in Core
- Stub implementation in `KanaanFlow.Sync` — ready for future cloud sync

## Default Categories
Food, Transport, Salary, Rent, Entertainment, Health, Shopping, Other

## License System
JWT-based license validation. Enter license key on first launch. Validated against embedded public key.

## Tech Stack
- .NET 10 / .NET MAUI
- EF Core 10 + SQLite
- CommunityToolkit.Mvvm 8.4
- xUnit for tests

## Running Tests
```bash
dotnet test KanaanFlow.Tests/KanaanFlow.Tests.csproj
```
