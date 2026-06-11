using Microsoft.AspNetCore.Http;

namespace Mabuntle.Api.Authentication;

public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    public string RefreshTokenPath { get; set; } = "/api/auth";

    public string CsrfPath { get; set; } = "/";

    public string? Domain { get; set; }

    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    public bool Secure { get; set; } = true;
}
