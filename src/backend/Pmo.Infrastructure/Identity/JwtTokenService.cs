using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Pmo.Infrastructure.Identity;

/// <summary>Creates the short-lived access token (JWT, HS256). Claims: sub, email, name, role.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public (string Token, int ExpiresInSeconds) Create(AppUser user, string role)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["email"] = user.Email!,
                ["name"] = user.FullName,
                ["role"] = role
            },
            SigningCredentials = new SigningCredentials(SigningKey(jwt), SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), jwt.AccessTokenMinutes * 60);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.Key));
}
