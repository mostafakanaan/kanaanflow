namespace KanaanFlow.Data.Db;

using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

public sealed class DbInitializer(AppDbContext dbContext)
{
    private readonly AppDbContext db = dbContext;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
