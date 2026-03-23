namespace KanaanFlow.Data.Repositories;

using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using KanaanFlow.Data.Db;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class LoanRepository : ILoanRepository
{
    private readonly AppDbContext db;

    public LoanRepository(AppDbContext dbContext)
    {
        db = dbContext;
    }

    public async Task<IReadOnlyList<Loan>> GetAllAsync(CancellationToken cancellationToken)
    {
        List<Loan> items = await db.Loans
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Loans.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(Loan loan, CancellationToken cancellationToken)
    {
        await db.Loans.AddAsync(loan, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Loan loan, CancellationToken cancellationToken)
    {
        db.Loans.Update(loan);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Loan? existing = await db.Loans.FindAsync(new object[] { id }, cancellationToken);
        if (existing != null)
        {
            db.Loans.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
