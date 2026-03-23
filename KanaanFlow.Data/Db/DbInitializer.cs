namespace KanaanFlow.Data.Db;

using Microsoft.EntityFrameworkCore;
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
    }
}
