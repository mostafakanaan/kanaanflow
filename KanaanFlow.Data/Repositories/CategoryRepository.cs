namespace KanaanFlow.Data.Repositories;

using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using KanaanFlow.Data.Db;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext db;

    public CategoryRepository(AppDbContext dbContext)
    {
        db = dbContext;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken)
    {
        List<Category> items = await db.Categories
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Categories.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        await db.Categories.AddAsync(category, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken)
    {
        db.Categories.Update(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Category? existing = await db.Categories.FindAsync(new object[] { id }, cancellationToken);
        if (existing != null)
        {
            db.Categories.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
