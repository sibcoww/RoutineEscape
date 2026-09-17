using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoutineEscape.Infrastructure.Persistence;

public sealed class RoutineEscapeDbContextFactory : IDesignTimeDbContextFactory<RoutineEscapeDbContext>
{
    public RoutineEscapeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ROUTINEESCAPE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ROUTINEESCAPE_CONNECTION_STRING before running EF migrations.");
        var options = new DbContextOptionsBuilder<RoutineEscapeDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RoutineEscapeDbContext(options);
    }
}
