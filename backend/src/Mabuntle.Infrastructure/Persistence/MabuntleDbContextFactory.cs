using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;
using System.Text.Json;

namespace Mabuntle.Infrastructure.Persistence;

public sealed class MabuntleDbContextFactory : IDesignTimeDbContextFactory<MabuntleDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=mabuntle;Username=mabuntle;Password=mabuntle_dev_password";

    public MabuntleDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("MABUNTLE_MIGRATIONS_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? TryReadConnectionStringFromApiSettings("appsettings.Development.json")
            ?? TryReadConnectionStringFromApiSettings("appsettings.json")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor(TimeProvider.System))
            .Options;

        return new MabuntleDbContext(options);
    }

    private static string? TryReadConnectionStringFromApiSettings(string fileName)
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(
                currentDirectory.FullName,
                "backend",
                "src",
                "Mabuntle.Api",
                fileName);

            if (File.Exists(candidate))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));

                if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                    && connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
                {
                    return defaultConnection.GetString();
                }
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }
}
