import type { INavigationApi } from '../interfaces'
import type { ApiNavigationData } from '../types'

export class RealNavigationApi implements INavigationApi {
  async getNavigation(): Promise<ApiNavigationData> {
    const res = await fetch('/api/me/navigation', {
      headers: { Accept: 'application/json' },
      credentials: 'include',
    })
    if (!res.ok) throw new Error('Error al cargar navegación')
    return res.json()
  }
}
