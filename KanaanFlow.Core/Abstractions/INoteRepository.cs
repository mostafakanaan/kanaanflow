namespace KanaanFlow.Core.Abstractions;

using KanaanFlow.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface INoteRepository
{
    Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken);

    Task AddAsync(Note note, CancellationToken cancellationToken);
}
