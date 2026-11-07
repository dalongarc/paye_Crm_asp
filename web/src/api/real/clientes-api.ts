import type { IClientesApi } from '../interfaces'
import type {
  ClienteDto,
  ClientesPagedResult,
  ClienteFilterQuery,
  CreateClienteRequest,
  UpdateClienteRequest,
} from '../types'
import { apiFetch } from '../client'

export class RealClientesApi implements IClientesApi {
  async getClientes(query?: ClienteFilterQuery): Promise<ClientesPagedResult> {
    const params = new URLSearchParams()
    if (query?.searchTerm) params.set('SearchTerm', query.searchTerm)
    if (query?.activo !== undefined) params.set('Activo', String(query.activo))
    if (query?.sortBy) params.set('SortBy', query.sortBy)
    if (query?.sortDescending !== undefined)
      params.set('SortDescending', String(query.sortDescending))
    if (query?.page !== undefined) params.set('Page', String(query.page))
    if (query?.pageSize !== undefined) params.set('PageSize', String(query.pageSize))

    const url = `/api/clientes${params.toString() ? `?${params.toString()}` : ''}`
    return apiFetch<ClientesPagedResult>(url)
  }

  async getClienteById(id: string): Promise<ClienteDto> {
    return apiFetch<ClienteDto>(`/api/clientes/${encodeURIComponent(id)}`)
  }

  async createCliente(data: CreateClienteRequest): Promise<ClienteDto> {
    return apiFetch<ClienteDto>('/api/clientes', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    })
  }

  async updateCliente(id: string, data: UpdateClienteRequest): Promise<ClienteDto> {
    return apiFetch<ClienteDto>(`/api/clientes/${encodeURIComponent(id)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    })
  }

  async deleteCliente(id: string): Promise<void> {
    return apiFetch<void>(`/api/clientes/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    })
  }
}
