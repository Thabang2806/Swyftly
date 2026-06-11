using Mabuntle.Application.Common.Validation;

namespace Mabuntle.Application.Common.Errors;

public sealed record Error(
    string Code,
    string Description,
    ErrorType Type = ErrorType.Failure,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static readonly Error None = new(
        string.Empty,
        string.Empty,
        ErrorType.None);

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);

    public static Error Validation(IReadOnlyCollection<ValidationFailure> failures)
    {
        var details = failures
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        return new(
            "Validation.Failed",
            "One or more validation errors occurred.",
            ErrorType.Validation,
            details);
    }
}
