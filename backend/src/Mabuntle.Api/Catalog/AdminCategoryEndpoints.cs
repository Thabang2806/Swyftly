using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Admin;
using Mabuntle.Application.Identity;
using Mabuntle.Domain.Catalog;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Catalog;

public static class AdminCategoryEndpoints
{
    public static IEndpointRouteBuilder MapAdminCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/categories")
            .WithTags("Admin Categories")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("", ListAsync)
            .WithName("ListAdminCategories")
            .WithSummary("Returns category hierarchy metadata and category attribute definitions.")
            .Produces<IReadOnlyCollection<AdminCategoryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("", CreateCategoryAsync)
            .WithName("CreateAdminCategory")
            .WithSummary("Creates a catalog category.")
            .Produces<AdminCategoryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{categoryId:guid}", UpdateCategoryAsync)
            .WithName("UpdateAdminCategory")
            .WithSummary("Updates safe catalog category metadata.")
            .Produces<AdminCategoryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{categoryId:guid}/activate", ActivateCategoryAsync)
            .WithName("ActivateAdminCategory")
            .WithSummary("Activates a catalog category.")
            .Produces<AdminCategoryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{categoryId:guid}/deactivate", DeactivateCategoryAsync)
            .WithName("DeactivateAdminCategory")
            .WithSummary("Deactivates a catalog category without deleting it.")
            .Produces<AdminCategoryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{categoryId:guid}/attributes", CreateAttributeAsync)
            .WithName("CreateAdminCategoryAttribute")
            .WithSummary("Creates a category attribute definition.")
            .Produces<AdminCategoryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{categoryId:guid}/attributes/{attributeId:guid}", UpdateAttributeAsync)
            .WithName("UpdateAdminCategoryAttribute")
            .WithSummary("Updates safe category attribute metadata.")
            .Produces<AdminCategoryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{categoryId:guid}/attributes/{attributeId:guid}/activate", ActivateAttributeAsync)
            .WithName("ActivateAdminCategoryAttribute")
            .WithSummary("Activates a category attribute.")
            .Produces<AdminCategoryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{categoryId:guid}/attributes/{attributeId:guid}/deactivate", DeactivateAttributeAsync)
            .WithName("DeactivateAdminCategoryAttribute")
            .WithSummary("Deactivates a category attribute without deleting it.")
            .Produces<AdminCategoryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListAsync(
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var attributes = await dbContext.CategoryAttributes
            .OrderBy(attribute => attribute.DisplayOrder)
            .ThenBy(attribute => attribute.Name)
            .ToListAsync(cancellationToken);

        var attributesByCategory = attributes
            .GroupBy(attribute => attribute.CategoryId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(attribute => new AdminCategoryAttributeResponse(
                        attribute.Id,
                        attribute.Name,
                        attribute.Key,
                        attribute.DataType.ToString(),
                        attribute.IsRequired,
                        attribute.AllowedValues,
                        attribute.DisplayOrder,
                        attribute.IsActive))
                    .ToArray() as IReadOnlyCollection<AdminCategoryAttributeResponse>);

        var productCounts = await dbContext.Products
            .Where(product => product.CategoryId.HasValue)
            .GroupBy(product => product.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);
        var childCounts = await dbContext.Categories
            .Where(category => category.ParentCategoryId.HasValue)
            .GroupBy(category => category.ParentCategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);

        var categories = await dbContext.Categories
            .OrderBy(category => category.ParentCategoryId == null ? 0 : 1)
            .ThenBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new AdminCategoryResponse(
                category.Id,
                category.ParentCategoryId,
                category.Name,
                category.Slug,
                category.DisplayOrder,
                category.IsActive,
                0,
                0,
                Array.Empty<AdminCategoryAttributeResponse>()))
            .ToListAsync(cancellationToken);

        var response = categories
            .Select(category => category with
            {
                Attributes = attributesByCategory.GetValueOrDefault(category.CategoryId)
                    ?? Array.Empty<AdminCategoryAttributeResponse>(),
                ProductCount = productCounts.GetValueOrDefault(category.CategoryId),
                ChildCount = childCounts.GetValueOrDefault(category.CategoryId)
            })
            .ToArray();

        return HttpResults.Ok(response);
    }

    private static async Task<IResult> CreateCategoryAsync(
        UpsertAdminCategoryRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var parentValidation = await ValidateParentAsync(null, request.ParentCategoryId, dbContext, cancellationToken);
        if (parentValidation is not null)
        {
            return parentValidation;
        }

        if (await CategorySlugExistsAsync(request.Slug, null, dbContext, cancellationToken))
        {
            return Conflict("AdminCategories.DuplicateSlug", "A category with this slug already exists.");
        }

        Category category;
        try
        {
            category = new Category(request.ParentCategoryId, request.Name, request.Slug, request.DisplayOrder);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("category", exception.Message);
        }

        dbContext.Categories.Add(category);
        await RecordAuditAsync(
            principal,
            httpContext,
            auditLogService,
            "CategoryCreated",
            "Category",
            category.Id,
            null,
            CategorySnapshot(category),
            "Category created.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetCategoryResponseAsync(category.Id, dbContext, cancellationToken);
        return HttpResults.Created($"/api/admin/categories/{category.Id}", response);
    }

    private static async Task<IResult> UpdateCategoryAsync(
        Guid categoryId,
        UpsertAdminCategoryRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return CategoryNotFound();
        }

        var parentValidation = await ValidateParentAsync(category.Id, request.ParentCategoryId, dbContext, cancellationToken);
        if (parentValidation is not null)
        {
            return parentValidation;
        }

        if (await CategorySlugExistsAsync(request.Slug, category.Id, dbContext, cancellationToken))
        {
            return Conflict("AdminCategories.DuplicateSlug", "A category with this slug already exists.");
        }

        var previousValue = CategorySnapshot(category);
        try
        {
            category.Update(request.ParentCategoryId, request.Name, request.Slug, request.DisplayOrder);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("category", exception.Message);
        }

        await RecordAuditAsync(
            principal,
            httpContext,
            auditLogService,
            "CategoryUpdated",
            "Category",
            category.Id,
            previousValue,
            CategorySnapshot(category),
            "Category updated.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await GetCategoryResponseAsync(category.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> ActivateCategoryAsync(
        Guid categoryId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return CategoryNotFound();
        }

        var previousValue = CategorySnapshot(category);
        category.Activate();
        await RecordAuditAsync(principal, httpContext, auditLogService, "CategoryActivated", "Category", category.Id, previousValue, CategorySnapshot(category), "Category activated.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await GetCategoryResponseAsync(category.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> DeactivateCategoryAsync(
        Guid categoryId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return CategoryNotFound();
        }

        var previousValue = CategorySnapshot(category);
        category.Deactivate();
        await RecordAuditAsync(principal, httpContext, auditLogService, "CategoryDeactivated", "Category", category.Id, previousValue, CategorySnapshot(category), "Category deactivated.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await GetCategoryResponseAsync(category.Id, dbContext, cancellationToken));
    }

    private static async Task<IResult> CreateAttributeAsync(
        Guid categoryId,
        UpsertAdminCategoryAttributeRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            return CategoryNotFound();
        }

        if (!TryParseDataType(request.DataType, out var dataType, out var dataTypeError))
        {
            return Validation("dataType", dataTypeError);
        }

        var key = NormalizeKey(request.Key);
        if (string.IsNullOrWhiteSpace(key))
        {
            return Validation("key", "Attribute key is required.");
        }

        if (await AttributeKeyExistsAsync(categoryId, key, null, dbContext, cancellationToken))
        {
            return Conflict("AdminCategories.DuplicateAttributeKey", "An attribute with this key already exists for the category.");
        }

        var requiredValidation = await ValidateRequiredDoesNotBreakExistingProductsAsync(
            categoryId,
            key,
            isRequiredBefore: false,
            isRequiredAfter: request.IsRequired,
            dbContext,
            cancellationToken);
        if (requiredValidation is not null)
        {
            return requiredValidation;
        }

        CategoryAttribute attribute;
        try
        {
            attribute = new CategoryAttribute(
                categoryId,
                request.Name,
                request.Key,
                dataType,
                request.IsRequired,
                request.AllowedValues,
                request.DisplayOrder);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("attribute", exception.Message);
        }

        dbContext.CategoryAttributes.Add(attribute);
        await RecordAuditAsync(
            principal,
            httpContext,
            auditLogService,
            "CategoryAttributeCreated",
            "CategoryAttribute",
            attribute.Id,
            null,
            AttributeSnapshot(attribute),
            "Category attribute created.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Created(
            $"/api/admin/categories/{categoryId}/attributes/{attribute.Id}",
            await GetCategoryResponseAsync(categoryId, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateAttributeAsync(
        Guid categoryId,
        Guid attributeId,
        UpsertAdminCategoryAttributeRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var attribute = await dbContext.CategoryAttributes
            .SingleOrDefaultAsync(attribute => attribute.Id == attributeId && attribute.CategoryId == categoryId, cancellationToken);
        if (attribute is null)
        {
            return AttributeNotFound();
        }

        if (!TryParseDataType(request.DataType, out var dataType, out var dataTypeError))
        {
            return Validation("dataType", dataTypeError);
        }

        var key = NormalizeKey(request.Key);
        if (string.IsNullOrWhiteSpace(key))
        {
            return Validation("key", "Attribute key is required.");
        }

        if (await AttributeKeyExistsAsync(categoryId, key, attribute.Id, dbContext, cancellationToken))
        {
            return Conflict("AdminCategories.DuplicateAttributeKey", "An attribute with this key already exists for the category.");
        }

        var hasExistingValues = await ProductAttributeValuesExistAsync(categoryId, attribute.Key, dbContext, cancellationToken);
        if (hasExistingValues && !string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("AdminCategories.AttributeKeyInUse", "Attribute key cannot be changed while product attribute values use it.");
        }

        if (hasExistingValues && attribute.DataType != dataType)
        {
            return Conflict("AdminCategories.AttributeTypeInUse", "Attribute data type cannot be changed while product attribute values use it.");
        }

        var allowedValueValidation = await ValidateAllowedValuesDoNotRemoveUsedValuesAsync(
            categoryId,
            attribute.Key,
            attribute.DataType,
            dataType,
            request.AllowedValues,
            dbContext,
            cancellationToken);
        if (allowedValueValidation is not null)
        {
            return allowedValueValidation;
        }

        var requiredValidation = await ValidateRequiredDoesNotBreakExistingProductsAsync(
            categoryId,
            attribute.Key,
            attribute.IsRequired,
            request.IsRequired,
            dbContext,
            cancellationToken);
        if (requiredValidation is not null)
        {
            return requiredValidation;
        }

        var previousValue = AttributeSnapshot(attribute);
        try
        {
            attribute.Update(
                request.Name,
                request.Key,
                dataType,
                request.IsRequired,
                request.AllowedValues,
                request.DisplayOrder);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Validation("attribute", exception.Message);
        }

        await RecordAuditAsync(
            principal,
            httpContext,
            auditLogService,
            "CategoryAttributeUpdated",
            "CategoryAttribute",
            attribute.Id,
            previousValue,
            AttributeSnapshot(attribute),
            "Category attribute updated.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await GetCategoryResponseAsync(categoryId, dbContext, cancellationToken));
    }

    private static async Task<IResult> ActivateAttributeAsync(
        Guid categoryId,
        Guid attributeId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var attribute = await dbContext.CategoryAttributes
            .SingleOrDefaultAsync(attribute => attribute.Id == attributeId && attribute.CategoryId == categoryId, cancellationToken);
        if (attribute is null)
        {
            return AttributeNotFound();
        }

        var previousValue = AttributeSnapshot(attribute);
        attribute.Activate();
        await RecordAuditAsync(principal, httpContext, auditLogService, "CategoryAttributeActivated", "CategoryAttribute", attribute.Id, previousValue, AttributeSnapshot(attribute), "Category attribute activated.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await GetCategoryResponseAsync(categoryId, dbContext, cancellationToken));
    }

    private static async Task<IResult> DeactivateAttributeAsync(
        Guid categoryId,
        Guid attributeId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        MabuntleDbContext dbContext,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var attribute = await dbContext.CategoryAttributes
            .SingleOrDefaultAsync(attribute => attribute.Id == attributeId && attribute.CategoryId == categoryId, cancellationToken);
        if (attribute is null)
        {
            return AttributeNotFound();
        }

        var previousValue = AttributeSnapshot(attribute);
        attribute.Deactivate();
        await RecordAuditAsync(principal, httpContext, auditLogService, "CategoryAttributeDeactivated", "CategoryAttribute", attribute.Id, previousValue, AttributeSnapshot(attribute), "Category attribute deactivated.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(await GetCategoryResponseAsync(categoryId, dbContext, cancellationToken));
    }

    private static async Task<AdminCategoryResponse> GetCategoryResponseAsync(
        Guid categoryId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.AsNoTracking().SingleAsync(category => category.Id == categoryId, cancellationToken);
        var attributes = await dbContext.CategoryAttributes
            .Where(attribute => attribute.CategoryId == category.Id)
            .AsNoTracking()
            .OrderBy(attribute => attribute.DisplayOrder)
            .ThenBy(attribute => attribute.Name)
            .Select(attribute => new AdminCategoryAttributeResponse(
                attribute.Id,
                attribute.Name,
                attribute.Key,
                attribute.DataType.ToString(),
                attribute.IsRequired,
                attribute.AllowedValues,
                attribute.DisplayOrder,
                attribute.IsActive))
            .ToArrayAsync(cancellationToken);
        var productCount = await dbContext.Products.CountAsync(product => product.CategoryId == category.Id, cancellationToken);
        var childCount = await dbContext.Categories.CountAsync(child => child.ParentCategoryId == category.Id, cancellationToken);

        return new AdminCategoryResponse(
            category.Id,
            category.ParentCategoryId,
            category.Name,
            category.Slug,
            category.DisplayOrder,
            category.IsActive,
            productCount,
            childCount,
            attributes);
    }

    private static async Task<IResult?> ValidateParentAsync(
        Guid? categoryId,
        Guid? parentCategoryId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!parentCategoryId.HasValue)
        {
            return null;
        }

        if (parentCategoryId == Guid.Empty)
        {
            return Validation("parentCategoryId", "Parent category id cannot be empty.");
        }

        if (categoryId.HasValue && parentCategoryId == categoryId.Value)
        {
            return Validation("parentCategoryId", "A category cannot be its own parent.");
        }

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Select(category => new { category.Id, category.ParentCategoryId })
            .ToListAsync(cancellationToken);
        if (!categories.Any(category => category.Id == parentCategoryId.Value))
        {
            return Validation("parentCategoryId", "Parent category was not found.");
        }

        if (!categoryId.HasValue)
        {
            return null;
        }

        var parentById = categories.ToDictionary(category => category.Id, category => category.ParentCategoryId);
        var current = parentCategoryId.Value;
        while (parentById.TryGetValue(current, out var nextParent) && nextParent.HasValue)
        {
            if (nextParent.Value == categoryId.Value)
            {
                return Validation("parentCategoryId", "A category cannot be moved under one of its descendants.");
            }

            current = nextParent.Value;
        }

        return null;
    }

    private static Task<bool> CategorySlugExistsAsync(
        string? slug,
        Guid? exceptCategoryId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = slug?.Trim().ToLowerInvariant();
        return dbContext.Categories.AnyAsync(
            category => category.Slug == normalizedSlug && (!exceptCategoryId.HasValue || category.Id != exceptCategoryId.Value),
            cancellationToken);
    }

    private static Task<bool> AttributeKeyExistsAsync(
        Guid categoryId,
        string key,
        Guid? exceptAttributeId,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();
        return dbContext.CategoryAttributes.AnyAsync(
            attribute => attribute.CategoryId == categoryId
                && attribute.Key == normalizedKey
                && (!exceptAttributeId.HasValue || attribute.Id != exceptAttributeId.Value),
            cancellationToken);
    }

    private static Task<bool> ProductAttributeValuesExistAsync(
        Guid categoryId,
        string key,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.ProductAttributeValues.AnyAsync(
            attribute => attribute.Key == key
                && dbContext.Products.Any(product => product.Id == attribute.ProductId && product.CategoryId == categoryId),
            cancellationToken);

    private static async Task<IResult?> ValidateAllowedValuesDoNotRemoveUsedValuesAsync(
        Guid categoryId,
        string key,
        CategoryAttributeDataType currentDataType,
        CategoryAttributeDataType nextDataType,
        IReadOnlyCollection<string>? nextAllowedValues,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (currentDataType is not (CategoryAttributeDataType.Select or CategoryAttributeDataType.MultiSelect)
            || nextDataType is not (CategoryAttributeDataType.Select or CategoryAttributeDataType.MultiSelect))
        {
            return null;
        }

        var allowed = (nextAllowedValues ?? [])
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedValues = await dbContext.ProductAttributeValues
            .Where(attribute => attribute.Key == key
                && dbContext.Products.Any(product => product.Id == attribute.ProductId && product.CategoryId == categoryId))
            .Select(attribute => attribute.ValueJson)
            .ToListAsync(cancellationToken);

        var removedUsedValues = usedValues
            .SelectMany(ReadStringValues)
            .Where(value => !allowed.Contains(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return removedUsedValues.Length == 0
            ? null
            : Conflict("AdminCategories.AllowedValueInUse", $"Allowed values are in use and cannot be removed: {string.Join(", ", removedUsedValues)}.");
    }

    private static async Task<IResult?> ValidateRequiredDoesNotBreakExistingProductsAsync(
        Guid categoryId,
        string key,
        bool isRequiredBefore,
        bool isRequiredAfter,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (isRequiredBefore || !isRequiredAfter)
        {
            return null;
        }

        var productsWithoutValue = await dbContext.Products.CountAsync(
            product => product.CategoryId == categoryId
                && !dbContext.ProductAttributeValues.Any(attribute => attribute.ProductId == product.Id && attribute.Key == key),
            cancellationToken);

        return productsWithoutValue == 0
            ? null
            : Conflict("AdminCategories.RequiredAttributeMissingValues", "Attribute cannot be required while existing products in the category are missing this value.");
    }

    private static IReadOnlyCollection<string> ReadStringValues(string valueJson)
    {
        using var document = JsonDocument.Parse(valueJson);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(document.RootElement.GetString())
                ? []
                : [document.RootElement.GetString()!.Trim()],
            JsonValueKind.Array => document.RootElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                .Select(item => item.GetString()!.Trim())
                .ToArray(),
            _ => []
        };
    }

    private static bool TryParseDataType(
        string? dataType,
        out CategoryAttributeDataType parsed,
        out string error)
    {
        parsed = default;
        error = string.Empty;
        if (!Enum.TryParse(dataType, ignoreCase: true, out parsed) || !Enum.IsDefined(parsed))
        {
            error = "Data type must be Text, Number, Decimal, Boolean, Select, MultiSelect, or Date.";
            return false;
        }

        return true;
    }

    private static string NormalizeKey(string? key) => key?.Trim().ToLowerInvariant() ?? string.Empty;

    private static CategoryAuditSnapshot CategorySnapshot(Category category) =>
        new(category.ParentCategoryId, category.Name, category.Slug, category.DisplayOrder, category.IsActive);

    private static CategoryAttributeAuditSnapshot AttributeSnapshot(CategoryAttribute attribute) =>
        new(
            attribute.CategoryId,
            attribute.Name,
            attribute.Key,
            attribute.DataType.ToString(),
            attribute.IsRequired,
            attribute.AllowedValues,
            attribute.DisplayOrder,
            attribute.IsActive);

    private static async Task RecordAuditAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IAuditLogService auditLogService,
        string actionType,
        string entityType,
        Guid entityId,
        object? previousValue,
        object? newValue,
        string reason,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                GetActorRole(principal),
                actionType,
                entityType,
                entityId.ToString(),
                previousValue is null ? null : JsonSerializer.Serialize(previousValue),
                newValue is null ? null : JsonSerializer.Serialize(newValue),
                reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static string GetActorRole(ClaimsPrincipal principal) =>
        principal.IsInRole(MabuntleRoles.SuperAdmin)
            ? MabuntleRoles.SuperAdmin
            : MabuntleRoles.Admin;

    private static IResult CategoryNotFound() =>
        HttpResults.Problem(
            title: "AdminCategories.NotFound",
            detail: "Category was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult AttributeNotFound() =>
        HttpResults.Problem(
            title: "AdminCategories.AttributeNotFound",
            detail: "Category attribute was not found.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult Conflict(string title, string detail) =>
        HttpResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });
}

public sealed record AdminCategoryResponse(
    Guid CategoryId,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    int DisplayOrder,
    bool IsActive,
    int ProductCount,
    int ChildCount,
    IReadOnlyCollection<AdminCategoryAttributeResponse> Attributes);

public sealed record AdminCategoryAttributeResponse(
    Guid AttributeId,
    string Name,
    string Key,
    string DataType,
    bool IsRequired,
    IReadOnlyCollection<string> AllowedValues,
    int DisplayOrder,
    bool IsActive);

public sealed record UpsertAdminCategoryRequest(
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    int DisplayOrder);

public sealed record UpsertAdminCategoryAttributeRequest(
    string Name,
    string Key,
    string DataType,
    bool IsRequired,
    IReadOnlyCollection<string>? AllowedValues,
    int DisplayOrder);

internal sealed record CategoryAuditSnapshot(
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    int DisplayOrder,
    bool IsActive);

internal sealed record CategoryAttributeAuditSnapshot(
    Guid CategoryId,
    string Name,
    string Key,
    string DataType,
    bool IsRequired,
    IReadOnlyCollection<string> AllowedValues,
    int DisplayOrder,
    bool IsActive);
