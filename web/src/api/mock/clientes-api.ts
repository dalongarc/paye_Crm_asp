import type { IClientesApi } from '../interfaces'
import {
  ConcurrencyConflictError,
  type ClienteDto,
  type ClientesPagedResult,
  type ClienteFilterQuery,
  type CreateClienteRequest,
  type UpdateClienteRequest,
} from '../types'

let mockClientes: ClienteDto[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    nombreFiscal: 'Acme Corporation S.L.',
    nombreComercial: 'Acme Labs',
    cifNif: 'B12345678',
    email: 'contacto@acme.test',
    telefono: '+34 912 345 678',
    direccion: 'Gran Vía 42, 3º Izq',
    codigoPostal: '28013',
    ciudad: 'Madrid',
    provincia: 'Madrid',
    activo: true,
    notas: 'Cliente principal cuenta premium.',
    rowVersion: 1001,
    createdAtUtc: new Date(Date.now() - 30 * 86400000).toISOString(),
    createdBy: 'system',
    updatedAtUtc: null,
    updatedBy: null,
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    nombreFiscal: 'Soluciones Digitales del Sur S.A.',
    nombreComercial: 'SurDigital',
    cifNif: 'A87654321',
    email: 'admin@surdigital.test',
    telefono: '+34 954 112 233',
    direccion: 'Av. Constitución 12',
    codigoPostal: '41001',
    ciudad: 'Sevilla',
    provincia: 'Sevilla',
    activo: true,
    notas: 'Facturación recurrente mensual.',
    rowVersion: 1002,
    createdAtUtc: new Date(Date.now() - 15 * 86400000).toISOString(),
    createdBy: 'system',
    updatedAtUtc: null,
    updatedBy: null,
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    nombreFiscal: 'Innovación Gastronómica Norte S.L.',
    nombreComercial: 'Bistró Moderno',
    cifNif: 'B99887766',
    email: 'info@bistromoderno.test',
    telefono: '+34 944 987 654',
    direccion: 'Alameda Recalde 8',
    codigoPostal: '48009',
    ciudad: 'Bilbao',
    provincia: 'Bizkaia',
    activo: false,
    notas: 'Suspendido temporalmente por reforma.',
    rowVersion: 1003,
    createdAtUtc: new Date(Date.now() - 60 * 86400000).toISOString(),
    createdBy: 'system',
    updatedAtUtc: null,
    updatedBy: null,
  },
]

export class MockClientesApi implements IClientesApi {
  async getClientes(query?: ClienteFilterQuery): Promise<ClientesPagedResult> {
    await new Promise((resolve) => setTimeout(resolve, 80))

    let filtered = [...mockClientes]
    if (query?.searchTerm) {
      const term = query.searchTerm.toLowerCase()
      filtered = filtered.filter(
        (c) =>
          c.nombreFiscal.toLowerCase().includes(term) ||
          c.cifNif.toLowerCase().includes(term) ||
          c.email.toLowerCase().includes(term)
      )
    }

    if (query?.activo !== undefined) {
      filtered = filtered.filter((c) => c.activo === query.activo)
    }

    const page = query?.page ?? 1
    const pageSize = query?.pageSize ?? 10
    const start = (page - 1) * pageSize
    const items = filtered.slice(start, start + pageSize)

    return {
      items,
      totalCount: filtered.length,
      page,
      pageSize,
      totalPages: Math.ceil(filtered.length / pageSize),
    }
  }

  async getClienteById(id: string): Promise<ClienteDto> {
    await new Promise((resolve) => setTimeout(resolve, 50))
    const cliente = mockClientes.find((c) => c.id === id)
    if (!cliente) {
      throw new Error('Cliente no encontrado')
    }
    return cliente
  }

  async createCliente(data: CreateClienteRequest): Promise<ClienteDto> {
    await new Promise((resolve) => setTimeout(resolve, 100))
    const nuevo: ClienteDto = {
      id: crypto.randomUUID(),
      nombreFiscal: data.nombreFiscal,
      nombreComercial: data.nombreComercial || '',
      cifNif: data.cifNif,
      email: data.email || '',
      telefono: data.telefono || '',
      direccion: data.direccion || '',
      codigoPostal: data.codigoPostal || '',
      ciudad: data.ciudad || '',
      provincia: data.provincia || '',
      activo: data.activo ?? true,
      notas: data.notas || null,
      rowVersion: Math.floor(Math.random() * 100000) + 1,
      createdAtUtc: new Date().toISOString(),
      createdBy: 'usuario_demo',
      updatedAtUtc: null,
      updatedBy: null,
    }
    mockClientes.unshift(nuevo)
    return nuevo
  }

  async updateCliente(id: string, data: UpdateClienteRequest): Promise<ClienteDto> {
    await new Promise((resolve) => setTimeout(resolve, 100))
    const idx = mockClientes.findIndex((c) => c.id === id)
    if (idx === -1) {
      throw new Error('Cliente no encontrado')
    }

    const current = mockClientes[idx]

    // Simulación de detección de concurrencia: si el rowVersion recibido difiere del actual
    if (String(current.rowVersion) !== String(data.rowVersion)) {
      throw new ConcurrencyConflictError(
        'El cliente fue modificado por otro usuario mientras lo editabas.',
        {
          title: 'Conflicto de concurrencia',
          status: 409,
          detail: 'El cliente fue modificado por otro usuario mientras lo editabas.',
        }
      )
    }

    const updated: ClienteDto = {
      ...current,
      nombreFiscal: data.nombreFiscal,
      nombreComercial: data.nombreComercial || '',
      cifNif: data.cifNif,
      email: data.email || '',
      telefono: data.telefono || '',
      direccion: data.direccion || '',
      codigoPostal: data.codigoPostal || '',
      ciudad: data.ciudad || '',
      provincia: data.provincia || '',
      activo: data.activo ?? current.activo,
      notas: data.notas ?? current.notas,
      rowVersion: Number(current.rowVersion) + 1,
      updatedAtUtc: new Date().toISOString(),
      updatedBy: 'usuario_demo',
    }

    mockClientes[idx] = updated
    return updated
  }

  async deleteCliente(id: string): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 100))
    mockClientes = mockClientes.filter((c) => c.id !== id)
  }
}
