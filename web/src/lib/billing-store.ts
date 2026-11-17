import { useSyncExternalStore } from 'react'

export type QuarterId = '1T' | '2T' | '3T' | '4T'

export interface CompanyConfig {
  razonSocial: string
  nombreComercial: string
  nifCif: string
  direccionFiscal: string
  codigoPostal: string
  ciudad: string
  provincia: string
  pais: string
  telefono: string
  emailFacturacion: string
  iban: string
  registroMercantil: string
  pieFactura: string
  logoWebpDataUrl: string | null
  logoPrivateFileId: string | null
  logoOriginalSizeKb?: number
  logoWebpSizeKb?: number
}

export interface TaxRateItem {
  id: string
  code: string
  name: string
  percentage: number
  kind: 'iva' | 'irpf' | 'recargo'
  isDefault?: boolean
  active: boolean
}

export interface InvoiceTemplateConfig {
  style: 'modern' | 'classic' | 'compact'
  accentColor: 'emerald' | 'slate' | 'blue' | 'indigo'
  showLogo: boolean
  showIban: boolean
  showQrVerifactu: boolean
  showRegistroMercantil: boolean
  defaultPaymentMethod: string
}

export interface VerifactuConfig {
  enabled: boolean
  mode: 'verifactu_aeat' | 'conservacion_firmada'
  environment: 'pruebas' | 'produccion'
  softwareName: string
  softwareVersion: string
  developerNif: string
  autoChainSha256: boolean
  includeQrInPdf: boolean
}

export interface CatalogItem {
  id: string
  sku: string
  concepto: string
  categoria: 'servicio' | 'producto'
  precioUnitario: number
  ivaPercent: number
}

export interface LineaFactura {
  id: string
  concepto: string
  cantidad: number
  precioUnitario: number
  ivaPercent: number
  total: number
}

export interface InvoiceItem {
  id: string
  numero: string
  serie: string
  fecha: string
  trimestre: QuarterId
  ejercicio: number
  fechaVencimiento: string
  clienteId?: string
  cliente: string
  cifNif: string
  direccion: string
  email: string
  baseImponible: number
  iva: number
  irpfPercent: number
  irpfImporte: number
  recargoPercent: number
  recargoImporte: number
  total: number
  estado: 'emitida' | 'cobrada' | 'borrador' | 'rectificativa'
  formaPago: string
  ibanParcial?: string
  notas?: string
  hashVerifactu: string
  lineas: LineaFactura[]
}

export interface QuoteItem {
  id: string
  numero: string // P2026-01, P2026-02...
  serie: string
  fecha: string
  fechaValidez: string
  clienteId?: string
  cliente: string
  cifNif: string
  direccion: string
  email: string
  baseImponible: number
  iva: number
  irpfPercent?: number
  irpfImporte?: number
  total: number
  estado: 'pendiente' | 'aceptado' | 'rechazado' | 'facturado'
  formaPago?: string
  notas?: string
  lineas: LineaFactura[]
}

export interface ExpenseItem {
  id: string
  numeroDocumento: string
  fecha: string
  trimestre: QuarterId
  ejercicio: number
  proveedor: string
  cifProveedor: string
  categoria:
    | 'Mercancías y Materias Primas'
    | 'Suministros (Luz, Agua, Fibra)'
    | 'Alquiler y Local'
    | 'Servicios Profesionales'
    | 'Software y Suscripciones'
    | 'Transporte y Dietas'
  concepto: string
  baseImponible: number
  ivaPercent: number
  ivaSoportado: number
  irpfRetenido: number
  total: number
  deducible: boolean
  comprobanteWebpDataUrl?: string | null
  comprobanteNombre?: string
  ocrConfidence?: number
}

export interface CalendarEventItem {
  id: string
  fecha: string // YYYY-MM-DD
  horaInicio: string // HH:mm
  horaFin: string // HH:mm
  titulo: string
  tipo: 'visita' | 'tarea' | 'reunion' | 'fiscal'
  cliente?: string
  ubicacion?: string
  notas?: string
  completado: boolean
}

export function getQuarterFromDate(dateStr: string): QuarterId {
  const parts = dateStr.split('-')
  const month = parts.length >= 2 ? parseInt(parts[1], 10) : 1
  if (month <= 3) return '1T'
  if (month <= 6) return '2T'
  if (month <= 9) return '3T'
  return '4T'
}

export function getYearFromDate(dateStr: string): number {
  const parts = dateStr.split('-')
  const year = parts.length >= 1 ? parseInt(parts[0], 10) : 2026
  return Number.isNaN(year) ? 2026 : year
}

/**
 * Convierte cualquier archivo de imagen (PNG, JPEG, WebP) a formato WebP
 * con una compresión del 65% (quality = 0.65).
 */
export async function convertImageToWebp65(file: File): Promise<{
  blob: Blob
  webpFile: File
  dataUrl: string
  originalSizeKb: number
  webpSizeKb: number
  width: number
  height: number
}> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onerror = () => reject(new Error('No se pudo leer el archivo de imagen.'))
    reader.onload = () => {
      const img = new Image()
      img.onerror = () => reject(new Error('Formato de imagen no válido.'))
      img.onload = () => {
        const maxDim = 900
        let width = img.naturalWidth || img.width || 300
        let height = img.naturalHeight || img.height || 120
        if (width > maxDim || height > maxDim) {
          const ratio = Math.min(maxDim / width, maxDim / height)
          width = Math.round(width * ratio)
          height = Math.round(height * ratio)
        }

        const canvas = document.createElement('canvas')
        canvas.width = width
        canvas.height = height
        const ctx = canvas.getContext('2d')
        if (!ctx) {
          reject(new Error('No se pudo inicializar el motor gráfico de conversión WebP.'))
          return
        }

        // Fondo blanco limpio para evitar transparencias negras
        ctx.fillStyle = '#FFFFFF'
        ctx.fillRect(0, 0, width, height)
        ctx.drawImage(img, 0, 0, width, height)

        const quality = 0.65
        const dataUrl = canvas.toDataURL('image/webp', quality)

        canvas.toBlob(
          (blob) => {
            if (!blob) {
              reject(new Error('Error al comprimir a WebP 65%.'))
              return
            }
            const baseName = file.name.replace(/\.[^/.]+$/, '')
            const webpFile = new File([blob], `${baseName}.webp`, {
              type: 'image/webp',
            })
            resolve({
              blob,
              webpFile,
              dataUrl,
              originalSizeKb: Math.max(1, Math.round((file.size / 1024) * 10) / 10),
              webpSizeKb: Math.max(1, Math.round((blob.size / 1024) * 10) / 10),
              width,
              height,
            })
          },
          'image/webp',
          quality
        )
      }
      img.src = reader.result as string
    }
    reader.readAsDataURL(file)
  })
}

interface BillingState {
  company: CompanyConfig
  taxes: TaxRateItem[]
  template: InvoiceTemplateConfig
  verifactu: VerifactuConfig
  catalog: CatalogItem[]
  facturas: InvoiceItem[]
  presupuestos: QuoteItem[]
  gastos: ExpenseItem[]
  eventos: CalendarEventItem[]
}

const STORAGE_KEY = 'paye_pos_billing_store_v1'

const defaultState: BillingState = {
  company: {
    razonSocial: 'PAYÉ RETAIL S.L.',
    nombreComercial: 'Payé Base',
    nifCif: 'B88776655',
    direccionFiscal: 'Calle del Comercio 42, Planta 2',
    codigoPostal: '28014',
    ciudad: 'Madrid',
    provincia: 'Madrid',
    pais: 'España',
    telefono: '+34 910 245 800',
    emailFacturacion: 'facturas@payedigital.com',
    iban: 'ES91 2100 0418 4502 0005 8821',
    registroMercantil:
      'Inscrita en el Registro Mercantil de Madrid, Tomo 41.220, Folio 15, Hoja M-730114.',
    pieFactura:
      'Régimen general del IVA. Factura verificable conforme al Real Decreto 1007/2023 (VeriFactu).',
    logoWebpDataUrl: null,
    logoPrivateFileId: null,
  },
  taxes: [
    { id: 'iva-21', code: 'IVA21', name: 'IVA General (21%)', percentage: 21, kind: 'iva', isDefault: true, active: true },
    { id: 'iva-10', code: 'IVA10', name: 'IVA Reducido (10%)', percentage: 10, kind: 'iva', isDefault: false, active: true },
    { id: 'iva-4', code: 'IVA4', name: 'IVA Superreducido (4%)', percentage: 4, kind: 'iva', isDefault: false, active: true },
    { id: 'iva-0', code: 'IVA0', name: 'Exento de IVA (0%)', percentage: 0, kind: 'iva', isDefault: false, active: true },
    { id: 'irpf-0', code: 'IRPF0', name: 'Sin retención IRPF (0%)', percentage: 0, kind: 'irpf', isDefault: true, active: true },
    { id: 'irpf-7', code: 'IRPF7', name: 'IRPF Inicio Actividad (7%)', percentage: 7, kind: 'irpf', isDefault: false, active: true },
    { id: 'irpf-15', code: 'IRPF15', name: 'IRPF Profesional (15%)', percentage: 15, kind: 'irpf', isDefault: false, active: true },
    { id: 'irpf-19', code: 'IRPF19', name: 'IRPF Arrendamientos (19%)', percentage: 19, kind: 'irpf', isDefault: false, active: true },
    { id: 're-0', code: 'RE0', name: 'Sin Recargo Equivalencia (0%)', percentage: 0, kind: 'recargo', isDefault: true, active: true },
    { id: 're-52', code: 'RE52', name: 'Recargo Equivalencia General (5,2%)', percentage: 5.2, kind: 'recargo', isDefault: false, active: true },
    { id: 're-14', code: 'RE14', name: 'Recargo Equivalencia Reducido (1,4%)', percentage: 1.4, kind: 'recargo', isDefault: false, active: true },
    { id: 're-05', code: 'RE05', name: 'Recargo Equivalencia Superreducido (0,5%)', percentage: 0.5, kind: 'recargo', isDefault: false, active: true },
  ],
  template: {
    style: 'modern',
    accentColor: 'emerald',
    showLogo: true,
    showIban: true,
    showQrVerifactu: true,
    showRegistroMercantil: true,
    defaultPaymentMethod: 'Transferencia bancaria a 30 días',
  },
  verifactu: {
    enabled: true,
    mode: 'verifactu_aeat',
    environment: 'pruebas',
    softwareName: 'Payé Base Facturación',
    softwareVersion: '1.0.0',
    developerNif: 'B88776655',
    autoChainSha256: true,
    includeQrInPdf: true,
  },
  catalog: [],
  facturas: [],
  presupuestos: [],
  gastos: [],
  eventos: [],
}

export function getNextInvoiceNumber(
  facturas: InvoiceItem[],
  year = new Date().getFullYear(),
  serie = 'F'
): string {
  const yearPrefix = `${serie}${year}-`
  let maxSeq = 0
  for (const f of facturas) {
    if (f.numero?.startsWith(yearPrefix)) {
      const match = f.numero.slice(yearPrefix.length).match(/^(\d+)/)
      if (match) {
        const num = parseInt(match[1], 10)
        if (!isNaN(num) && num > maxSeq) {
          maxSeq = num
        }
      }
    }
  }
  const nextSeq = maxSeq + 1
  return `${yearPrefix}${String(nextSeq).padStart(2, '0')}`
}

export function getNextQuoteNumber(
  presupuestos: QuoteItem[],
  year = new Date().getFullYear(),
  serie = 'P'
): string {
  const yearPrefix = `${serie}${year}-`
  let maxSeq = 0
  for (const p of presupuestos) {
    if (p.numero?.startsWith(yearPrefix)) {
      const match = p.numero.slice(yearPrefix.length).match(/^(\d+)/)
      if (match) {
        const num = parseInt(match[1], 10)
        if (!isNaN(num) && num > maxSeq) {
          maxSeq = num
        }
      }
    }
  }
  const nextSeq = maxSeq + 1
  return `${yearPrefix}${String(nextSeq).padStart(2, '0')}`
}

function isMockData(facturas: InvoiceItem[]): boolean {
  return facturas.some(
    (f) =>
      f.cliente === 'Acme Telecomunicaciones S.L.' ||
      f.numero === 'F2026-00124' ||
      f.numero === 'F2026-00128' ||
      f.cifNif === 'B98765432'
  )
}

function loadState(): BillingState {
  if (typeof window === 'undefined') return defaultState
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return defaultState
    const parsed = JSON.parse(raw) as Partial<BillingState>

    const loadedFacturas = Array.isArray(parsed.facturas) ? parsed.facturas : []
    const loadedPresupuestos = Array.isArray(parsed.presupuestos) ? parsed.presupuestos : []
    const loadedGastos = Array.isArray(parsed.gastos) ? parsed.gastos : []
    const loadedCatalog = Array.isArray(parsed.catalog) ? parsed.catalog : []
    const loadedEventos = Array.isArray(parsed.eventos) ? parsed.eventos : []

    // Si los datos guardados en localStorage son los datos de muestra antiguos, purgarlos
    if (isMockData(loadedFacturas)) {
      const cleanState: BillingState = {
        company: { ...defaultState.company, ...(parsed.company ?? {}) },
        taxes: Array.isArray(parsed.taxes) && parsed.taxes.length > 0 ? parsed.taxes : defaultState.taxes,
        template: { ...defaultState.template, ...(parsed.template ?? {}) },
        verifactu: { ...defaultState.verifactu, ...(parsed.verifactu ?? {}) },
        catalog: [],
        facturas: [],
        presupuestos: [],
        gastos: [],
        eventos: [],
      }
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(cleanState))
      } catch {
        // ignore
      }
      return cleanState
    }

    return {
      company: { ...defaultState.company, ...(parsed.company ?? {}) },
      taxes: Array.isArray(parsed.taxes) && parsed.taxes.length > 0 ? parsed.taxes : defaultState.taxes,
      template: { ...defaultState.template, ...(parsed.template ?? {}) },
      verifactu: { ...defaultState.verifactu, ...(parsed.verifactu ?? {}) },
      catalog: loadedCatalog,
      facturas: loadedFacturas,
      presupuestos: loadedPresupuestos,
      gastos: loadedGastos,
      eventos: loadedEventos,
    }
  } catch {
    return defaultState
  }
}

let currentState: BillingState = loadState()
const listeners = new Set<() => void>()

function emitChange() {
  if (typeof window !== 'undefined') {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(currentState))
    } catch {
      // ignore quota errors
    }
  }
  for (const listener of listeners) {
    listener()
  }
}

export const billingActions = {
  updateCompany(patch: Partial<CompanyConfig>) {
    currentState = {
      ...currentState,
      company: { ...currentState.company, ...patch },
    }
    emitChange()
  },

  updateTemplate(patch: Partial<InvoiceTemplateConfig>) {
    currentState = {
      ...currentState,
      template: { ...currentState.template, ...patch },
    }
    emitChange()
  },

  updateVerifactu(patch: Partial<VerifactuConfig>) {
    currentState = {
      ...currentState,
      verifactu: { ...currentState.verifactu, ...patch },
    }
    emitChange()
  },

  setTaxes(taxes: TaxRateItem[]) {
    currentState = {
      ...currentState,
      taxes,
    }
    emitChange()
  },

  addTax(tax: Omit<TaxRateItem, 'id'>) {
    const newTax: TaxRateItem = {
      ...tax,
      id: `tax-${Date.now()}`,
    }
    let updated = [...currentState.taxes, newTax]
    if (newTax.isDefault) {
      updated = updated.map((t) =>
        t.kind === newTax.kind && t.id !== newTax.id ? { ...t, isDefault: false } : t
      )
    }
    currentState = { ...currentState, taxes: updated }
    emitChange()
  },

  toggleTaxDefault(id: string) {
    const target = currentState.taxes.find((t) => t.id === id)
    if (!target) return
    const updated = currentState.taxes.map((t) =>
      t.kind === target.kind ? { ...t, isDefault: t.id === id } : t
    )
    currentState = { ...currentState, taxes: updated }
    emitChange()
  },

  addCatalogItem(item: Omit<CatalogItem, 'id'>) {
    const newItem: CatalogItem = { ...item, id: `cat-${Date.now()}` }
    currentState = {
      ...currentState,
      catalog: [...currentState.catalog, newItem],
    }
    emitChange()
  },

  saveInvoice(invoice: InvoiceItem) {
    const exists = currentState.facturas.some((f) => f.id === invoice.id)
    const updated = exists
      ? currentState.facturas.map((f) => (f.id === invoice.id ? invoice : f))
      : [invoice, ...currentState.facturas]
    currentState = {
      ...currentState,
      facturas: updated,
    }
    emitChange()
  },

  saveQuote(quote: QuoteItem) {
    const exists = currentState.presupuestos.some((p) => p.id === quote.id)
    const updated = exists
      ? currentState.presupuestos.map((p) => (p.id === quote.id ? quote : p))
      : [quote, ...currentState.presupuestos]
    currentState = {
      ...currentState,
      presupuestos: updated,
    }
    emitChange()
  },

  deleteQuote(id: string) {
    currentState = {
      ...currentState,
      presupuestos: currentState.presupuestos.filter((p) => p.id !== id),
    }
    emitChange()
  },

  convertQuoteToInvoice(quoteId: string): InvoiceItem | null {
    const quote = currentState.presupuestos.find((p) => p.id === quoteId)
    if (!quote) return null

    const year = new Date().getFullYear()
    const numero = getNextInvoiceNumber(currentState.facturas, year, 'F')
    const fecha = new Date().toISOString().split('T')[0]
    const vencDate = new Date()
    vencDate.setDate(vencDate.getDate() + 30)
    const fechaVencimiento = vencDate.toISOString().split('T')[0]

    const newInvoice: InvoiceItem = {
      id: `inv-${Date.now()}`,
      numero,
      serie: 'F',
      fecha,
      trimestre: getQuarterFromDate(fecha),
      ejercicio: year,
      fechaVencimiento,
      clienteId: quote.clienteId,
      cliente: quote.cliente,
      cifNif: quote.cifNif,
      direccion: quote.direccion,
      email: quote.email,
      baseImponible: quote.baseImponible,
      iva: quote.iva,
      irpfPercent: quote.irpfPercent ?? 0,
      irpfImporte: quote.irpfImporte ?? 0,
      recargoPercent: 0,
      recargoImporte: 0,
      total: quote.total,
      estado: 'emitida',
      formaPago: quote.formaPago || currentState.template.defaultPaymentMethod,
      notas: quote.notas
        ? `Presupuesto de origen: ${quote.numero}\n${quote.notas}`
        : `Presupuesto de origen: ${quote.numero}`,
      hashVerifactu: `PRE-${Date.now().toString(16).toUpperCase()}`,
      lineas: quote.lineas,
    }

    const updatedQuotes = currentState.presupuestos.map((p) =>
      p.id === quoteId ? { ...p, estado: 'facturado' as const } : p
    )

    currentState = {
      ...currentState,
      facturas: [newInvoice, ...currentState.facturas],
      presupuestos: updatedQuotes,
    }
    emitChange()
    return newInvoice
  },

  addExpense(expense: Omit<ExpenseItem, 'id'>) {
    const newExpense: ExpenseItem = {
      ...expense,
      id: `g-${Date.now()}`,
    }
    currentState = {
      ...currentState,
      gastos: [newExpense, ...currentState.gastos],
    }
    emitChange()
    return newExpense
  },

  deleteExpense(id: string) {
    currentState = {
      ...currentState,
      gastos: currentState.gastos.filter((g) => g.id !== id),
    }
    emitChange()
  },

  addCalendarEvent(event: Omit<CalendarEventItem, 'id'>) {
    const newEvent: CalendarEventItem = {
      ...event,
      id: `ev-${Date.now()}`,
    }
    currentState = {
      ...currentState,
      eventos: [...currentState.eventos, newEvent],
    }
    emitChange()
    return newEvent
  },

  toggleCalendarEvent(id: string) {
    currentState = {
      ...currentState,
      eventos: currentState.eventos.map((e) =>
        e.id === id ? { ...e, completado: !e.completado } : e
      ),
    }
    emitChange()
  },

  deleteCalendarEvent(id: string) {
    currentState = {
      ...currentState,
      eventos: currentState.eventos.filter((e) => e.id !== id),
    }
    emitChange()
  },
}

export function useBillingStore(): BillingState {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    () => currentState,
    () => defaultState
  )
}
