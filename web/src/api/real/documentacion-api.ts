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
import { apiFetch } from '../client'

export class RealDocumentacionApi implements IDocumentacionApi {
  async getDocumentos(query?: DocumentacionFilterQuery): Promise<DocumentacionPagedResult> {
    const params = new URLSearchParams()
    if (query?.tipo !== undefined) params.set('Tipo', String(query.tipo))
    if (query?.clienteId) params.set('ClienteId', query.clienteId)
    if (query?.search) params.set('Search', query.search)
    if (query?.page !== undefined) params.set('Page', String(query.page))
    if (query?.pageSize !== undefined) params.set('PageSize', String(query.pageSize))

    const url = `/api/documentacion${params.toString() ? `?${params.toString()}` : ''}`
    return apiFetch<DocumentacionPagedResult>(url)
  }

  async getDocumentoById(id: string): Promise<DocumentoComercialDetailDto> {
    return apiFetch<DocumentoComercialDetailDto>(`/api/documentacion/${encodeURIComponent(id)}`)
  }

  async createDocumento(data: SaveDocumentoComercialRequest): Promise<DocumentoComercialDetailDto> {
    return apiFetch<DocumentoComercialDetailDto>('/api/documentacion', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    })
  }

  async updateDocumento(
    id: string,
    data: SaveDocumentoComercialRequest
  ): Promise<DocumentoComercialDetailDto> {
    const cleanBlocks = (data.bloques ?? []).filter(
      (b) => b.id !== '__meta_lineas_sync__' && b.tipoBloque !== 'meta-lineas-sync'
    )
    const linesJson = JSON.stringify(data.lineas ?? [])
    const bytes = new TextEncoder().encode(linesJson)
    let binary = ''
    for (const b of bytes) {
      binary += String.fromCharCode(b)
    }
    const base64Lines = btoa(binary)

    const payload: SaveDocumentoComercialRequest = {
      ...data,
      rowVersion: 0,
      lineas: [],
      bloques: [
        ...cleanBlocks,
        {
          id: '__meta_lineas_sync__',
          templateBlockId: base64Lines,
          tipoBloque: 'meta-lineas-sync',
          numeroPastilla: null,
          titulo: 'meta',
          contenidoHtml: '',
          visible: false,
          orden: 9998,
          esPersonalizado: false,
        },
      ],
    }

    await apiFetch<DocumentoComercialDetailDto>(`/api/documentacion/${encodeURIComponent(id)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })

    return this.getDocumentoById(id)
  }

  async duplicateDocumento(id: string): Promise<DocumentoComercialDetailDto> {
    return apiFetch<DocumentoComercialDetailDto>(
      `/api/documentacion/${encodeURIComponent(id)}/duplicar`,
      {
        method: 'POST',
      }
    )
  }

  async deleteDocumento(id: string): Promise<void> {
    return apiFetch<void>(`/api/documentacion/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    })
  }

  async getCompanySettings(): Promise<DocumentacionCompanySettingsDto> {
    return apiFetch<DocumentacionCompanySettingsDto>('/api/documentacion/ajustes')
  }

  async updateCompanySettings(
    data: UpdateDocumentacionCompanySettingsRequest
  ): Promise<DocumentacionCompanySettingsDto> {
    return apiFetch<DocumentacionCompanySettingsDto>('/api/documentacion/ajustes', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    })
  }

  async calculateLines(lineas: DocumentoLineaInputDto[]): Promise<CalculatedDocumentoTotals> {
    return apiFetch<CalculatedDocumentoTotals>('/api/documentacion/calcular-lineas', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(lineas),
    })
  }

  async exportDocumentoPdf(id: string, fileName?: string): Promise<void> {
    const response = await fetch(`/api/documentacion/${encodeURIComponent(id)}/pdf`, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/pdf' },
    })

    if (!response.ok) {
      throw new Error(`No se pudo exportar el PDF (HTTP ${response.status}).`)
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
    return apiFetch<DocumentoSecretScanResult>('/api/documentacion/ai/scan-secrets', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ textoOrigen }),
    })
  }

  async extractFile(file: File): Promise<DocumentoArchivoExtraidoDto> {
    const formData = new FormData()
    formData.append('file', file)
    return apiFetch<DocumentoArchivoExtraidoDto>('/api/documentacion/ai/extract-file', {
      method: 'POST',
      body: formData,
    })
  }

  async generarBorrador(request: GenerarBorradorAiRequest): Promise<DocumentoAiBorradorResponseDto> {
    return apiFetch<DocumentoAiBorradorResponseDto>('/api/documentacion/ai/generar-borrador', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })
  }

  async regenerarBloque(request: RegenerarBloqueAiRequest): Promise<DocumentoAiBloqueGeneradoDto> {
    return apiFetch<DocumentoAiBloqueGeneradoDto>('/api/documentacion/ai/regenerar-bloque', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })
  }
}
