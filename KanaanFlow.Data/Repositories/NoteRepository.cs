namespace KanaanFlow.Data.Repositories;

using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using KanaanFlow.Data.Db;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class NoteRepository : INoteRepository
{
    private readonly AppDbContext db;

    public NoteRepository(AppDbContext dbContext)
    {
        db = dbContext;
    }

    public async Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken)
    {
        List<Note> items = await db.Notes
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken)
    {
        await db.Notes.AddAsync(note, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
