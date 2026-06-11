namespace Mabuntle.Application.Ai;

public interface IBuyerAiPersonalizationService
{
    Task<IReadOnlyList<BuyerAiPersonalizationResult>> PersonalizeAsync(
        Guid userId,
        IReadOnlyCollection<Guid> productIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed record BuyerAiPersonalizationResult(
    Guid ProductId,
    int Score,
    bool PersonalizationApplied,
    IReadOnlyCollection<string> PersonalizationReasons);
