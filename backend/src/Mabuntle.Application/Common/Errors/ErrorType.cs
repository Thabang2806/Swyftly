namespace Mabuntle.Application.Common.Errors;

public enum ErrorType
{
    None = 0,
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}
