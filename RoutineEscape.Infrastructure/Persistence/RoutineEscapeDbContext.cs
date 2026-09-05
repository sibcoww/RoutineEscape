using Microsoft.EntityFrameworkCore;
using RoutineEscape.Application.Abstractions.Persistence;
using RoutineEscape.Domain.Entities;

namespace RoutineEscape.Infrastructure.Persistence;

public sealed class RoutineEscapeDbContext(DbContextOptions<RoutineEscapeDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<MessageSource> MessageSources => Set<MessageSource>();
    public DbSet<Draft> Drafts => Set<Draft>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoutineEscapeDbContext).Assembly);
}
