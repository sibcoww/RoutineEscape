using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoutineEscape.Infrastructure.Persistence;

public sealed class RoutineEscapeDbContextFactory : IDesignTimeDbContextFactory<RoutineEscapeDbContext>
{
    public RoutineEscapeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ROUTINEESCAPE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=routineescape;Username=routineescape;Password=routineescape_dev";
        var options = new DbContextOptionsBuilder<RoutineEscapeDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RoutineEscapeDbContext(options);
    }
}
