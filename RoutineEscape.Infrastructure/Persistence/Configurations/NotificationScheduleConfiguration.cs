using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoutineEscape.Domain.Entities;

namespace RoutineEscape.Infrastructure.Persistence.Configurations;

internal sealed class NotificationScheduleConfiguration : IEntityTypeConfiguration<NotificationSchedule>
{
    public void Configure(EntityTypeBuilder<NotificationSchedule> builder)
    {
        builder.ToTable("notification_schedules");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.UserId).HasColumnName("user_id");
        builder.Property(item => item.RecordId).HasColumnName("record_id");
        builder.Property(item => item.Intent).HasColumnName("intent").HasConversion<string>();
        builder.Property(item => item.SourceAt).HasColumnName("source_at");
        builder.Property(item => item.DueAt).HasColumnName("due_at");
        builder.Property(item => item.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(item => item.ActionToken).HasColumnName("action_token");
        builder.Property(item => item.Stage).HasColumnName("stage");
        builder.Property(item => item.Attempts).HasColumnName("attempts");
        builder.Property(item => item.LastSentAt).HasColumnName("last_sent_at");
        builder.HasIndex(item => new { item.Intent, item.RecordId }).IsUnique();
        builder.HasIndex(item => item.ActionToken).IsUnique();
        builder.HasIndex(item => item.NextAttemptAt);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
