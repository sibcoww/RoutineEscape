using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoutineEscape.Application.Abstractions.Persistence;

namespace RoutineEscape.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddRoutineEscapePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<RoutineEscapeDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<RoutineEscapeDbContext>());
        return services;
    }
}
