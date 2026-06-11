namespace Mabuntle.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("MABUNTLE_RUN_POSTGRES_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set MABUNTLE_RUN_POSTGRES_TESTS=true to run PostgreSQL integration tests.";
        }
    }
}
