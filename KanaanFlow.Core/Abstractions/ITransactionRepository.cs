namespace KanaanFlow.Core.Abstractions;

using KanaanFlow.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ITransactionRepository
{
    Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken);
    Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken);
}
