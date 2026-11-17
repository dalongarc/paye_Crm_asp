import type { IDocumentacionApi } from '../interfaces'
import type {
  DocumentacionFilterQuery,
  DocumentacionPagedResult,
  DocumentoComercialDetailDto,
  SaveDocumentoComercialRequest,
  DocumentacionCompanySettingsDto,
  UpdateDocumentacionCompanySettingsRequest,
  DocumentoLineaInputDto,
  CalculatedDocumentoTotals,
  DocumentoSecretScanResult,
  DocumentoArchivoExtraidoDto,
  GenerarBorradorAiRequest,
  DocumentoAiBorradorResponseDto,
  RegenerarBloqueAiRequest,
  DocumentoAiBloqueGeneradoDto,
} from '../types'
import { calculateDocumentoTotals } from '@/features/documentacion/utils/totals-calculator'

const STORAGE_DOCS_KEY = 'paye_mock_documentacion_docs'
const STORAGE_SETTINGS_KEY = 'paye_mock_documentacion_settings'

const calcLinesLocal = calculateDocumentoTotals

export class MockDocumentacionApi implements IDocumentacionApi {
  private loadDocs(): DocumentoComercialDetailDto[] {
    try {
      const raw = localStorage.getItem(STORAGE_DOCS_KEY)
      return raw ? JSON.parse(raw) : []
    } catch {
      return []
    }
  }

  private saveDocs(docs: DocumentoComercialDetailDto[]): void {
    localStorage.setItem(STORAGE_DOCS_KEY, JSON.stringify(docs))
  }

  async getDocumentos(query?: DocumentacionFilterQuery): Promise<DocumentacionPagedResult> {
    let docs = this.loadDocs()
    if (query?.tipo) {
      docs = docs.filter((d) => d.tipo === query.tipo)
    }
    if (query?.clienteId) {
      docs = docs.filter((d) => d.clienteId === query.clienteId)
    }
    if (query?.search) {
      const s = query.search.toLowerCase()
      docs = docs.filter(
        (d) =>
          d.numeroDocumento.toLowerCase().includes(s) ||
          d.titulo.toLowerCase().includes(s) ||
          d.clienteNombre.toLowerCase().includes(s)
      )
    }
    return {
      items: docs,
      totalCount: docs.length,
      page: query?.page ?? 1,
      pageSize: query?.pageSize ?? 20,
    }
  }

  async getDocumentoById(id: string): Promise<DocumentoComercialDetailDto> {
    const found = this.loadDocs().find((d) => d.id === id)
    if (!found) throw new Error('Documento no encontrado')
    return found
  }

  async createDocumento(data: SaveDocumentoComercialRequest): Promise<DocumentoComercialDetailDto> {
    const docs = this.loadDocs()
    const year = new Date().getFullYear()
    const sameTypeYear = docs.filter((d) => d.tipo === data.tipo && d.fiscalYear === year)
    const seq = sameTypeYear.reduce((max, d) => Math.max(max, d.sequentialNumber), 0) + 1
    const prefix = data.tipo === 1 ? 'CONT' : 'PRES'
    const numeroDocumento = `${prefix}-${year}-${String(seq).padStart(3, '0')}`
    const calc = calcLinesLocal(data.lineas ?? [])
    const now = new Date().toISOString()

    const created: DocumentoComercialDetailDto = {
      id: crypto.randomUUID(),
      tipo: data.tipo,
      fiscalYear: year,
      sequentialNumber: seq,
      numeroDocumento,
      titulo: data.titulo || `${prefix} — ${data.clienteNombreOverride || 'Cliente'}`,
      subtitulo: data.subtitulo || '',
      categoria: data.categoria || (data.tipo === 1 ? 'Contrato' : 'Propuesta Comercial'),
      clienteId: data.clienteId ?? null,
      clienteNombre: data.clienteNombreOverride || 'Cliente sin asignar',
      clienteCifNif: data.clienteCifNifOverride || '',
      clienteDireccion: data.clienteDireccionOverride || '',
      clienteEmail: data.clienteEmailOverride || '',
      clienteTelefono: data.clienteTelefonoOverride || '',
      fechaEmision: data.fechaEmision || now.slice(0, 10),
      fechaValidez: data.fechaValidez ?? null,
      objetoContrato: data.objetoContrato ?? null,
      duracionContrato: data.duracionContrato ?? null,
      precioTextoContrato: data.precioTextoContrato ?? null,
      formaPagoContrato: data.formaPagoContrato ?? null,
      lugarFirma: data.lugarFirma ?? null,
      bloques: data.bloques ?? [],
      lineas: calc.lineas.map((l) => ({ ...l, id: crypto.randomUUID() })),
      desgloseIva: calc.desgloseIva,
      subtotalBruto: calc.subtotalBruto,
      totalDescuento: calc.totalDescuento,
      baseImponible: calc.baseImponible,
      totalIva: calc.totalIva,
      totalGeneral: calc.totalGeneral,
      createdAtUtc: now,
      updatedAtUtc: now,
      rowVersion: 1,
    }

    docs.unshift(created)
    this.saveDocs(docs)
    return created
  }

  async updateDocumento(
    id: string,
    data: SaveDocumentoComercialRequest
  ): Promise<DocumentoComercialDetailDto> {
    const docs = this.loadDocs()
    const idx = docs.findIndex((d) => d.id === id)
    if (idx === -1) throw new Error('Documento no encontrado')

    const existing = docs[idx]
    const calc = calcLinesLocal(data.lineas ?? [])
    const updated: DocumentoComercialDetailDto = {
      ...existing,
      clienteId: data.clienteId !== undefined ? data.clienteId : existing.clienteId,
      clienteNombre: data.clienteNombreOverride ?? existing.clienteNombre,
      clienteCifNif: data.clienteCifNifOverride ?? existing.clienteCifNif,
      clienteDireccion: data.clienteDireccionOverride ?? existing.clienteDireccion,
      clienteEmail: data.clienteEmailOverride ?? existing.clienteEmail,
      clienteTelefono: data.clienteTelefonoOverride ?? existing.clienteTelefono,
      titulo: data.titulo ?? existing.titulo,
      subtitulo: data.subtitulo ?? existing.subtitulo,
      categoria: data.categoria ?? existing.categoria,
      fechaEmision: data.fechaEmision ?? existing.fechaEmision,
      fechaValidez: data.fechaValidez ?? existing.fechaValidez,
      objetoContrato: data.objetoContrato ?? existing.objetoContrato,
      duracionContrato: data.duracionContrato ?? existing.duracionContrato,
      precioTextoContrato: data.precioTextoContrato ?? existing.precioTextoContrato,
      formaPagoContrato: data.formaPagoContrato ?? existing.formaPagoContrato,
      lugarFirma: data.lugarFirma ?? existing.lugarFirma,
      bloques: data.bloques ?? existing.bloques,
      lineas: calc.lineas.map((l) => ({ ...l, id: crypto.randomUUID() })),
      desgloseIva: calc.desgloseIva,
      subtotalBruto: calc.subtotalBruto,
      totalDescuento: calc.totalDescuento,
      baseImponible: calc.baseImponible,
      totalIva: calc.totalIva,
      totalGeneral: calc.totalGeneral,
      updatedAtUtc: new Date().toISOString(),
      rowVersion: existing.rowVersion + 1,
    }

    docs[idx] = updated
    this.saveDocs(docs)
    return updated
  }

  async duplicateDocumento(id: string): Promise<DocumentoComercialDetailDto> {
    const orig = await this.getDocumentoById(id)
    return this.createDocumento({
      tipo: orig.tipo,
      clienteId: orig.clienteId,
      titulo: `${orig.titulo} (Copia)`,
      subtitulo: orig.subtitulo,
      categoria: orig.categoria,
      clienteNombreOverride: orig.clienteNombre,
      clienteCifNifOverride: orig.clienteCifNif,
      clienteDireccionOverride: orig.clienteDireccion,
      clienteEmailOverride: orig.clienteEmail,
      clienteTelefonoOverride: orig.clienteTelefono,
      objetoContrato: orig.objetoContrato,
      duracionContrato: orig.duracionContrato,
      precioTextoContrato: orig.precioTextoContrato,
      formaPagoContrato: orig.formaPagoContrato,
      lugarFirma: orig.lugarFirma,
      bloques: orig.bloques,
      lineas: orig.lineas,
    })
  }

  async deleteDocumento(id: string): Promise<void> {
    const docs = this.loadDocs().filter((d) => d.id !== id)
    this.saveDocs(docs)
  }

  async getCompanySettings(): Promise<DocumentacionCompanySettingsDto> {
    try {
      const raw = localStorage.getItem(STORAGE_SETTINGS_KEY)
      if (raw) return JSON.parse(raw)
    } catch {
      // ignore
    }
    return {
      id: 'default-settings',
      nombreEmpresa: 'Payé Digital',
      cifNif: '',
      direccion: '',
      telefono: '',
      email: '',
      web: '',
      iban: '',
      logoUrl: '/images/documentacion/logo-paye-dark.png',
      textoLegalTitulo: 'Propiedad Intelectual y Confidencialidad',
      textoLegalContenido:
        'El contenido de este documento es propiedad exclusiva de la empresa emisora. Queda prohibida la reproducción parcial o total, así como su distribución a terceros sin autorización expresa.',
      modeloIa: 'gemini-3.8-flash',
      rowVersion: 1,
    }
  }

  async updateCompanySettings(
    data: UpdateDocumentacionCompanySettingsRequest
  ): Promise<DocumentacionCompanySettingsDto> {
    const saved: DocumentacionCompanySettingsDto = {
      id: 'default-settings',
      ...data,
      modeloIa: data.modeloIa || 'gemini-3.8-flash',
      rowVersion: (data.rowVersion || 1) + 1,
    }
    localStorage.setItem(STORAGE_SETTINGS_KEY, JSON.stringify(saved))
    return saved
  }

  async calculateLines(lineas: DocumentoLineaInputDto[]): Promise<CalculatedDocumentoTotals> {
    return calcLinesLocal(lineas)
  }

  async exportDocumentoPdf(id: string, fileName?: string): Promise<void> {
    const response = await fetch(`/api/documentacion/${encodeURIComponent(id)}/pdf`, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/pdf' },
    })
    if (!response.ok) {
      throw new Error(`No se pudo exportar el PDF en el servidor (HTTP ${response.status}).`)
    }
    const blob = await response.blob()
    const blobUrl = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = blobUrl
    link.download = fileName ? (fileName.endsWith('.pdf') ? fileName : `${fileName}.pdf`) : `documento-${id}.pdf`
    document.body.appendChild(link)
    link.click()
    link.remove()
    setTimeout(() => URL.revokeObjectURL(blobUrl), 5000)
  }

  async scanSecrets(textoOrigen: string): Promise<DocumentoSecretScanResult> {
    return {
      hasSecrets: false,
      findings: [],
      redactedText: textoOrigen,
    }
  }

  async extractFile(file: File): Promise<DocumentoArchivoExtraidoDto> {
    const text = await file.text()
    return {
      fileName: file.name,
      extractedText: text.slice(0, 60000),
      characterCount: text.length,
      exceededMaxCharacters: text.length > 60000,
      maxAllowedCharacters: 60000,
    }
  }

  async generarBorrador(request: GenerarBorradorAiRequest): Promise<DocumentoAiBorradorResponseDto> {
    return {
      titulo: request.tipo === 1 ? 'Contrato de Desarrollo a Medida' : 'Propuesta Digital a Medida',
      subtitulo: 'Generado a partir de la documentación del proyecto',
      bloques: [
        {
          tipo: 'texto',
          titulo: request.tipo === 1 ? 'Objeto del contrato' : 'Introducción',
          contenido: '<p>Propuesta redactada en lenguaje claro según la documentación aportada.</p>',
          pendiente: [],
        },
      ],
      lineasSugeridas:
        request.tipo === 2
          ? [{ descripcion: 'Puesta en marcha y configuración inicial', cantidad: 1 }]
          : [],
      modeloUtilizado: 'gemini-2.5-flash (mock)',
      caracteresOrigenProcesados: request.textoOrigen.length,
      reintentoJsonAplicado: false,
    }
  }

  async regenerarBloque(request: RegenerarBloqueAiRequest): Promise<DocumentoAiBloqueGeneradoDto> {
    return {
      tipo: 'texto',
      titulo: request.bloqueTitulo,
      contenido: request.bloqueContenidoActual,
      pendiente: [],
    }
  }
}
