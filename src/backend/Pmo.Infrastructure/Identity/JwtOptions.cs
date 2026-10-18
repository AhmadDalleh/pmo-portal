namespace Pmo.Infrastructure.Identity;

/// <summary>Config section "Jwt". The signing key is a secret: dotnet user-secrets locally, environment variable in Docker.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "pmo-portal";
    public string Audience { get; set; } = "pmo-portal-web";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
