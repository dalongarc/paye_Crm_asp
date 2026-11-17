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

export type ClienteDepartamentoDto = components['schemas']['ClienteDepartamentoDto']
export type ClienteDto = components['schemas']['ClienteDto']
export type ClientesPagedResult = components['schemas']['ClientesPagedResult']
export type CreateClienteRequest = components['schemas']['CreateClienteRequest']
export type UpdateClienteRequest = components['schemas']['UpdateClienteRequest']
export type ProblemDetails = components['schemas']['ProblemDetails']
export type ValidationProblemDetails = components['schemas']['ValidationProblemDetails']

export interface ClienteFilterQuery {
  searchTerm?: string
  activo?: boolean
  esBorrador?: boolean
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

export interface UserItemDto {
  id: string
  firstName: string
  lastName: string
  username: string
  email: string
  phoneNumber: string
  status: 'active' | 'inactive' | 'invited' | 'suspended'
  role: 'admin'
  createdAt: string
  updatedAt: string
}

export interface CreateUserApiRequest {
  firstName: string
  lastName: string
  username: string
  email: string
  phoneNumber?: string
  password: string
  role?: string
}

export interface UpdateUserApiRequest {
  firstName: string
  lastName: string
  username: string
  email: string
  phoneNumber?: string
  password?: string
  status?: string
  role?: string
}

// ============================================================================
// MÓDULO DOCUMENTACIÓN (CONTRATOS Y PRESUPUESTOS)
// ============================================================================

export type TipoDocumentoComercial = 1 | 2 // 1 = Contrato, 2 = Presupuesto

export interface DocumentoBloqueDto {
  id: string
  templateBlockId?: string | null
  tipoBloque: string
  numeroPastilla?: string | null
  titulo: string
  contenidoHtml: string
  visible: boolean
  orden: number
  esPersonalizado: boolean
}

export interface DocumentoLineaInputDto {
  descripcion: string
  cantidad: number
  precioUnitario: number
  descuentoPorcentaje: number
  ivaPorcentaje: number // 21, 10, 4, 0
}

export interface DocumentoLineaDto {
  id: string
  orden: number
  descripcion: string
  cantidad: number
  precioUnitario: number
  descuentoPorcentaje: number
  ivaPorcentaje: number
  importeBruto: number
  importeDescuento: number
  baseImponible: number
  cuotaIva: number
  totalLinea: number
}

export interface DesgloseIvaItemDto {
  ivaPorcentaje: number
  etiqueta: string
  baseImponible: number
  cuotaIva: number
  totalConIva: number
}

export interface DocumentoComercialListItemDto {
  id: string
  tipo: TipoDocumentoComercial
  numeroDocumento: string
  titulo: string
  subtitulo: string
  clienteId?: string | null
  clienteNombre: string
  clienteCifNif: string
  fechaEmision: string
  fechaValidez?: string | null
  baseImponible: number
  totalIva: number
  totalGeneral: number
  createdAtUtc: string
  updatedAtUtc?: string | null
  rowVersion: number
}

export interface DocumentoComercialDetailDto {
  id: string
  tipo: TipoDocumentoComercial
  fiscalYear: number
  sequentialNumber: number
  numeroDocumento: string
  titulo: string
  subtitulo: string
  categoria: string
  clienteId?: string | null
  clienteNombre: string
  clienteCifNif: string
  clienteDireccion: string
  clienteEmail: string
  clienteTelefono: string
  fechaEmision: string
  fechaValidez?: string | null
  objetoContrato?: string | null
  duracionContrato?: string | null
  precioTextoContrato?: string | null
  formaPagoContrato?: string | null
  lugarFirma?: string | null
  bloques: DocumentoBloqueDto[]
  lineas: DocumentoLineaDto[]
  desgloseIva: DesgloseIvaItemDto[]
  subtotalBruto: number
  totalDescuento: number
  baseImponible: number
  totalIva: number
  totalGeneral: number
  createdAtUtc: string
  updatedAtUtc?: string | null
  rowVersion: number
  textoOrigenDatos?: string | null
  nombreArchivoOrigen?: string | null
}

export interface SaveDocumentoComercialRequest {
  tipo: TipoDocumentoComercial
  clienteId?: string | null
  titulo?: string | null
  subtitulo?: string | null
  categoria?: string | null
  clienteNombreOverride?: string | null
  clienteCifNifOverride?: string | null
  clienteDireccionOverride?: string | null
  clienteEmailOverride?: string | null
  clienteTelefonoOverride?: string | null
  fechaEmision?: string | null
  fechaValidez?: string | null
  objetoContrato?: string | null
  duracionContrato?: string | null
  precioTextoContrato?: string | null
  formaPagoContrato?: string | null
  lugarFirma?: string | null
  bloques?: DocumentoBloqueDto[]
  lineas?: DocumentoLineaInputDto[]
  textoOrigenDatos?: string | null
  nombreArchivoOrigen?: string | null
  rowVersion?: number
}

export interface DocumentacionFilterQuery {
  tipo?: TipoDocumentoComercial
  clienteId?: string
  search?: string
  page?: number
  pageSize?: number
}

export interface DocumentacionPagedResult {
  items: DocumentoComercialListItemDto[]
  totalCount: number
  page: number
  pageSize: number
}

export interface DocumentacionCompanySettingsDto {
  id: string
  nombreEmpresa: string
  cifNif: string
  direccion: string
  telefono: string
  email: string
  web: string
  iban: string
  logoUrl: string
  textoLegalTitulo: string
  textoLegalContenido: string
  rowVersion: number
  modeloIa?: string
}

export interface UpdateDocumentacionCompanySettingsRequest {
  nombreEmpresa: string
  cifNif: string
  direccion: string
  telefono: string
  email: string
  web: string
  iban: string
  logoUrl: string
  textoLegalTitulo: string
  textoLegalContenido: string
  modeloIa?: string
  geminiApiKey?: string
  rowVersion: number
}

export interface CalculatedDocumentoTotals {
  subtotalBruto: number
  totalDescuento: number
  baseImponible: number
  totalIva: number
  totalGeneral: number
  lineas: Omit<DocumentoLineaDto, 'id'>[]
  desgloseIva: DesgloseIvaItemDto[]
}

// -- AI Generation Types --
export interface DocumentoSecretFindingDto {
  category: string
  description: string
  maskedPreview: string
}

export interface DocumentoSecretScanResult {
  hasSecrets: boolean
  findings: DocumentoSecretFindingDto[]
  redactedText: string
}

export interface DocumentoArchivoExtraidoDto {
  fileName: string
  extractedText: string
  characterCount: number
  exceededMaxCharacters: boolean
  maxAllowedCharacters: number
}

export interface GenerarBorradorAiRequest {
  tipo: TipoDocumentoComercial
  documentoId?: string | null
  textoOrigen: string
  tratoCliente?: string | null
  instruccionesExtra?: string | null
  modeloIa?: string | null
  eliminarSecretosDetectados?: boolean
}

export interface DocumentoAiBloqueGeneradoDto {
  tipo: 'texto' | 'lista' | 'fases'
  titulo: string
  contenido: string
  pendiente: string[]
}

export interface DocumentoAiLineaSugeridaDto {
  descripcion: string
  cantidad: number
}

export interface DocumentoAiBorradorResponseDto {
  titulo: string
  subtitulo: string
  bloques: DocumentoAiBloqueGeneradoDto[]
  lineasSugeridas: DocumentoAiLineaSugeridaDto[]
  modeloUtilizado: string
  caracteresOrigenProcesados: number
  reintentoJsonAplicado?: boolean
}

export interface RegenerarBloqueAiRequest {
  tipo: TipoDocumentoComercial
  documentoId?: string | null
  bloqueTitulo: string
  bloqueContenidoActual: string
  modo: 'regenerar' | 'simplificar'
  instruccionesExtra?: string | null
  textoOrigen?: string | null
  tratoCliente?: string | null
  eliminarSecretosDetectados?: boolean
}
