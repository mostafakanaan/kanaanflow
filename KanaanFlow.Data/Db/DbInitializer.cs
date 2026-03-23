namespace KanaanFlow.Data.Db;

using KanaanFlow.Core.Enums;
using KanaanFlow.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class DbInitializer
{
    private readonly AppDbContext db;

    public DbInitializer(AppDbContext dbContext)
    {
        db = dbContext;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await SeedCategoriesAsync(cancellationToken);
    }

    private async Task SeedCategoriesAsync(CancellationToken cancellationToken)
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
