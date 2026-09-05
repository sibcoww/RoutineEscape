using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoutineEscape.Domain.Common;
using RoutineEscape.Domain.Entities;

namespace RoutineEscape.Infrastructure.Persistence.Configurations;

internal static class RelationalConfiguration
{
    public static void ConfigureUserOwned<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IEntity
    {
        builder.HasKey(nameof(IEntity.Id));
        builder.Property(nameof(IEntity.Id)).HasColumnName("id").ValueGeneratedNever();
        builder.Property("UserId").HasColumnName("user_id");
        builder.Property("SourceId").HasColumnName("source_id");
        builder.Property("CreatedAt").HasColumnName("created_at");
        builder.HasOne<AppUser>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MessageSource>().WithMany().HasForeignKey("SourceId").OnDelete(DeleteBehavior.SetNull);
    }
}
