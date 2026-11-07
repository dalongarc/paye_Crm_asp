import type { INavigationApi } from '../interfaces'
import type { ApiNavigationData } from '../types'

export class MockNavigationApi implements INavigationApi {
  async getNavigation(): Promise<ApiNavigationData> {
    // Simula latencia mínima de red
    await new Promise((resolve) => setTimeout(resolve, 50))

    return {
      user: {
        id: 'usr-1',
        name: 'Dario Propietario',
        email: 'admin@paye.local',
        avatar: '',
        role: 'Propietario',
        permissions: ['*'],
      },
      teams: [
        {
          name: 'Payé Base',
          logoName: 'Command',
          plan: 'Single-Tenant Pro',
        },
      ],
      navGroups: [
        {
          title: 'Gestión',
          items: [
            {
              title: 'Dashboard',
              url: '/',
              iconName: 'LayoutDashboard',
            },
            {
              title: 'Clientes',
              url: '/clientes',
              iconName: 'Users',
              badge: '12',
            },
            {
              title: 'Facturación',
              url: '/facturas',
              iconName: 'Receipt',
            },
          ],
        },
        {
          title: 'Configuración',
          items: [
            {
              title: 'Ajustes',
              url: '/settings',
              iconName: 'Settings',
            },
          ],
        },
      ],
    }
  }
}
