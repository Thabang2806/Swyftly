namespace Mabuntle.Application.Common.Validation;

public interface IValidator<in TRequest>
{
    ValidationResult Validate(TRequest request);
}
