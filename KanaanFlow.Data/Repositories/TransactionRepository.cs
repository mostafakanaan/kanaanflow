namespace KanaanFlow.Data.Repositories;

using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using KanaanFlow.Data.Db;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext db;

    public TransactionRepository(AppDbContext dbContext)
    {
        db = dbContext;
    }

    public async Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken)
    {
        List<Transaction> items = await db.Transactions
            .Include(x => x.Category)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Transactions
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        await db.Transactions.AddAsync(transaction, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        db.Transactions.Update(transaction);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Transaction? existing = await db.Transactions.FindAsync(new object[] { id }, cancellationToken);
        if (existing != null)
        {
            db.Transactions.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        List<Transaction> items = await db.Transactions
            .Include(x => x.Category)
            .Where(x => x.Date >= from && x.Date <= to)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);
        return items;
    }
}
