import type {
  CompanyConfig,
  ExpenseItem,
  InvoiceItem,
  QuarterId,
} from './billing-store'

/**
 * Codifica texto UTF-8 a bytes WinAnsi (Windows-1252 / Latin1) compatibles con las fuentes Type1 estándar de PDF 1.4
 * y escapa paréntesis y barras invertidas.
 */
function encodePdfText(text: string): string {
  const safe = (text ?? '')
    .replace(/€/g, 'EUR')
    .replace(/[—–]/g, '-')
    .replace(/[“”]/g, '"')
    .replace(/[‘’]/g, "'")

  let out = ''
  for (let i = 0; i < safe.length; i++) {
    const code = safe.charCodeAt(i)
    if (code === 0x28 || code === 0x29 || code === 0x5c) {
      out += '\\' + String.fromCharCode(code)
    } else if (code >= 32 && code <= 126) {
      out += String.fromCharCode(code)
    } else if (code >= 160 && code <= 255) {
      out += '\\' + code.toString(8).padStart(3, '0')
    } else {
      out += '?'
    }
  }
  return out
}

function fmtMoney(n: number): string {
  return (
    n.toLocaleString('es-ES', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }) + ' EUR'
  )
}

class PdfPageBuilder {
  private ops: string[] = []

  rectFill(x: number, y: number, w: number, h: number, r: number, g: number, b: number) {
    this.ops.push(`${r.toFixed(2)} ${g.toFixed(2)} ${b.toFixed(2)} rg`)
    this.ops.push(`${x.toFixed(1)} ${y.toFixed(1)} ${w.toFixed(1)} ${h.toFixed(1)} re f`)
  }

  rectStroke(x: number, y: number, w: number, h: number, r: number, g: number, b: number, lineW = 0.6) {
    this.ops.push(`${lineW.toFixed(2)} w`)
    this.ops.push(`${r.toFixed(2)} ${g.toFixed(2)} ${b.toFixed(2)} RG`)
    this.ops.push(`${x.toFixed(1)} ${y.toFixed(1)} ${w.toFixed(1)} ${h.toFixed(1)} re S`)
  }

  line(x1: number, y1: number, x2: number, y2: number, r = 0.85, g = 0.87, b = 0.9, lineW = 0.6) {
    this.ops.push(`${lineW.toFixed(2)} w`)
    this.ops.push(`${r.toFixed(2)} ${g.toFixed(2)} ${b.toFixed(2)} RG`)
    this.ops.push(`${x1.toFixed(1)} ${y1.toFixed(1)} m ${x2.toFixed(1)} ${y2.toFixed(1)} l S`)
  }

  text(
    str: string,
    x: number,
    y: number,
    size = 10,
    bold = false,
    r = 0.08,
    g = 0.1,
    b = 0.14
  ) {
    const font = bold ? '/F2' : '/F1'
    this.ops.push('BT')
    this.ops.push(`${font} ${size} Tf`)
    this.ops.push(`${r.toFixed(2)} ${g.toFixed(2)} ${b.toFixed(2)} rg`)
    this.ops.push(`${x.toFixed(1)} ${y.toFixed(1)} Td`)
    this.ops.push(`(${encodePdfText(str)}) Tj`)
    this.ops.push('ET')
  }

  buildStream(): string {
    return this.ops.join('\n')
  }
}

function buildMultiPagePdf(pageStreams: string[]): Blob {
  // Object 1: Catalog
  // Object 2: Pages
  // Object 3: Font F1 (Helvetica)
  // Object 4: Font F2 (Helvetica-Bold)
  // Then for each page i: Page object + Content stream object
  const objects: string[] = []

  const pageObjIds: number[] = []
  let nextId = 5
  for (let i = 0; i < pageStreams.length; i++) {
    pageObjIds.push(nextId)
    nextId += 2
  }

  objects.push(`1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj`)
  objects.push(
    `2 0 obj\n<< /Type /Pages /Kids [${pageObjIds.map((id) => `${id} 0 R`).join(' ')}] /Count ${pageStreams.length} >>\nendobj`
  )
  objects.push(
    `3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj`
  )
  objects.push(
    `4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj`
  )

  for (let i = 0; i < pageStreams.length; i++) {
    const pageObjId = pageObjIds[i]
    const contentObjId = pageObjId + 1
    const stream = pageStreams[i]
    objects.push(
      `${pageObjId} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents ${contentObjId} 0 R >>\nendobj`
    )
    objects.push(
      `${contentObjId} 0 obj\n<< /Length ${stream.length} >>\nstream\n${stream}\nendstream\nendobj`
    )
  }

  let pdf = '%PDF-1.4\n'
  const offsets: number[] = [0]
  for (const obj of objects) {
    offsets.push(pdf.length)
    pdf += obj + '\n'
  }

  const xrefOffset = pdf.length
  pdf += `xref\n0 ${objects.length + 1}\n`
  pdf += '0000000000 65535 f \n'
  for (let i = 1; i < offsets.length; i++) {
    pdf += `${offsets[i].toString().padStart(10, '0')} 00000 n \n`
  }
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xrefOffset}\n%%EOF`

  const bytes = new Uint8Array(pdf.length)
  for (let i = 0; i < pdf.length; i++) {
    bytes[i] = pdf.charCodeAt(i) & 0xff
  }
  return new Blob([bytes], { type: 'application/pdf' })
}

function renderInvoicePage(invoice: InvoiceItem, company: CompanyConfig): string {
  const p = new PdfPageBuilder()

  // Cabecera barra superior
  p.rectFill(40, 775, 515, 36, 0.06, 0.09, 0.16)
  p.text(company.razonSocial.toUpperCase(), 52, 789, 13, true, 1, 1, 1)
  p.text(`FACTURA: ${invoice.numero}`, 400, 789, 11, true, 0.3, 0.9, 0.65)

  // Datos del Emisor
  p.text('EMISOR / OBLIGADO TRIBUTARIO', 40, 750, 8, true, 0.4, 0.45, 0.52)
  p.text(company.razonSocial, 40, 736, 10, true)
  p.text(`NIF/CIF: ${company.nifCif}`, 40, 723, 9)
  p.text(`${company.direccionFiscal}`, 40, 710, 9)
  p.text(`${company.codigoPostal} ${company.ciudad} (${company.provincia})`, 40, 697, 9)
  p.text(`Email: ${company.emailFacturacion} | Tel: ${company.telefono}`, 40, 684, 8.5, false, 0.35, 0.4, 0.45)

  // Metadatos de la Factura
  p.text('DATOS DEL DOCUMENTO', 360, 750, 8, true, 0.4, 0.45, 0.52)
  p.text(`Número: ${invoice.numero}`, 360, 736, 10, true)
  p.text(`Fecha emisión: ${invoice.fecha}`, 360, 723, 9)
  p.text(`Vencimiento: ${invoice.fechaVencimiento}`, 360, 710, 9)
  p.text(`Trimestre fiscal: ${invoice.trimestre} ${invoice.ejercicio}`, 360, 697, 9)
  p.text(`Estado: ${invoice.estado.toUpperCase()}`, 360, 684, 9, true, 0.05, 0.5, 0.35)

  p.line(40, 670, 555, 670)

  // Bloque Cliente Receptor
  p.rectFill(40, 600, 515, 58, 0.96, 0.97, 0.98)
  p.rectStroke(40, 600, 515, 58, 0.85, 0.87, 0.9)
  p.text('CLIENTE / DESTINATARIO', 50, 644, 8, true, 0.4, 0.45, 0.52)
  p.text(invoice.cliente, 50, 630, 10.5, true)
  p.text(`NIF/CIF: ${invoice.cifNif}`, 50, 617, 9)
  p.text(`Dirección: ${invoice.direccion || 'No especificada'}`, 50, 605, 8.5)
  p.text(`Forma de pago: ${invoice.formaPago}`, 340, 630, 9)
  p.text(`Email: ${invoice.email || '-'}`, 340, 617, 8.5)
  if (company.iban) {
    p.text(`IBAN: ${company.iban}`, 340, 605, 8.5, true)
  }

  // Cabecera Tabla de Líneas
  let y = 572
  p.rectFill(40, y - 6, 515, 22, 0.92, 0.94, 0.96)
  p.text('CONCEPTO / DESCRIPCIÓN', 48, y, 8.5, true)
  p.text('CANT.', 340, y, 8.5, true)
  p.text('PRECIO', 390, y, 8.5, true)
  p.text('IVA', 455, y, 8.5, true)
  p.text('IMPORTE NETO', 490, y, 8.5, true)

  y -= 24
  for (const linea of invoice.lineas) {
    const desc = linea.concepto.length > 52 ? linea.concepto.slice(0, 49) + '...' : linea.concepto
    p.text(desc, 48, y, 9)
    p.text(String(linea.cantidad), 345, y, 9)
    p.text(fmtMoney(linea.precioUnitario), 385, y, 9)
    p.text(`${linea.ivaPercent}%`, 455, y, 9)
    p.text(fmtMoney(linea.total), 490, y, 9, true)
    p.line(40, y - 6, 555, y - 6, 0.9, 0.91, 0.93)
    y -= 20
    if (y < 230) break
  }

  // Bloque de Totales e Impuestos
  const boxY = Math.min(y - 20, 320)
  p.rectFill(335, boxY - 95, 220, 105, 0.08, 0.1, 0.14)
  p.text('RESUMEN DE IMPUESTOS Y TOTAL', 347, boxY - 8, 8, true, 0.65, 0.7, 0.78)
  p.text('Base Imponible:', 347, boxY - 24, 9, false, 0.85, 0.88, 0.92)
  p.text(fmtMoney(invoice.baseImponible), 470, boxY - 24, 9, true, 1, 1, 1)

  p.text('Cuota IVA:', 347, boxY - 38, 9, false, 0.85, 0.88, 0.92)
  p.text(fmtMoney(invoice.iva), 470, boxY - 38, 9, true, 1, 1, 1)

  if (invoice.recargoImporte > 0) {
    p.text(`Recargo Eq. (${invoice.recargoPercent}%):`, 347, boxY - 52, 8.5, false, 0.85, 0.88, 0.92)
    p.text(fmtMoney(invoice.recargoImporte), 470, boxY - 52, 8.5, true, 1, 1, 1)
  }

  if (invoice.irpfImporte > 0) {
    p.text(`Retención IRPF (-${invoice.irpfPercent}%):`, 347, boxY - 65, 8.5, false, 0.95, 0.6, 0.6)
    p.text('-' + fmtMoney(invoice.irpfImporte), 466, boxY - 65, 8.5, true, 0.95, 0.6, 0.6)
  }

  p.line(347, boxY - 72, 543, boxY - 72, 0.3, 0.35, 0.42)
  p.text('TOTAL FACTURA:', 347, boxY - 86, 10, true, 0.3, 0.9, 0.65)
  p.text(fmtMoney(invoice.total), 460, boxY - 86, 11, true, 0.3, 0.9, 0.65)

  // Huella VeriFactu a la izquierda del total
  p.rectStroke(40, boxY - 95, 280, 75, 0.82, 0.85, 0.89)
  p.text('REGISTRO VERIFACTU AEAT (RD 1007/2023)', 50, boxY - 36, 8, true, 0.05, 0.5, 0.35)
  p.text(`Huella SHA-256: ${invoice.hashVerifactu}`, 50, boxY - 50, 8)
  p.text('Documento con firma encadenada verificable.', 50, boxY - 63, 8, false, 0.4, 0.45, 0.5)
  if (invoice.notas) {
    p.text(`Observaciones: ${invoice.notas.slice(0, 50)}`, 50, boxY - 78, 8, false, 0.3, 0.3, 0.3)
  }

  // Pie de página legal
  p.line(40, 70, 555, 70)
  p.text(company.registroMercantil, 40, 55, 7.5, false, 0.45, 0.48, 0.52)
  p.text(company.pieFactura, 40, 43, 7.5, false, 0.45, 0.48, 0.52)

  return p.buildStream()
}

function renderQuarterSummaryCoverPage(
  quarter: QuarterId | 'todas',
  year: number,
  invoices: InvoiceItem[],
  expenses: ExpenseItem[],
  company: CompanyConfig
): string {
  const p = new PdfPageBuilder()

  const titleLabel = quarter === 'todas' ? `EJERCICIO ANUAL ${year}` : `TRIMESTRE FISCAL ${quarter} ${year}`

  // Banner superior ejecutivo
  p.rectFill(40, 755, 515, 52, 0.06, 0.09, 0.16)
  p.text('LIBRO REGISTRO Y LIQUIDACIÓN TRIMESTRAL', 54, 786, 14, true, 1, 1, 1)
  p.text(`${company.razonSocial} · NIF: ${company.nifCif} · Periodo: ${titleLabel}`, 54, 768, 9.5, false, 0.3, 0.9, 0.65)

  // Cálculos del trimestre
  const validInvoices = invoices.filter((f) => f.estado !== 'borrador')
  const totalBaseVentas = validInvoices.reduce((acc, f) => acc + f.baseImponible, 0)
  const totalIvaRepercutido = validInvoices.reduce((acc, f) => acc + f.iva, 0)
  const totalIrpfVentas = validInvoices.reduce((acc, f) => acc + f.irpfImporte, 0)
  const totalFacturado = validInvoices.reduce((acc, f) => acc + f.total, 0)

  const deducibleExpenses = expenses.filter((g) => g.deducible)
  const totalBaseGastos = deducibleExpenses.reduce((acc, g) => acc + g.baseImponible, 0)
  const totalIvaSoportado = deducibleExpenses.reduce((acc, g) => acc + g.ivaSoportado, 0)
  const totalGastos = deducibleExpenses.reduce((acc, g) => acc + g.total, 0)

  const ivaALiquidar = totalIvaRepercutido - totalIvaSoportado
  const rendimientoNeto = totalBaseVentas - totalBaseGastos

  // Cuadro 1: Resumen de Ventas / Facturación Emitida
  p.rectFill(40, 645, 250, 92, 0.96, 0.98, 0.97)
  p.rectStroke(40, 645, 250, 92, 0.75, 0.85, 0.8)
  p.text('1. INGRESOS Y FACTURAS EMITIDAS', 50, 722, 9, true, 0.05, 0.45, 0.3)
  p.text(`Nº facturas computadas: ${validInvoices.length}`, 50, 706, 8.5)
  p.text(`Base Imponible Ventas: ${fmtMoney(totalBaseVentas)}`, 50, 691, 9, true)
  p.text(`IVA Repercutido (+): ${fmtMoney(totalIvaRepercutido)}`, 50, 676, 9)
  p.text(`IRPF Retenido (-): ${fmtMoney(totalIrpfVentas)}`, 50, 662, 8.5)
  p.text(`Total Facturado: ${fmtMoney(totalFacturado)}`, 50, 650, 9, true)

  // Cuadro 2: Resumen de Gastos Deducibles
  p.rectFill(305, 645, 250, 92, 0.98, 0.97, 0.95)
  p.rectStroke(305, 645, 250, 92, 0.88, 0.82, 0.75)
  p.text('2. GASTOS DEDUCIBLES REGISTRADOS', 315, 722, 9, true, 0.6, 0.35, 0.05)
  p.text(`Nº documentos de gasto: ${deducibleExpenses.length}`, 315, 706, 8.5)
  p.text(`Base Imponible Gastos: ${fmtMoney(totalBaseGastos)}`, 315, 691, 9, true)
  p.text(`IVA Soportado Deducible (-): ${fmtMoney(totalIvaSoportado)}`, 315, 676, 9)
  p.text(`Total Gastos Computados: ${fmtMoney(totalGastos)}`, 315, 655, 9, true)

  // Cuadro 3: Liquidación Fiscal del Trimestre (Modelo 303 + Rendimiento)
  p.rectFill(40, 565, 515, 66, 0.08, 0.1, 0.14)
  p.text('3. RESULTADO DE LIQUIDACIÓN FISCAL DEL PERIODO', 52, 615, 9, true, 0.3, 0.9, 0.65)
  p.text(
    `Liquidación IVA (Mod. 303) = IVA Repercutido (${fmtMoney(totalIvaRepercutido)}) - IVA Soportado (${fmtMoney(totalIvaSoportado)})`,
    52,
    597,
    8.5,
    false,
    0.85,
    0.88,
    0.92
  )
  p.text(
    `CUOTA IVA A PAGAR: ${fmtMoney(ivaALiquidar)}`,
    52,
    578,
    11,
    true,
    ivaALiquidar >= 0 ? 0.3 : 0.6,
    0.9,
    0.65
  )
  p.text(
    `Rendimiento Neto (Base Ventas - Base Gastos): ${fmtMoney(rendimientoNeto)}`,
    310,
    578,
    9.5,
    true,
    1,
    1,
    1
  )

  // Tabla Índice de Facturas del Trimestre (en orden cronológico)
  p.text('ÍNDICE ORDENADO DE FACTURAS INCLUIDAS EN ESTE LIBRO TRIMESTRAL', 40, 540, 9.5, true)
  let y = 518
  p.rectFill(40, y - 5, 515, 20, 0.92, 0.94, 0.96)
  p.text('NÚMERO', 46, y, 8, true)
  p.text('FECHA', 120, y, 8, true)
  p.text('CLIENTE / RAZÓN SOCIAL', 180, y, 8, true)
  p.text('NIF/CIF', 355, y, 8, true)
  p.text('BASE', 415, y, 8, true)
  p.text('IVA', 468, y, 8, true)
  p.text('TOTAL', 510, y, 8, true)

  y -= 20
  const sorted = [...invoices].sort((a, b) => a.fecha.localeCompare(b.fecha))
  for (const inv of sorted) {
    p.text(inv.numero, 46, y, 8.5, true)
    p.text(inv.fecha, 120, y, 8.5)
    p.text(inv.cliente.slice(0, 30), 180, y, 8.5)
    p.text(inv.cifNif, 355, y, 8.5)
    p.text(inv.baseImponible.toFixed(2), 415, y, 8.5)
    p.text(inv.iva.toFixed(2), 468, y, 8.5)
    p.text(inv.total.toFixed(2), 510, y, 8.5, true)
    p.line(40, y - 5, 555, y - 5, 0.9, 0.91, 0.93)
    y -= 17
    if (y < 95) break
  }

  p.line(40, 70, 555, 70)
  p.text(
    `Página 1 de ${sorted.length + 1} — Resumen Ejecutivo del Trimestre seguido de ${sorted.length} facturas individuales en orden.`,
    40,
    54,
    8,
    false,
    0.4,
    0.45,
    0.5
  )

  return p.buildStream()
}

function triggerBlobDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  setTimeout(() => URL.revokeObjectURL(url), 3000)
}

export function downloadSingleInvoicePdf(invoice: InvoiceItem, company: CompanyConfig) {
  const stream = renderInvoicePage(invoice, company)
  const blob = buildMultiPagePdf([stream])
  const safeNum = invoice.numero.replace(/[^a-zA-Z0-9_-]/g, '_')
  triggerBlobDownload(blob, `Factura_${safeNum}.pdf`)
}

export function downloadQuarterBookPdf(
  quarter: QuarterId | 'todas',
  year: number,
  invoices: InvoiceItem[],
  expenses: ExpenseItem[],
  company: CompanyConfig
) {
  const sortedInvoices = [...invoices].sort((a, b) => a.fecha.localeCompare(b.fecha))
  const pages: string[] = [
    renderQuarterSummaryCoverPage(quarter, year, sortedInvoices, expenses, company),
    ...sortedInvoices.map((inv) => renderInvoicePage(inv, company)),
  ]
  const blob = buildMultiPagePdf(pages)
  const label = quarter === 'todas' ? `Anual_${year}` : `${quarter}_${year}`
  triggerBlobDownload(blob, `Resumen_Trimestral_${label}_y_Facturas.pdf`)
}
