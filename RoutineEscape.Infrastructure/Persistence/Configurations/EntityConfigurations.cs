using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoutineEscape.Domain.Entities;

namespace RoutineEscape.Infrastructure.Persistence.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.TelegramUserId).HasColumnName("telegram_user_id");
        builder.Property(entity => entity.Username).HasColumnName("username").HasMaxLength(64);
        builder.Property(entity => entity.FirstName).HasColumnName("first_name").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.LastName).HasColumnName("last_name").HasMaxLength(128);
        builder.Property(entity => entity.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Language).HasColumnName("language").HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(entity => entity.TelegramUserId).IsUnique();
    }
}

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        ConfigureOwnedEntity(builder);
        builder.Property(entity => entity.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(entity => entity.DeadlineUtc).HasColumnName("deadline_utc");
        builder.Property(entity => entity.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.OriginalText).HasColumnName("original_text").HasMaxLength(8000);
        builder.Property(entity => entity.CompletedAt).HasColumnName("completed_at");
        builder.HasIndex(entity => new { entity.UserId, entity.Status });
        builder.HasIndex(entity => entity.DeadlineUtc);
    }

    private static void ConfigureOwnedEntity(EntityTypeBuilder<TaskItem> builder) =>
        RelationalConfiguration.ConfigureUserOwned(builder);
}

internal sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events");
        RelationalConfiguration.ConfigureUserOwned(builder);
        builder.Property(entity => entity.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(entity => entity.StartUtc).HasColumnName("start_utc");
        builder.Property(entity => entity.EndUtc).HasColumnName("end_utc");
        builder.Property(entity => entity.Location).HasColumnName("location").HasMaxLength(500);
        builder.Property(entity => entity.ExternalCalendarId).HasColumnName("external_calendar_id").HasMaxLength(512);
        builder.HasIndex(entity => new { entity.UserId, entity.StartUtc });
    }
}

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders");
        RelationalConfiguration.ConfigureUserOwned(builder);
        builder.Property(entity => entity.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Property(entity => entity.TriggerAtUtc).HasColumnName("trigger_at_utc");
        builder.Property(entity => entity.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.TriggeredAt).HasColumnName("triggered_at");
        builder.Ignore(entity => entity.IsTriggered);
        builder.HasIndex(entity => new { entity.Status, entity.TriggerAtUtc });
    }
}

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        RelationalConfiguration.ConfigureUserOwned(builder);
        builder.Property(entity => entity.Title).HasColumnName("title").HasMaxLength(500);
        builder.Property(entity => entity.Content).HasColumnName("content").HasMaxLength(8000).IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        builder.Property<List<string>>("_tags").HasColumnName("tags").HasColumnType("text[]");
        builder.Ignore(entity => entity.Tags);
    }
}

internal sealed class MessageSourceConfiguration : IEntityTypeConfiguration<MessageSource>
{
    public void Configure(EntityTypeBuilder<MessageSource> builder)
    {
        builder.ToTable("message_sources");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.TelegramUserId).HasColumnName("telegram_user_id");
        builder.Property(entity => entity.TelegramChatId).HasColumnName("telegram_chat_id");
        builder.Property(entity => entity.TelegramMessageId).HasColumnName("telegram_message_id");
        builder.Property(entity => entity.Username).HasColumnName("username").HasMaxLength(64);
        builder.Property(entity => entity.DisplayName).HasColumnName("display_name").HasMaxLength(256);
        builder.Property(entity => entity.ChatTitle).HasColumnName("chat_title").HasMaxLength(256);
        builder.Property(entity => entity.AuthorSignature).HasColumnName("author_signature").HasMaxLength(256);
        builder.Property(entity => entity.OriginalMessageDateUtc).HasColumnName("original_message_date_utc");
        builder.Property(entity => entity.IsHiddenUser).HasColumnName("is_hidden_user");
        builder.Property(entity => entity.RawMetadataJson).HasColumnName("raw_metadata_json").HasColumnType("jsonb");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
    }
}

internal sealed class DraftConfiguration : IEntityTypeConfiguration<Draft>
{
    public void Configure(EntityTypeBuilder<Draft> builder)
    {
        builder.ToTable("drafts");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.UserId).HasColumnName("user_id");
        builder.Property(entity => entity.TelegramMessageId).HasColumnName("telegram_message_id");
        builder.Property(entity => entity.Intent).HasColumnName("intent").HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Confidence).HasColumnName("confidence").HasPrecision(5, 4);
        builder.Property(entity => entity.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
        builder.Property(entity => entity.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.ExpiresAt).HasColumnName("expires_at");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => new { entity.UserId, entity.TelegramMessageId });
        builder.HasIndex(entity => new { entity.Status, entity.ExpiresAt });
    }
}
