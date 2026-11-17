import type {
  CalculatedDocumentoTotals,
  DesgloseIvaItemDto,
  DocumentoLineaInputDto,
} from '@/api/types'

export const ALLOWED_VAT_RATES = [21, 10, 4, 0] as const
export const DEFAULT_LINE_VAT_RATE = 21

/**
 * Redondeo determinista MidpointRounding.AwayFromZero a 2 decimales,
 * idéntico a Math.Round(value, 2, MidpointRounding.AwayFromZero) en C# (.NET 10).
 * Garantiza que Vista Previa == Servidor == PDF al céntimo exacto.
 */
export function roundCurrencyAwayFromZero(value: number): number {
  if (!Number.isFinite(value) || value === 0) return 0
  const sign = value < 0 ? -1 : 1
  const abs = Math.abs(value)
  // Sumamos 1e-9 para evitar imprecisiones binarias de coma flotante en mitades exactas (.005)
  return (sign * Math.floor(abs * 100 + 0.5 + 1e-9)) / 100
}

export function roundQuantityAwayFromZero(value: number): number {
  if (!Number.isFinite(value) || value === 0) return 0
  const sign = value < 0 ? -1 : 1
  const abs = Math.abs(value)
  return (sign * Math.floor(abs * 10000 + 0.5 + 1e-9)) / 10000
}

export function calculateDocumentoTotals(
  inputLines: DocumentoLineaInputDto[] = []
): CalculatedDocumentoTotals {
  const lineas = inputLines.map((raw, idx) => {
    const cantidad = Math.max(0, roundQuantityAwayFromZero(Number(raw.cantidad) || 0))
    const precioUnitario = Math.max(0, roundCurrencyAwayFromZero(Number(raw.precioUnitario) || 0))
    const descuentoPorcentaje = Math.min(
      100,
      Math.max(0, roundCurrencyAwayFromZero(Number(raw.descuentoPorcentaje) || 0))
    )
    const rawVat = roundCurrencyAwayFromZero(
      raw.ivaPorcentaje !== undefined && raw.ivaPorcentaje !== null
        ? Number(raw.ivaPorcentaje)
        : DEFAULT_LINE_VAT_RATE
    )
    const ivaPorcentaje = (ALLOWED_VAT_RATES as readonly number[]).includes(rawVat)
      ? rawVat
      : DEFAULT_LINE_VAT_RATE

    const importeBruto = roundCurrencyAwayFromZero(cantidad * precioUnitario)
    const importeDescuento =
      descuentoPorcentaje > 0
        ? roundCurrencyAwayFromZero(importeBruto * (descuentoPorcentaje / 100))
        : 0
    const baseImponible = roundCurrencyAwayFromZero(importeBruto - importeDescuento)
    const cuotaIva =
      ivaPorcentaje > 0 ? roundCurrencyAwayFromZero(baseImponible * (ivaPorcentaje / 100)) : 0
    const totalLinea = roundCurrencyAwayFromZero(baseImponible + cuotaIva)

    return {
      orden: idx + 1,
      descripcion: raw.descripcion ?? '',
      cantidad,
      precioUnitario,
      descuentoPorcentaje,
      ivaPorcentaje,
      importeBruto,
      importeDescuento,
      baseImponible,
      cuotaIva,
      totalLinea,
    }
  })

  // Agrupación por tipo de IVA (21%, 10%, 4%, 0%) sumando las bases y cuotas redondeadas por línea
  const groups = new Map<number, { base: number; cuota: number }>()
  for (const line of lineas) {
    const current = groups.get(line.ivaPorcentaje) ?? { base: 0, cuota: 0 }
    groups.set(line.ivaPorcentaje, {
      base: roundCurrencyAwayFromZero(current.base + line.baseImponible),
      cuota: roundCurrencyAwayFromZero(current.cuota + line.cuotaIva),
    })
  }

  const desgloseIva: DesgloseIvaItemDto[] = Array.from(groups.entries())
    .sort((a, b) => b[0] - a[0])
    .map(([ivaPorcentaje, sums]) => ({
      ivaPorcentaje,
      etiqueta: ivaPorcentaje === 0 ? 'Exento / 0%' : `IVA ${ivaPorcentaje}%`,
      baseImponible: sums.base,
      cuotaIva: sums.cuota,
      totalConIva: roundCurrencyAwayFromZero(sums.base + sums.cuota),
    }))

  const subtotalBruto = roundCurrencyAwayFromZero(
    lineas.reduce((acc, l) => acc + l.importeBruto, 0)
  )
  const totalDescuento = roundCurrencyAwayFromZero(
    lineas.reduce((acc, l) => acc + l.importeDescuento, 0)
  )
  const baseImponible = roundCurrencyAwayFromZero(
    desgloseIva.reduce((acc, d) => acc + d.baseImponible, 0)
  )
  const totalIva = roundCurrencyAwayFromZero(
    desgloseIva.reduce((acc, d) => acc + d.cuotaIva, 0)
  )
  const totalGeneral = roundCurrencyAwayFromZero(baseImponible + totalIva)

  return {
    subtotalBruto,
    totalDescuento,
    baseImponible,
    totalIva,
    totalGeneral,
    lineas,
    desgloseIva,
  }
}
