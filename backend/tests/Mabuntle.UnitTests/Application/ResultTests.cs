using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;

namespace Mabuntle.UnitTests.Application;

public class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResultWithoutError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_CreatesFailedResultWithError()
    {
        var error = Error.Failure("Test.Failed", "The test failed.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result<string>.Success("value");

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public void GenericFailure_ThrowsWhenValueIsAccessed()
    {
        var result = Result<string>.Failure(Error.NotFound("Test.NotFound", "Not found."));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
