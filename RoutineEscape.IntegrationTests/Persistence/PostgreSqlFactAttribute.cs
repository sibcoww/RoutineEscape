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
}
