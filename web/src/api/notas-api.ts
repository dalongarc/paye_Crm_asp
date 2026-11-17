import { apiFetch } from './client'

export interface NotaDto {
  id: string
  titulo: string
  contenido: string
  archivada: boolean
  createdAtUtc: string
  createdBy: string
  updatedAtUtc?: string | null
  updatedBy?: string | null
  rowVersion: number
}

export interface NotasPagedResult {
  items: NotaDto[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface CreateNotaInput {
  titulo: string
  contenido: string
  archivada?: boolean
}

export interface EditNotaInput {
  id: string
  titulo: string
  contenido: string
  archivada: boolean
  rowVersion: number
}

export const notasApi = {
  async getNotas(params?: {
    q?: string
    estado?: 'activas' | 'archivadas'
    page?: number
  }): Promise<NotasPagedResult> {
    const query = new URLSearchParams()
    if (params?.q) query.set('q', params.q)
    if (params?.estado) query.set('estado', params.estado)
    if (params?.page) query.set('page', String(params.page))
    const qs = query.toString() ? `?${query.toString()}` : ''

    try {
      return await apiFetch<NotasPagedResult>(`/api/notas${qs}`)
    } catch {
      // Fallback a /notas/api por retrocompatibilidad
      return await apiFetch<NotasPagedResult>(`/notas/api${qs}`)
    }
  },

  async createNota(data: CreateNotaInput): Promise<{ success: boolean; id: string }> {
    try {
      return await apiFetch<{ success: boolean; id: string }>('/api/notas', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      })
    } catch {
      return await apiFetch<{ success: boolean; id: string }>('/notas/api', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      })
    }
  },

  async updateNota(id: string, data: EditNotaInput): Promise<{ success: boolean }> {
    try {
      return await apiFetch<{ success: boolean }>(`/api/notas/${encodeURIComponent(id)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      })
    } catch {
      return await apiFetch<{ success: boolean }>(`/notas/api/${encodeURIComponent(id)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      })
    }
  },

  async deleteNota(id: string): Promise<{ success: boolean }> {
    try {
      return await apiFetch<{ success: boolean }>(`/api/notas/${encodeURIComponent(id)}`, {
        method: 'DELETE',
      })
    } catch {
      return await apiFetch<{ success: boolean }>(`/notas/api/${encodeURIComponent(id)}`, {
        method: 'DELETE',
      })
    }
  },
}
