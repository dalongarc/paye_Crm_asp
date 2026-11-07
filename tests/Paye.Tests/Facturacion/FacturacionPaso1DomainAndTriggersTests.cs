using Microsoft.EntityFrameworkCore;
using Npgsql;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;
using Paye.Infrastructure.Data;
using Paye.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Paye.Tests.Facturacion;

public sealed class FacturacionPaso1PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("paye_test_facturacion_p1")
        .WithUsername("paye_test_user")
        .WithPassword("TestOnly_NotProd_FactP1!")
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

public sealed class FacturacionPaso1DomainAndTriggersTests(FacturacionPaso1PostgresFixture dbFixture)
    : IClassFixture<FacturacionPaso1PostgresFixture>
{
    [Theory]
    [InlineData("12345678Z", true)]
    [InlineData("ES12345678Z", true)]
    [InlineData("12345678-Z", true)]
    [InlineData("12345678A", false)]
    [InlineData("X1234567L", true)]
    [InlineData("Y1234567X", true)]
    [InlineData("Z1234567R", true)]
    [InlineData("X1234567A", false)]
    [InlineData("B12345674", true)]
    [InlineData("A58818501", true)]
    [InlineData("Q2826000H", true)]
    [InlineData("B12345679", false)]
    [InlineData("Q28260004", false)]
    [InlineData("12345", false)]
    public void SpanishTaxIdValidator_Validates_Format_And_ControlDigit_For_Nif_Nie_And_Cif(string taxId, bool expectedValid)
    {
        var isValid = SpanishTaxIdValidator.IsValid(taxId, out var error);

        Assert.Equal(expectedValid, isValid);
        if (expectedValid)
        {
            Assert.Null(error);
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
    }

    [Fact]
    public void InvoiceTotalsCalculator_Persists_Only_LineNet_And_Guarantees_Totals_Match_VatBreakdown()
    {
        var rawLines = new (string Description, decimal Quantity, decimal UnitPriceNet, decimal VatPercentage)[]
        {
            ("Producto A (21%)", 1m, 1.02m, 21m),
            ("Producto B (21%)", 1m, 1.02m, 21m),
            ("Producto C (10%)", 2m, 3.33m, 10m)
        };

        var result = InvoiceTotalsCalculator.Calculate(rawLines, "EUR", VatCalculationMode.AggregateByVatRate);

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(2, result.VatBreakdown.Count);

        var sumBreakdownBase = result.VatBreakdown.Sum(b => b.TaxableBase.Amount);
        var sumBreakdownTax = result.VatBreakdown.Sum(b => b.TaxAmount.Amount);
        var sumBreakdownGross = result.VatBreakdown.Sum(b => b.GrossAmount.Amount);

        Assert.Equal(result.SubtotalNet.Amount, sumBreakdownBase);
        Assert.Equal(result.TotalTax.Amount, sumBreakdownTax);
        Assert.Equal(result.TotalGross.Amount, sumBreakdownGross);
        Assert.Equal(result.SubtotalNet.Amount + result.TotalTax.Amount, result.TotalGross.Amount);

        // Verificar que las entidades de línea solo tienen LineNet y no LineTax
        var lineProps = typeof(InvoiceLine).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(nameof(InvoiceLine.LineNet), lineProps);
        Assert.DoesNotContain("LineTax", lineProps);
        Assert.DoesNotContain("LineGross", lineProps);
    }

    [Fact]
    public void InvoiceTotalsCalculator_Calculates_Iva_RecargoEquivalencia_And_Irpf_Matching_Aeat_Verifactu_Rules()
    {
        // Ejemplo B2B con IVA 21% + R.E. 5,2% en línea 1, IVA 10% + R.E. 1,4% en línea 2, y retención IRPF 15%
        var lines = new[]
        {
            new TaxedLineInput(
                Description: "Lote cafeteras profesionales",
                Quantity: 1m,
                UnitPriceNet: 100.00m,
                VatPercentage: 21.00m,
                VatTaxName: "IVA General 21%",
                SurchargeTaxName: "Recargo de Equivalencia 5,2%",
                SurchargePercentage: 5.20m),
            new TaxedLineInput(
                Description: "Café en grano tueste natural",
                Quantity: 2m,
                UnitPriceNet: 50.00m,
                VatPercentage: 10.00m,
                VatTaxName: "IVA Reducido 10%",
                SurchargeTaxName: "Recargo de Equivalencia 1,4%",
                SurchargePercentage: 1.40m)
        };

        var withholding = new WithholdingTaxInput(15.00m, null, "Retención IRPF Profesional 15%");
        var result = InvoiceTotalsCalculator.Calculate(lines, withholding);

        // Base total = 100 + 100 = 200.00
        // IVA = 21.00 + 10.00 = 31.00
        // R.E. = 5.20 + 1.40 = 6.60
        // TotalTax (CuotaTotal AEAT) = 31.00 + 6.60 = 37.60
        // TotalGross (ImporteTotal AEAT) = 200.00 + 37.60 = 237.60 (sin restar IRPF)
        // TotalWithholding (IRPF 15% s/ 200.00) = 30.00
        // TotalPayable (Líquido comercial) = 237.60 - 30.00 = 207.60
        Assert.Equal(200.00m, result.SubtotalNet.Amount);
        Assert.Equal(31.00m, result.TotalVat.Amount);
        Assert.Equal(6.60m, result.TotalSurcharge.Amount);
        Assert.Equal(37.60m, result.TotalTax.Amount);
        Assert.Equal(237.60m, result.TotalGross.Amount);
        Assert.Equal(30.00m, result.TotalWithholding.Amount);
        Assert.Equal(207.60m, result.TotalPayable.Amount);

        // Snapshot AppliedTaxes: 2 filas IVA + 2 filas R.E. (ClaveRegimen 18) + 1 fila IRPF = 5 filas
        Assert.Equal(5, result.AppliedTaxes.Count);
        Assert.All(
            result.AppliedTaxes.Where(t => t.TaxKind == TaxKind.RecargoEquivalencia),
            re => Assert.Equal("18", re.AeatRegimeKey));
    }

    [Fact]
    public async Task PostgreSqlTriggers_Reject_Update_And_Delete_On_Issued_Invoice_Lines_And_TaxBreakdowns()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PayeDbContext>();
        optionsBuilder.UseNpgsql(dbFixture.ConnectionString);

        var seriesId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var taxBreakdownId = Guid.NewGuid();

        await using (var db = new PayeDbContext(optionsBuilder.Options))
        {
            var draft = new Invoice
            {
                Id = invoiceId,
                InvoiceType = InvoiceDocumentType.Ordinaria,
                Status = InvoiceStatus.Borrador,
                SeriesId = seriesId,
                SeriesCode = "F",
                FiscalYear = 2026,
                IssueDate = new DateOnly(2026, 10, 26),
                IssuerTaxName = "Comercio Pruebas S.L.",
                IssuerTaxId = "B12345674",
                IssuerFiscalAddress = "Calle Mayor 1, 28001 Madrid",
                CustomerName = "Cliente Demo S.L.",
                CustomerTaxId = "A58818501",
                CustomerAddress = "Gran Via 10, 28013 Madrid",
                PaymentMethod = BillingPaymentMethod.Tarjeta,
                SubtotalNet = 100.00m,
                TotalVat = 21.00m,
                TotalSurcharge = 0.00m,
                TotalTax = 21.00m,
                TotalGross = 121.00m,
                TotalWithholding = 0.00m,
                TotalPayable = 121.00m,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "test-actor",
                Lines =
                [
                    new InvoiceLine
                    {
                        Id = lineId,
                        InvoiceId = invoiceId,
                        LineOrder = 1,
                        Description = "Servicio de consultoría",
                        Quantity = 1m,
                        UnitPriceNet = 100.00m,
                        VatTaxName = "IVA General 21%",
                        VatPercentage = 21.00m,
                        LineNet = 100.00m
                    }
                ],
                TaxBreakdowns =
                [
                    new InvoiceTaxBreakdown
                    {
                        Id = taxBreakdownId,
                        InvoiceId = invoiceId,
                        TaxKind = TaxKind.Iva,
                        TaxName = "IVA General 21%",
                        TaxPercentage = 21.00m,
                        AeatTaxCode = "01",
                        AeatRegimeKey = "01",
                        AeatOperationQualification = "S1",
                        TaxableBase = 100.00m,
                        TaxAmount = 21.00m
                    }
                ]
            };

            db.Set<Invoice>().Add(draft);
            await db.SaveChangesAsync();

            // Mientras está en Borrador se permite actualizar y finalmente pasar a Emitida
            draft.Notes = "Borrador revisado antes de emitir";
            await db.SaveChangesAsync();

            draft.Status = InvoiceStatus.Emitida;
            draft.SequentialNumber = 1;
            draft.FormattedNumber = "F-2026-0001";
            draft.IssuedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        await using var conn = new NpgsqlConnection(dbFixture.ConnectionString);
        await conn.OpenAsync();

        // 1. UPDATE sobre factura emitida -> rechazado por trg_facturacion_facturas_immutable (SQLSTATE 55000)
        await using (var updateInvoiceCmd = new NpgsqlCommand(
            """UPDATE facturacion_facturas SET "TotalGross" = 999.99 WHERE "Id" = @id;""", conn))
        {
            updateInvoiceCmd.Parameters.AddWithValue("id", invoiceId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => updateInvoiceCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }

        // 2. DELETE sobre factura emitida -> rechazado por trg_facturacion_facturas_immutable (SQLSTATE 55000)
        await using (var deleteInvoiceCmd = new NpgsqlCommand(
            """DELETE FROM facturacion_facturas WHERE "Id" = @id;""", conn))
        {
            deleteInvoiceCmd.Parameters.AddWithValue("id", invoiceId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => deleteInvoiceCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }

        // 3. UPDATE sobre línea de factura emitida -> rechazado por trg_facturacion_factura_lineas_immutable (SQLSTATE 55000)
        await using (var updateLineCmd = new NpgsqlCommand(
            """UPDATE facturacion_factura_lineas SET "LineNet" = 50.00 WHERE "Id" = @lineId;""", conn))
        {
            updateLineCmd.Parameters.AddWithValue("lineId", lineId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => updateLineCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }

        // 4. DELETE sobre línea de factura emitida -> rechazado por trg_facturacion_factura_lineas_immutable (SQLSTATE 55000)
        await using (var deleteLineCmd = new NpgsqlCommand(
            """DELETE FROM facturacion_factura_lineas WHERE "Id" = @lineId;""", conn))
        {
            deleteLineCmd.Parameters.AddWithValue("lineId", lineId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => deleteLineCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }

        // 5. INSERT de nueva línea en factura ya emitida -> rechazado por trg_facturacion_factura_lineas_immutable (SQLSTATE 55000)
        await using (var insertLineCmd = new NpgsqlCommand(
            """
            INSERT INTO facturacion_factura_lineas ("Id", "InvoiceId", "LineOrder", "Description", "Quantity", "UnitPriceNet", "VatTaxName", "VatPercentage", "SurchargePercentage", "LineNet")
            VALUES (@newId, @invoiceId, 2, 'Linea intrusa', 1, 10, 'IVA 21%', 21, 0, 10);
            """, conn))
        {
            insertLineCmd.Parameters.AddWithValue("newId", Guid.NewGuid());
            insertLineCmd.Parameters.AddWithValue("invoiceId", invoiceId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => insertLineCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }

        // 6. UPDATE sobre desglose fiscal (facturacion_factura_impuestos) de factura emitida -> rechazado (SQLSTATE 55000)
        await using (var updateTaxCmd = new NpgsqlCommand(
            """UPDATE facturacion_factura_impuestos SET "TaxAmount" = 0.00 WHERE "Id" = @taxId;""", conn))
        {
            updateTaxCmd.Parameters.AddWithValue("taxId", taxBreakdownId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => updateTaxCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }

        // 7. DELETE sobre desglose fiscal (facturacion_factura_impuestos) de factura emitida -> rechazado (SQLSTATE 55000)
        await using (var deleteTaxCmd = new NpgsqlCommand(
            """DELETE FROM facturacion_factura_impuestos WHERE "Id" = @taxId;""", conn))
        {
            deleteTaxCmd.Parameters.AddWithValue("taxId", taxBreakdownId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => deleteTaxCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }
    }
}
