namespace RoutineEscape.IntegrationTests.Persistence;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "ROUTINEESCAPE_TEST_CONNECTION_STRING";

    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Set {ConnectionStringVariable} to run PostgreSQL integration tests.";
        }
    }

    public static void RequireIsolatedDatabase(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        var isolatedDatabase = builder.Database?.StartsWith("routineescape_test", StringComparison.OrdinalIgnoreCase) == true;
        var schema = builder.SearchPath ?? string.Empty;
        var isolatedSchema = System.Text.RegularExpressions.Regex.IsMatch(schema, @"^routineescape_[a-z0-9_]*(?:test|audit)[a-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!isolatedDatabase && !isolatedSchema)
            throw new InvalidOperationException("PostgreSQL tests require a dedicated routineescape_test* database or single routineescape_*test/audit* schema. Never use the live bot database search path.");
    }
}
