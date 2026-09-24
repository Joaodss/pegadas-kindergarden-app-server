using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pegadas.BuildingBlocks.Security;

namespace Pegadas.Api.IntegrationTests.Infrastructure;

/// <summary>Mints access tokens with the host's signing key, shaped like the Identity module's tokens.</summary>
internal static class TestTokens
{
    public static string Create(
        IServiceProvider services,
        Guid schoolId,
        string role = PegadasRoles.Educator,
        IEnumerable<Guid>? rooms = null,
        TimeSpan? lifetime = null)
    {
        var keys = services.GetRequiredService<JwtSigningKeyProvider>();
        var jwt = services.GetRequiredService<IOptions<JwtOptions>>().Value;

        var claims = new List<Claim>
        {
            new(PegadasClaimTypes.Subject, Guid.CreateVersion7().ToString()),
            new(PegadasClaimTypes.SchoolId, schoolId.ToString()),
            new(PegadasClaimTypes.DeviceId, Guid.CreateVersion7().ToString()),
            new(PegadasClaimTypes.Role, role),
        };
        claims.AddRange((rooms ?? []).Select(room => new Claim(PegadasClaimTypes.Rooms, room.ToString())));

        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.AddSeconds(-5),
            IssuedAt = now,
            Expires = now + (lifetime ?? jwt.AccessTokenLifetime),
            SigningCredentials = keys.SigningCredentials,
        });
    }
}
