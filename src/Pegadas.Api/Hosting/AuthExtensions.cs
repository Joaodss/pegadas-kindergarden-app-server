using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pegadas.BuildingBlocks.Security;

namespace Pegadas.Api.Hosting;

internal static class AuthExtensions
{
    /// <summary>
    /// JWT bearer validation (ES256, 15-minute tokens issued by the Identity module) and the
    /// authorization policies. Secure by default: every endpoint requires an authenticated
    /// user unless it explicitly allows anonymous access (ADR-0006).
    /// </summary>
    public static WebApplicationBuilder AddPegadasAuth(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        builder.Services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSigningKeyProvider, IOptions<JwtOptions>>((options, keys, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = keys.SigningKey,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = PegadasClaimTypes.Subject,
                    RoleClaimType = PegadasClaimTypes.Role,
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(PegadasPolicies.Educator, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(PegadasClaimTypes.SchoolId)
                .RequireRole(PegadasRoles.Educator, PegadasRoles.Coordinator))
            .AddPolicy(PegadasPolicies.Coordinator, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(PegadasClaimTypes.SchoolId)
                .RequireRole(PegadasRoles.Coordinator));

        return builder;
    }
}
