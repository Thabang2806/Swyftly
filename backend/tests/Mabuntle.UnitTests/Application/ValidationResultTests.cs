using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Validation;

namespace Mabuntle.UnitTests.Application;

public class ValidationResultTests
{
    [Fact]
    public void Success_HasNoFailures()
    {
        var result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
        Assert.Equal(Error.None, result.ToError());
    }

    [Fact]
    public void Failure_AggregatesFailuresIntoValidationError()
    {
        var result = ValidationResult.Failure(
            new ValidationFailure("Name", "Name is required."),
            new ValidationFailure("Name", "Name is too short."),
            new ValidationFailure("Price", "Price must be positive."));

        var error = result.ToError();

        Assert.False(result.IsValid);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.NotNull(error.Details);
        Assert.Equal(2, error.Details["Name"].Length);
        Assert.Single(error.Details["Price"]);
    }
}
