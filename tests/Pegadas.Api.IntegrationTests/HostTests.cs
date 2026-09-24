using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pegadas.Api.IntegrationTests.Infrastructure;

namespace Pegadas.Api.IntegrationTests;

public sealed class HostTests(PegadasApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Liveness_probe_is_anonymous_and_healthy()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/health/live", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_probe_checks_the_database()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/health/ready", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Min_version_is_anonymous_and_cacheable()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/v1/meta/min-version", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("1.0.0", body.GetProperty("minimumVersion").GetString());
        Assert.Equal("1.0.0", body.GetProperty("latestVersion").GetString());
    }

    [Fact]
    public async Task Endpoints_require_authentication_by_default()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/v1/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_valid_access_token_is_accepted()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(fixture.Factory.Services, Guid.CreateVersion7()));

        using var response = await client.GetAsync("/v1/does-not-exist", Ct);

        // Authenticated, so the request reaches routing and gets a plain 404.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_access_token_is_rejected()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(fixture.Factory.Services, Guid.CreateVersion7(), lifetime: TimeSpan.FromMinutes(-5)));

        using var response = await client.GetAsync("/v1/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Outdated_app_versions_get_426_problem()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-App-Version", "0.9.0");

        using var response = await client.GetAsync("/v1/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("app_upgrade_required", problem.GetProperty("code").GetString());
        Assert.Equal("1.0.0", problem.GetProperty("minimumVersion").GetString());
    }

    [Fact]
    public async Task Outdated_apps_can_still_read_the_minimum_version()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-App-Version", "0.9.0");

        using var response = await client.GetAsync("/v1/meta/min-version", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_v1_is_not_cached()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/v1/does-not-exist", Ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}
