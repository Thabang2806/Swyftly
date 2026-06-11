using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mabuntle.Infrastructure.Persistence;

public static class CatalogSeeder
{
    public static async Task TrySeedCatalogAsync(
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        try
        {
            await SeedCatalogAsync(serviceProvider);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Catalog seed data could not be applied. Ensure database migrations are applied before using catalog endpoints.");
        }
    }

    public static async Task SeedCatalogAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MabuntleDbContext>();

        var existingCategoryIds = await dbContext.Categories
            .Select(category => category.Id)
            .ToListAsync();
        var missingCategories = CatalogSeedData.CreateCategories()
            .Where(category => !existingCategoryIds.Contains(category.Id))
            .ToArray();

        if (missingCategories.Length > 0)
        {
            dbContext.Categories.AddRange(missingCategories);
        }

        var existingAttributeIds = await dbContext.CategoryAttributes
            .Select(attribute => attribute.Id)
            .ToListAsync();
        var missingAttributes = CatalogSeedData.CreateCategoryAttributes()
            .Where(attribute => !existingAttributeIds.Contains(attribute.Id))
            .ToArray();

        if (missingAttributes.Length > 0)
        {
            dbContext.CategoryAttributes.AddRange(missingAttributes);
        }

        if (missingCategories.Length > 0 || missingAttributes.Length > 0)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
