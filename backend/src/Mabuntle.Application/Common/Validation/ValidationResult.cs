using Mabuntle.Application.Common.Errors;

namespace Mabuntle.Application.Common.Validation;

public sealed class ValidationResult
{
    private ValidationResult(IReadOnlyList<ValidationFailure> failures)
    {
        Failures = failures;
    }

    public IReadOnlyList<ValidationFailure> Failures { get; }

    public bool IsValid => Failures.Count == 0;

    public static ValidationResult Success() => new([]);

    public static ValidationResult Failure(params ValidationFailure[] failures) =>
        new(failures);

    public static ValidationResult Failure(IEnumerable<ValidationFailure> failures) =>
        new(failures.ToArray());

    public Error ToError() => IsValid
        ? Error.None
        : Error.Validation(Failures);
}
