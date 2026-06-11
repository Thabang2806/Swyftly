using System.Security.Claims;
using Mabuntle.Application.Abstractions;

namespace Mabuntle.Api.Authentication;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string? UserId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        httpContextAccessor.HttpContext?.User.Identity?.Name;

    public IReadOnlyCollection<string> Roles =>
        httpContextAccessor.HttpContext?.User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];

    public bool IsInRole(string role) =>
        httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
}
