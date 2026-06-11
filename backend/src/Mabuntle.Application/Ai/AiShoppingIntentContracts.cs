using Mabuntle.Application.Common.Results;

namespace Mabuntle.Application.Ai;

public interface IAiShoppingIntentService
{
    Task<Result<ShoppingIntent>> ExtractIntentAsync(
        ShoppingIntentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAiShoppingIntentProvider
{
    Task<ShoppingIntent> ExtractIntentAsync(
        ShoppingIntentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ShoppingIntentExtractionRequest(
    string BuyerMessage,
    string? UserId = null);

public sealed record ShoppingIntent(
    string? Category,
    string? Subcategory,
    decimal? BudgetMax,
    decimal? BudgetMin,
    string? Size,
    string? Colour,
    string? Occasion,
    string? Style,
    string? Material,
    string? Brand,
    string? BeautySkinType,
    string? BeautyConcern,
    string SearchText,
    bool IsVague,
    string? ClarificationPrompt);
