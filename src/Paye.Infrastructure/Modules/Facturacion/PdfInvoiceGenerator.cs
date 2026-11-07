using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Paye.Core.Invoicing;
using Paye.Core.Modules.Facturacion;

namespace Paye.Infrastructure.Modules.Facturacion;

/// <summary>
/// Generador nativo de documentos PDF 1.4 en C# puro (Opción A, 0 paquetes NuGet externos, 100% compatible con contenedores runtime-deps chiseled).
/// Valida estrictamente el logotipo para admitir únicamente JPEG o PNG opaco (sin canal alfa ni transparencia).
/// </summary>
public sealed class PdfInvoiceGenerator : IPdfInvoiceGenerator
{
    static PdfInvoiceGenerator()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public byte[] GenerateInvoicePdf(
        InvoiceDto invoice,
        BillingSettingsDto settings,
        byte[]? logoBytes = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(settings);

        var parsedLogo = logoBytes is { Length: > 0 }
            ? ValidateAndParseLogo(logoBytes)
            : (ParsedPdfImage?)null;

        var title = invoice.Status == InvoiceStatus.Borrador
            ? "BORRADOR DE FACTURA"
            : invoice.InvoiceType switch
            {
                InvoiceDocumentType.Rectificativa => "FACTURA RECTIFICATIVA",
                InvoiceDocumentType.Simplificada => "FACTURA SIMPLIFICADA",
                _ => "FACTURA"
            };

        var numberLabel = invoice.FormattedNumber ?? $"BORRADOR ({invoice.SeriesCode}-{invoice.FiscalYear.ToString(CultureInfo.InvariantCulture)})";
        var subtitle = $"Número: {numberLabel}   |   Fecha expedición: {invoice.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}   |   Pago: {invoice.PaymentMethod}";

        var extraMetaLines = new List<string>();
        if (invoice.InvoiceType == InvoiceDocumentType.Rectificativa)
        {
            var scopeText = invoice.RectificationScope == RectificationScope.Total ? "Sustitución / Total" : "Por diferencias / Parcial";
            extraMetaLines.Add($"Rectifica a factura ID: {invoice.RectifiedInvoiceId?.ToString() ?? "-"} ({scopeText})");
            if (!string.IsNullOrWhiteSpace(invoice.RectificationReason))
            {
                extraMetaLines.Add($"Motivo rectificación: {invoice.RectificationReason}");
            }
        }

        var lines = invoice.Lines.Select(l => new PdfLineRow(
            l.LineOrder,
            l.Description,
            l.Quantity,
            l.UnitPriceNet,
            l.VatTaxName,
            l.VatPercentage,
            l.SurchargePercentage,
            l.LineNet)).ToList();

        var breakdowns = invoice.AppliedTaxes.Select(b => new PdfTaxBreakdownRow(
            b.TaxName,
            b.TaxKind,
            b.TaxPercentage,
            b.TaxableBase,
            b.TaxAmount)).ToList();

        return BuildPdfDocument(
            documentTitle: title,
            subtitle: subtitle,
            issuerName: string.IsNullOrWhiteSpace(invoice.IssuerTaxName) ? settings.TaxName : invoice.IssuerTaxName,
            issuerTaxId: string.IsNullOrWhiteSpace(invoice.IssuerTaxId) ? settings.TaxId : invoice.IssuerTaxId,
            issuerAddress: string.IsNullOrWhiteSpace(invoice.IssuerFiscalAddress) ? settings.FiscalAddress : invoice.IssuerFiscalAddress,
            customerName: invoice.CustomerName,
            customerTaxId: invoice.CustomerTaxId,
            customerAddress: invoice.CustomerAddress,
            extraMetaLines: extraMetaLines,
            lines: lines,
            taxBreakdowns: breakdowns,
            subtotalNet: invoice.SubtotalNet,
            totalVat: invoice.TotalVat,
            totalSurcharge: invoice.TotalSurcharge,
            totalTax: invoice.TotalTax,
            totalGross: invoice.TotalGross,
            totalWithholding: invoice.TotalWithholding,
            totalPayable: invoice.TotalPayable,
            notes: invoice.Notes,
            legalTerms: settings.LegalTermsText,
            footer: settings.FooterText,
            logo: parsedLogo);
    }

    public byte[] GenerateQuotePdf(
        QuoteDto quote,
        BillingSettingsDto settings,
        byte[]? logoBytes = null)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(settings);

        var parsedLogo = logoBytes is { Length: > 0 }
            ? ValidateAndParseLogo(logoBytes)
            : (ParsedPdfImage?)null;

        var validUntilText = quote.ValidUntilDate.HasValue
            ? $"   |   Válido hasta: {quote.ValidUntilDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}"
            : string.Empty;

        var subtitle = $"Número: {quote.FormattedNumber}   |   Fecha: {quote.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}   |   Estado: {quote.Status}{validUntilText}";

        var lines = quote.Lines.Select(l => new PdfLineRow(
            l.LineOrder,
            l.Description,
            l.Quantity,
            l.UnitPriceNet,
            l.VatTaxName,
            l.VatPercentage,
            l.SurchargePercentage,
            l.LineNet)).ToList();

        var breakdowns = new List<PdfTaxBreakdownRow>();
        foreach (var vatGroup in quote.Lines.GroupBy(l => new { l.VatTaxName, l.VatPercentage }).OrderBy(g => g.Key.VatPercentage))
        {
            var baseAmount = vatGroup.Sum(x => x.LineNet);
            var taxAmount = decimal.Round(baseAmount * (vatGroup.Key.VatPercentage / 100m), 2, Paye.Core.ValueObjects.Money.DefaultRounding);
            breakdowns.Add(new PdfTaxBreakdownRow(vatGroup.Key.VatTaxName, TaxKind.Iva, vatGroup.Key.VatPercentage, baseAmount, taxAmount));
        }

        foreach (var reGroup in quote.Lines.Where(l => l.SurchargePercentage > 0m).GroupBy(l => new { l.SurchargeTaxName, l.SurchargePercentage }).OrderBy(g => g.Key.SurchargePercentage))
        {
            var baseAmount = reGroup.Sum(x => x.LineNet);
            var taxAmount = decimal.Round(baseAmount * (reGroup.Key.SurchargePercentage / 100m), 2, Paye.Core.ValueObjects.Money.DefaultRounding);
            breakdowns.Add(new PdfTaxBreakdownRow(reGroup.Key.SurchargeTaxName ?? "Recargo de equivalencia", TaxKind.RecargoEquivalencia, reGroup.Key.SurchargePercentage, baseAmount, taxAmount));
        }

        if (quote.WithholdingPercentage > 0m && quote.TotalWithholding > 0m)
        {
            breakdowns.Add(new PdfTaxBreakdownRow(
                quote.WithholdingTaxName ?? "IRPF",
                TaxKind.Irpf,
                quote.WithholdingPercentage,
                quote.SubtotalNet,
                quote.TotalWithholding));
        }

        return BuildPdfDocument(
            documentTitle: "PRESUPUESTO COMERCIAL (NO FISCAL)",
            subtitle: subtitle,
            issuerName: settings.TaxName,
            issuerTaxId: settings.TaxId,
            issuerAddress: settings.FiscalAddress,
            customerName: quote.CustomerName,
            customerTaxId: quote.CustomerTaxId,
            customerAddress: quote.CustomerAddress,
            extraMetaLines: ["Documento informativo no válido como factura fiscal."],
            lines: lines,
            taxBreakdowns: breakdowns,
            subtotalNet: quote.SubtotalNet,
            totalVat: quote.TotalVat,
            totalSurcharge: quote.TotalSurcharge,
            totalTax: quote.TotalTax,
            totalGross: quote.TotalGross,
            totalWithholding: quote.TotalWithholding,
            totalPayable: quote.TotalPayable,
            notes: quote.Notes,
            legalTerms: settings.LegalTermsText,
            footer: settings.FooterText,
            logo: parsedLogo);
    }

    /// <summary>
    /// Valida que el archivo de logotipo sea estrictamente JPEG o PNG opaco (8 bits sin transparencia).
    /// Rechaza cualquier otro formato o PNG con canal alfa/transparencia con un mensaje claro.
    /// </summary>
    public static (int Width, int Height) EnsureValidLogoFormat(ReadOnlySpan<byte> bytes)
    {
        var parsed = ValidateAndParseLogo(bytes);
        return (parsed.Width, parsed.Height);
    }

    private static ParsedPdfImage ValidateAndParseLogo(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
        {
            throw new InvalidOperationException(
                "El logotipo proporcionado no es válido. Solo se admiten imágenes JPEG (.jpg/.jpeg) o PNG opaco sin transparencia (.png).");
        }

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ParseJpeg(bytes);
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes[..8].SequenceEqual(pngSignature))
        {
            return ParseOpaquePng(bytes);
        }

        throw new InvalidOperationException(
            "Formato de logotipo no soportado. El generador PDF solo admite imágenes JPEG o PNG sin transparencia (opaco).");
    }

    private static ParsedPdfImage ParseJpeg(ReadOnlySpan<byte> bytes)
    {
        var index = 2;
        while (index + 4 < bytes.Length)
        {
            if (bytes[index] != 0xFF)
            {
                index++;
                continue;
            }

            while (index < bytes.Length && bytes[index] == 0xFF)
            {
                index++;
            }

            if (index >= bytes.Length)
            {
                break;
            }

            var marker = bytes[index++];
            if (marker is 0xD8 or 0xD9 || (marker >= 0xD0 && marker <= 0xD7))
            {
                continue;
            }

            if (index + 2 > bytes.Length)
            {
                break;
            }

            int segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index, 2));
            if (segmentLength < 2 || index + segmentLength > bytes.Length)
            {
                break;
            }

            // SOF0 (Baseline DCT), SOF1 (Extended sequential DCT), SOF2 (Progressive DCT)
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (segmentLength < 8)
                {
                    throw new InvalidOperationException("El archivo JPEG del logotipo tiene una cabecera SOF truncada.");
                }

                int bitsPerComponent = bytes[index + 2];
                int height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index + 3, 2));
                int width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index + 5, 2));
                int components = bytes[index + 7];

                if (width <= 0 || height <= 0 || bitsPerComponent != 8 || (components != 1 && components != 3))
                {
                    throw new InvalidOperationException(
                        "El logotipo JPEG debe ser RGB o escala de grises de 8 bits por componente (no se admite CMYK).");
                }

                var colorSpace = components == 1 ? "/DeviceGray" : "/DeviceRGB";
                return new ParsedPdfImage(
                    Width: width,
                    Height: height,
                    BitsPerComponent: 8,
                    ColorSpace: colorSpace,
                    Filter: "/DCTDecode",
                    DecodeParms: null,
                    EncodedStreamBytes: bytes.ToArray());
            }

            index += segmentLength;
        }

        throw new InvalidOperationException("No se pudo leer las dimensiones del logotipo JPEG.");
    }

    private static ParsedPdfImage ParseOpaquePng(ReadOnlySpan<byte> bytes)
    {
        var offset = 8;
        var width = 0;
        var height = 0;
        var bitDepth = 0;
        var colorType = -1;
        using var idatBuffer = new MemoryStream();

        while (offset + 8 <= bytes.Length)
        {
            var chunkLength = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(offset, 4));
            var chunkType = Encoding.ASCII.GetString(bytes.Slice(offset + 4, 4));
            offset += 8;

            if (chunkLength < 0 || offset + chunkLength + 4 > bytes.Length)
            {
                throw new InvalidOperationException("El archivo PNG del logotipo está corrupto o truncado.");
            }

            var chunkData = bytes.Slice(offset, chunkLength);

            if (chunkType == "IHDR")
            {
                if (chunkLength < 13)
                {
                    throw new InvalidOperationException("Cabecera IHDR de PNG inválida.");
                }

                width = BinaryPrimitives.ReadInt32BigEndian(chunkData[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(chunkData.Slice(4, 4));
                bitDepth = chunkData[8];
                colorType = chunkData[9];
                int interlace = chunkData[12];

                // colorType 0 = Grayscale, 2 = Truecolor RGB (opacos).
                // colorType 3 = Indexed, 4 = Grayscale+Alpha, 6 = RGBA -> rechazados.
                if (colorType is 4 or 6)
                {
                    throw new InvalidOperationException(
                        "El logotipo PNG contiene canal alfa (transparencia). Súbalo en JPEG o en PNG opaco sin transparencia.");
                }

                if (colorType is not (0 or 2) || bitDepth != 8 || interlace != 0)
                {
                    throw new InvalidOperationException(
                        "El logotipo PNG debe ser RGB o escala de grises de 8 bits no entrelazado y sin paleta ni transparencia.");
                }
            }
            else if (chunkType == "tRNS")
            {
                throw new InvalidOperationException(
                    "El logotipo PNG contiene un bloque de transparencia (tRNS). Súbalo en JPEG o en PNG opaco sin transparencia.");
            }
            else if (chunkType == "IDAT")
            {
                idatBuffer.Write(chunkData);
            }
            else if (chunkType == "IEND")
            {
                break;
            }

            offset += chunkLength + 4; // saltar datos + CRC
        }

        if (width <= 0 || height <= 0 || idatBuffer.Length == 0)
        {
            throw new InvalidOperationException("El logotipo PNG no contiene datos de imagen válidos.");
        }

        var colors = colorType == 0 ? 1 : 3;
        var colorSpace = colorType == 0 ? "/DeviceGray" : "/DeviceRGB";
        var decodeParms = $"<< /Predictor 15 /Colors {colors.ToString(CultureInfo.InvariantCulture)} /BitsPerComponent 8 /Columns {width.ToString(CultureInfo.InvariantCulture)} >>";

        return new ParsedPdfImage(
            Width: width,
            Height: height,
            BitsPerComponent: 8,
            ColorSpace: colorSpace,
            Filter: "/FlateDecode",
            DecodeParms: decodeParms,
            EncodedStreamBytes: idatBuffer.ToArray());
    }

    private static byte[] BuildPdfDocument(
        string documentTitle,
        string subtitle,
        string issuerName,
        string issuerTaxId,
        string issuerAddress,
        string customerName,
        string? customerTaxId,
        string? customerAddress,
        IReadOnlyList<string> extraMetaLines,
        IReadOnlyList<PdfLineRow> lines,
        IReadOnlyList<PdfTaxBreakdownRow> taxBreakdowns,
        decimal subtotalNet,
        decimal totalVat,
        decimal totalSurcharge,
        decimal totalTax,
        decimal totalGross,
        decimal totalWithholding,
        decimal totalPayable,
        string? notes,
        string? legalTerms,
        string? footer,
        ParsedPdfImage? logo)
    {
        var content = new StringBuilder(4096);

        // Si hay logo, dibujarlo en la esquina superior derecha
        if (logo is not null)
        {
            const decimal maxLogoWidth = 110m;
            const decimal maxLogoHeight = 45m;
            var scaleX = maxLogoWidth / logo.Width;
            var scaleY = maxLogoHeight / logo.Height;
            var scale = Math.Min(scaleX, scaleY);
            var drawW = Math.Round(logo.Width * scale, 2);
            var drawH = Math.Round(logo.Height * scale, 2);
            var drawX = 555m - drawW;
            var drawY = 795m - drawH;

            content.AppendLine(CultureInfo.InvariantCulture, $"q {drawW:0.##} 0 0 {drawH:0.##} {drawX:0.##} {drawY:0.##} cm /ImLogo Do Q");
        }

        var y = 785m;
        AppendText(content, "F2", 16m, 40m, y, documentTitle);
        y -= 18m;
        AppendText(content, "F1", 9.5m, 40m, y, subtitle);
        y -= 14m;

        foreach (var meta in extraMetaLines)
        {
            AppendText(content, "F2", 9m, 40m, y, meta);
            y -= 13m;
        }

        y -= 6m;
        AppendHorizontalLine(content, 40m, 555m, y);
        y -= 16m;

        // Bloque Emisor (izquierda) y Cliente (derecha)
        AppendText(content, "F2", 10m, 40m, y, "EMISOR");
        AppendText(content, "F2", 10m, 305m, y, "CLIENTE / DESTINATARIO");
        y -= 14m;

        AppendText(content, "F2", 9.5m, 40m, y, Truncate(issuerName, 48));
        AppendText(content, "F2", 9.5m, 305m, y, Truncate(customerName, 48));
        y -= 13m;

        AppendText(content, "F1", 9m, 40m, y, $"NIF/CIF: {(string.IsNullOrWhiteSpace(issuerTaxId) ? "Pendiente" : issuerTaxId)}");
        AppendText(content, "F1", 9m, 305m, y, $"NIF/CIF: {(string.IsNullOrWhiteSpace(customerTaxId) ? "-" : customerTaxId)}");
        y -= 13m;

        AppendText(content, "F1", 8.5m, 40m, y, Truncate(issuerAddress, 55));
        AppendText(content, "F1", 8.5m, 305m, y, Truncate(customerAddress ?? "-", 55));
        y -= 16m;

        AppendHorizontalLine(content, 40m, 555m, y);
        y -= 15m;

        // Cabecera de tabla de líneas
        AppendText(content, "F2", 8.5m, 40m, y, "#");
        AppendText(content, "F2", 8.5m, 60m, y, "DESCRIPCIÓN");
        AppendText(content, "F2", 8.5m, 295m, y, "CANT.");
        AppendText(content, "F2", 8.5m, 345m, y, "PRECIO NETO");
        AppendText(content, "F2", 8.5m, 420m, y, "IVA");
        AppendText(content, "F2", 8.5m, 460m, y, "R.E.");
        AppendText(content, "F2", 8.5m, 500m, y, "BASE LÍNEA");
        y -= 6m;
        AppendHorizontalLine(content, 40m, 555m, y);
        y -= 13m;

        foreach (var line in lines.Take(22))
        {
            AppendText(content, "F1", 8.5m, 40m, y, line.LineOrder.ToString(CultureInfo.InvariantCulture));
            AppendText(content, "F1", 8.5m, 60m, y, Truncate(line.Description, 46));
            AppendText(content, "F1", 8.5m, 295m, y, FormatDecimal(line.Quantity));
            AppendText(content, "F1", 8.5m, 345m, y, $"{FormatCurrency(line.UnitPriceNet)} EUR");
            AppendText(content, "F1", 8.5m, 420m, y, $"{FormatDecimal(line.VatPercentage)}%");
            AppendText(content, "F1", 8.5m, 460m, y, line.SurchargePercentage > 0m ? $"{FormatDecimal(line.SurchargePercentage)}%" : "-");
            AppendText(content, "F1", 8.5m, 500m, y, $"{FormatCurrency(line.LineNet)} EUR");
            y -= 13m;
        }

        y -= 4m;
        AppendHorizontalLine(content, 40m, 555m, y);
        y -= 16m;

        // Desglose de impuestos (izquierda) y Totales (derecha)
        var leftY = y;
        var rightY = y;

        AppendText(content, "F2", 9m, 40m, leftY, "DESGLOSE DE IMPUESTOS");
        leftY -= 13m;
        foreach (var bd in taxBreakdowns)
        {
            var sign = bd.Kind == TaxKind.Irpf ? "-" : string.Empty;
            var rowText = $"{Truncate(bd.TaxName, 24)} ({FormatDecimal(bd.Percentage)}% s/ {FormatCurrency(bd.TaxableBase)}): {sign}{FormatCurrency(bd.TaxAmount)} EUR";
            AppendText(content, "F1", 8.5m, 40m, leftY, rowText);
            leftY -= 12m;
        }

        AppendText(content, "F1", 9m, 345m, rightY, $"Base Imponible Total: {FormatCurrency(subtotalNet)} EUR");
        rightY -= 13m;
        AppendText(content, "F1", 9m, 345m, rightY, $"Cuota IVA: {FormatCurrency(totalVat)} EUR");
        rightY -= 13m;
        if (totalSurcharge != 0m)
        {
            AppendText(content, "F1", 9m, 345m, rightY, $"Cuota Recargo Eq.: {FormatCurrency(totalSurcharge)} EUR");
            rightY -= 13m;
        }

        AppendText(content, "F1", 9m, 345m, rightY, $"Total Impuestos (IVA+RE): {FormatCurrency(totalTax)} EUR");
        rightY -= 13m;
        AppendText(content, "F2", 9.5m, 345m, rightY, $"Total Factura: {FormatCurrency(totalGross)} EUR");
        rightY -= 13m;

        if (totalWithholding > 0m)
        {
            AppendText(content, "F1", 9m, 345m, rightY, $"Retención IRPF: -{FormatCurrency(totalWithholding)} EUR");
            rightY -= 13m;
        }

        AppendText(content, "F2", 11m, 345m, rightY, $"TOTAL A PAGAR: {FormatCurrency(totalPayable)} EUR");
        rightY -= 18m;

        y = Math.Min(leftY, rightY) - 10m;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            AppendText(content, "F2", 8.5m, 40m, y, "Observaciones:");
            y -= 12m;
            AppendText(content, "F1", 8.5m, 40m, y, Truncate(notes, 95));
            y -= 14m;
        }

        if (!string.IsNullOrWhiteSpace(legalTerms))
        {
            AppendText(content, "F1", 7.5m, 40m, 65m, Truncate(legalTerms, 110));
        }

        if (!string.IsNullOrWhiteSpace(footer))
        {
            AppendText(content, "F1", 7.5m, 40m, 50m, Truncate(footer, 110));
        }

        return SerializePdf14(content.ToString(), logo);
    }

    private static void AppendText(StringBuilder sb, string fontKey, decimal fontSize, decimal x, decimal y, string text)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"BT /{fontKey} {fontSize:0.##} Tf {x:0.##} {y:0.##} Td ({EscapePdfString(text)}) Tj ET");
    }

    private static void AppendHorizontalLine(StringBuilder sb, decimal x1, decimal x2, decimal y)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"0.75 w {x1:0.##} {y:0.##} m {x2:0.##} {y:0.##} l S");
    }

    private static string EscapePdfString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

        return normalized;
    }

    private static string Truncate(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var clean = text.Trim();
        return clean.Length <= maxChars ? clean : string.Concat(clean.AsSpan(0, maxChars - 3), "...");
    }

    private static string FormatCurrency(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static byte[] SerializePdf14(string contentStreamText, ParsedPdfImage? logo)
    {
        var winAnsi = Encoding.GetEncoding(1252);
        var ascii = Encoding.ASCII;
        var contentBytes = winAnsi.GetBytes(contentStreamText);

        using var ms = new MemoryStream();
        var offsets = new List<long> { 0 }; // índice 0 reservado para tabla xref

        void WriteAscii(string s)
        {
            var b = ascii.GetBytes(s);
            ms.Write(b, 0, b.Length);
        }

        void StartObject(int objNum)
        {
            offsets.Add(ms.Position);
            WriteAscii($"{objNum.ToString(CultureInfo.InvariantCulture)} 0 obj\n");
        }

        void EndObject()
        {
            WriteAscii("\nendobj\n");
        }

        // Cabecera PDF 1.4 con comentario binario
        WriteAscii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        // Objeto 1: Catalog
        StartObject(1);
        WriteAscii("<< /Type /Catalog /Pages 2 0 R >>");
        EndObject();

        // Objeto 2: Pages
        StartObject(2);
        WriteAscii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        EndObject();

        // Objeto 3: Page (A4 595 x 842)
        var xObjectDict = logo is not null ? " /XObject << /ImLogo 7 0 R >>" : string.Empty;
        StartObject(3);
        WriteAscii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >>{xObjectDict} >> /Contents 6 0 R >>");
        EndObject();

        // Objeto 4: Font F1 (Helvetica)
        StartObject(4);
        WriteAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        EndObject();

        // Objeto 5: Font F2 (Helvetica-Bold)
        StartObject(5);
        WriteAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        EndObject();

        // Objeto 6: Content Stream
        StartObject(6);
        WriteAscii($"<< /Length {contentBytes.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
        ms.Write(contentBytes, 0, contentBytes.Length);
        WriteAscii("\nendstream");
        EndObject();

        // Objeto 7 (opcional): Imagen XObject para el logotipo
        if (logo is not null)
        {
            StartObject(7);
            var decodeParmsPart = logo.DecodeParms is not null ? $" /DecodeParms {logo.DecodeParms}" : string.Empty;
            WriteAscii(
                $"<< /Type /XObject /Subtype /Image /Width {logo.Width.ToString(CultureInfo.InvariantCulture)} /Height {logo.Height.ToString(CultureInfo.InvariantCulture)} /ColorSpace {logo.ColorSpace} /BitsPerComponent {logo.BitsPerComponent.ToString(CultureInfo.InvariantCulture)} /Filter {logo.Filter}{decodeParmsPart} /Length {logo.EncodedStreamBytes.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
            ms.Write(logo.EncodedStreamBytes, 0, logo.EncodedStreamBytes.Length);
            WriteAscii("\nendstream");
            EndObject();
        }

        var xrefPosition = ms.Position;
        var totalObjects = offsets.Count;
        WriteAscii($"xref\n0 {totalObjects.ToString(CultureInfo.InvariantCulture)}\n");
        WriteAscii("0000000000 65535 f \n");
        for (var i = 1; i < totalObjects; i++)
        {
            WriteAscii($"{offsets[i].ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        WriteAscii($"trailer\n<< /Size {totalObjects.ToString(CultureInfo.InvariantCulture)} /Root 1 0 R >>\nstartxref\n{xrefPosition.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        return ms.ToArray();
    }

    private sealed record PdfLineRow(
        int LineOrder,
        string Description,
        decimal Quantity,
        decimal UnitPriceNet,
        string VatTaxName,
        decimal VatPercentage,
        decimal SurchargePercentage,
        decimal LineNet);

    private sealed record PdfTaxBreakdownRow(
        string TaxName,
        TaxKind Kind,
        decimal Percentage,
        decimal TaxableBase,
        decimal TaxAmount);

    private sealed record ParsedPdfImage(
        int Width,
        int Height,
        int BitsPerComponent,
        string ColorSpace,
        string Filter,
        string? DecodeParms,
        byte[] EncodedStreamBytes);
}
