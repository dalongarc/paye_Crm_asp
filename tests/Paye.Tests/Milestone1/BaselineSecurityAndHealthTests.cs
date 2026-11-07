using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Paye.Tests.Infrastructure;

namespace Paye.Tests.Milestone1;

public sealed class BaselineSecurityAndHealthTests
{
    [Fact]
    public async Task HealthEndpoint_Returns200AndMinimalJson_WhenAnonymous()
    {
        using var factory = new PayeWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", body);
    }

    [Fact]
    public async Task SecurityHeaders_EnforceStrictCspWithoutUnsafeInlineOrEval_AndFrameAncestorsNone()
    {
        using var factory = new PayeWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/health");

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = string.Join(" ", cspValues);

        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("style-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Equal("nosniff", nosniff.Single());

        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var frameOptions));
        Assert.Equal("DENY", frameOptions.Single());

        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrer));
        Assert.Equal("strict-origin-when-cross-origin", referrer.Single());

        Assert.True(response.Headers.TryGetValues("Strict-Transport-Security", out var hsts));
        Assert.Contains("max-age=31536000", hsts.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrivateRoute_DeniesAnonymousAccess_ByDefault()
    {
        using var factory = new PayeWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/account/login", response.Headers.Location.PathAndQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cors_DoesNotReflectUntrustedOrigin()
    {
        using var factory = new PayeWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://malicious.example.com");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Host=localhost;Database=paye;Username=u;Password=CHANGE_ME")]
    [InlineData("InvalidConnectionStringWithoutHost")]
    public void Startup_FailsClosed_WhenDatabaseConnectionInvalid(string invalidConnection)
    {
        using var factory = new PayeWebFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = invalidConnection
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            using var _ = factory.CreateClient();
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("CHANGE_ME_NOMBRE_COMERCIO")]
    public void Startup_FailsClosed_WhenClientBusinessNameMissingOrPlaceholder(string invalidBusinessName)
    {
        using var factory = new PayeWebFactory(new Dictionary<string, string?>
        {
            ["ClientSettings:BusinessName"] = invalidBusinessName
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            using var _ = factory.CreateClient();
        });
    }

    [Fact]
    public void Startup_FailsClosed_WhenCorsContainsWildcard_OrUploadsInsideWwwroot()
    {
        using var wildcardCorsFactory = new PayeWebFactory(new Dictionary<string, string?>
        {
            ["Security:AllowedOrigins:0"] = "*"
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            using var _ = wildcardCorsFactory.CreateClient();
        });

        using var wwwrootUploadsFactory = new PayeWebFactory(new Dictionary<string, string?>
        {
            ["Security:PrivateUploadsPath"] = "/app/wwwroot/uploads"
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            using var _ = wwwrootUploadsFactory.CreateClient();
        });
    }

    [Fact]
    public void TestDatabaseGuard_Throws_WhenTestDatabaseMatchesProductionDatabase()
    {
        const string sameConn = "Host=db;Database=paye_real;Username=app;Password=secret";

        Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureIsolatedTestDatabase(sameConn, sameConn));

        Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureIsolatedTestDatabase(""));
    }

    [Fact]
    public async Task ErrorPage_SetsStrictCsp_AndHtmxIndicatorStylesDisabledMeta()
    {
        using var factory = new PayeWebFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/error");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("includeIndicatorStyles", html, StringComparison.Ordinal);
        Assert.Contains("\"includeIndicatorStyles\":false", html, StringComparison.Ordinal);

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = string.Join(" ", cspValues);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.OrdinalIgnoreCase);
    }
}
