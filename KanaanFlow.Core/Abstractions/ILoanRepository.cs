namespace KanaanFlow.Core.Abstractions;

using KanaanFlow.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ILoanRepository
{
    Task<IReadOnlyList<Loan>> GetAllAsync(CancellationToken cancellationToken);
    Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Loan loan, CancellationToken cancellationToken);
    Task UpdateAsync(Loan loan, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
