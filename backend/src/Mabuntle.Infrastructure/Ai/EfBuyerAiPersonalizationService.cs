using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Ai;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Ai;

public sealed class EfBuyerAiPersonalizationService(MabuntleDbContext dbContext) : IBuyerAiPersonalizationService
{
    public async Task<IReadOnlyList<BuyerAiPersonalizationResult>> PersonalizeAsync(
        Guid userId,
        IReadOnlyCollection<Guid> productIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || productIds.Count == 0)
        {
            return [];
        }

        var buyer = await dbContext.BuyerProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => new { profile.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (buyer is null)
        {
            return [];
        }

        var preference = await dbContext.BuyerAiDiscoveryPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.BuyerId == buyer.Id, cancellationToken);
        if (preference?.PersonalizationEnabled != true)
        {
            return [];
        }

        var candidateProductIds = productIds.Distinct().ToArray();
        var candidateCategories = await dbContext.Products
            .AsNoTracking()
            .Where(product => candidateProductIds.Contains(product.Id))
            .Select(product => new { product.Id, product.CategoryId })
            .ToDictionaryAsync(product => product.Id, product => product.CategoryId, cancellationToken);

        var wishlistSignals = await dbContext.BuyerWishlistItems
            .AsNoTracking()
            .Where(item => item.BuyerId == buyer.Id)
            .Join(
                dbContext.Products.AsNoTracking(),
                item => item.ProductId,
                product => product.Id,
                (item, product) => new { product.Id, product.CategoryId })
            .ToListAsync(cancellationToken);
        var wishlistProductIds = wishlistSignals.Select(signal => signal.Id).ToHashSet();
        var wishlistCategoryIds = wishlistSignals.Where(signal => signal.CategoryId.HasValue).Select(signal => signal.CategoryId!.Value).ToHashSet();

        var cartSignals = await dbContext.Carts
            .AsNoTracking()
            .Where(cart => cart.BuyerId == buyer.Id)
            .SelectMany(cart => cart.Items.Select(item => item.ProductId))
            .Join(
                dbContext.Products.AsNoTracking(),
                productId => productId,
                product => product.Id,
                (productId, product) => new { product.Id, product.CategoryId })
            .ToListAsync(cancellationToken);
        var orderSignals = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.BuyerId == buyer.Id)
            .SelectMany(order => order.Items.Select(item => item.ProductId))
            .Join(
                dbContext.Products.AsNoTracking(),
                productId => productId,
                product => product.Id,
                (productId, product) => new { product.Id, product.CategoryId })
            .ToListAsync(cancellationToken);
        var shoppingProductIds = cartSignals.Concat(orderSignals).Select(signal => signal.Id).ToHashSet();
        var shoppingCategoryIds = cartSignals.Concat(orderSignals).Where(signal => signal.CategoryId.HasValue).Select(signal => signal.CategoryId!.Value).ToHashSet();

        var historySignals = preference.HistoryEnabled
            ? await dbContext.BuyerAiDiscoveryHistory
                .AsNoTracking()
                .Where(item => item.BuyerId == buyer.Id)
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(20)
                .Select(item => new
                {
                    item.Category,
                    item.ProductIds
                })
                .ToListAsync(cancellationToken)
            : [];
        var historyProductIds = historySignals.SelectMany(signal => signal.ProductIds).ToHashSet();
        var historyCategories = historySignals
            .Select(signal => signal.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidateCategoryIds = candidateCategories.Values
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var categoryNames = historyCategories.Count == 0 || candidateCategoryIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Categories
                .AsNoTracking()
                .Where(category => candidateCategoryIds.Contains(category.Id))
                .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        var results = new List<BuyerAiPersonalizationResult>();
        foreach (var productId in candidateProductIds)
        {
            candidateCategories.TryGetValue(productId, out var categoryId);
            var reasons = new List<string>();
            var score = 0;

            if (wishlistProductIds.Contains(productId))
            {
                score += 4;
                reasons.Add("Similar to saved items");
            }

            if (categoryId.HasValue && wishlistCategoryIds.Contains(categoryId.Value))
            {
                score += 2;
                reasons.Add("Similar to saved items");
            }

            if (shoppingProductIds.Contains(productId))
            {
                score += 3;
                reasons.Add("Matches recent cart interest");
            }

            if (categoryId.HasValue && shoppingCategoryIds.Contains(categoryId.Value))
            {
                score += 1;
                reasons.Add("Matches recent cart interest");
            }

            if (historyProductIds.Contains(productId))
            {
                score += 2;
                reasons.Add("Aligned with your enabled AI history");
            }

            if (categoryId.HasValue
                && categoryNames.TryGetValue(categoryId.Value, out var categoryName)
                && historyCategories.Contains(categoryName))
            {
                score += 1;
                reasons.Add("Aligned with your enabled AI history");
            }

            if (score > 0)
            {
                results.Add(new BuyerAiPersonalizationResult(
                    productId,
                    score,
                    PersonalizationApplied: true,
                    reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
            }
        }

        return results;
    }
}
