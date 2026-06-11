using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Mabuntle.Application.Identity;

namespace Mabuntle.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task TrySeedIdentityRolesAsync(
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        try
        {
            await SeedIdentityRolesAsync(serviceProvider);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Identity roles could not be seeded. Ensure the database is available and migrations are applied before using auth endpoints.");
        }
    }

    public static async Task SeedIdentityRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in MabuntleRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));

                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                    throw new InvalidOperationException($"Failed to seed role '{role}': {errors}");
                }
            }
        }
    }
}
