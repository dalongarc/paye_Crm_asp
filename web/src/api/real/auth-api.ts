import type { IAuthApi } from '../interfaces'
import {
  AuthError,
  type ApiUser,
  type AuthPublicConfig,
  type LoginResult,
} from '../types'
import { apiFetch, clearAntiforgeryToken } from '../client'

interface BackendLoginResponse {
  requiresTwoFactor: boolean
  email?: string | null
  user?: ApiUser | null
}

export class RealAuthApi implements IAuthApi {
  async getCurrentUser(): Promise<ApiUser> {
    try {
      return await apiFetch<ApiUser>('/api/me')
    } catch (err: unknown) {
      const status = (err as { status?: number }).status
      if (status === 401 || status === 403) {
        throw new AuthError('BAD_CREDENTIALS', 'No autenticado')
      }
      if (err instanceof AuthError) throw err
      throw new AuthError('NETWORK_ERROR', 'Error al conectar con el servidor')
    }
  }

  async getPublicConfig(): Promise<AuthPublicConfig> {
    try {
      return await apiFetch<AuthPublicConfig>('/api/auth/config')
    } catch {
      return { demoSandboxEnabled: false }
    }
  }

  async login(email: string, password: string): Promise<LoginResult> {
    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ email, password, rememberMe: false }),
      })

      if (res.status === 401) {
        throw new AuthError(
          'BAD_CREDENTIALS',
          'El correo o la contraseña son incorrectos.'
        )
      }
      if (res.status === 423) {
        throw new AuthError(
          'LOCKED_OUT',
          'Cuenta bloqueada temporalmente tras 5 intentos fallidos. Inténtalo de nuevo en 15 minutos.',
          15
        )
      }
      if (res.status === 429) {
        throw new AuthError(
          'RATE_LIMITED',
          'Has superado el límite de peticiones permitidas (429). Espera unos minutos.'
        )
      }
      if (res.status === 403) {
        throw new AuthError(
          'INACTIVE',
          'Esta cuenta ha sido desactivada por el administrador.'
        )
      }
      if (!res.ok) {
        throw new AuthError(
          'NETWORK_ERROR',
          'Error del servidor al procesar la solicitud de inicio de sesión.'
        )
      }

      const data = (await res.json()) as BackendLoginResponse
      if (data.requiresTwoFactor) {
        return { status: 'requires2fa', email: data.email || email }
      }

      if (data.user) {
        return { status: 'success', user: data.user }
      }

      // Si por alguna razón no viene en el cuerpo, obtener de /api/me
      const meUser = await this.getCurrentUser()
      return { status: 'success', user: meUser }
    } catch (err) {
      if (err instanceof AuthError) throw err
      throw new AuthError(
        'NETWORK_ERROR',
        'No se pudo conectar con el servidor de Payé Base. Comprueba tu conexión.'
      )
    }
  }

  async verify2fa(email: string, code: string): Promise<ApiUser> {
    try {
      const res = await fetch('/api/auth/2fa', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ email, code, rememberMe: false }),
      })

      if (res.status === 423) {
        throw new AuthError(
          'LOCKED_OUT',
          'Cuenta bloqueada temporalmente tras intentos fallidos. Inténtalo de nuevo en 15 minutos.',
          15
        )
      }
      if (!res.ok) {
        throw new AuthError(
          'INVALID_2FA',
          'Código 2FA incorrecto o expirado.'
        )
      }

      return await res.json()
    } catch (err) {
      if (err instanceof AuthError) throw err
      throw new AuthError('NETWORK_ERROR', 'Error al verificar el código 2FA.')
    }
  }

  async loginDemoSandbox(): Promise<ApiUser> {
    try {
      const res = await fetch('/api/auth/demo-sandbox', {
        method: 'POST',
        headers: {
          Accept: 'application/json',
        },
        credentials: 'include',
      })

      if (!res.ok) {
        throw new AuthError(
          'BAD_CREDENTIALS',
          'El modo Demo Sandbox no está disponible en este entorno.'
        )
      }

      return await res.json()
    } catch (err) {
      if (err instanceof AuthError) throw err
      throw new AuthError('NETWORK_ERROR', 'Error al acceder al modo demo.')
    }
  }

  async forgotPassword(email: string): Promise<void> {
    try {
      await fetch('/Account/ForgotPasswordApi', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ email }),
      })
    } catch {
      // Fail-safe silencioso
    }
  }

  async logout(): Promise<void> {
    try {
      await apiFetch<{ message: string }>('/api/auth/logout', {
        method: 'POST',
      })
    } finally {
      clearAntiforgeryToken()
    }
  }
}
