export interface ApiUser {
  id: string
  name: string
  email: string
  avatar: string
  role: string
  permissions: string[]
}

export type AuthErrorCode =
  | 'BAD_CREDENTIALS'
  | 'LOCKED_OUT'
  | 'RATE_LIMITED'
  | 'INACTIVE'
  | 'NETWORK_ERROR'
  | 'INVALID_2FA'

export class AuthError extends Error {
  constructor(
    public code: AuthErrorCode,
    message: string,
    public retryAfterMinutes?: number
  ) {
    super(message)
    this.name = 'AuthError'
  }
}

export type LoginResult =
  | { status: 'success'; user: ApiUser }
  | { status: 'requires2fa'; email: string }

export interface AuthPublicConfig {
  demoSandboxEnabled: boolean
}

export interface ApiTeam {
  name: string
  logoName: string
  plan: string
}

export interface ApiNavItem {
  title: string
  url?: string
  iconName?: string
  badge?: string
  items?: {
    title: string
    url: string
    iconName?: string
    badge?: string
  }[]
}

export interface ApiNavGroup {
  title: string
  items: ApiNavItem[]
}

export interface ApiNavigationData {
  user: ApiUser
  teams: ApiTeam[]
  navGroups: ApiNavGroup[]
}

export interface DashboardKpis {
  totalFacturadoMes: number
  facturadoVariacionPorcentaje: number
  baseImponibleMes: number
  ivaMes: number
  pendienteCobro: number
  facturasPendientesCount: number
  clientesActivos: number
  clientesNuevosMes: number
  efectividadCobroPorcentaje: number
  diasPromedioCobro: number
}

export interface DashboardMesEvolucion {
  mes: string
  total: number
  base: number
}

export interface DashboardFacturaReciente {
  id: string
  numero: string
  clienteNombre: string
  servicio: string
  importe: number
  estado: 'cobrada' | 'pendiente' | 'vencida'
  fecha: string
}

export interface DashboardServicioDistribucion {
  nombre: string
  clientesCount: number
  porcentaje: number
}

export interface DashboardVencimientoProximo {
  id: string
  numero: string
  clienteNombre: string
  importe: number
  diasRestantes: number
}

export interface DashboardSummary {
  periodo: string
  kpis: DashboardKpis
  evolucion: DashboardMesEvolucion[]
  ultimasFacturas: DashboardFacturaReciente[]
  servicios: DashboardServicioDistribucion[]
  proximosVencimientos: DashboardVencimientoProximo[]
}

import type { components } from './generated-types'

export type ClienteDto = components['schemas']['ClienteDto']
export type ClientesPagedResult = components['schemas']['ClientesPagedResult']
export type CreateClienteRequest = components['schemas']['CreateClienteRequest']
export type UpdateClienteRequest = components['schemas']['UpdateClienteRequest']
export type ProblemDetails = components['schemas']['ProblemDetails']
export type ValidationProblemDetails = components['schemas']['ValidationProblemDetails']

export interface ClienteFilterQuery {
  searchTerm?: string
  activo?: boolean
  sortBy?: string
  sortDescending?: boolean
  page?: number
  pageSize?: number
}

export class ConcurrencyConflictError extends Error {
  constructor(
    message: string = 'El registro ha sido modificado por otro usuario.',
    public problemDetails?: ProblemDetails
  ) {
    super(message)
    this.name = 'ConcurrencyConflictError'
  }
}
