using Mabuntle.Application.Ai;
using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;

namespace Mabuntle.Infrastructure.Ai;

public sealed class AiShoppingIntentService(IAiShoppingIntentProvider provider) : IAiShoppingIntentService
{
    public async Task<Result<ShoppingIntent>> ExtractIntentAsync(
        ShoppingIntentExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BuyerMessage))
        {
            return Result<ShoppingIntent>.Failure(Error.Validation([
                new("buyerMessage", "A shopping request is required.")
            ]));
        }

        try
        {
            var intent = await provider.ExtractIntentAsync(request, cancellationToken);
            return Result<ShoppingIntent>.Success(intent);
        }
        catch (Exception exception)
        {
            return Result<ShoppingIntent>.Failure(Error.Failure(
                "AiShoppingIntent.ProviderFailed",
                $"The AI intent provider failed: {exception.Message}"));
        }
    }
}
