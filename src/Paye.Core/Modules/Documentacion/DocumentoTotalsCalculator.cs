namespace Paye.Core.Modules.Documentacion;

/// <summary>
/// Calculador fiscal determinista en servidor para Presupuestos y Contratos.
/// Valida que cada línea use un tipo de IVA permitido (21%, 10%, 4%, 0%),
/// calcula descuento por línea, base imponible, cuota de IVA y agrupa el desglose por tipo de IVA.
/// </summary>
public static class DocumentoTotalsCalculator
{
    public static readonly IReadOnlySet<decimal> AllowedVatRates = new HashSet<decimal>
    {
        21m, 10m, 4m, 0m
    };

    public static decimal RoundCurrency(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal RoundQuantity(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);

    public static (CalculatedDocumentoTotals? Result, string? ErrorMessage) ValidateAndCalculate(
        IReadOnlyList<DocumentoLineaInputDto>? inputLines,
        bool requireAtLeastOneLine)
    {
        if (inputLines is null || inputLines.Count == 0)
        {
            if (requireAtLeastOneLine)
            {
                return (null, "El presupuesto debe incluir al menos una línea de concepto.");
            }

            return (CalculatedDocumentoTotals.Empty, null);
        }

        var calculatedLines = new List<CalculatedDocumentoLinea>(inputLines.Count);

        for (var i = 0; i < inputLines.Count; i++)
        {
            var raw = inputLines[i];
            var lineNumber = i + 1;

            if (string.IsNullOrWhiteSpace(raw.Descripcion) || raw.Descripcion.Trim().Length > 500)
            {
                return (null, $"La línea {lineNumber} debe tener una descripción válida (máximo 500 caracteres).");
            }

            var cantidad = RoundQuantity(raw.Cantidad);
            if (cantidad <= 0m || cantidad > 1_000_000m)
            {
                return (null, $"La cantidad en la línea {lineNumber} debe ser mayor que 0.");
            }

            var precioUnitario = RoundCurrency(raw.PrecioUnitario);
            if (precioUnitario < 0m || precioUnitario > 100_000_000m)
            {
                return (null, $"El precio unitario en la línea {lineNumber} no puede ser negativo.");
            }

            var descuentoPct = RoundCurrency(raw.DescuentoPorcentaje);
            if (descuentoPct < 0m || descuentoPct > 100m)
            {
                return (null, $"El descuento en la línea {lineNumber} debe estar entre 0% y 100%.");
            }

            var ivaPct = RoundCurrency(raw.IvaPorcentaje);
            if (!AllowedVatRates.Contains(ivaPct))
            {
                return (null, $"El tipo de IVA ({ivaPct}%) en la línea {lineNumber} no es válido. Tipos admitidos: 21%, 10%, 4% y 0%.");
            }

            var importeBruto = RoundCurrency(cantidad * precioUnitario);
            var importeDescuento = descuentoPct > 0m
                ? RoundCurrency(importeBruto * (descuentoPct / 100m))
                : 0m;
            var baseImponible = RoundCurrency(importeBruto - importeDescuento);
            var cuotaIva = ivaPct > 0m
                ? RoundCurrency(baseImponible * (ivaPct / 100m))
                : 0m;
            var totalLinea = RoundCurrency(baseImponible + cuotaIva);

            calculatedLines.Add(new CalculatedDocumentoLinea(
                Orden: i + 1,
                Descripcion: raw.Descripcion.Trim(),
                Cantidad: cantidad,
                PrecioUnitario: precioUnitario,
                DescuentoPorcentaje: descuentoPct,
                IvaPorcentaje: ivaPct,
                ImporteBruto: importeBruto,
                ImporteDescuento: importeDescuento,
                BaseImponible: baseImponible,
                CuotaIva: cuotaIva,
                TotalLinea: totalLinea));
        }

        // Agrupar el desglose de IVA por cada tipo aplicado (21%, 10%, 4%, 0%), ordenado de mayor a menor.
        // La cuota del grupo suma las cuotas redondeadas por línea (MidpointRounding.AwayFromZero)
        // para garantizar que Suma(TotalLinea) == Suma(DesgloseIva) == BaseImponible + TotalIva == TotalGeneral al céntimo exacto.
        var desgloseIva = calculatedLines
            .GroupBy(l => l.IvaPorcentaje)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var baseGrupo = RoundCurrency(g.Sum(x => x.BaseImponible));
                var cuotaGrupo = RoundCurrency(g.Sum(x => x.CuotaIva));
                var totalGrupo = RoundCurrency(baseGrupo + cuotaGrupo);

                return new DesgloseIvaItemDto(
                    IvaPorcentaje: g.Key,
                    Etiqueta: g.Key == 0m ? "Exento / 0%" : $"IVA {g.Key:0.##}%",
                    BaseImponible: baseGrupo,
                    CuotaIva: cuotaGrupo,
                    TotalConIva: totalGrupo);
            })
            .ToList();

        // Asegurar coherencia de céntimos entre suma de bases y suma de cuotas agrupadas
        var subtotalBruto = RoundCurrency(calculatedLines.Sum(l => l.ImporteBruto));
        var totalDescuento = RoundCurrency(calculatedLines.Sum(l => l.ImporteDescuento));
        var baseImponibleTotal = RoundCurrency(desgloseIva.Sum(d => d.BaseImponible));
        var totalIva = RoundCurrency(desgloseIva.Sum(d => d.CuotaIva));
        var totalGeneral = RoundCurrency(baseImponibleTotal + totalIva);

        return (new CalculatedDocumentoTotals(
            SubtotalBruto: subtotalBruto,
            TotalDescuento: totalDescuento,
            BaseImponible: baseImponibleTotal,
            TotalIva: totalIva,
            TotalGeneral: totalGeneral,
            Lineas: calculatedLines,
            DesgloseIva: desgloseIva), null);
    }
}

public sealed record CalculatedDocumentoLinea(
    int Orden,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal DescuentoPorcentaje,
    decimal IvaPorcentaje,
    decimal ImporteBruto,
    decimal ImporteDescuento,
    decimal BaseImponible,
    decimal CuotaIva,
    decimal TotalLinea);

public sealed record CalculatedDocumentoTotals(
    decimal SubtotalBruto,
    decimal TotalDescuento,
    decimal BaseImponible,
    decimal TotalIva,
    decimal TotalGeneral,
    IReadOnlyList<CalculatedDocumentoLinea> Lineas,
    IReadOnlyList<DesgloseIvaItemDto> DesgloseIva)
{
    public static readonly CalculatedDocumentoTotals Empty = new(
        0m, 0m, 0m, 0m, 0m, [], []);
}
