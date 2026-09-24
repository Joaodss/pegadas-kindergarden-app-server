using System.ComponentModel.DataAnnotations;

namespace Pegadas.BuildingBlocks.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "https://api.pegadas.pt";

    [Required]
    public string Audience { get; set; } = "pegadas-app";

    /// <summary>
    /// PEM-encoded EC P-256 private key used to sign access tokens (ES256). Comes from Key Vault
    /// (secret <c>Jwt--SigningKeyPem</c>). Required outside Development/Testing, where an
    /// ephemeral key is generated instead.
    /// </summary>
    public string? SigningKeyPem { get; set; }

    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
}
