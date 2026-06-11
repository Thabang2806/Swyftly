using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Results;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        return result.IsSuccess
            ? HttpResults.NoContent()
            : ToProblemResult(result.Error);
    }

    public static IResult ToHttpResult<T>(
        this Result<T> result,
        Func<T, IResult>? onSuccess = null)
    {
        return result.IsSuccess
            ? onSuccess?.Invoke(result.Value) ?? HttpResults.Ok(result.Value)
            : ToProblemResult(result.Error);
    }

    private static IResult ToProblemResult(Error error)
    {
        var statusCode = ToStatusCode(error.Type);

        if (error.Type == ErrorType.Validation && error.Details is not null)
        {
            return HttpResults.ValidationProblem(
                error.Details,
                title: error.Code,
                detail: error.Description,
                statusCode: statusCode,
                extensions: CreateExtensions(error));
        }

        return HttpResults.Problem(
            title: error.Code,
            detail: error.Description,
            statusCode: statusCode,
            extensions: CreateExtensions(error));
    }

    private static int ToStatusCode(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static Dictionary<string, object?> CreateExtensions(Error error)
    {
        return new Dictionary<string, object?>
        {
            ["errorCode"] = error.Code,
            ["errorType"] = error.Type.ToString()
        };
    }
}
