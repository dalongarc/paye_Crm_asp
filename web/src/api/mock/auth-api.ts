import type { IAuthApi } from '../interfaces'
import {
  AuthError,
  type ApiUser,
  type AuthPublicConfig,
  type LoginResult,
} from '../types'

export class MockAuthApi implements IAuthApi {
  private currentUser: ApiUser | null = {
    id: 'usr-1',
    name: 'Dario Propietario',
    email: 'admin@paye.local',
    avatar: '',
    role: 'Propietario',
    permissions: ['*'],
  }

  async getCurrentUser(): Promise<ApiUser> {
    await new Promise((resolve) => setTimeout(resolve, 50))
    if (!this.currentUser) {
      throw new AuthError('BAD_CREDENTIALS', 'No autenticado')
    }
    return this.currentUser
  }

  async getPublicConfig(): Promise<AuthPublicConfig> {
    await new Promise((resolve) => setTimeout(resolve, 30))
    return {
      demoSandboxEnabled: true,
    }
  }

  async login(email: string, password: string): Promise<LoginResult> {
    await new Promise((resolve) => setTimeout(resolve, 150))
    const normalizedEmail = email.trim().toLowerCase()

    // 1. Simulación mágica: Cuenta bloqueada (5 intentos, 15 min)
    if (normalizedEmail === 'bloqueado@paye.local') {
      throw new AuthError(
        'LOCKED_OUT',
        'Cuenta bloqueada temporalmente tras 5 intentos fallidos. Inténtalo de nuevo en 15 minutos o contacta al administrador.',
        15
      )
    }

    // 2. Simulación mágica: Demasiadas peticiones (429 Rate Limit)
    if (normalizedEmail === 'limitado@paye.local') {
      throw new AuthError(
        'RATE_LIMITED',
        'Has superado el límite de intentos permitidos (429). Por favor, espera unos minutos antes de volver a intentarlo.'
      )
    }

    // 3. Simulación mágica: Cuenta desactivada
    if (normalizedEmail === 'desactivado@paye.local') {
      throw new AuthError(
        'INACTIVE',
        'Tu cuenta de usuario ha sido desactivada por un administrador del sistema.'
      )
    }

    // 4. Simulación mágica: Error de red / Servidor
    if (normalizedEmail === 'error@paye.local') {
      throw new AuthError(
        'NETWORK_ERROR',
        'No se pudo conectar con el servidor de Payé Base. Comprueba tu conexión a internet o el estado del servicio.'
      )
    }

    // 5. Simulación mágica: Requiere código 2FA (TOTP)
    if (normalizedEmail === 'dosfa@paye.local') {
      return {
        status: 'requires2fa',
        email: normalizedEmail,
      }
    }

    // 6. Credenciales incorrectas
    if (password !== 'paye123') {
      throw new AuthError(
        'BAD_CREDENTIALS',
        'El correo o la contraseña son incorrectos. Verifica tus datos de acceso.'
      )
    }

    // 7. Login exitoso
    const user: ApiUser = {
      id: 'usr-1',
      name: 'Dario Propietario',
      email: normalizedEmail,
      avatar: '',
      role: 'Propietario',
      permissions: ['*'],
    }
    this.currentUser = user
    return {
      status: 'success',
      user,
    }
  }

  async verify2fa(email: string, code: string): Promise<ApiUser> {
    await new Promise((resolve) => setTimeout(resolve, 150))
    if (code === '000000') {
      throw new AuthError(
        'INVALID_2FA',
        'El código de autenticación de 6 dígitos es inválido o ha expirado.'
      )
    }

    const user: ApiUser = {
      id: 'usr-2fa',
      name: 'Usuario Verificado 2FA',
      email,
      avatar: '',
      role: 'Propietario',
      permissions: ['*'],
    }
    this.currentUser = user
    return user
  }

  async loginDemoSandbox(): Promise<ApiUser> {
    await new Promise((resolve) => setTimeout(resolve, 150))
    const user: ApiUser = {
      id: 'demo-sandbox-guid',
      name: 'Invitado Sandbox',
      email: 'demo-sandbox@paye.local',
      avatar: '',
      role: 'DemoSandbox',
      permissions: ['lectura'],
    }
    this.currentUser = user
    return user
  }

  async forgotPassword(_email: string): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 100))
  }

  async logout(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 50))
    this.currentUser = null
  }
}
