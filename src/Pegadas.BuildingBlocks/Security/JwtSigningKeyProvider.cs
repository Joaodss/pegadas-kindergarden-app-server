using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Pegadas.BuildingBlocks.Security;

/// <summary>
/// Holds the ES256 key used to sign (Identity module) and validate (JwtBearer) access tokens.
/// </summary>
public sealed class JwtSigningKeyProvider : IDisposable
{
    private readonly ECDsa _key;

    public JwtSigningKeyProvider(IOptions<JwtOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var pem = options.Value.SigningKeyPem;
        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        if (!string.IsNullOrWhiteSpace(pem))
        {
            _key.ImportFromPem(pem);
        }
        else if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            _key.Dispose();
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKeyPem)} must be configured outside Development.");
        }

        // Without a configured key the freshly generated one is used: tokens do not survive a restart.
        var keyId = Convert.ToHexStringLower(SHA256.HashData(_key.ExportSubjectPublicKeyInfo()))[..16];
        SigningKey = new ECDsaSecurityKey(_key) { KeyId = keyId };
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.EcdsaSha256);
    }

    public SecurityKey SigningKey { get; }

    public SigningCredentials SigningCredentials { get; }

    public void Dispose() => _key.Dispose();
}
