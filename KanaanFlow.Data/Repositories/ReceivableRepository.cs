namespace KanaanFlow.Data.Repositories;

using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using KanaanFlow.Data.Db;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class ReceivableRepository : IReceivableRepository
{
    private readonly AppDbContext db;

    public ReceivableRepository(AppDbContext dbContext)
    {
        db = dbContext;
    }

    public async Task<IReadOnlyList<Receivable>> GetAllAsync(CancellationToken cancellationToken)
    {
        List<Receivable> items = await db.Receivables
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Receivables.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(Receivable receivable, CancellationToken cancellationToken)
    {
        await db.Receivables.AddAsync(receivable, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken)
    {
        db.Receivables.Update(receivable);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Receivable? existing = await db.Receivables.FindAsync(new object[] { id }, cancellationToken);
        if (existing != null)
        {
            db.Receivables.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
