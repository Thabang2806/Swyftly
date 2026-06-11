using System.Text.Json;
using Mabuntle.Application.Ai;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Domain.Ai;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Ai;

public sealed class AiListingAssistantService(
    IAiProviderClient aiProviderClient,
    AiPromptBuilder promptBuilder,
    AiSuggestionValidator suggestionValidator,
    AiUsageLogger usageLogger,
    MabuntleDbContext dbContext,
    TimeProvider timeProvider) : IAiListingAssistantService
{
    private const string FeatureName = "ListingAssistant";

    public async Task<Result<AiListingSuggestionResponse>> GenerateSuggestionAsync(
        AiListingAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SellerId == Guid.Empty)
        {
            return Result<AiListingSuggestionResponse>.Failure(Error.Validation([
                new("sellerId", "Seller id is required.")
            ]));
        }

        if (request.ProductId == Guid.Empty)
        {
            return Result<AiListingSuggestionResponse>.Failure(Error.Validation([
                new("productId", "Product id is required.")
            ]));
        }

        var prompt = promptBuilder.Build(request);
        var userId = string.IsNullOrWhiteSpace(request.UserId)
            ? request.SellerId.ToString()
            : request.UserId;
        AiProviderResponse providerResponse;

        try
        {
            providerResponse = await aiProviderClient.GenerateListingSuggestionAsync(
                new AiProviderRequest(prompt, request.PromptVersion),
                cancellationToken);
        }
        catch (Exception exception)
        {
            await usageLogger.LogAsync(
                FeatureName,
                userId,
                request.SellerId,
                "unknown",
                null,
                null,
                null,
                0,
                success: false,
                exception.Message,
                cancellationToken);

            return Result<AiListingSuggestionResponse>.Failure(Error.Failure(
                "AiListingAssistant.ProviderFailed",
                "The AI provider failed while generating a listing suggestion."));
        }

        var validationResult = suggestionValidator.Validate(providerResponse.Json, request);
        if (validationResult.IsFailure)
        {
            await usageLogger.LogAsync(
                FeatureName,
                userId,
                request.SellerId,
                providerResponse.ModelUsed,
                providerResponse.InputTokenEstimate,
                providerResponse.OutputTokenEstimate,
                providerResponse.CostEstimate,
                providerResponse.LatencyMs,
                success: false,
                validationResult.Error.Description,
                cancellationToken);

            return Result<AiListingSuggestionResponse>.Failure(validationResult.Error);
        }

        var validated = validationResult.Value;
        var now = timeProvider.GetUtcNow();
        var suggestion = new AiProductSuggestion(
            request.SellerId,
            request.ProductId,
            request.SellerNotes,
            JsonSerializer.Serialize(request.ImageReferences.Select(image => image.ImageId)),
            validated.SuggestedTitle,
            validated.SuggestedShortDescription,
            validated.SuggestedFullDescription,
            validated.SuggestedCategoryId,
            validated.SuggestedCategoryPath,
            validated.SuggestedAttributesJson,
            validated.SuggestedTagsJson,
            validated.MissingFieldsJson,
            validated.RiskFlagsJson,
            validated.QualityScore,
            providerResponse.ModelUsed,
            request.PromptVersion,
            now);

        dbContext.AiProductSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync(cancellationToken);

        await usageLogger.LogAsync(
            FeatureName,
            userId,
            request.SellerId,
            providerResponse.ModelUsed,
            providerResponse.InputTokenEstimate,
            providerResponse.OutputTokenEstimate,
            providerResponse.CostEstimate,
            providerResponse.LatencyMs,
            success: true,
            errorMessage: null,
            cancellationToken);

        return Result<AiListingSuggestionResponse>.Success(new AiListingSuggestionResponse(
            suggestion.Id,
            suggestion.SellerId,
            suggestion.ProductId,
            suggestion.SuggestedTitle,
            suggestion.SuggestedShortDescription,
            suggestion.SuggestedFullDescription,
            suggestion.SuggestedCategoryId,
            suggestion.SuggestedCategoryPath,
            validated.SuggestedAttributes,
            validated.SuggestedTags,
            validated.MissingFields,
            validated.RiskFlags,
            suggestion.QualityScore,
            suggestion.ModelUsed,
            suggestion.PromptVersion,
            suggestion.Status.ToString(),
            suggestion.CreatedAtUtc,
            suggestion.AcceptedAtUtc,
            suggestion.AppliedAtUtc));
    }
}
