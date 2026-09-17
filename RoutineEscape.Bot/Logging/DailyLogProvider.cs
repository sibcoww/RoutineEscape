using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace RoutineEscape.Bot.Logging;

/// <summary>One sanitizing sink for both console and daily local files.</summary>
public sealed class DailyLogProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly object gate = new();
    private readonly string directory;
    private readonly SecretRedactor redactor;
    private readonly TimeProvider clock;
    private readonly TextWriter console;
    private readonly ITimer cleanupTimer;
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();
    private bool disposed;
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public DailyLogProvider(string directory, SecretRedactor redactor, TimeProvider? clock = null, TextWriter? console = null)
    {
        this.directory = Path.GetFullPath(directory);
        this.redactor = redactor;
        this.clock = clock ?? TimeProvider.System;
        this.console = console ?? Console.Out;
        Directory.CreateDirectory(this.directory);
        Cleanup();
        cleanupTimer = this.clock.CreateTimer(_ => Cleanup(), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public ILogger CreateLogger(string categoryName) => new DailyLogger(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

    private void Cleanup()
    {
        lock (gate)
        {
            if (disposed) return;
            try
            {
                var cutoff = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-6);
                foreach (var path in Directory.EnumerateFiles(directory, "routineescape-*.jsonl"))
                {
                    var name = Path.GetFileNameWithoutExtension(path)["routineescape-".Length..];
                    if (DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date < cutoff)
                        File.Delete(path);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                console.WriteLine("RoutineEscape logging: retention cleanup failed (filesystem unavailable).");
            }
        }
    }

    private void Write<TState>(string category, LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // HTTP diagnostics may contain token-bearing URLs even on failures. Never emit them.
        if (category.StartsWith("System.Net.Http", StringComparison.Ordinal)) return;
        lock (gate)
        {
            if (disposed) return;
            Cleanup();
            var timestamp = clock.GetUtcNow();
            var scopeValues = new List<string>();
            scopes.ForEachScope((scope, list) => list.Add(redactor.Redact(scope?.ToString() ?? string.Empty)), scopeValues);
            // Exception messages/data/stack traces can contain credentials. Keep only exception types.
            var errorTypes = new List<string>();
            for (var error = exception; error is not null; error = error.InnerException)
                errorTypes.Add(error.GetType().FullName ?? error.GetType().Name);
            var line = JsonSerializer.Serialize(new
            {
                timestamp, level = level.ToString(), category, eventId = eventId.Id,
                scopes = scopeValues, message = redactor.Redact(formatter(state, null)), errorTypes,
            }, JsonOptions);
            console.WriteLine(line);
            try
            {
                File.AppendAllText(Path.Combine(directory, $"routineescape-{timestamp:yyyy-MM-dd}.jsonl"), line + Environment.NewLine);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                console.WriteLine("RoutineEscape logging: file write failed (filesystem unavailable).");
            }
        }
    }

    public void Dispose()
    {
        lock (gate) disposed = true;
        cleanupTimer.Dispose();
    }

    private sealed class DailyLogger(DailyLogProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner.scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && !category.StartsWith("System.Net.Http", StringComparison.Ordinal);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner.Write(category, logLevel, eventId, state, exception, formatter);
        }
    }
}
