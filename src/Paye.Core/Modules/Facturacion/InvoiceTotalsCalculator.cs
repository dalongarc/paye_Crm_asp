using System.Globalization;
using Paye.Core.Invoicing;
using Paye.Core.ValueObjects;

namespace Paye.Core.Modules.Facturacion;

/// <summary>
/// Estrategia de cálculo de la cuota de IVA y Recargo de Equivalencia:
/// - AggregateByVatRate (por defecto): agrupa las bases imponibles (LineNet) por cada combinación
///   (VatPercentage, SurchargePercentage) y calcula las cuotas sobre la base agregada del grupo,
///   garantizando que el total de la factura cuadre siempre al céntimo con el bloque DetalleDesglose de la AEAT.
/// - SumLineByLine: redondea la cuota en cada línea y suma las cuotas individuales.
/// </summary>
public enum VatCalculationMode
{
    AggregateByVatRate = 1,
    SumLineByLine = 2
}

public sealed record TaxedLineInput(
    string Description,
    decimal Quantity,
    decimal UnitPriceNet,
    decimal VatPercentage,
    Guid? VatTaxId = null,
    string? VatTaxName = null,
    Guid? SurchargeTaxId = null,
    string? SurchargeTaxName = null,
    decimal SurchargePercentage = 0m,
    string AeatTaxCode = "01",
    string AeatRegimeKey = "01",
    string AeatOperationQualification = "S1");

public sealed record WithholdingTaxInput(
    decimal Percentage,
    Guid? TaxId = null,
    string? TaxName = null);

public sealed record VatBreakdownItem(
    decimal VatPercentage,
    string VatTaxName,
    Money TaxableBase,
    Money TaxAmount,
    decimal SurchargePercentage,
    string? SurchargeTaxName,
    Money SurchargeAmount,
    Money GrossAmount,
    string AeatTaxCode = "01",
    string AeatRegimeKey = "01",
    string AeatOperationQualification = "S1");

public sealed record AppliedTaxSnapshotItem(
    Guid? TaxId,
    TaxKind TaxKind,
    string TaxName,
    decimal TaxPercentage,
    decimal? LinkedVatPercentage,
    string AeatTaxCode,
    string AeatRegimeKey,
    string AeatOperationQualification,
    Money TaxableBase,
    Money TaxAmount);

public sealed record CalculatedDocumentLine(
    int LineOrder,
    string Description,
    decimal Quantity,
    decimal UnitPriceNet,
    Guid? VatTaxId,
    string VatTaxName,
    decimal VatPercentage,
    Guid? SurchargeTaxId,
    string? SurchargeTaxName,
    decimal SurchargePercentage,
    Money LineNet);

public sealed record CalculatedDocumentTotals(
    IReadOnlyList<CalculatedDocumentLine> Lines,
    IReadOnlyList<VatBreakdownItem> VatBreakdown,
    IReadOnlyList<AppliedTaxSnapshotItem> AppliedTaxes,
    Money SubtotalNet,
    Money TotalVat,
    Money TotalSurcharge,
    Money TotalTax,
    Money TotalGross,
    Money TotalWithholding,
    Money TotalPayable);

/// <summary>
/// Único punto de cálculo de bases imponibles por línea, desglose de IVA, Recargo de Equivalencia,
/// retención de IRPF y totales del documento.
/// Cumple con la Orden HAC/1177/2024 (VERI*FACTU / NO VERI*FACTU):
/// - TotalTax (CuotaTotal AEAT) = TotalVat + TotalSurcharge
/// - TotalGross (ImporteTotal AEAT) = SubtotalNet + TotalTax (sin descontar IRPF)
/// - TotalPayable (Líquido a pagar comercial) = TotalGross - TotalWithholding
/// </summary>
public static class InvoiceTotalsCalculator
{
    public const VatCalculationMode DefaultCalculationMode = VatCalculationMode.AggregateByVatRate;

    public static decimal RoundMonetary(decimal value, int decimals = 2) =>
        decimal.Round(value, decimals, Money.DefaultRounding);

    public static Money CalculateLineNet(decimal quantity, decimal unitPriceNet, string currency = "EUR")
    {
        var rawNet = quantity * unitPriceNet;
        return new Money(rawNet, currency, 2);
    }

    public static CalculatedDocumentTotals Calculate(
        IEnumerable<(string Description, decimal Quantity, decimal UnitPriceNet, decimal VatPercentage)> rawLines,
        string currency = "EUR",
        VatCalculationMode mode = DefaultCalculationMode)
    {
        var mapped = rawLines.Select(l => new TaxedLineInput(
            Description: l.Description,
            Quantity: l.Quantity,
            UnitPriceNet: l.UnitPriceNet,
            VatPercentage: l.VatPercentage));

        return Calculate(mapped, withholding: null, currency: currency, mode: mode);
    }

    public static CalculatedDocumentTotals Calculate(
        IEnumerable<TaxedLineInput> rawLines,
        WithholdingTaxInput? withholding = null,
        string currency = "EUR",
        VatCalculationMode mode = DefaultCalculationMode)
    {
        ArgumentNullException.ThrowIfNull(rawLines);

        var calculatedLines = new List<CalculatedDocumentLine>();
        var inputWithOrder = new List<(CalculatedDocumentLine Line, TaxedLineInput Raw)>();
        var order = 1;

        foreach (var item in rawLines)
        {
            var vatPct = RoundMonetary(item.VatPercentage, 2);
            var surchargePct = RoundMonetary(item.SurchargePercentage, 2);
            var unitNet = RoundMonetary(item.UnitPriceNet, 2);
            var qty = decimal.Round(item.Quantity, 4, Money.DefaultRounding);
            var lineNet = CalculateLineNet(qty, unitNet, currency);

            var vatName = string.IsNullOrWhiteSpace(item.VatTaxName)
                ? $"IVA {vatPct.ToString("0.##", CultureInfo.InvariantCulture)}%"
                : item.VatTaxName.Trim();

            var surchargeName = surchargePct != 0m
                ? (string.IsNullOrWhiteSpace(item.SurchargeTaxName)
                    ? $"R.E. {surchargePct.ToString("0.##", CultureInfo.InvariantCulture)}%"
                    : item.SurchargeTaxName.Trim())
                : null;

            var line = new CalculatedDocumentLine(
                LineOrder: order++,
                Description: item.Description.Trim(),
                Quantity: qty,
                UnitPriceNet: unitNet,
                VatTaxId: item.VatTaxId,
                VatTaxName: vatName,
                VatPercentage: vatPct,
                SurchargeTaxId: surchargePct != 0m ? item.SurchargeTaxId : null,
                SurchargeTaxName: surchargeName,
                SurchargePercentage: surchargePct,
                LineNet: lineNet);

            calculatedLines.Add(line);
            inputWithOrder.Add((line, item));
        }

        var breakdown = new List<VatBreakdownItem>();
        var appliedTaxes = new List<AppliedTaxSnapshotItem>();

        foreach (var group in inputWithOrder
                     .GroupBy(x => (x.Line.VatPercentage, x.Line.SurchargePercentage))
                     .OrderBy(g => g.Key.VatPercentage)
                     .ThenBy(g => g.Key.SurchargePercentage))
        {
            var first = group.First();
            var vatRate = new VatRate(group.Key.VatPercentage);
            var surchargeRate = new VatRate(group.Key.SurchargePercentage);

            var groupBase = Money.Zero(currency);
            foreach (var entry in group)
            {
                groupBase += entry.Line.LineNet;
            }

            Money groupVat;
            Money groupSurcharge;
            if (mode == VatCalculationMode.AggregateByVatRate)
            {
                groupVat = vatRate.CalculateTaxFromNet(groupBase);
                groupSurcharge = group.Key.SurchargePercentage != 0m
                    ? surchargeRate.CalculateTaxFromNet(groupBase)
                    : Money.Zero(currency);
            }
            else
            {
                groupVat = Money.Zero(currency);
                groupSurcharge = Money.Zero(currency);
                foreach (var entry in group)
                {
                    groupVat += vatRate.CalculateTaxFromNet(entry.Line.LineNet);
                    if (group.Key.SurchargePercentage != 0m)
                    {
                        groupSurcharge += surchargeRate.CalculateTaxFromNet(entry.Line.LineNet);
                    }
                }
            }

            var aeatTaxCode = string.IsNullOrWhiteSpace(first.Raw.AeatTaxCode) ? "01" : first.Raw.AeatTaxCode;
            var aeatRegimeKey = group.Key.SurchargePercentage != 0m && first.Raw.AeatRegimeKey == "01"
                ? "18"
                : (string.IsNullOrWhiteSpace(first.Raw.AeatRegimeKey) ? "01" : first.Raw.AeatRegimeKey);
            var aeatQualification = string.IsNullOrWhiteSpace(first.Raw.AeatOperationQualification)
                ? (group.Key.VatPercentage == 0m ? "E1" : "S1")
                : first.Raw.AeatOperationQualification;

            var groupGross = groupBase + groupVat + groupSurcharge;

            breakdown.Add(new VatBreakdownItem(
                VatPercentage: vatRate.Percentage,
                VatTaxName: first.Line.VatTaxName,
                TaxableBase: groupBase,
                TaxAmount: groupVat,
                SurchargePercentage: surchargeRate.Percentage,
                SurchargeTaxName: first.Line.SurchargeTaxName,
                SurchargeAmount: groupSurcharge,
                GrossAmount: groupGross,
                AeatTaxCode: aeatTaxCode,
                AeatRegimeKey: aeatRegimeKey,
                AeatOperationQualification: aeatQualification));

            appliedTaxes.Add(new AppliedTaxSnapshotItem(
                TaxId: first.Line.VatTaxId,
                TaxKind: TaxKind.Iva,
                TaxName: first.Line.VatTaxName,
                TaxPercentage: vatRate.Percentage,
                LinkedVatPercentage: null,
                AeatTaxCode: aeatTaxCode,
                AeatRegimeKey: aeatRegimeKey,
                AeatOperationQualification: aeatQualification,
                TaxableBase: groupBase,
                TaxAmount: groupVat));

            if (group.Key.SurchargePercentage != 0m)
            {
                appliedTaxes.Add(new AppliedTaxSnapshotItem(
                    TaxId: first.Line.SurchargeTaxId,
                    TaxKind: TaxKind.RecargoEquivalencia,
                    TaxName: first.Line.SurchargeTaxName ?? $"R.E. {surchargeRate.Percentage.ToString("0.##", CultureInfo.InvariantCulture)}%",
                    TaxPercentage: surchargeRate.Percentage,
                    LinkedVatPercentage: vatRate.Percentage,
                    AeatTaxCode: aeatTaxCode,
                    AeatRegimeKey: "18",
                    AeatOperationQualification: aeatQualification,
                    TaxableBase: groupBase,
                    TaxAmount: groupSurcharge));
            }
        }

        var subtotalNet = Money.Zero(currency);
        var totalVat = Money.Zero(currency);
        var totalSurcharge = Money.Zero(currency);

        foreach (var b in breakdown)
        {
            subtotalNet += b.TaxableBase;
            totalVat += b.TaxAmount;
            totalSurcharge += b.SurchargeAmount;
        }

        var totalTax = totalVat + totalSurcharge;
        var totalGross = subtotalNet + totalTax;

        var totalWithholding = Money.Zero(currency);
        if (withholding is not null && RoundMonetary(withholding.Percentage, 2) > 0m)
        {
            var irpfPct = RoundMonetary(withholding.Percentage, 2);
            var irpfRate = new VatRate(irpfPct);
            totalWithholding = irpfRate.CalculateTaxFromNet(subtotalNet);

            var irpfName = string.IsNullOrWhiteSpace(withholding.TaxName)
                ? $"Retención IRPF {irpfPct.ToString("0.##", CultureInfo.InvariantCulture)}%"
                : withholding.TaxName.Trim();

            appliedTaxes.Add(new AppliedTaxSnapshotItem(
                TaxId: withholding.TaxId,
                TaxKind: TaxKind.Irpf,
                TaxName: irpfName,
                TaxPercentage: irpfPct,
                LinkedVatPercentage: null,
                AeatTaxCode: "IRPF",
                AeatRegimeKey: "01",
                AeatOperationQualification: "NA",
                TaxableBase: subtotalNet,
                TaxAmount: totalWithholding));
        }

        var totalPayable = totalGross - totalWithholding;

        return new CalculatedDocumentTotals(
            Lines: calculatedLines,
            VatBreakdown: breakdown,
            AppliedTaxes: appliedTaxes,
            SubtotalNet: subtotalNet,
            TotalVat: totalVat,
            TotalSurcharge: totalSurcharge,
            TotalTax: totalTax,
            TotalGross: totalGross,
            TotalWithholding: totalWithholding,
            TotalPayable: totalPayable);
    }
}
