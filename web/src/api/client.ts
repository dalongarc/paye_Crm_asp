import { ConcurrencyConflictError, type ProblemDetails } from './types'

let cachedAntiforgeryToken: string | null = null
let antiforgeryPromise: Promise<string> | null = null

export async function getAntiforgeryToken(): Promise<string> {
  if (cachedAntiforgeryToken) {
    return cachedAntiforgeryToken
  }

  if (antiforgeryPromise) {
    return antiforgeryPromise
  }

  antiforgeryPromise = (async () => {
    try {
      const res = await fetch('/api/antiforgery', {
        headers: { Accept: 'application/json' },
        credentials: 'include',
      })

      if (!res.ok) {
        throw new Error('No se pudo obtener el token antiforgery del servidor')
      }

      const data = (await res.json()) as { token: string }
      cachedAntiforgeryToken = data.token
      return data.token
    } finally {
      antiforgeryPromise = null
    }
  })()

  return antiforgeryPromise
}

export function clearAntiforgeryToken(): void {
  cachedAntiforgeryToken = null
}

export interface ApiFetchOptions extends RequestInit {
  skipAntiforgery?: boolean
}

export async function apiFetch<T>(
  url: string,
  options: ApiFetchOptions = {}
): Promise<T> {
  const method = (options.method || 'GET').toUpperCase()
  const isMutating = ['POST', 'PUT', 'DELETE', 'PATCH'].includes(method)

  const headers = new Headers(options.headers || {})
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json')
  }

  if (isMutating && !options.skipAntiforgery && !headers.has('RequestVerificationToken')) {
    try {
      const token = await getAntiforgeryToken()
      if (token) {
        headers.set('RequestVerificationToken', token)
      }
    } catch {
      // Continuar y dejar que el servidor valide o responda
    }
  }

  const response = await fetch(url, {
    ...options,
    headers,
    credentials: 'include',
  })

  if (response.status === 409) {
    let problemDetails: ProblemDetails | undefined
    try {
      problemDetails = (await response.json()) as ProblemDetails
    } catch {
      // Ignorar fallo de parseo
    }
    throw new ConcurrencyConflictError(
      problemDetails?.detail || problemDetails?.title || 'Conflicto de concurrencia detectado.',
      problemDetails
    )
  }

  if (!response.ok) {
    let errorMessage = `Error HTTP ${response.status}: ${response.statusText}`
    try {
      const errorJson = (await response.json()) as ProblemDetails
      if (errorJson.detail || errorJson.title) {
        errorMessage = errorJson.detail || errorJson.title || errorMessage
      }
    } catch {
      // Sin cuerpo JSON
    }

    const error = new Error(errorMessage)
    ;(error as unknown as { status: number }).status = response.status
    throw error
  }

  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as unknown as T
  }

  return response.json() as Promise<T>
}
