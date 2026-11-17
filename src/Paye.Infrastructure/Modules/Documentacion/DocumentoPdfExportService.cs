using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Paye.Core.Modules.Documentacion;

namespace Paye.Infrastructure.Modules.Documentacion;

/// <summary>
/// Servicio de generación de PDF A4 en servidor.
/// Construye un documento HTML5 autocontenido con las mismas reglas CSS (@page A4, variables, paginación),
/// incrustando únicamente recursos locales (fuentes Montserrat WOFF2 e imágenes PNG/SVG de wwwroot)
/// y lo renderiza mediante motor Chromium/Edge headless con red totalmente bloqueada (--host-resolver-rules="MAP * ~NOTFOUND").
/// </summary>
public sealed partial class DocumentoPdfExportService : IDocumentoPdfExportService
{
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");
    private const int MaxLinesPerTableSlice = 10;
    private const int MaxCharsPerTextSlice = 2400;
    private const int MaxPageWeight = 100;

    private readonly IDocumentacionService _documentacionService;

    public DocumentoPdfExportService(IDocumentacionService documentacionService)
    {
        _documentacionService = documentacionService;
    }

    public async Task<DocumentacionOperationResult<DocumentoPdfExportResult>> ExportPdfByIdAsync(
        Guid id,
        DocumentoA4LayoutOptionsDto? layoutOptions = null,
        CancellationToken cancellationToken = default)
    {
        var doc = await _documentacionService.GetByIdAsync(id, cancellationToken);
        if (doc is null)
        {
            return DocumentacionOperationResult.NotFound<DocumentoPdfExportResult>(
                "El documento solicitado no existe.");
        }

        var company = await _documentacionService.GetCompanySettingsAsync(cancellationToken);
        var html = BuildSelfContainedA4Html(doc, company, layoutOptions);
        var safeDocNum = Regex.Replace(doc.NumeroDocumento, @"[^A-Za-z0-9_-]", "_");
        var fileName = $"{safeDocNum}.pdf";

        var pdfBytes = await RenderHtmlToVectorPdfOfflineAsync(html, cancellationToken);
        return DocumentacionOperationResult.Ok(
            new DocumentoPdfExportResult(pdfBytes, fileName, html));
    }

    public string BuildSelfContainedA4Html(
        DocumentoComercialDetailDto document,
        DocumentacionCompanySettingsDto company,
        DocumentoA4LayoutOptionsDto? layoutOptions = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(company);

        var options = layoutOptions ?? new DocumentoA4LayoutOptionsDto();
        var wwwroot = ResolveWwwrootPath();

        var fontFaceCss = BuildEmbeddedMontserratFontFacesCss(wwwroot);
        var baseA4Css = LoadLocalA4Css(wwwroot);

        var logoHorizontalDataUri = LoadLocalAssetAsDataUri(
            wwwroot,
            "images/documentacion/logo-paye-horizontal.png",
            "image/png");
        var cenefaHorizontalDataUri = LoadLocalAssetAsDataUri(
            wwwroot,
            "images/documentacion/cenefa-horizontal.png",
            "image/png");
        var sparkleRedDataUri = LoadLocalAssetAsDataUri(
            wwwroot,
            "images/documentacion/sparkle-red.svg",
            "image/svg+xml");

        var effectiveLogoUri = ResolveCompanyLogoDataUri(
            wwwroot,
            company.LogoUrl,
            logoHorizontalDataUri);

        var visibleBlocks = document.Bloques
            .Where(b => b.Visible)
            .OrderBy(b => b.Orden)
            .ToList();

        var slices = ExpandBlocksIntoRenderableSlices(visibleBlocks, document.Lineas);
        var hasFooterLegal = !string.IsNullOrWhiteSpace(company.TextoLegalTitulo) || !string.IsNullOrWhiteSpace(company.TextoLegalContenido);
        var interiorPages = PaginateSlices(slices, hasFooterLegal);

        var isContrato = document.Tipo == TipoDocumentoComercial.Contrato;
        var coverKindLabel = isContrato ? "CONTRATO" : "PROPUESTA";

        var sb = new StringBuilder(65536);
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline';\" />");
        sb.Append("  <title>").Append(Encode(document.NumeroDocumento)).Append(" — ").Append(Encode(document.ClienteNombre)).AppendLine("</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(fontFaceCss);
        sb.AppendLine(baseA4Css);
        sb.AppendLine("  @page { size: A4; margin: 0 !important; }");
        sb.AppendLine("  html, body, *, *::before, *::after { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; color-adjust: exact !important; }");
        sb.AppendLine("  html, body { width: 210mm; margin: 0 !important; padding: 0 !important; background: #FFFFFF !important; }");
        sb.AppendLine("  .doc-a4-preview-stack { display: block !important; gap: 0 !important; padding: 0 !important; margin: 0 !important; background: #FFFFFF !important; }");
        sb.AppendLine("  .doc-a4-sheet { width: 210mm !important; height: 297mm !important; min-height: 297mm !important; max-height: 297mm !important; box-shadow: none !important; margin: 0 !important; border: none !important; }");
        sb.AppendLine("  .doc-a4-cover { background-color: #000000 !important; color: #FFFFFF !important; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"doc-a4-root doc-a4-preview-stack\">");

        // HOJA 1: PORTADA A4 (FONDO NEGRO #000000)
        sb.AppendLine("    <section id=\"doc-a4-page-1\" data-page-number=\"1\" class=\"doc-a4-sheet doc-a4-cover\" aria-label=\"Portada A4\">");
        sb.AppendLine("      <div class=\"doc-cover-top-spacer\">");
        if (options.MostrarCenefaPortada && !string.IsNullOrEmpty(cenefaHorizontalDataUri))
        {
            sb.Append("        <img src=\"").Append(cenefaHorizontalDataUri).AppendLine("\" alt=\"Cenefa corporativa\" class=\"doc-cover-cenefa-top\" />");
        }
        else if (options.MostrarCodigoEnPortada)
        {
            sb.Append("        <span class=\"doc-cover-doc-number\">").Append(Encode(document.NumeroDocumento)).AppendLine("</span>");
        }
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"doc-cover-center\">");
        sb.Append("        <h1 class=\"doc-cover-kind\">").Append(Encode(coverKindLabel)).AppendLine("</h1>");
        sb.Append("        <h2 class=\"doc-cover-client\">").Append(Encode(string.IsNullOrWhiteSpace(document.ClienteNombre) ? "Cliente" : document.ClienteNombre)).AppendLine("</h2>");
        var effectiveTitle = !string.IsNullOrWhiteSpace(document.Titulo)
            ? document.Titulo
            : (isContrato ? "Prestación de servicios digitales" : "Web para aumentar venta online");
        sb.Append("        <p class=\"doc-cover-title\">").Append(Encode(effectiveTitle)).AppendLine("</p>");

        var categoryText = !string.IsNullOrWhiteSpace(document.Categoria)
            ? document.Categoria
            : document.Subtitulo;
        if (!string.IsNullOrWhiteSpace(categoryText))
        {
            sb.Append("        <p class=\"doc-cover-category\">").Append(Encode(categoryText)).AppendLine("</p>");
        }
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"doc-cover-footer\">");
        if (!string.IsNullOrEmpty(effectiveLogoUri))
        {
            sb.Append("        <img src=\"").Append(effectiveLogoUri).Append("\" alt=\"").Append(Encode(string.IsNullOrWhiteSpace(company.NombreEmpresa) ? "Payé Digital" : company.NombreEmpresa)).AppendLine("\" class=\"doc-cover-logo\" />");
        }
        sb.AppendLine("      </div>");
        sb.AppendLine("    </section>");

        // HOJAS INTERIORES A4 (NUMERADAS 1..N)
        for (int pageIndex = 0; pageIndex < interiorPages.Count; pageIndex++)
        {
            var pageSlices = interiorPages[pageIndex];
            var interiorPageNumber = pageIndex + 1;
            var totalSheetNumber = pageIndex + 2;
            var isLastInteriorPage = pageIndex == interiorPages.Count - 1;

            sb.Append("    <section id=\"doc-a4-page-").Append(totalSheetNumber.ToString(CultureInfo.InvariantCulture))
              .Append("\" data-page-number=\"").Append(totalSheetNumber.ToString(CultureInfo.InvariantCulture))
              .Append("\" class=\"doc-a4-sheet doc-a4-interior\" aria-label=\"Página ")
              .Append(interiorPageNumber.ToString(CultureInfo.InvariantCulture)).AppendLine("\">");

            if (options.MostrarCabeceraEmpresa)
            {
                sb.AppendLine("      <header class=\"doc-page-header\">");
                sb.AppendLine("        <div class=\"doc-page-header-issuer\">");
                sb.Append("          <span>").Append(Encode(string.IsNullOrWhiteSpace(company.NombreEmpresa) ? "Payé Digital" : company.NombreEmpresa)).AppendLine("</span>");
                if (!string.IsNullOrWhiteSpace(company.CifNif))
                {
                    sb.Append("          <span>· CIF/NIF: ").Append(Encode(company.CifNif)).AppendLine("</span>");
                }
                if (!string.IsNullOrWhiteSpace(company.Telefono))
                {
                    sb.Append("          <span>· Tel: ").Append(Encode(company.Telefono)).AppendLine("</span>");
                }
                if (!string.IsNullOrWhiteSpace(company.Email))
                {
                    sb.Append("          <span>· ").Append(Encode(company.Email)).AppendLine("</span>");
                }
                if (!string.IsNullOrWhiteSpace(company.Web))
                {
                    sb.Append("          <span>· ").Append(Encode(company.Web)).AppendLine("</span>");
                }
                sb.AppendLine("        </div>");
                sb.AppendLine("        <div class=\"doc-page-header-meta\">");
                sb.Append("          <span>").Append(Encode(document.NumeroDocumento)).AppendLine("</span>");
                sb.Append("          <span>· ").Append(FormatDateEs(document.FechaEmision)).AppendLine("</span>");
                if (!isContrato && document.FechaValidez.HasValue)
                {
                    sb.Append("          <span>· Validez: ").Append(FormatDateEs(document.FechaValidez.Value)).AppendLine("</span>");
                }
                sb.AppendLine("        </div>");
                sb.AppendLine("      </header>");
            }

            sb.AppendLine("      <div class=\"doc-page-content\">");
            foreach (var slice in pageSlices)
            {
                sb.AppendLine("        <div class=\"doc-block-wrapper\">");
                AppendSliceHtml(sb, slice, document, company, sparkleRedDataUri);
                sb.AppendLine("        </div>");
            }
            sb.AppendLine("      </div>");

            // PIE DE PÁGINA A4
            sb.AppendLine("      <footer class=\"doc-a4-footer\">");
            sb.AppendLine("        <div class=\"doc-footer-logo-pill\">");
            if (!string.IsNullOrEmpty(effectiveLogoUri))
            {
                sb.Append("          <img src=\"").Append(effectiveLogoUri).Append("\" alt=\"").Append(Encode(string.IsNullOrWhiteSpace(company.NombreEmpresa) ? "Payé Digital" : company.NombreEmpresa)).AppendLine("\" class=\"doc-footer-logo-img\" />");
            }
            sb.AppendLine("        </div>");

            if (isLastInteriorPage && (!string.IsNullOrWhiteSpace(company.TextoLegalTitulo) || !string.IsNullOrWhiteSpace(company.TextoLegalContenido)))
            {
                sb.AppendLine("        <div class=\"doc-footer-legal\">");
                if (!string.IsNullOrWhiteSpace(company.TextoLegalTitulo))
                {
                    sb.Append("          <h5 class=\"doc-footer-legal-title\">").Append(Encode(company.TextoLegalTitulo)).AppendLine("</h5>");
                }
                if (!string.IsNullOrWhiteSpace(company.TextoLegalContenido))
                {
                    sb.Append("          <p class=\"doc-footer-legal-text\">").Append(Encode(company.TextoLegalContenido)).AppendLine("</p>");
                }
                sb.AppendLine("        </div>");
            }

            if (options.MostrarDatosFiscalesPie && (!string.IsNullOrWhiteSpace(company.Direccion) || !string.IsNullOrWhiteSpace(company.Iban)))
            {
                sb.AppendLine("        <div class=\"doc-footer-company-contact\">");
                if (!string.IsNullOrWhiteSpace(company.Direccion))
                {
                    sb.Append("          <span>").Append(Encode(company.Direccion)).AppendLine("</span>");
                }
                if (!string.IsNullOrWhiteSpace(company.Iban))
                {
                    sb.Append("          <span>· IBAN: ").Append(Encode(company.Iban)).AppendLine("</span>");
                }
                sb.AppendLine("        </div>");
            }

            sb.Append("        <div class=\"doc-footer-page-number\">").Append(interiorPageNumber.ToString(CultureInfo.InvariantCulture)).AppendLine("</div>");
            sb.AppendLine("      </footer>");
            sb.AppendLine("    </section>");
        }

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void AppendSliceHtml(
        StringBuilder sb,
        RenderableBlockSlice slice,
        DocumentoComercialDetailDto document,
        DocumentacionCompanySettingsDto company,
        string sparkleRedDataUri)
    {
        var block = slice.Block;
        var headingParts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(block.NumeroPastilla))
        {
            headingParts.Add(block.NumeroPastilla.Trim());
        }
        if (!string.IsNullOrWhiteSpace(block.Titulo))
        {
            headingParts.Add(block.Titulo.Trim());
        }
        var headingLabel = string.Join(" ", headingParts);
        var safeHtml = DocumentoHtmlSanitizer.SanitizeHtml(block.ContenidoHtml);

        switch (block.TipoBloque)
        {
            case "highlight-cost-box":
                sb.AppendLine("          <div class=\"doc-highlight-cost-box\">");
                if (!string.IsNullOrEmpty(sparkleRedDataUri))
                {
                    sb.Append("            <img src=\"").Append(sparkleRedDataUri).AppendLine("\" alt=\"\" aria-hidden=\"true\" class=\"doc-highlight-sparkle\" />");
                }
                if (!string.IsNullOrWhiteSpace(block.Titulo))
                {
                    sb.Append("            <h3 class=\"doc-highlight-cost-title\">").Append(Encode(block.Titulo)).AppendLine("</h3>");
                }
                sb.Append("            <div class=\"doc-section-body\">").Append(safeHtml).AppendLine("</div>");
                if (document.BaseImponible > 0m)
                {
                    var formattedBase = decimal.Truncate(document.BaseImponible) == document.BaseImponible
                        ? document.BaseImponible.ToString("N0", SpanishCulture)
                        : document.BaseImponible.ToString("N2", SpanishCulture);
                    sb.AppendLine("            <div class=\"doc-highlight-cost-total-line\">");
                    sb.Append("              <span>• Total: ").Append(formattedBase).AppendLine(" + IVA</span>");
                    sb.AppendLine("            </div>");
                }
                sb.AppendLine("          </div>");
                break;

            case "timeline-steps":
                sb.AppendLine("          <div>");
                if (!string.IsNullOrWhiteSpace(headingLabel))
                {
                    sb.Append("            <h3 class=\"doc-pill-heading\">").Append(Encode(headingLabel)).AppendLine("</h3>");
                }
                sb.Append("            <div class=\"doc-section-body doc-timeline-container\">").Append(safeHtml).AppendLine("</div>");
                sb.AppendLine("          </div>");
                break;

            case "pricing-table":
                var rows = slice.LineSlice ?? document.Lineas;
                var allLines = document.Lineas;
                var hasAnyDiscount = allLines.Any(l => l.DescuentoPorcentaje > 0m) || document.TotalDescuento > 0m;
                var showLineTotalCol = hasAnyDiscount;

                sb.AppendLine("          <div class=\"doc-pricing-table-wrap\">");
                if (!string.IsNullOrWhiteSpace(headingLabel))
                {
                    sb.Append("            <h3 class=\"doc-pill-heading\">").Append(Encode(headingLabel)).AppendLine("</h3>");
                }
                if (!string.IsNullOrWhiteSpace(safeHtml))
                {
                    sb.Append("            <div class=\"doc-section-body\">").Append(safeHtml).AppendLine("</div>");
                }
                sb.AppendLine("            <div class=\"doc-pricing-table-frame\">");
                sb.AppendLine("              <table class=\"doc-pricing-table\">");
                sb.AppendLine("                <thead>");
                sb.AppendLine("                  <tr>");
                sb.AppendLine("                    <th class=\"doc-col-desc\">Concepto / Descripción</th>");
                sb.AppendLine("                    <th class=\"num doc-col-price\">Precio Ud.</th>");
                if (hasAnyDiscount)
                {
                    sb.AppendLine("                    <th class=\"num doc-col-narrow\">Dto.</th>");
                }
                if (showLineTotalCol)
                {
                    sb.AppendLine("                    <th class=\"num doc-col-total\">Importe</th>");
                }
                sb.AppendLine("                  </tr>");
                sb.AppendLine("                </thead>");
                sb.AppendLine("                <tbody>");
                foreach (var line in rows)
                {
                    sb.AppendLine("                  <tr>");
                    sb.Append("                    <td class=\"doc-col-desc\">").Append(Encode(string.IsNullOrWhiteSpace(line.Descripcion) ? "—" : line.Descripcion)).AppendLine("</td>");
                    sb.Append("                    <td class=\"num doc-col-price\">").Append(FormatCurrencyEs(line.PrecioUnitario)).AppendLine("</td>");
                    if (hasAnyDiscount)
                    {
                        var dtoText = line.DescuentoPorcentaje > 0m
                            ? $"{line.DescuentoPorcentaje.ToString("0.##", SpanishCulture)}%"
                            : "—";
                        sb.Append("                    <td class=\"num doc-col-narrow\">").Append(dtoText).AppendLine("</td>");
                    }
                    if (showLineTotalCol)
                    {
                        sb.Append("                    <td class=\"num doc-col-total\">").Append(FormatCurrencyEs(line.BaseImponible)).AppendLine("</td>");
                    }
                    sb.AppendLine("                  </tr>");
                }
                sb.AppendLine("                </tbody>");
                sb.AppendLine("              </table>");
                sb.AppendLine("            </div>");

                if (slice.ShowPricingSummary)
                {
                    sb.AppendLine("            <div class=\"doc-pricing-summary-grid\">");
                    sb.AppendLine("              <div class=\"doc-pricing-summary-card\">");
                    if (hasAnyDiscount)
                    {
                        sb.AppendLine("                <div class=\"doc-pricing-summary-row\">");
                        sb.Append("                  <span>Subtotal bruto</span><strong>").Append(FormatCurrencyEs(document.SubtotalBruto)).AppendLine("</strong>");
                        sb.AppendLine("                </div>");
                        sb.AppendLine("                <div class=\"doc-pricing-summary-row\">");
                        sb.Append("                  <span>Descuento aplicado</span><strong>-").Append(FormatCurrencyEs(document.TotalDescuento)).AppendLine("</strong>");
                        sb.AppendLine("                </div>");
                    }
                    sb.AppendLine("                <div class=\"doc-pricing-summary-row\">");
                    sb.Append("                  <span>Base Imponible Total</span><strong>").Append(FormatCurrencyEs(document.BaseImponible)).AppendLine("</strong>");
                    sb.AppendLine("                </div>");
                    foreach (var vat in document.DesgloseIva)
                    {
                        sb.AppendLine("                <div class=\"doc-pricing-summary-row vat-row\">");
                        sb.Append("                  <span>").Append(Encode(vat.Etiqueta)).Append(" (s/ ").Append(FormatCurrencyEs(vat.BaseImponible)).Append(")</span><span>")
                          .Append(FormatCurrencyEs(vat.CuotaIva)).AppendLine("</span>");
                        sb.AppendLine("                </div>");
                    }
                    sb.AppendLine("                <div class=\"doc-pricing-summary-total\">");
                    sb.Append("                  <span>TOTAL (IVA incl.)</span><span>").Append(FormatCurrencyEs(document.TotalGeneral)).AppendLine("</span>");
                    sb.AppendLine("                </div>");
                    sb.AppendLine("              </div>");
                    sb.AppendLine("            </div>");
                }
                sb.AppendLine("          </div>");
                break;

            case "contract-parties":
                sb.AppendLine("          <div>");
                if (!string.IsNullOrWhiteSpace(headingLabel))
                {
                    sb.Append("            <h3 class=\"doc-pill-heading\">").Append(Encode(headingLabel)).AppendLine("</h3>");
                }
                sb.AppendLine("            <div class=\"doc-parties-grid\">");
                sb.AppendLine("              <div class=\"doc-party-card\">");
                sb.AppendLine("                <span class=\"doc-party-role\">El Prestador</span>");
                sb.Append("                <div><strong>").Append(Encode(string.IsNullOrWhiteSpace(company.NombreEmpresa) ? "Payé Digital" : company.NombreEmpresa)).AppendLine("</strong></div>");
                if (!string.IsNullOrWhiteSpace(company.CifNif))
                {
                    sb.Append("                <div>CIF/NIF: ").Append(Encode(company.CifNif)).AppendLine("</div>");
                }
                if (!string.IsNullOrWhiteSpace(company.Direccion))
                {
                    sb.Append("                <div>Domicilio: ").Append(Encode(company.Direccion)).AppendLine("</div>");
                }
                if (!string.IsNullOrWhiteSpace(company.Email))
                {
                    sb.Append("                <div>Email: ").Append(Encode(company.Email)).AppendLine("</div>");
                }
                if (!string.IsNullOrWhiteSpace(company.Telefono))
                {
                    sb.Append("                <div>Tel: ").Append(Encode(company.Telefono)).AppendLine("</div>");
                }
                sb.AppendLine("              </div>");

                sb.AppendLine("              <div class=\"doc-party-card\">");
                sb.AppendLine("                <span class=\"doc-party-role\">El Cliente</span>");
                sb.Append("                <div><strong>").Append(Encode(string.IsNullOrWhiteSpace(document.ClienteNombre) ? "Cliente" : document.ClienteNombre)).AppendLine("</strong></div>");
                if (!string.IsNullOrWhiteSpace(document.ClienteCifNif))
                {
                    sb.Append("                <div>CIF/NIF: ").Append(Encode(document.ClienteCifNif)).AppendLine("</div>");
                }
                if (!string.IsNullOrWhiteSpace(document.ClienteDireccion))
                {
                    sb.Append("                <div>Domicilio: ").Append(Encode(document.ClienteDireccion)).AppendLine("</div>");
                }
                if (!string.IsNullOrWhiteSpace(document.ClienteEmail))
                {
                    sb.Append("                <div>Email: ").Append(Encode(document.ClienteEmail)).AppendLine("</div>");
                }
                if (!string.IsNullOrWhiteSpace(document.ClienteTelefono))
                {
                    sb.Append("                <div>Tel: ").Append(Encode(document.ClienteTelefono)).AppendLine("</div>");
                }
                sb.AppendLine("              </div>");
                sb.AppendLine("            </div>");

                if (!string.IsNullOrWhiteSpace(document.ObjetoContrato) ||
                    !string.IsNullOrWhiteSpace(document.DuracionContrato) ||
                    !string.IsNullOrWhiteSpace(document.PrecioTextoContrato) ||
                    !string.IsNullOrWhiteSpace(document.FormaPagoContrato))
                {
                    sb.AppendLine("            <div class=\"doc-contract-summary-strip\">");
                    if (!string.IsNullOrWhiteSpace(document.ObjetoContrato))
                    {
                        sb.Append("              <div><strong>Objeto:</strong> ").Append(Encode(document.ObjetoContrato)).AppendLine("</div>");
                    }
                    if (!string.IsNullOrWhiteSpace(document.DuracionContrato))
                    {
                        sb.Append("              <div><strong>Duración:</strong> ").Append(Encode(document.DuracionContrato)).AppendLine("</div>");
                    }
                    if (!string.IsNullOrWhiteSpace(document.PrecioTextoContrato))
                    {
                        sb.Append("              <div><strong>Precio pactado:</strong> ").Append(Encode(document.PrecioTextoContrato)).AppendLine("</div>");
                    }
                    if (!string.IsNullOrWhiteSpace(document.FormaPagoContrato))
                    {
                        sb.Append("              <div><strong>Forma de pago:</strong> ").Append(Encode(document.FormaPagoContrato)).AppendLine("</div>");
                    }
                    sb.AppendLine("            </div>");
                }

                if (!string.IsNullOrWhiteSpace(safeHtml))
                {
                    sb.Append("            <div class=\"doc-section-body doc-mt-sm\">").Append(safeHtml).AppendLine("</div>");
                }
                sb.AppendLine("          </div>");
                break;

            case "signatures":
                sb.AppendLine("          <div>");
                if (!string.IsNullOrWhiteSpace(headingLabel))
                {
                    sb.Append("            <h3 class=\"doc-pill-heading\">").Append(Encode(headingLabel)).AppendLine("</h3>");
                }
                if (!string.IsNullOrWhiteSpace(safeHtml))
                {
                    sb.Append("            <div class=\"doc-section-body\">").Append(safeHtml).AppendLine("</div>");
                }
                var lugar = string.IsNullOrWhiteSpace(document.LugarFirma) ? "A Coruña" : document.LugarFirma;
                sb.Append("            <p class=\"doc-section-body doc-signature-place-date\">En ")
                  .Append(Encode(lugar)).Append(", a ").Append(FormatDateEs(document.FechaEmision)).AppendLine(".</p>");
                sb.AppendLine("            <div class=\"doc-signatures-grid\">");
                sb.AppendLine("              <div class=\"doc-signature-box\">");
                sb.AppendLine("                <span class=\"doc-party-role\">Por el Prestador</span>");
                sb.Append("                <div class=\"doc-signature-line\">Fdo.: ")
                  .Append(Encode(string.IsNullOrWhiteSpace(company.NombreEmpresa) ? "Payé Digital" : company.NombreEmpresa));
                if (!string.IsNullOrWhiteSpace(company.CifNif))
                {
                    sb.Append(" (").Append(Encode(company.CifNif)).Append(')');
                }
                sb.AppendLine("</div>");
                sb.AppendLine("              </div>");
                sb.AppendLine("              <div class=\"doc-signature-box\">");
                sb.AppendLine("                <span class=\"doc-party-role\">Por el Cliente</span>");
                sb.Append("                <div class=\"doc-signature-line\">Fdo.: ")
                  .Append(Encode(string.IsNullOrWhiteSpace(document.ClienteNombre) ? "Cliente" : document.ClienteNombre));
                if (!string.IsNullOrWhiteSpace(document.ClienteCifNif))
                {
                    sb.Append(" (").Append(Encode(document.ClienteCifNif)).Append(')');
                }
                sb.AppendLine("</div>");
                sb.AppendLine("              </div>");
                sb.AppendLine("            </div>");
                sb.AppendLine("          </div>");
                break;

            default:
                sb.AppendLine("          <div>");
                if (!string.IsNullOrWhiteSpace(headingLabel))
                {
                    sb.Append("            <h3 class=\"doc-pill-heading\">").Append(Encode(headingLabel)).AppendLine("</h3>");
                }
                sb.Append("            <div class=\"doc-section-body\">").Append(safeHtml).AppendLine("</div>");
                sb.AppendLine("          </div>");
                break;
        }
    }

    private sealed record RenderableBlockSlice(
        DocumentoBloqueDto Block,
        IReadOnlyList<DocumentoLineaDto>? LineSlice = null,
        bool ShowPricingSummary = false);

    private static List<RenderableBlockSlice> ExpandBlocksIntoRenderableSlices(
        IReadOnlyList<DocumentoBloqueDto> visibleBlocks,
        IReadOnlyList<DocumentoLineaDto> allLines)
    {
        var slices = new List<RenderableBlockSlice>();

        foreach (var block in visibleBlocks)
        {
            if (string.Equals(block.TipoBloque, "pricing-table", StringComparison.OrdinalIgnoreCase))
            {
                if (allLines.Count <= MaxLinesPerTableSlice)
                {
                    slices.Add(new RenderableBlockSlice(block, allLines, ShowPricingSummary: true));
                }
                else
                {
                    for (int i = 0; i < allLines.Count; i += MaxLinesPerTableSlice)
                    {
                        var chunk = allLines.Skip(i).Take(MaxLinesPerTableSlice).ToList();
                        var isLast = i + MaxLinesPerTableSlice >= allLines.Count;
                        var isCont = i > 0;
                        var sliceBlock = isCont
                            ? block with { Titulo = $"{block.Titulo} (Cont.)", ContenidoHtml = string.Empty }
                            : block;
                        slices.Add(new RenderableBlockSlice(sliceBlock, chunk, ShowPricingSummary: isLast));
                    }
                }
                continue;
            }

            if (string.Equals(block.TipoBloque, "pill-section", StringComparison.OrdinalIgnoreCase))
            {
                var plainLen = StripTagsRegex().Replace(block.ContenidoHtml ?? string.Empty, string.Empty).Length;
                if (plainLen > MaxCharsPerTextSlice)
                {
                    var paragraphs = ParagraphSplitRegex()
                        .Split(block.ContenidoHtml ?? string.Empty)
                        .Select(p => p.Trim())
                        .Where(p => p.Length > 0)
                        .ToList();

                    if (paragraphs.Count > 1)
                    {
                        var currentHtml = new StringBuilder();
                        int currentLen = 0;
                        int partIndex = 0;

                        foreach (var p in paragraphs)
                        {
                            int pLen = StripTagsRegex().Replace(p, string.Empty).Length;
                            if (currentHtml.Length > 0 && currentLen + pLen > MaxCharsPerTextSlice)
                            {
                                var sliceBlock = partIndex == 0
                                    ? block with { ContenidoHtml = currentHtml.ToString() }
                                    : block with { Titulo = $"{block.Titulo} (Cont.)", ContenidoHtml = currentHtml.ToString() };
                                slices.Add(new RenderableBlockSlice(sliceBlock));
                                partIndex++;
                                currentHtml.Clear().Append(p);
                                currentLen = pLen;
                            }
                            else
                            {
                                currentHtml.Append(p);
                                currentLen += pLen;
                            }
                        }

                        if (currentHtml.Length > 0)
                        {
                            var sliceBlock = partIndex == 0
                                ? block with { ContenidoHtml = currentHtml.ToString() }
                                : block with { Titulo = $"{block.Titulo} (Cont.)", ContenidoHtml = currentHtml.ToString() };
                            slices.Add(new RenderableBlockSlice(sliceBlock));
                        }
                        continue;
                    }
                }
            }

            slices.Add(new RenderableBlockSlice(block));
        }

        return slices;
    }

    private static int EstimateSliceWeight(RenderableBlockSlice slice)
    {
        var block = slice.Block;
        switch (block.TipoBloque)
        {
            case "highlight-cost-box":
                return 44;
            case "timeline-steps":
                return 76;
            case "pricing-table":
                int rowsCount = slice.LineSlice?.Count ?? 0;
                double headerWeight = 16;
                double rowsWeight = rowsCount * 5.2;
                double summaryWeight = slice.ShowPricingSummary ? 28 : 0;
                return Math.Min(96, (int)Math.Round(headerWeight + rowsWeight + summaryWeight, MidpointRounding.AwayFromZero));
            case "signatures":
                return 44;
            case "contract-parties":
                return 38;
            default:
                int textLen = StripTagsRegex().Replace(block.ContenidoHtml ?? string.Empty, string.Empty).Length;
                if (textLen > 1050) return 56;
                if (textLen > 650) return 46;
                if (textLen > 450) return 34;
                if (textLen > 180) return 24;
                return 18;
        }
    }

    private static List<List<RenderableBlockSlice>> PaginateSlices(List<RenderableBlockSlice> slices, bool hasFooterLegal)
    {
        if (slices.Count == 0)
        {
            return [[]];
        }

        var pages = new List<List<RenderableBlockSlice>>();
        var currentPage = new List<RenderableBlockSlice>();
        int currentWeight = 0;

        foreach (var slice in slices)
        {
            int w = EstimateSliceWeight(slice);
            if (currentPage.Count > 0 && currentWeight + w > MaxPageWeight)
            {
                pages.Add(currentPage);
                currentPage = [slice];
                currentWeight = w;
            }
            else
            {
                currentPage.Add(slice);
                currentWeight += w;
            }
        }

        if (currentPage.Count > 0)
        {
            pages.Add(currentPage);
        }

        // Si la última página interior incluye el texto legal de Propiedad Intelectual,
        // aseguramos que tenga espacio vertical suficiente para no desbordar los 297mm del A4
        if (hasFooterLegal && pages.Count > 0)
        {
            var lastPage = pages[^1];
            int lastPageWeight = lastPage.Sum(EstimateSliceWeight);
            if (lastPage.Count > 1 && lastPageWeight > MaxPageWeight - 18)
            {
                var moved = lastPage[^1];
                lastPage.RemoveAt(lastPage.Count - 1);
                pages.Add([moved]);
            }
        }

        return pages;
    }

    private static async Task<byte[]> RenderHtmlToVectorPdfOfflineAsync(
        string htmlContent,
        CancellationToken cancellationToken)
    {
        var browserExe = FindLocalHeadlessBrowserExecutable();
        if (string.IsNullOrEmpty(browserExe))
        {
            throw new InvalidOperationException(
                "No se encontró un motor Chromium/Edge local para renderizar el PDF A4.");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "paye-doc-pdf");
        Directory.CreateDirectory(tempDir);

        var jobId = Guid.NewGuid().ToString("N");
        var htmlPath = Path.Combine(tempDir, $"doc-{jobId}.html");
        var pdfPath = Path.Combine(tempDir, $"doc-{jobId}.pdf");

        try
        {
            await File.WriteAllTextAsync(htmlPath, htmlContent, Encoding.UTF8, cancellationToken);
            var htmlFileUri = new Uri(htmlPath).AbsoluteUri;

            var startInfo = new ProcessStartInfo
            {
                FileName = browserExe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add("--disable-gpu");
            startInfo.ArgumentList.Add("--no-pdf-header-footer");
            startInfo.ArgumentList.Add("--print-to-pdf-no-header");
            startInfo.ArgumentList.Add("--disable-extensions");
            startInfo.ArgumentList.Add("--disable-background-networking");
            startInfo.ArgumentList.Add("--disable-sync");
            // Bloquea cualquier resolución DNS o petición externa durante el renderizado del PDF
            startInfo.ArgumentList.Add("--host-resolver-rules=MAP * ~NOTFOUND");
            startInfo.ArgumentList.Add($"--print-to-pdf={pdfPath}");
            startInfo.ArgumentList.Add(htmlFileUri);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("No se pudo iniciar el proceso de exportación a PDF.");

            await process.WaitForExitAsync(cancellationToken);

            if (!File.Exists(pdfPath))
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"El motor PDF no generó el archivo de salida (código {process.ExitCode}): {stderr}");
            }

            return await File.ReadAllBytesAsync(pdfPath, cancellationToken);
        }
        finally
        {
            TryDeleteFile(htmlPath);
            TryDeleteFile(pdfPath);
        }
    }

    private static string? FindLocalHeadlessBrowserExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var playwrightRoot = Path.Combine(localAppData, "ms-playwright");
        if (Directory.Exists(playwrightRoot))
        {
            var pwChromes = Directory.GetFiles(playwrightRoot, "chrome.exe", SearchOption.AllDirectories);
            if (pwChromes.Length > 0)
            {
                return pwChromes[0];
            }
        }

        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string ResolveWwwrootPath()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Paye.Web", "wwwroot"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Paye.Web", "wwwroot")),
        ];

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "styles", "documentacion-a4.css")))
            {
                return candidate;
            }
        }

        return candidates.FirstOrDefault(Directory.Exists) ?? AppContext.BaseDirectory;
    }

    private static string BuildEmbeddedMontserratFontFacesCss(string wwwroot)
    {
        var specs = new (string FileName, int Weight, string Style)[]
        {
            ("montserrat-latin-200-normal.woff2", 200, "normal"),
            ("montserrat-latin-300-normal.woff2", 300, "normal"),
            ("montserrat-latin-400-normal.woff2", 400, "normal"),
            ("montserrat-latin-400-italic.woff2", 400, "italic"),
            ("montserrat-latin-700-normal.woff2", 700, "normal"),
            ("montserrat-latin-800-normal.woff2", 800, "normal"),
            ("montserrat-latin-900-normal.woff2", 900, "normal"),
        };

        var sb = new StringBuilder();
        foreach (var (fileName, weight, style) in specs)
        {
            var dataUri = LoadLocalAssetAsDataUri(wwwroot, $"fonts/montserrat/{fileName}", "font/woff2");
            if (string.IsNullOrEmpty(dataUri))
            {
                continue;
            }

            sb.AppendLine("@font-face {");
            sb.AppendLine("  font-family: 'Montserrat';");
            sb.Append("  font-style: ").Append(style).AppendLine(";");
            sb.Append("  font-weight: ").Append(weight.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
            sb.AppendLine("  font-display: block;");
            sb.Append("  src: url('").Append(dataUri).AppendLine("') format('woff2');");
            sb.AppendLine("}");
        }
        return sb.ToString();
    }

    private static string LoadLocalA4Css(string wwwroot)
    {
        var cssPath = Path.Combine(wwwroot, "styles", "documentacion-a4.css");
        return File.Exists(cssPath) ? File.ReadAllText(cssPath, Encoding.UTF8) : string.Empty;
    }

    private static string ResolveCompanyLogoDataUri(
        string wwwroot,
        string? configuredLogoUrl,
        string fallbackLogoDataUri)
    {
        var sanitized = configuredLogoUrl?.Trim() ?? string.Empty;
        if (!DocumentoHtmlSanitizer.IsValidLogoOrImageUrl(sanitized) ||
            string.IsNullOrWhiteSpace(sanitized) ||
            string.Equals(sanitized, "/images/documentacion/logo-paye-dark.png", StringComparison.OrdinalIgnoreCase))
        {
            return fallbackLogoDataUri;
        }

        if (sanitized.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return sanitized;
        }

        if (sanitized.StartsWith('/'))
        {
            var relative = sanitized.TrimStart('/');
            var ext = Path.GetExtension(relative).ToUpperInvariant();
            var mime = ext switch
            {
                ".SVG" => "image/svg+xml",
                ".JPG" or ".JPEG" => "image/jpeg",
                ".WEBP" => "image/webp",
                _ => "image/png",
            };
            var loaded = LoadLocalAssetAsDataUri(wwwroot, relative, mime);
            if (!string.IsNullOrEmpty(loaded))
            {
                return loaded;
            }
        }

        return fallbackLogoDataUri;
    }

    private static string LoadLocalAssetAsDataUri(string wwwroot, string relativePath, string mimeType)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(wwwroot, normalized);
        if (!File.Exists(fullPath))
        {
            return string.Empty;
        }

        var bytes = File.ReadAllBytes(fullPath);
        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static string Encode(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : WebUtility.HtmlEncode(value);

    private static string FormatCurrencyEs(decimal amount) =>
        $"{amount.ToString("N2", SpanishCulture)} €";

    private static string FormatDateEs(DateOnly date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Ignorar bloqueo efímero de archivo temporal
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorar error de permisos en limpieza temporal
        }
    }

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StripTagsRegex();

    [GeneratedRegex(@"(?=<p[\s>]|<ul[\s>]|<ol[\s>])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ParagraphSplitRegex();
}
