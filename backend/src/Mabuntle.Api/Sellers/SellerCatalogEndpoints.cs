using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Sellers;

public static class SellerCatalogEndpoints
{
    public static IEndpointRouteBuilder MapSellerCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seller/catalog")
            .WithTags("Seller Catalog")
            .RequireAuthorization(MabuntlePolicies.SellerOnly);

        group.MapGet("/categories", ListCategoriesAsync)
            .WithName("ListSellerCatalogCategories")
            .WithSummary("Returns active categories and attribute definitions for seller product forms.")
            .Produces<IReadOnlyCollection<SellerCatalogCategoryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> ListCategoriesAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var attributes = await dbContext.CategoryAttributes
            .Where(attribute => attribute.IsActive)
            .OrderBy(attribute => attribute.DisplayOrder)
            .ThenBy(attribute => attribute.Name)
            .ToListAsync(cancellationToken);

        var attributesByCategory = attributes
            .GroupBy(attribute => attribute.CategoryId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(attribute => new SellerCatalogCategoryAttributeResponse(
                        attribute.Id,
                        attribute.Name,
                        attribute.Key,
                        attribute.DataType.ToString(),
                        attribute.IsRequired,
                        attribute.AllowedValues,
                        attribute.DisplayOrder))
                    .ToArray() as IReadOnlyCollection<SellerCatalogCategoryAttributeResponse>);

        var categories = await dbContext.Categories
            .Where(category => category.IsActive)
            .OrderBy(category => category.ParentCategoryId == null ? 0 : 1)
            .ThenBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new SellerCatalogCategoryResponse(
                category.Id,
                category.ParentCategoryId,
                category.Name,
                category.Slug,
                category.DisplayOrder,
                Array.Empty<SellerCatalogCategoryAttributeResponse>()))
            .ToListAsync(cancellationToken);

        var response = categories
            .Select(category => category with
            {
                Attributes = attributesByCategory.GetValueOrDefault(category.CategoryId)
                    ?? Array.Empty<SellerCatalogCategoryAttributeResponse>()
            })
            .ToArray();

        return HttpResults.Ok(response);
    }
}

public sealed record SellerCatalogCategoryResponse(
    Guid CategoryId,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    int DisplayOrder,
    IReadOnlyCollection<SellerCatalogCategoryAttributeResponse> Attributes);

public sealed record SellerCatalogCategoryAttributeResponse(
    Guid AttributeId,
    string Name,
    string Key,
    string DataType,
    bool IsRequired,
    IReadOnlyCollection<string> AllowedValues,
    int DisplayOrder);
