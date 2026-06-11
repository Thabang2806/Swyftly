namespace Mabuntle.Domain.Catalog;

public sealed record CategoryAttributeValidationResult(IReadOnlyCollection<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static CategoryAttributeValidationResult Success { get; } = new([]);
}
