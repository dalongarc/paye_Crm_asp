using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Paye.Core.Auditing;
using Paye.Core.Authorization;
using Paye.Core.Common;
using Paye.Core.Files;
using Paye.Core.Integrations;
using Paye.Core.ValueObjects;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Identity;
using Paye.Infrastructure.Integrations;
using Paye.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Milestone3a;

public sealed class Milestone3aPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_m3a")
        .WithUsername("paye_test_user")
        .WithPassword("TestOnly_NotProd_987!")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        TestDatabaseGuard.EnsureIsolatedTestDatabase(ConnectionString);

        var optionsBuilder = new DbContextOptionsBuilder<PayeDbContext>();
        optionsBuilder.UseNpgsql(ConnectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PayeDbContext).Assembly.FullName);
        });

        await using var db = new PayeDbContext(optionsBuilder.Options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

public sealed class CrossCuttingFeaturesTests(Milestone3aPostgresFixture dbFixture)
    : IClassFixture<Milestone3aPostgresFixture>
{
    private const string OwnerEmail = "propietario.m3a@payedigital.test";
    private const string OwnerPassword = "ClaveSegura#Paye2026!";

    private async Task<PayeWebFactory> CreateInitializedFactoryAsync(
        string? privateStoragePath = null,
        IDictionary<string, string?>? extraOverrides = null)
    {
        var storageDir = privateStoragePath
            ?? Path.Combine(Path.GetTempPath(), "paye-test-private-files-" + Guid.NewGuid().ToString("N"));

        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = dbFixture.ConnectionString,
            ["Security:PrivateUploadsPath"] = storageDir,
            ["SEED_OWNER_EMAIL"] = OwnerEmail,
            ["SEED_OWNER_PASSWORD"] = OwnerPassword,
            ["SEED_OWNER_FULLNAME"] = "Propietario M3a"
        };

        if (extraOverrides is not null)
        {
            foreach (var kvp in extraOverrides)
            {
                overrides[kvp.Key] = kvp.Value;
            }
        }

        var factory = new PayeWebFactory(overrides);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedRolesAndPermissionsAsync();
        await seeder.SeedInitialOwnerFromEnvironmentAsync();

        return factory;
    }

    [Fact]
    public async Task AuditableEntity_And_SoftDelete_PopulateMetadata_And_FilterDeletedRecords()
    {
        using var factory = await CreateInitializedFactoryAsync();

        Guid recordId;
        DateTimeOffset initialCreatedAt;

        // 1. Insert entity -> CreatedAtUtc & CreatedBy populated automatically
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var record = new PrivateFileRecord
            {
                OriginalDisplayName = "contrato-prueba.pdf",
                StoredFileName = Guid.NewGuid().ToString("N") + ".pdf",
                ContentType = "application/pdf",
                SizeBytes = 1024
            };

            db.PrivateFiles.Add(record);
            await db.SaveChangesAsync();

            recordId = record.Id;
            initialCreatedAt = record.CreatedAtUtc;

            Assert.NotEqual(default, record.CreatedAtUtc);
            Assert.Equal("system", record.CreatedBy);
            Assert.Null(record.UpdatedAtUtc);
            Assert.False(record.IsDeleted);
            Assert.True(record.RowVersion > 0);
        }

        // 2. Update entity -> UpdatedAtUtc & UpdatedBy populated, CreatedAtUtc preserved
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var record = await db.PrivateFiles.SingleAsync(f => f.Id == recordId);
            var previousVersion = record.RowVersion;

            record.OriginalDisplayName = "contrato-actualizado.pdf";
            await db.SaveChangesAsync();

            Assert.True((record.CreatedAtUtc - initialCreatedAt).Duration() < TimeSpan.FromMilliseconds(1));
            Assert.NotNull(record.UpdatedAtUtc);
            Assert.Equal("system", record.UpdatedBy);
            Assert.NotEqual(previousVersion, record.RowVersion);
        }

        // 3. Delete entity -> Soft-deleted (IsDeleted = true), hidden from normal queries, visible with IgnoreQueryFilters()
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var record = await db.PrivateFiles.SingleAsync(f => f.Id == recordId);

            db.PrivateFiles.Remove(record);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();

            var normalQuery = await db.PrivateFiles.FirstOrDefaultAsync(f => f.Id == recordId);
            Assert.Null(normalQuery);

            var unfiltered = await db.PrivateFiles
                .IgnoreQueryFilters()
                .SingleAsync(f => f.Id == recordId);

            Assert.True(unfiltered.IsDeleted);
            Assert.NotNull(unfiltered.DeletedAtUtc);
            Assert.Equal("system", unfiltered.DeletedBy);
        }
    }

    [Fact]
    public async Task OptimisticConcurrency_Xmin_ThrowsDbUpdateConcurrencyException_OnConflictingSave()
    {
        using var factory = await CreateInitializedFactoryAsync();

        Guid recordId;
        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<PayeDbContext>();
            var record = new PrivateFileRecord
            {
                OriginalDisplayName = "concurrencia.pdf",
                StoredFileName = Guid.NewGuid().ToString("N") + ".pdf",
                ContentType = "application/pdf",
                SizeBytes = 512
            };
            db.PrivateFiles.Add(record);
            await db.SaveChangesAsync();
            recordId = record.Id;
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();

        var dbA = scopeA.ServiceProvider.GetRequiredService<PayeDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<PayeDbContext>();

        var entityA = await dbA.PrivateFiles.SingleAsync(f => f.Id == recordId);
        var entityB = await dbB.PrivateFiles.SingleAsync(f => f.Id == recordId);

        entityA.OriginalDisplayName = "modificado-por-A.pdf";
        await dbA.SaveChangesAsync();

        entityB.OriginalDisplayName = "modificado-por-B.pdf";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    [Fact]
    public void Money_And_VatRate_EnforceDecimalPrecision_BankersRounding_And_SpanishVatBreakdown()
    {
        var net = Money.Euros(100.00m);
        var vatAmount = VatRate.General21.CalculateTaxFromNet(net);
        var grossAmount = VatRate.General21.CalculateGrossFromNet(net);

        Assert.Equal(100.00m, net.Amount);
        Assert.Equal(21.00m, vatAmount.Amount);
        Assert.Equal(121.00m, grossAmount.Amount);

        var (extractedNet, extractedVat, extractedGross) = VatRate.General21.BreakdownFromGross(grossAmount);
        Assert.Equal(100.00m, extractedNet.Amount);
        Assert.Equal(21.00m, extractedVat.Amount);
        Assert.Equal(121.00m, extractedGross.Amount);

        // Reduced 10%, Super-reduced 4%, Exempt 0%
        Assert.Equal(10.00m, VatRate.Reducido10.CalculateTaxFromNet(net).Amount);
        Assert.Equal(4.00m, VatRate.Superreducido4.CalculateTaxFromNet(net).Amount);
        Assert.Equal(0.00m, VatRate.Exento0.CalculateTaxFromNet(net).Amount);

        // Banker's rounding (MidpointRounding.ToEven)
        var roundedEven = Money.Euros(10.005m);
        Assert.Equal(10.00m, roundedEven.Amount);

        var roundedUp = Money.Euros(10.015m);
        Assert.Equal(10.02m, roundedUp.Amount);

        // Currency mismatch throws InvalidOperationException
        var usd = new Money(10m, "USD");
        Assert.Throws<InvalidOperationException>(() => _ = net + usd);

        // Spanish formatting contains €
        Assert.Contains("€", grossAmount.FormatSpanish());
    }

    [Fact]
    public void MadridTimeConverter_ConvertsUtcToMadridTimezone_InWinterAndSummer()
    {
        // January (CET = UTC+1)
        var winterUtc = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var winterMadrid = MadridTimeConverter.ToMadridTime(winterUtc);
        Assert.Equal(TimeSpan.FromHours(1), winterMadrid.Offset);
        Assert.Equal("15/01/2026 11:30", MadridTimeConverter.FormatSpanishDateTime(winterUtc));

        // July (CEST = UTC+2)
        var summerUtc = new DateTimeOffset(2026, 7, 15, 10, 30, 0, TimeSpan.Zero);
        var summerMadrid = MadridTimeConverter.ToMadridTime(summerUtc);
        Assert.Equal(TimeSpan.FromHours(2), summerMadrid.Offset);
        Assert.Equal("15/07/2026 12:30", MadridTimeConverter.FormatSpanishDateTime(summerUtc));
    }

    [Fact]
    public void ExternalIntegrations_AreDisabledWhenKeysAreMissingOrPlaceholders_AndEnabledWhenConfigured()
    {
        var defaultProvider = new ExternalIntegrationStatusProvider(
            Options.Create(new ExternalIntegrationsSettings()));

        Assert.False(defaultProvider.IsGeminiEnabled);
        Assert.False(defaultProvider.IsGoogleEnabled);

        var placeholderProvider = new ExternalIntegrationStatusProvider(
            Options.Create(new ExternalIntegrationsSettings
            {
                Gemini = new GeminiIntegrationOptions { ApiKey = "CHANGE_ME_SECRET" },
                Google = new GoogleWorkspaceIntegrationOptions { ClientId = "id", ClientSecret = "CHANGE_ME" }
            }));

        Assert.False(placeholderProvider.IsGeminiEnabled);
        Assert.False(placeholderProvider.IsGoogleEnabled);

        var configuredProvider = new ExternalIntegrationStatusProvider(
            Options.Create(new ExternalIntegrationsSettings
            {
                Gemini = new GeminiIntegrationOptions { ApiKey = "test-gemini-key-fictitious" },
                Google = new GoogleWorkspaceIntegrationOptions
                {
                    ClientId = "test-client-id.apps.googleusercontent.com",
                    ClientSecret = "test-client-secret-fictitious"
                }
            }));

        Assert.True(configuredProvider.IsGeminiEnabled);
        Assert.True(configuredProvider.IsGoogleEnabled);
    }

    [Fact]
    public async Task PrivateFileStorage_ValidatesMagicBytes_RejectsFakeFiles_ProtectsPathTraversal_AndRequiresAuth()
    {
        var tempStorageDir = Path.Combine(Path.GetTempPath(), "paye-files-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = await CreateInitializedFactoryAsync(tempStorageDir);

            // 1. Direct storage service rejects fake PDF (HTML/script disguised as .pdf)
            using (var scope = factory.Services.CreateScope())
            {
                var storage = scope.ServiceProvider.GetRequiredService<IPrivateFileStorage>();
                var fakePdfBytes = Encoding.UTF8.GetBytes("<script>alert('xss')</script>");
                await using var fakeStream = new MemoryStream(fakePdfBytes);

                var fakeSaveResult = await storage.SaveValidatedAsync(fakeStream, "malicious.pdf");
                Assert.False(fakeSaveResult.Succeeded);
                Assert.Null(fakeSaveResult.File);

                // Rejects path traversal on OpenReadAsync
                var traversalRead = await storage.OpenReadAsync("../appsettings.json");
                Assert.Null(traversalRead);
            }

            // 2. HTTP upload & download via PrivateFilesController requires authentication
            using var anonClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

            var unauthCheck = await anonClient.GetAsync($"/files/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.Redirect, unauthCheck.StatusCode);
            Assert.Contains("/account/login", unauthCheck.Headers.Location?.ToString() ?? string.Empty);

            // 3. Login as Owner and upload a valid PDF (%PDF-1.7 magic header)
            using var authClient = await CreateAuthenticatedOwnerClientAsync(factory);

            var validPdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");
            var uploadToken = await GetAntiforgeryTokenFromPageAsync(authClient, "/admin/users");

            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(uploadToken), "__RequestVerificationToken");
            var fileContent = new ByteArrayContent(validPdfBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            multipart.Add(fileContent, "File", "albaran-ficticio.pdf");

            var uploadResponse = await authClient.PostAsync("/files/upload", multipart);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

            using var jsonDoc = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
            var uploadedId = jsonDoc.RootElement.GetProperty("id").GetGuid();

            // Verify stored file on disk has random server-generated name (not original name) and lives outside wwwroot
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();
                var record = await db.PrivateFiles.SingleAsync(f => f.Id == uploadedId);
                Assert.Equal("albaran-ficticio.pdf", record.OriginalDisplayName);
                Assert.NotEqual("albaran-ficticio.pdf", record.StoredFileName);
                Assert.Matches("^[a-f0-9]{32}\\.pdf$", record.StoredFileName);
                Assert.True(File.Exists(Path.Combine(tempStorageDir, record.StoredFileName)));
            }

            // Anonymous user cannot download the uploaded file
            var anonDownload = await anonClient.GetAsync($"/files/{uploadedId}");
            Assert.Equal(HttpStatusCode.Redirect, anonDownload.StatusCode);

            // Authenticated user can download with nosniff
            var authDownload = await authClient.GetAsync($"/files/{uploadedId}");
            Assert.Equal(HttpStatusCode.OK, authDownload.StatusCode);
            Assert.Equal("application/pdf", authDownload.Content.Headers.ContentType?.MediaType);
            Assert.Equal("nosniff", authDownload.Headers.GetValues("X-Content-Type-Options").FirstOrDefault());
            var downloadedBytes = await authDownload.Content.ReadAsByteArrayAsync();
            Assert.Equal(validPdfBytes, downloadedBytes);
        }
        finally
        {
            if (Directory.Exists(tempStorageDir))
            {
                Directory.Delete(tempStorageDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GdprService_ExportsUserDataJson_And_AnonymizesPersonalDataPreservingAuditTrail()
    {
        using var factory = await CreateInitializedFactoryAsync();
        using var ownerClient = await CreateAuthenticatedOwnerClientAsync(factory);

        string targetUserId;
        const string targetEmail = "empleado.rgpd@payedigital.test";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var targetUser = new ApplicationUser
            {
                UserName = targetEmail,
                Email = targetEmail,
                FullName = "María Empleada Ficticia",
                EmailConfirmed = true,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(targetUser, "Empleado#Rgpd2026!");
            Assert.True(createResult.Succeeded);
            await userManager.AddToRoleAsync(targetUser, SystemRoles.Empleado);
            targetUserId = targetUser.Id;

            await auditLogger.LogCriticalActionAsync(
                action: "Users.Create",
                entityName: nameof(ApplicationUser),
                entityId: targetUserId,
                summary: $"Alta de prueba para {targetEmail}");
        }

        // 1. Export GDPR JSON via HTTP endpoint
        var exportResponse = await ownerClient.GetAsync($"/admin/users/{targetUserId}/export-gdpr");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/json", exportResponse.Content.Headers.ContentType?.MediaType);

        var exportJson = await exportResponse.Content.ReadAsStringAsync();
        using var exportDoc = JsonDocument.Parse(exportJson);
        Assert.Equal(targetEmail, exportDoc.RootElement.GetProperty("Email").GetString());
        Assert.Equal("María Empleada Ficticia", exportDoc.RootElement.GetProperty("FullName").GetString());

        // 2. Anonymize user via HTTP POST endpoint
        var csrfToken = await GetAntiforgeryTokenFromPageAsync(ownerClient, "/admin/users");
        var anonymizeResponse = await ownerClient.PostAsync(
            $"/admin/users/{targetUserId}/anonymize-gdpr",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = csrfToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, anonymizeResponse.StatusCode);

        // 3. Verify personal data is scrubbed in DB and audit logs are preserved
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<PayeDbContext>();

            var anonymizedUser = await userManager.FindByIdAsync(targetUserId);
            Assert.NotNull(anonymizedUser);
            Assert.False(anonymizedUser.IsActive);
            Assert.Equal("Usuario Anonimizado RGPD", anonymizedUser.FullName);
            Assert.EndsWith("@anon.invalid", anonymizedUser.Email);
            Assert.Null(anonymizedUser.PasswordHash);

            var roles = await userManager.GetRolesAsync(anonymizedUser);
            Assert.Empty(roles);

            var gdprAuditExists = await db.AuditLogs.AnyAsync(
                a => a.Action == "GDPR.AnonymizeUser" && a.EntityId == targetUserId);
            Assert.True(gdprAuditExists);
        }
    }

    private static async Task<HttpClient> CreateAuthenticatedOwnerClientAsync(PayeWebFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginToken = await GetAntiforgeryTokenFromPageAsync(client, "/account/login");
        var loginResponse = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = loginToken,
            ["Email"] = OwnerEmail,
            ["Password"] = OwnerPassword
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return client;
    }

    private static async Task<string> GetAntiforgeryTokenFromPageAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            match = Regex.Match(
                html,
                @"value=""([^""]+)""\s+name=""__RequestVerificationToken""",
                RegexOptions.IgnoreCase);
        }

        Assert.True(match.Success, $"No se encontró token antiforgery en {path}");
        return match.Groups[1].Value;
    }
}
