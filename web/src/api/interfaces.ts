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
  UserItemDto,
  CreateUserApiRequest,
  UpdateUserApiRequest,
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
  DocumentoAiBloqueGeneradoDto
} from './types'

export interface IUsersApi {
  getUsers(): Promise<UserItemDto[]>
  createUser(data: CreateUserApiRequest): Promise<UserItemDto>
  updateUser(id: string, data: UpdateUserApiRequest): Promise<UserItemDto>
  deleteUser(id: string): Promise<void>
}

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

export interface IDocumentacionApi {
  getDocumentos(query?: DocumentacionFilterQuery): Promise<DocumentacionPagedResult>
  getDocumentoById(id: string): Promise<DocumentoComercialDetailDto>
  createDocumento(data: SaveDocumentoComercialRequest): Promise<DocumentoComercialDetailDto>
  updateDocumento(id: string, data: SaveDocumentoComercialRequest): Promise<DocumentoComercialDetailDto>
  duplicateDocumento(id: string): Promise<DocumentoComercialDetailDto>
  deleteDocumento(id: string): Promise<void>
  getCompanySettings(): Promise<DocumentacionCompanySettingsDto>
  updateCompanySettings(data: UpdateDocumentacionCompanySettingsRequest): Promise<DocumentacionCompanySettingsDto>
  calculateLines(lineas: DocumentoLineaInputDto[]): Promise<CalculatedDocumentoTotals>
  exportDocumentoPdf(id: string, fileName?: string): Promise<void>
  
  // AI Methods
  scanSecrets(text: string): Promise<DocumentoSecretScanResult>
  extractFile(file: File): Promise<DocumentoArchivoExtraidoDto>
  generarBorrador(request: GenerarBorradorAiRequest): Promise<DocumentoAiBorradorResponseDto>
  regenerarBloque(request: RegenerarBloqueAiRequest): Promise<DocumentoAiBloqueGeneradoDto>
}
