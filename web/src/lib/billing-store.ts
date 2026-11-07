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
  catalog: [
    {
      id: 'cat-1',
      sku: 'SRV-TPV-PRO',
      concepto: 'Suscripción TPV Cloud Payé Pro (Anual)',
      categoria: 'servicio',
      precioUnitario: 890.0,
      ivaPercent: 21,
    },
    {
      id: 'cat-2',
      sku: 'HW-NFC-01',
      concepto: 'Lector de tarjetas inalámbrico NFC Payé Terminal',
      categoria: 'producto',
      precioUnitario: 180.0,
      ivaPercent: 21,
    },
    {
      id: 'cat-3',
      sku: 'SRV-MANT-01',
      concepto: 'Mantenimiento preventivo e integración balanza TPV',
      categoria: 'servicio',
      precioUnitario: 350.0,
      ivaPercent: 21,
    },
    {
      id: 'cat-4',
      sku: 'CONSUM-50',
      concepto: 'Rollo papel térmico libre de bisfenol (Pack 50 uds)',
      categoria: 'producto',
      precioUnitario: 65.0,
      ivaPercent: 21,
    },
    {
      id: 'cat-5',
      sku: 'SRV-FORM-01',
      concepto: 'Jornada formativa presencial equipo administrativo',
      categoria: 'servicio',
      precioUnitario: 640.0,
      ivaPercent: 21,
    },
    {
      id: 'cat-6',
      sku: 'SRV-CONS-HORA',
      concepto: 'Hora de consultoría técnica y parametrización fiscal',
      categoria: 'servicio',
      precioUnitario: 75.0,
      ivaPercent: 21,
    },
  ],
  facturas: [
    {
      id: '1',
      numero: 'F2026-00124',
      serie: 'F2026',
      fecha: '2026-11-05',
      trimestre: '4T',
      ejercicio: 2026,
      fechaVencimiento: '2026-12-05',
      cliente: 'Acme Telecomunicaciones S.L.',
      cifNif: 'B98765432',
      direccion: 'Avenida Gran Vía 42, 28013 Madrid',
      email: 'facturacion@acmetelecom.es',
      baseImponible: 1250.0,
      iva: 262.5,
      irpfPercent: 0,
      irpfImporte: 0,
      recargoPercent: 0,
      recargoImporte: 0,
      total: 1512.5,
      estado: 'cobrada',
      formaPago: 'Transferencia bancaria a 30 días',
      ibanParcial: 'ES91 **** **** **** 8821',
      hashVerifactu: 'a7b3c9d1e2f4...892e',
      lineas: [
        {
          id: 'l1',
          concepto: 'Suscripción TPV Cloud Payé Pro (Anual)',
          cantidad: 1,
          precioUnitario: 890.0,
          ivaPercent: 21,
          total: 890.0,
        },
        {
          id: 'l2',
          concepto: 'Lector de tarjetas inalámbrico NFC Payé Terminal',
          cantidad: 2,
          precioUnitario: 180.0,
          ivaPercent: 21,
          total: 360.0,
        },
      ],
    },
    {
      id: '2',
      numero: 'F2026-00125',
      serie: 'F2026',
      fecha: '2026-11-04',
      trimestre: '4T',
      ejercicio: 2026,
      fechaVencimiento: '2026-11-18',
      cliente: 'Restaurante El Molino C.B.',
      cifNif: 'E12345678',
      direccion: 'Plaza Mayor 14, 45001 Toledo',
      email: 'administracion@elmolinotoledo.com',
      baseImponible: 480.0,
      iva: 100.8,
      irpfPercent: 0,
      irpfImporte: 0,
      recargoPercent: 0,
      recargoImporte: 0,
      total: 580.8,
      estado: 'emitida',
      formaPago: 'Domiciliación bancaria SEPA',
      ibanParcial: 'ES23 **** **** **** 1092',
      hashVerifactu: 'c8d4e1f2a3b5...441a',
      lineas: [
        {
          id: 'l3',
          concepto: 'Mantenimiento preventivo e integración balanza TPV',
          cantidad: 1,
          precioUnitario: 350.0,
          ivaPercent: 21,
          total: 350.0,
        },
        {
          id: 'l4',
          concepto: 'Rollo papel térmico libre de bisfenol (Pack 50 uds)',
          cantidad: 2,
          precioUnitario: 65.0,
          ivaPercent: 21,
          total: 130.0,
        },
      ],
    },
    {
      id: '3',
      numero: 'F2026-00126',
      serie: 'F2026',
      fecha: '2026-10-18',
      trimestre: '4T',
      ejercicio: 2026,
      fechaVencimiento: '2026-11-18',
      cliente: 'Talleres Mecánicos Sur S.A.',
      cifNif: 'A87654321',
      direccion: 'Polígono Industrial El Nevero, Nave 12, 06006 Badajoz',
      email: 'contabilidad@talleresmecanicossur.es',
      baseImponible: 2840.0,
      iva: 596.4,
      irpfPercent: 0,
      irpfImporte: 0,
      recargoPercent: 0,
      recargoImporte: 0,
      total: 3436.4,
      estado: 'cobrada',
      formaPago: 'Transferencia bancaria a 30 días',
      ibanParcial: 'ES45 **** **** **** 7734',
      hashVerifactu: 'e1f2a3b4c5d6...782b',
      lineas: [
        {
          id: 'l5',
          concepto: 'Implantación módulo Control de Stock y Gestión de Taller',
          cantidad: 1,
          precioUnitario: 2200.0,
          ivaPercent: 21,
          total: 2200.0,
        },
        {
          id: 'l6',
          sku: 'SRV-FORM-01',
          concepto: 'Jornada formativa presencial equipo administrativo',
          cantidad: 1,
          precioUnitario: 640.0,
          ivaPercent: 21,
          total: 640.0,
        } as LineaFactura,
      ],
    },
    {
      id: '4',
      numero: 'F2026-00098',
      serie: 'F2026',
      fecha: '2026-08-14',
      trimestre: '3T',
      ejercicio: 2026,
      fechaVencimiento: '2026-09-14',
      cliente: 'Clínica Dental Alameda S.L.P.',
      cifNif: 'B55443322',
      direccion: 'Calle Alameda de San Antón 8, 30205 Cartagena',
      email: 'info@dentalalameda.es',
      baseImponible: 920.0,
      iva: 193.2,
      irpfPercent: 0,
      irpfImporte: 0,
      recargoPercent: 0,
      recargoImporte: 0,
      total: 1113.2,
      estado: 'cobrada',
      formaPago: 'Tarjeta de crédito / Débito (TPV Físico)',
      hashVerifactu: 'f3a4b5c6d7e8...119c',
      lineas: [
        {
          id: 'l7',
          concepto: 'Licencia anual terminal de caja Payé Punto de Venta',
          cantidad: 2,
          precioUnitario: 460.0,
          ivaPercent: 21,
          total: 920.0,
        },
      ],
    },
    {
      id: '5',
      numero: 'F2026-00061',
      serie: 'F2026',
      fecha: '2026-05-20',
      trimestre: '2T',
      ejercicio: 2026,
      fechaVencimiento: '2026-06-20',
      cliente: 'Darytravel Aventuras S.L.',
      cifNif: 'B12345678',
      direccion: 'Paseo de la Castellana 120, 28046 Madrid',
      email: 'reservas@darytravel.es',
      baseImponible: 1650.0,
      iva: 346.5,
      irpfPercent: 0,
      irpfImporte: 0,
      recargoPercent: 0,
      recargoImporte: 0,
      total: 1996.5,
      estado: 'cobrada',
      formaPago: 'Transferencia bancaria a 30 días',
      hashVerifactu: '9981a2b3c4d5...301f',
      lineas: [
        {
          id: 'l8',
          concepto: 'Pasarela de reservas y facturación automática agencias',
          cantidad: 1,
          precioUnitario: 1650.0,
          ivaPercent: 21,
          total: 1650.0,
        },
      ],
    },
  ],
  gastos: [
    {
      id: 'g1',
      numeroDocumento: 'PROV-2026-8841',
      fecha: '2026-11-02',
      trimestre: '4T',
      ejercicio: 2026,
      proveedor: 'Cloud Infraestructura Europa S.L.',
      cifProveedor: 'B86421975',
      categoria: 'Software y Suscripciones',
      concepto: 'Servidores dedicados PostgreSQL y copias de seguridad (Noviembre)',
      baseImponible: 320.0,
      ivaPercent: 21,
      ivaSoportado: 67.2,
      irpfRetenido: 0,
      total: 387.2,
      deducible: true,
      ocrConfidence: 99,
    },
    {
      id: 'g2',
      numeroDocumento: 'INM-2026-11',
      fecha: '2026-11-01',
      trimestre: '4T',
      ejercicio: 2026,
      proveedor: 'Inmobiliaria Centro Empresarial S.A.',
      cifProveedor: 'A28019283',
      categoria: 'Alquiler y Local',
      concepto: 'Arrendamiento oficina técnica Calle del Comercio 42',
      baseImponible: 950.0,
      ivaPercent: 21,
      ivaSoportado: 199.5,
      irpfRetenido: 180.5,
      total: 969.0,
      deducible: true,
      ocrConfidence: 98,
    },
    {
      id: 'g3',
      numeroDocumento: 'DIST-99201',
      fecha: '2026-10-15',
      trimestre: '4T',
      ejercicio: 2026,
      proveedor: 'Mayorista Hardware TPV Ibérica S.L.',
      cifProveedor: 'B45678901',
      categoria: 'Mercancías y Materias Primas',
      concepto: 'Lote terminales NFC y rollos térmicos homologados',
      baseImponible: 610.0,
      ivaPercent: 21,
      ivaSoportado: 128.1,
      irpfRetenido: 0,
      total: 738.1,
      deducible: true,
      ocrConfidence: 96,
    },
    {
      id: 'g4',
      numeroDocumento: 'IBER-2026-08',
      fecha: '2026-08-10',
      trimestre: '3T',
      ejercicio: 2026,
      proveedor: 'Comercializadora Eléctrica Peninsular S.A.',
      cifProveedor: 'A98127634',
      categoria: 'Suministros (Luz, Agua, Fibra)',
      concepto: 'Suministro eléctrico y fibra óptica simétrica 1Gbps',
      baseImponible: 210.0,
      ivaPercent: 21,
      ivaSoportado: 44.1,
      irpfRetenido: 0,
      total: 254.1,
      deducible: true,
      ocrConfidence: 97,
    },
  ],
  eventos: [
    {
      id: 'ev-1',
      fecha: '2026-11-06',
      horaInicio: '10:00',
      horaFin: '11:30',
      titulo: 'Visita comercial e instalación TPV en local',
      tipo: 'visita',
      cliente: 'Restaurante El Molino C.B.',
      ubicacion: 'Plaza Mayor 14, Toledo',
      notas: 'Llevar 2 lectores NFC configurados y rollos térmicos.',
      completado: false,
    },
    {
      id: 'ev-2',
      fecha: '2026-11-06',
      horaInicio: '16:30',
      horaFin: '17:30',
      titulo: 'Revisión trimestral de facturación y gastos con asesoría',
      tipo: 'reunion',
      cliente: 'Acme Telecomunicaciones S.L.',
      ubicacion: 'Videollamada / Sala Juntas',
      notas: 'Repasar facturas emitidas del 4T y retenciones.',
      completado: false,
    },
    {
      id: 'ev-3',
      fecha: '2026-11-12',
      horaInicio: '09:30',
      horaFin: '11:00',
      titulo: 'Visita de demostración catálogo ModaStock',
      tipo: 'visita',
      cliente: 'ModaStock Retail S.A.',
      ubicacion: 'Calle Serrano 88, Madrid',
      notas: 'Presentar el nuevo módulo de control de stock y tickets.',
      completado: false,
    },
    {
      id: 'ev-4',
      fecha: '2026-11-20',
      horaInicio: '09:00',
      horaFin: '14:00',
      titulo: 'Conciliación bancaria y cierre provisional IVA',
      tipo: 'fiscal',
      ubicacion: 'Sede Electrónica AEAT',
      notas: 'Verificar encadenamiento VeriFactu y libros registro de IVA.',
      completado: false,
    },
    {
      id: 'ev-5',
      fecha: '2026-11-25',
      horaInicio: '12:00',
      horaFin: '13:00',
      titulo: 'Renovación certificado digital representante legal',
      tipo: 'tarea',
      ubicacion: 'Oficina de Registro FNMT',
      notas: 'Imprescindible para firma automática de envíos VeriFactu.',
      completado: false,
    },
  ],
}

function loadState(): BillingState {
  if (typeof window === 'undefined') return defaultState
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return defaultState
    const parsed = JSON.parse(raw) as Partial<BillingState>
    return {
      company: { ...defaultState.company, ...(parsed.company ?? {}) },
      taxes: Array.isArray(parsed.taxes) && parsed.taxes.length > 0 ? parsed.taxes : defaultState.taxes,
      template: { ...defaultState.template, ...(parsed.template ?? {}) },
      verifactu: { ...defaultState.verifactu, ...(parsed.verifactu ?? {}) },
      catalog: Array.isArray(parsed.catalog) && parsed.catalog.length > 0 ? parsed.catalog : defaultState.catalog,
      facturas: Array.isArray(parsed.facturas) && parsed.facturas.length > 0 ? parsed.facturas : defaultState.facturas,
      gastos: Array.isArray(parsed.gastos) && parsed.gastos.length > 0 ? parsed.gastos : defaultState.gastos,
      eventos: Array.isArray(parsed.eventos) && parsed.eventos.length > 0 ? parsed.eventos : defaultState.eventos,
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
