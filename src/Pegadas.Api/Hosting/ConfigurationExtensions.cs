using Azure.Core;
using Azure.Identity;

namespace Pegadas.Api.Hosting;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Adds Azure Key Vault as a configuration source when <c>KeyVault:Uri</c> is set. Secrets
    /// map with "--" as the section separator (<c>ConnectionStrings--Pegadas</c>,
    /// <c>Jwt--SigningKeyPem</c>). Outside Development the app authenticates with its
    /// system-assigned managed identity only, which avoids the credential-chain probing at startup.
    /// </summary>
    public static WebApplicationBuilder AddPegadasConfiguration(this WebApplicationBuilder builder)
    {
        if (IsBuildTimeDocumentGeneration)
        {
            // The OpenAPI generator boots the host with no database or secrets: nothing may run in the background.
            builder.Configuration["Workers:Enabled"] = "false";
            return builder;
        }

        var vaultUri = builder.Configuration["KeyVault:Uri"];
        if (!string.IsNullOrWhiteSpace(vaultUri))
        {
            TokenCredential credential = builder.Environment.IsDevelopment()
                ? new DefaultAzureCredential()
                : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);

            builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), credential);
        }

        return builder;
    }

    /// <summary>True when the host is started by <c>Microsoft.Extensions.ApiDescription.Server</c> to write <c>openapi/v1.json</c>.</summary>
    private static bool IsBuildTimeDocumentGeneration =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
