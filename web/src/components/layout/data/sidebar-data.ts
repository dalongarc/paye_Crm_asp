import {
  LayoutDashboard,
  Calendar,
  Settings,
  UserCog,
  Users,
  Receipt,
  Command,
  Wallet,
  FileText,
  FileSignature,
} from 'lucide-react'
import { type SidebarData } from '../types'

export const sidebarData: SidebarData = {
  user: {
    name: 'Dario Propietario',
    email: 'propietario@payedigital.local',
    avatar: '',
  },
  teams: [
    {
      name: 'Payé Base',
      logo: Command,
      plan: 'Enterprise',
    },
  ],
  navGroups: [
    {
      title: 'General',
      items: [
        {
          title: 'Dashboard',
          url: '/',
          icon: LayoutDashboard,
        },
        {
          title: 'Clientes',
          url: '/clientes',
          icon: Users,
        },
        {
          title: 'Facturación',
          url: '/facturas',
          icon: Receipt,
        },
        {
          title: 'Gastos y Trimestre',
          url: '/gastos',
          icon: Wallet,
        },
        {
          title: 'Documentación',
          url: '/documentacion',
          icon: FileSignature,
        },
        {
          title: 'Notas',
          url: '/notas',
          icon: FileText,
        },
        {
          title: 'Calendario y Visitas',
          url: '/tasks',
          icon: Calendar,
        },
      ],
    },
    {
      title: 'Administración',
      items: [
        {
          title: 'Usuarios y roles',
          url: '/users',
          icon: UserCog,
        },
        {
          title: 'Configuración',
          url: '/settings',
          icon: Settings,
        },
      ],
    },
  ],
}
