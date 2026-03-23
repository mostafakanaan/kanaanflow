namespace KanaanFlow.Core.Abstractions;

using KanaanFlow.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IReceivableRepository
{
    Task<IReadOnlyList<Receivable>> GetAllAsync(CancellationToken cancellationToken);
    Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Receivable receivable, CancellationToken cancellationToken);
    Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
