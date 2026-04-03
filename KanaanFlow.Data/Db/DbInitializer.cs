namespace KanaanFlow.Data.Db;

using KanaanFlow.Core.Enums;
using KanaanFlow.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

public sealed class DbInitializer
{
    private readonly IServiceScopeFactory scopeFactory;

    public DbInitializer(IServiceScopeFactory serviceScopeFactory)
    {
        scopeFactory = serviceScopeFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        bool created = await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!created)
        {
            bool allTablesExist = await VerifyAllTablesExistAsync(db, cancellationToken);
            if (!allTablesExist)
            {
                await db.Database.EnsureDeletedAsync(cancellationToken);
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }
        }

        await ApplyManualMigrationsAsync(db, cancellationToken);
        await SeedCategoriesAsync(db, cancellationToken);
    }

    private static async Task<bool> VerifyAllTablesExistAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        string[] requiredTables = ["Transactions", "Categories", "Loans"];

        DbConnection connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (string table in requiredTables)
        {
            using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
            object? result = await cmd.ExecuteScalarAsync(cancellationToken);

            if (result is not long count || count == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task ApplyManualMigrationsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        // Lightweight migration runner for SQLite on mobile.
        // Add ALTER TABLE statements here as the schema evolves.
        // Each migration checks for its own precondition to be idempotent.
        await AddColumnIfNotExistsAsync(db, "Loans", "Notes", "TEXT DEFAULT '' NOT NULL", cancellationToken);
        await AddColumnIfNotExistsAsync(db, "Transactions", "Currency", "INTEGER DEFAULT 0 NOT NULL", cancellationToken);
        await AddColumnIfNotExistsAsync(db, "Loans", "Currency", "INTEGER DEFAULT 0 NOT NULL", cancellationToken);
    }

    private static async Task AddColumnIfNotExistsAsync(
        AppDbContext db, string table, string column, string columnDef, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";

        bool columnExists = false;
        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (!columnExists)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDef}";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedCategoriesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        bool hasCategories = await db.Categories.AnyAsync(cancellationToken);
        if (hasCategories)
        {
            return;
        }

        Category[] defaults = new Category[]
        {
            new Category { Id = Guid.NewGuid(), Name = "Food", Icon = "🍔", Color = "#FF6B6B" },
            new Category { Id = Guid.NewGuid(), Name = "Transport", Icon = "🚗", Color = "#4ECDC4" },
            new Category { Id = Guid.NewGuid(), Name = "Salary", Icon = "💰", Color = "#45B7D1" },
            new Category { Id = Guid.NewGuid(), Name = "Rent", Icon = "🏠", Color = "#96CEB4" },
            new Category { Id = Guid.NewGuid(), Name = "Entertainment", Icon = "🎬", Color = "#FFEAA7" },
            new Category { Id = Guid.NewGuid(), Name = "Health", Icon = "💊", Color = "#DDA0DD" },
            new Category { Id = Guid.NewGuid(), Name = "Shopping", Icon = "🛍️", Color = "#98D8C8" },
            new Category { Id = Guid.NewGuid(), Name = "Other", Icon = "📦", Color = "#B0B0B0" },
        };

        await db.Categories.AddRangeAsync(defaults, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
