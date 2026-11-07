import type {
  ApiNavigationData,
  ApiUser,
  LoginResult,
  AuthPublicConfig,
  DashboardSummary,
  ClienteDto,
  ClientesPagedResult,
  ClienteFilterQuery,
  CreateClienteRequest,
  UpdateClienteRequest,
} from './types'

export interface INavigationApi {
  getNavigation(): Promise<ApiNavigationData>
}

export interface IDashboardApi {
  getSummary(): Promise<DashboardSummary>
}

export interface IClientesApi {
  getClientes(query?: ClienteFilterQuery): Promise<ClientesPagedResult>
  getClienteById(id: string): Promise<ClienteDto>
  createCliente(data: CreateClienteRequest): Promise<ClienteDto>
  updateCliente(id: string, data: UpdateClienteRequest): Promise<ClienteDto>
  deleteCliente(id: string): Promise<void>
}

export interface IAuthApi {
  getCurrentUser(): Promise<ApiUser>
  getPublicConfig(): Promise<AuthPublicConfig>
  login(email: string, password: string): Promise<LoginResult>
  verify2fa(email: string, code: string): Promise<ApiUser>
  loginDemoSandbox(): Promise<ApiUser>
  forgotPassword(email: string): Promise<void>
  logout(): Promise<void>
}
