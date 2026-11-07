using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Core.Configuration;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;
using Paye.Infrastructure.Auditing;
using Paye.Infrastructure.Data;
using Paye.Infrastructure.Data.Interceptors;
using Paye.Infrastructure.Files;
using Paye.Infrastructure.Modules.Facturacion;
using Paye.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Facturacion;

public sealed class FacturacionPaso2PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_facturacion_p2")
        .WithUsername("paye_test_user")
        .WithPassword("TestOnly_NotProd_FactP2!")
        .Build();

    public string ConnectionString => _container.GetConnectionString();
    public string TempUploadsPath { get; } = Path.Combine(Path.GetTempPath(), $"paye_fact_p2_uploads_{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(TempUploadsPath);
        await _container.StartAsync();
        TestDatabaseGuard.EnsureIsolatedTestDatabase(ConnectionString);

        await using var db = CreateDbContext("system-migrations");
        await db.Database.MigrateAsync();
    }

    public PayeDbContext CreateDbContext(string actorId = "owner-user-1")
    {
        var currentUser = new FakeCurrentUserService(actorId);
        var interceptor = new AuditableEntitySaveChangesInterceptor(currentUser);
        var optionsBuilder = new DbContextOptionsBuilder<PayeDbContext>();
        optionsBuilder.UseNpgsql(ConnectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PayeDbContext).Assembly.FullName);
        });
        optionsBuilder.AddInterceptors(interceptor);
        return new PayeDbContext(optionsBuilder.Options);
    }

    public FacturacionService CreateService(
        PayeDbContext dbContext,
        string actorId = "owner-user-1",
        IFiscalInvoiceRegistry? customRegistry = null)
    {
        var currentUser = new FakeCurrentUserService(actorId);
        var auditLogger = new DatabaseAuditLogger(dbContext, currentUser);
        var securityOptions = Options.Create(new SecuritySettings
        {
            PrivateUploadsPath = TempUploadsPath
        });
        var fileStorage = new LocalPrivateFileStorage(securityOptions);
        var pdfGenerator = new PdfInvoiceGenerator();
        var registry = customRegistry ?? new NoOpFiscalInvoiceRegistry();

        return new FacturacionService(
            dbContext,
            currentUser,
            auditLogger,
            fileStorage,
            pdfGenerator,
            registry);
    }

    public async Task EnsureConfiguredIssuerAsync()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var current = await service.GetSettingsAsync();
        if (current.IsReadyToIssue)
        {
            return;
        }

        var res = await service.UpdateSettingsAsync(new UpdateBillingSettingsCommand(
            TaxName: "Payé Hostelería S.L.",
            TaxId: "B12345674",
            FiscalAddress: "Calle Alcalá 100, 28009 Madrid",
            LogoPrivateFileId: null,
            LegalTermsText: "Inscrita en el Registro Mercantil de Madrid.",
            FooterText: "Gracias por su visita.",
            RowVersion: current.RowVersion));

        Assert.True(res.Succeeded, res.ErrorMessage);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        if (Directory.Exists(TempUploadsPath))
        {
            Directory.Delete(TempUploadsPath, recursive: true);
        }
    }

    private sealed class FakeCurrentUserService(string userId) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? UserEmail => $"{userId}@example.test";
        public string? IpAddress => "127.0.0.1";
        public bool IsAuthenticated => true;
    }
}

public sealed class FacturacionPaso2ServiceAndConcurrencyTests(FacturacionPaso2PostgresFixture fixture)
    : IClassFixture<FacturacionPaso2PostgresFixture>
{
    [Fact]
    public async Task AtomicNumbering_RollbackOnError_LeavesZeroGaps_InSeriesSequence()
    {
        await fixture.EnsureConfiguredIssuerAsync();

        // Usamos un año específico (2031) para aislar la serie en este test
        const int testYear = 2031;
        Guid seriesId;

        await using (var db = fixture.CreateDbContext())
        {
            var svc = fixture.CreateService(db);
            var saveSeries = await svc.SaveSeriesAsync(new SaveBillingSeriesCommand(
                Id: null,
                Code: "FGAP",
                Name: "Serie Prueba Sin Huecos",
                DocumentKind: BillingDocumentKind.Ordinaria,
                FiscalYear: FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow),
                PaddingDigits: 4,
                IsActive: true,
                IsDefault: false));
            Assert.True(saveSeries.Succeeded, saveSeries.ErrorMessage);
            seriesId = saveSeries.DocumentId!.Value;
            _ = testYear;
        }

        // 1. Emitir primera factura -> FGAP-YYYY-0001
        Guid draft1Id;
        Guid draftFailId;
        Guid draft2Id;

        await using (var db = fixture.CreateDbContext())
        {
            var svc = fixture.CreateService(db);

            var r1 = await svc.SaveDraftInvoiceAsync(CreateSampleDraftCommand(seriesId, "Cliente Uno S.L."));
            var rFail = await svc.SaveDraftInvoiceAsync(CreateSampleDraftCommand(seriesId, "Cliente Fallo Rollback S.L."));
            var r2 = await svc.SaveDraftInvoiceAsync(CreateSampleDraftCommand(seriesId, "Cliente Dos S.L."));

            draft1Id = r1.DocumentId!.Value;
            draftFailId = rFail.DocumentId!.Value;
            draft2Id = r2.DocumentId!.Value;

            var d1 = await svc.GetInvoiceByIdAsync(draft1Id);
            var issue1 = await svc.IssueDraftInvoiceAsync(draft1Id, d1!.RowVersion);
            Assert.True(issue1.Succeeded, issue1.ErrorMessage);
            Assert.EndsWith("-0001", issue1.FormattedNumber, StringComparison.Ordinal);
        }

        // 2. Forzar un fallo dentro de la transacción DESPUÉS de haber ejecutado UPDATE ... RETURNING sobre facturacion_series
        await using (var dbFail = fixture.CreateDbContext())
        {
            var svcFail = fixture.CreateService(dbFail, customRegistry: new ThrowingFiscalInvoiceRegistry());
            var dFail = await svcFail.GetInvoiceByIdAsync(draftFailId);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svcFail.IssueDraftInvoiceAsync(draftFailId, dFail!.RowVersion));
        }

        // 3. Emitir el siguiente borrador válido -> debe recibir -0002 (sin hueco por el intento fallido)
        await using (var db2 = fixture.CreateDbContext())
        {
            var svc2 = fixture.CreateService(db2);
            var d2 = await svc2.GetInvoiceByIdAsync(draft2Id);
            var issue2 = await svc2.IssueDraftInvoiceAsync(draft2Id, d2!.RowVersion);

            Assert.True(issue2.Succeeded, issue2.ErrorMessage);
            Assert.EndsWith("-0002", issue2.FormattedNumber, StringComparison.Ordinal);

            var dFailAfter = await svc2.GetInvoiceByIdAsync(draftFailId);
            Assert.Equal(InvoiceStatus.Borrador, dFailAfter!.Status);
            Assert.Null(dFailAfter.SequentialNumber);
        }
    }

    [Fact]
    public async Task ConcurrentEmissions_OnSameSeries_ProduceSequentialNumbers_WithoutDuplicates()
    {
        await fixture.EnsureConfiguredIssuerAsync();

        Guid seriesId;
        Guid draftAId;
        uint draftARowVersion;
        Guid draftBId;
        uint draftBRowVersion;

        await using (var setupDb = fixture.CreateDbContext())
        {
            var setupSvc = fixture.CreateService(setupDb);
            var saveSeries = await setupSvc.SaveSeriesAsync(new SaveBillingSeriesCommand(
                Id: null,
                Code: "FCONC",
                Name: "Serie Concurrencia",
                DocumentKind: BillingDocumentKind.Ordinaria,
                FiscalYear: FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow),
                PaddingDigits: 4,
                IsActive: true,
                IsDefault: false));
            Assert.True(saveSeries.Succeeded, saveSeries.ErrorMessage);
            seriesId = saveSeries.DocumentId!.Value;

            var rA = await setupSvc.SaveDraftInvoiceAsync(CreateSampleDraftCommand(seriesId, "Cliente Concurrente A S.L."));
            var rB = await setupSvc.SaveDraftInvoiceAsync(CreateSampleDraftCommand(seriesId, "Cliente Concurrente B S.L."));

            draftAId = rA.DocumentId!.Value;
            draftBId = rB.DocumentId!.Value;

            draftARowVersion = (await setupSvc.GetInvoiceByIdAsync(draftAId))!.RowVersion;
            draftBRowVersion = (await setupSvc.GetInvoiceByIdAsync(draftBId))!.RowVersion;
        }

        using var startGate = new ManualResetEventSlim(false);

        var taskA = Task.Run(async () =>
        {
            await using var dbA = fixture.CreateDbContext("actor-a");
            var svcA = fixture.CreateService(dbA, "actor-a");
            startGate.Wait();
            return await svcA.IssueDraftInvoiceAsync(draftAId, draftARowVersion);
        });

        var taskB = Task.Run(async () =>
        {
            await using var dbB = fixture.CreateDbContext("actor-b");
            var svcB = fixture.CreateService(dbB, "actor-b");
            startGate.Wait();
            return await svcB.IssueDraftInvoiceAsync(draftBId, draftBRowVersion);
        });

        startGate.Set();
        var results = await Task.WhenAll(taskA, taskB);

        Assert.All(results, r => Assert.True(r.Succeeded, r.ErrorMessage));

        var formattedNumbers = results
            .Select(r => r.FormattedNumber!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, formattedNumbers.Distinct(StringComparer.Ordinal).Count());
        Assert.EndsWith("-0001", formattedNumbers[0], StringComparison.Ordinal);
        Assert.EndsWith("-0002", formattedNumbers[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentRectifications_OnSameInvoice_LockOriginalRow_And_AllowOnlyOneFullRectification()
    {
        await fixture.EnsureConfiguredIssuerAsync();

        Guid originalInvoiceId;
        await using (var setupDb = fixture.CreateDbContext())
        {
            var setupSvc = fixture.CreateService(setupDb);
            var issued = await setupSvc.CreateAndIssueAsync(new IssueInvoiceRequest(
                InvoiceType: InvoiceDocumentType.Ordinaria,
                PaymentMethod: BillingPaymentMethod.Tarjeta,
                CustomerName: "Cliente Rectificación Concurrente S.L.",
                CustomerTaxId: "A58818501",
                CustomerAddress: "Paseo de la Castellana 50, 28046 Madrid",
                Lines:
                [
                    new IssueInvoiceLineRequest("Equipamiento TPV", 1m, 500.00m, 21.00m)
                ]));

            Assert.True(issued.Succeeded, issued.ErrorMessage);
            originalInvoiceId = issued.Invoice!.Id;
        }

        using var startGate = new ManualResetEventSlim(false);

        var rectTask1 = Task.Run(async () =>
        {
            await using var db1 = fixture.CreateDbContext("rect-actor-1");
            var svc1 = fixture.CreateService(db1, "rect-actor-1");
            startGate.Wait();
            return await svc1.IssueRectifyingInvoiceAsync(new IssueRectifyingInvoiceRequest(
                OriginalInvoiceId: originalInvoiceId,
                Scope: RectificationScope.Total,
                Reason: "Devolución total concurrente 1",
                PaymentMethod: BillingPaymentMethod.Tarjeta));
        });

        var rectTask2 = Task.Run(async () =>
        {
            await using var db2 = fixture.CreateDbContext("rect-actor-2");
            var svc2 = fixture.CreateService(db2, "rect-actor-2");
            startGate.Wait();
            return await svc2.IssueRectifyingInvoiceAsync(new IssueRectifyingInvoiceRequest(
                OriginalInvoiceId: originalInvoiceId,
                Scope: RectificationScope.Total,
                Reason: "Devolución total concurrente 2",
                PaymentMethod: BillingPaymentMethod.Tarjeta));
        });

        startGate.Set();
        var rectResults = await Task.WhenAll(rectTask1, rectTask2);

        // Exactamente una debe tener éxito y la otra debe ser rechazada por validación tras esperar el bloqueo FOR UPDATE
        Assert.Single(rectResults, r => r.Succeeded);
        var failed = Assert.Single(rectResults, r => !r.Succeeded);
        Assert.Equal(FacturacionOperationStatus.ValidationError, failed.Status);
        Assert.Contains("ya ha sido rectificada en su totalidad", failed.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RectifyingInvoice_DoesNotAlterOriginal_And_SupportsPartialDifferencesUpToOriginalAmount()
    {
        await fixture.EnsureConfiguredIssuerAsync();

        await using var db = fixture.CreateDbContext();
        var svc = fixture.CreateService(db);

        var emitResult = await svc.CreateAndIssueAsync(new IssueInvoiceRequest(
            InvoiceType: InvoiceDocumentType.Ordinaria,
            PaymentMethod: BillingPaymentMethod.Contado,
            CustomerName: "Restaurante El Prado S.L.",
            CustomerTaxId: "B12345674",
            CustomerAddress: "Calle Prado 12, 28014 Madrid",
            Lines:
            [
                new IssueInvoiceLineRequest("Mantenimiento anual", 2m, 100.00m, 21.00m)
            ]));

        Assert.True(emitResult.Succeeded, emitResult.ErrorMessage);
        var originalBefore = await svc.GetInvoiceByIdAsync(emitResult.Invoice!.Id);
        Assert.NotNull(originalBefore);
        Assert.Equal(200.00m, originalBefore.SubtotalNet);
        Assert.Equal(242.00m, originalBefore.TotalGross);

        // 1. Rectificativa parcial por diferencias (-50.00 € base)
        var partial1 = await svc.IssueRectifyingInvoiceAsync(new IssueRectifyingInvoiceRequest(
            OriginalInvoiceId: originalBefore.Id,
            Scope: RectificationScope.PorDiferencias,
            Reason: "Descuento comercial sobre 1 unidad",
            PaymentMethod: BillingPaymentMethod.Contado,
            DifferenceLines:
            [
                new IssueInvoiceLineRequest("Ajuste parcial mantenimiento", -1m, 50.00m, 21.00m)
            ]));

        Assert.True(partial1.Succeeded, partial1.ErrorMessage);
        var rect1Dto = await svc.GetInvoiceByIdAsync(partial1.DocumentId!.Value);
        Assert.NotNull(rect1Dto);
        Assert.Equal(-50.00m, rect1Dto.SubtotalNet);
        Assert.Equal(-60.50m, rect1Dto.TotalGross);
        Assert.Equal(originalBefore.Id, rect1Dto.RectifiedInvoiceId);
        Assert.StartsWith("R-", rect1Dto.FormattedNumber, StringComparison.Ordinal);

        // Verificar que la factura original sigue intacta
        var originalAfter = await svc.GetInvoiceByIdAsync(originalBefore.Id);
        Assert.NotNull(originalAfter);
        Assert.Equal(originalBefore.FormattedNumber, originalAfter.FormattedNumber);
        Assert.Equal(originalBefore.SubtotalNet, originalAfter.SubtotalNet);
        Assert.Equal(originalBefore.TotalGross, originalAfter.TotalGross);
        Assert.Equal(originalBefore.RowVersion, originalAfter.RowVersion);

        // 2. Intentar rectificar por diferencias más del saldo restante (-160.00 € > 150.00 € restantes) -> rechazado
        var excessive = await svc.IssueRectifyingInvoiceAsync(new IssueRectifyingInvoiceRequest(
            OriginalInvoiceId: originalBefore.Id,
            Scope: RectificationScope.PorDiferencias,
            Reason: "Ajuste excesivo",
            PaymentMethod: BillingPaymentMethod.Contado,
            DifferenceLines:
            [
                new IssueInvoiceLineRequest("Ajuste excesivo", -1m, 160.00m, 21.00m)
            ]));

        Assert.False(excessive.Succeeded);
        Assert.Equal(FacturacionOperationStatus.ValidationError, excessive.Status);

        // 3. Rectificar exactamente el saldo restante (-150.00 €) -> permitido, dejando la factura totalmente rectificada
        var partial2 = await svc.IssueRectifyingInvoiceAsync(new IssueRectifyingInvoiceRequest(
            OriginalInvoiceId: originalBefore.Id,
            Scope: RectificationScope.PorDiferencias,
            Reason: "Anulación del resto de la factura",
            PaymentMethod: BillingPaymentMethod.Contado,
            DifferenceLines:
            [
                new IssueInvoiceLineRequest("Resto pendiente", -1m, 150.00m, 21.00m)
            ]));

        Assert.True(partial2.Succeeded, partial2.ErrorMessage);

        // 4. Intentar una tercera rectificación cuando ya está 100% rectificada -> rechazado
        var afterFull = await svc.IssueRectifyingInvoiceAsync(new IssueRectifyingInvoiceRequest(
            OriginalInvoiceId: originalBefore.Id,
            Scope: RectificationScope.PorDiferencias,
            Reason: "Intento adicional sobre factura agotada",
            PaymentMethod: BillingPaymentMethod.Contado,
            DifferenceLines:
            [
                new IssueInvoiceLineRequest("Ajuste extra", -1m, 1.00m, 21.00m)
            ]));

        Assert.False(afterFull.Succeeded);
        Assert.Contains("rectificada en su totalidad", afterFull.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quote_UsesNonFiscalSeries_DoesNotConsumeInvoiceSeries_And_ConvertsToDraftInvoice()
    {
        await fixture.EnsureConfiguredIssuerAsync();

        await using var db = fixture.CreateDbContext();
        var svc = fixture.CreateService(db);

        var currentYear = FiscalTimeZone.GetMadridFiscalYear(DateTimeOffset.UtcNow);
        var fSeriesBefore = await svc.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Ordinaria, currentYear);

        var createQuote = await svc.CreateQuoteAsync(new SaveQuoteCommand(
            Id: null,
            SeriesId: null,
            IssueDate: null,
            ValidUntilDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            CustomerName: "Panadería Artesana S.L.",
            CustomerTaxId: "B12345674",
            CustomerAddress: "Calle Sol 4, 28013 Madrid",
            Notes: "Presupuesto de prueba",
            Lines:
            [
                new DocumentLineInputDto("Instalación balanza conectada", 1m, 300.00m, 21.00m)
            ]));

        Assert.True(createQuote.Succeeded, createQuote.ErrorMessage);
        Assert.StartsWith("P-", createQuote.FormattedNumber, StringComparison.Ordinal);

        // La serie fiscal F no ha incrementado su contador
        var fSeriesAfterQuote = await svc.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Ordinaria, currentYear);
        Assert.Equal(fSeriesBefore.LastNumber, fSeriesAfterQuote.LastNumber);

        // Pasar a Enviado -> Aceptado -> Convertir en borrador de factura
        var quoteDto = await svc.GetQuoteByIdAsync(createQuote.DocumentId!.Value);
        var toSent = await svc.ChangeQuoteStatusAsync(quoteDto!.Id, QuoteStatus.Enviado, quoteDto.RowVersion);
        Assert.True(toSent.Succeeded, toSent.ErrorMessage);

        quoteDto = await svc.GetQuoteByIdAsync(quoteDto.Id);
        var toAccepted = await svc.ChangeQuoteStatusAsync(quoteDto!.Id, QuoteStatus.Aceptado, quoteDto.RowVersion);
        Assert.True(toAccepted.Succeeded, toAccepted.ErrorMessage);

        var convertResult = await svc.ConvertQuoteToDraftInvoiceAsync(quoteDto.Id);
        Assert.True(convertResult.Succeeded, convertResult.ErrorMessage);

        var draftInvoice = await svc.GetInvoiceByIdAsync(convertResult.DocumentId!.Value);
        Assert.NotNull(draftInvoice);
        Assert.Equal(InvoiceStatus.Borrador, draftInvoice.Status);
        Assert.Null(draftInvoice.SequentialNumber);
        Assert.Equal(quoteDto.Id, draftInvoice.SourceQuoteId);

        // El borrador sigue sin consumir numeración fiscal
        var fSeriesAfterDraft = await svc.EnsureDefaultSeriesForYearAsync(BillingDocumentKind.Ordinaria, currentYear);
        Assert.Equal(fSeriesBefore.LastNumber, fSeriesAfterDraft.LastNumber);
    }

    [Fact]
    public async Task ConfigurableTaxCatalog_SupportsCreateUpdateDeactivate_B2BFilter_And_PreservesHistoricalInvoiceSnapshot()
    {
        await fixture.EnsureConfiguredIssuerAsync();

        await using var db = fixture.CreateDbContext();
        var svc = fixture.CreateService(db);
        Assert.IsAssignableFrom<ITaxCatalogReader>(svc);

        var allTaxes = await svc.GetTaxesAsync(includeInactive: true);
        Assert.True(allTaxes.Count >= 10);

        // Verificar filtro B2C (no debe incluir R.E. ni IRPF que son B2B)
        var b2cTaxes = await svc.GetActiveTaxesAsync(TaxApplicability.B2C);
        Assert.DoesNotContain(b2cTaxes, t => t.Kind == TaxKind.RecargoEquivalencia);
        Assert.DoesNotContain(b2cTaxes, t => t.Kind == TaxKind.Irpf);

        // Verificar filtro B2B (incluye IVA Ambos + R.E. B2B + IRPF B2B)
        var b2bTaxes = await svc.GetActiveTaxesAsync(TaxApplicability.B2B);
        Assert.Contains(b2bTaxes, t => t.Kind == TaxKind.RecargoEquivalencia && t.Code == "RE-52");
        Assert.Contains(b2bTaxes, t => t.Kind == TaxKind.Irpf && t.Code == "IRPF-15");

        var iva21 = allTaxes.First(t => t.Code == "IVA-21");
        var re52 = allTaxes.First(t => t.Code == "RE-52");
        var irpf15 = allTaxes.First(t => t.Code == "IRPF-15");

        // Emitir una factura con IVA 21% + R.E. 5,2% + IRPF 15%
        var issued = await svc.CreateAndIssueAsync(new IssueInvoiceRequest(
            InvoiceType: InvoiceDocumentType.Ordinaria,
            PaymentMethod: BillingPaymentMethod.Contado,
            CustomerName: "Comercio Minorista Recargo S.L.",
            CustomerTaxId: "B12345674",
            CustomerAddress: "Calle Mayor 20, 28013 Madrid",
            Lines:
            [
                new IssueInvoiceLineRequest(
                    Description: "Mercancía con recargo",
                    Quantity: 1m,
                    UnitPriceNet: 100.00m,
                    VatPercentage: 21.00m,
                    VatTaxId: iva21.Id,
                    SurchargeTaxId: re52.Id,
                    SurchargePercentage: 5.20m)
            ],
            WithholdingTaxId: irpf15.Id,
            WithholdingPercentage: 15.00m));

        Assert.True(issued.Succeeded, issued.ErrorMessage);
        Assert.Equal(100.00m, issued.Invoice!.SubtotalNet);
        Assert.Equal(21.00m, issued.Invoice.TotalVat);
        Assert.Equal(5.20m, issued.Invoice.TotalSurcharge);
        Assert.Equal(26.20m, issued.Invoice.TotalTax);
        Assert.Equal(126.20m, issued.Invoice.TotalGross);
        Assert.Equal(15.00m, issued.Invoice.TotalWithholding);
        Assert.Equal(111.20m, issued.Invoice.TotalPayable);

        // Ahora creamos un impuesto nuevo, lo editamos y lo desactivamos; y renombramos RE-52 en el catálogo
        var createTax = await svc.CreateTaxAsync(new CreateTaxRateCommand(
            Code: "IVA-ESPECIAL-5",
            Name: "IVA Temporal 5%",
            Kind: TaxKind.Iva,
            Percentage: 5.00m,
            ParentVatTaxId: null,
            Applicability: TaxApplicability.Ambos));
        Assert.True(createTax.Succeeded, createTax.ErrorMessage);

        var createdTaxDto = (await svc.GetTaxesAsync(includeInactive: true))
            .First(t => t.Id == createTax.DocumentId!.Value);

        var deactivateRes = await svc.UpdateTaxAsync(new UpdateTaxRateCommand(
            Id: createdTaxDto.Id,
            Name: "IVA Temporal 5% (Histórico)",
            Applicability: TaxApplicability.B2B,
            IsActive: false,
            IsDefault: false,
            RowVersion: createdTaxDto.RowVersion));
        Assert.True(deactivateRes.Succeeded, deactivateRes.ErrorMessage);

        // Renombrar RE-52 en el catálogo
        var renameRe = await svc.UpdateTaxAsync(new UpdateTaxRateCommand(
            Id: re52.Id,
            Name: "R.E. 5,2% Renombrado en Catálogo",
            Applicability: re52.Applicability,
            IsActive: true,
            IsDefault: false,
            RowVersion: re52.RowVersion));
        Assert.True(renameRe.Succeeded, renameRe.ErrorMessage);

        // Comprobar que el impuesto desactivado no sale en activos pero sigue en BD
        var activeAfter = await svc.GetActiveTaxesAsync();
        Assert.DoesNotContain(activeAfter, t => t.Id == createdTaxDto.Id);
        var allAfter = await svc.GetTaxesAsync(includeInactive: true);
        Assert.Contains(allAfter, t => t.Id == createdTaxDto.Id && !t.IsActive);

        // Comprobar que la factura emitida conserva su snapshot original ("Recargo de Equivalencia 5,2%") inalterado
        var historicalInvoice = await svc.GetInvoiceByIdAsync(issued.Invoice.Id);
        Assert.NotNull(historicalInvoice);
        Assert.Equal(3, historicalInvoice.AppliedTaxes.Count);
        var snapshotRe = Assert.Single(historicalInvoice.AppliedTaxes, a => a.TaxKind == TaxKind.RecargoEquivalencia);
        Assert.Equal("Recargo de Equivalencia 5,2%", snapshotRe.TaxName);
        Assert.Equal(5.20m, snapshotRe.TaxPercentage);
        Assert.Equal(5.20m, snapshotRe.TaxAmount);
    }

    [Fact]
    public async Task DemoSandbox_AllowsQuotesAndDraftInvoices_But_BlocksFiscalEmissionAndConfiguration()
    {
        await fixture.EnsureConfiguredIssuerAsync();

        const string demoActor = "demo-998877665544";
        await using var db = fixture.CreateDbContext(demoActor);
        var demoSvc = fixture.CreateService(db, demoActor);

        // 1. El usuario Demo puede crear un presupuesto y un borrador de factura en su propio entorno aislado
        var quoteRes = await demoSvc.CreateQuoteAsync(new SaveQuoteCommand(
            Id: null,
            SeriesId: null,
            IssueDate: null,
            ValidUntilDate: null,
            CustomerName: "Cliente Demo Sandbox",
            CustomerTaxId: "12345678Z",
            CustomerAddress: "Calle Demo 1",
            Notes: null,
            Lines: [new DocumentLineInputDto("Concepto Demo", 1m, 50.00m, 21.00m)]));
        Assert.True(quoteRes.Succeeded, quoteRes.ErrorMessage);
        Assert.StartsWith("PD", quoteRes.FormattedNumber, StringComparison.Ordinal);

        var draftRes = await demoSvc.SaveDraftInvoiceAsync(new SaveDraftInvoiceCommand(
            Id: null,
            InvoiceType: InvoiceDocumentType.Ordinaria,
            SeriesId: null,
            IssueDate: null,
            CustomerName: "Cliente Demo Sandbox",
            CustomerTaxId: "12345678Z",
            CustomerAddress: "Calle Demo 1",
            PaymentMethod: BillingPaymentMethod.Contado,
            Notes: null,
            Lines: [new DocumentLineInputDto("Concepto Borrador Demo", 1m, 50.00m, 21.00m)]));
        Assert.True(draftRes.Succeeded, draftRes.ErrorMessage);

        // 2. El usuario Demo tiene BLOQUEADA la emisión fiscal definitiva y la configuración
        var draftDto = await demoSvc.GetInvoiceByIdAsync(draftRes.DocumentId!.Value);
        var issueAttempt = await demoSvc.IssueDraftInvoiceAsync(draftDto!.Id, draftDto.RowVersion);
        Assert.False(issueAttempt.Succeeded);
        Assert.Equal(FacturacionOperationStatus.ForbiddenInDemo, issueAttempt.Status);

        var rectAttempt = await demoSvc.IssueRectifyingInvoiceAsync(new IssueRectifyingInvoiceRequest(
            OriginalInvoiceId: Guid.NewGuid(),
            Scope: RectificationScope.Total,
            Reason: "Intento en demo",
            PaymentMethod: BillingPaymentMethod.Contado));
        Assert.False(rectAttempt.Succeeded);
        Assert.Equal(FacturacionOperationStatus.ForbiddenInDemo, rectAttempt.Status);

        var taxAttempt = await demoSvc.CreateTaxAsync(new CreateTaxRateCommand(
            Code: "DEMO-TAX",
            Name: "Impuesto Demo",
            Kind: TaxKind.Iva,
            Percentage: 12m,
            ParentVatTaxId: null,
            Applicability: TaxApplicability.Ambos));
        Assert.False(taxAttempt.Succeeded);
        Assert.Equal(FacturacionOperationStatus.ForbiddenInDemo, taxAttempt.Status);

        // 3. Limpieza al cerrar sesión Demo borra sus presupuestos, borradores y series sin tocar nada más
        var demoProvider = new FacturacionDemoDataProvider(db);
        await demoProvider.CleanupDemoDataAsync(demoActor);

        Assert.Empty(await demoSvc.GetQuotesAsync());
        Assert.Empty(await demoSvc.GetInvoicesAsync());
    }

    [Fact]
    public async Task PdfGenerator_ValidatesLogoFormats_GeneratesValidPdf14_And_LogsDownloadInAuditLogs()
    {
        await fixture.EnsureConfiguredIssuerAsync();

        // 1. Validar formatos de logo: JPEG válido y PNG RGB opaco válido -> OK; PNG RGBA con transparencia y WebP -> rechazados
        var validJpeg = CreateMinimalValidJpeg();
        var validOpaquePng = CreateMinimalPng(colorType: 2, includeTrnsChunk: false);
        var invalidTransparentRgbaPng = CreateMinimalPng(colorType: 6, includeTrnsChunk: false);
        var invalidTrnsPng = CreateMinimalPng(colorType: 2, includeTrnsChunk: true);
        var invalidWebp = "RIFF\x10\x00\x00\x00WEBP"u8.ToArray();

        var jpegInfo = PdfInvoiceGenerator.EnsureValidLogoFormat(validJpeg);
        Assert.Equal(2, jpegInfo.Width);
        Assert.Equal(2, jpegInfo.Height);

        var pngInfo = PdfInvoiceGenerator.EnsureValidLogoFormat(validOpaquePng);
        Assert.Equal(2, pngInfo.Width);
        Assert.Equal(2, pngInfo.Height);

        var rgbaEx = Assert.Throws<InvalidOperationException>(() =>
            PdfInvoiceGenerator.EnsureValidLogoFormat(invalidTransparentRgbaPng));
        Assert.Contains("transparencia", rgbaEx.Message, StringComparison.OrdinalIgnoreCase);

        var trnsEx = Assert.Throws<InvalidOperationException>(() =>
            PdfInvoiceGenerator.EnsureValidLogoFormat(invalidTrnsPng));
        Assert.Contains("transparencia", trnsEx.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Throws<InvalidOperationException>(() =>
            PdfInvoiceGenerator.EnsureValidLogoFormat(invalidWebp));

        // 2. Generar y descargar PDF de factura emitida y verificar que queda registrado en audit_logs
        await using var db = fixture.CreateDbContext("pdf-auditor");
        var svc = fixture.CreateService(db, "pdf-auditor");

        var issued = await svc.CreateAndIssueAsync(new IssueInvoiceRequest(
            InvoiceType: InvoiceDocumentType.Ordinaria,
            PaymentMethod: BillingPaymentMethod.Bizum,
            CustomerName: "Cliente Descarga PDF S.L.",
            CustomerTaxId: "B12345674",
            CustomerAddress: "Calle Serrano 40, 28001 Madrid",
            Lines: [new IssueInvoiceLineRequest("Licencia anual software", 1m, 250.00m, 21.00m)]));
        Assert.True(issued.Succeeded, issued.ErrorMessage);

        var pdfRes = await svc.DownloadInvoicePdfAsync(issued.Invoice!.Id);
        Assert.True(pdfRes.Succeeded, pdfRes.ErrorMessage);
        Assert.NotNull(pdfRes.PdfBytes);
        Assert.True(pdfRes.PdfBytes.Length > 400);

        var pdfHeader = Encoding.ASCII.GetString(pdfRes.PdfBytes, 0, 8);
        Assert.StartsWith("%PDF-1.4", pdfHeader, StringComparison.Ordinal);

        var entityIdStr = issued.Invoice.Id.ToString();
        var auditEntry = await db.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Action == "Facturacion.Invoice.DownloadPdf" && a.EntityId == entityIdStr);

        Assert.NotNull(auditEntry);
        Assert.Equal("pdf-auditor", auditEntry.ActorId);
    }

    [Fact]
    public void ModuleIsolation_Facturacion_DoesNotReference_Pos()
    {
        var facturacionTypes = typeof(FacturacionPermissions).Assembly.GetTypes()
            .Concat(typeof(FacturacionService).Assembly.GetTypes())
            .Where(t => t.Namespace is not null &&
                        t.Namespace.Contains("Modules.Facturacion", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(facturacionTypes);

        foreach (var type in facturacionTypes)
        {
            var fieldsAndProps = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
                .Select(f => f.FieldType)
                .Concat(type.GetProperties().Select(p => p.PropertyType));

            foreach (var memberType in fieldsAndProps)
            {
                Assert.False(
                    memberType.Namespace?.Contains("Modules.Pos", StringComparison.OrdinalIgnoreCase) ?? false,
                    $"El tipo {type.FullName} referencia indebidamente a {memberType.FullName}.");
            }
        }
    }

    private static SaveDraftInvoiceCommand CreateSampleDraftCommand(Guid seriesId, string customerName) =>
        new(
            Id: null,
            InvoiceType: InvoiceDocumentType.Ordinaria,
            SeriesId: seriesId,
            IssueDate: null,
            CustomerName: customerName,
            CustomerTaxId: "B12345674",
            CustomerAddress: "Calle Gran Vía 1, 28013 Madrid",
            PaymentMethod: BillingPaymentMethod.Contado,
            Notes: "Prueba de numeración",
            Lines:
            [
                new DocumentLineInputDto("Servicio técnico", 1m, 100.00m, 21.00m)
            ]);

    private static byte[] CreateMinimalValidJpeg() =>
    [
        0xFF, 0xD8, // SOI
        0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x02, 0x00, 0x02, 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01, // SOF0 2x2 RGB
        0xFF, 0xD9  // EOI
    ];

    private static byte[] CreateMinimalPng(byte colorType, bool includeTrnsChunk)
    {
        using var ms = new MemoryStream();
        // Signature
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        // IHDR chunk (length 13): width=2, height=2, bitDepth=8, colorType, compression=0, filter=0, interlace=0
        WritePngChunk(ms, "IHDR"u8, [0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x08, colorType, 0x00, 0x00, 0x00]);
        if (includeTrnsChunk)
        {
            WritePngChunk(ms, "tRNS"u8, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
        }

        // Minimal zlib-compressed scanlines for 2x2 RGB (each row: filter byte 0 + 6 bytes RGB = 7 bytes -> 14 bytes total)
        byte[] rawScanlines = colorType == 2
            ? [0, 255, 0, 0, 0, 255, 0, 0, 0, 0, 255, 255, 255, 255]
            : new byte[18];

        using var idatMs = new MemoryStream();
        idatMs.WriteByte(0x78);
        idatMs.WriteByte(0x9C);
        using (var deflate = new System.IO.Compression.DeflateStream(idatMs, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(rawScanlines);
        }

        idatMs.Write([0x00, 0x00, 0x00, 0x01]);
        WritePngChunk(ms, "IDAT"u8, idatMs.ToArray());
        WritePngChunk(ms, "IEND"u8, []);
        return ms.ToArray();
    }

    private static void WritePngChunk(Stream output, ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> data)
    {
        Span<byte> lenBytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)data.Length);
        output.Write(lenBytes);
        output.Write(chunkType);
        output.Write(data);
        output.Write([0x00, 0x00, 0x00, 0x00]); // CRC placeholder
    }

    private sealed class ThrowingFiscalInvoiceRegistry : IFiscalInvoiceRegistry
    {
        public Task<FiscalRegistrationResult> RegisterIssuedInvoiceAsync(
            IssuedInvoiceFiscalContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Fallo simulado tras reservar número correlativo para verificar rollback sin huecos.");
        }
    }
}
