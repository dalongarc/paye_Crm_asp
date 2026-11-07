import { useQuery } from '@tanstack/react-query'
import {
  LayoutDashboard,
  Users,
  Receipt,
  Settings,
  Command,
  HelpCircle,
  ListTodo,
  UserCog,
  Palette,
  ShieldCheck,
  Package,
  Calendar,
  Wallet,
  FileText,
  type LucideIcon,
} from 'lucide-react'
import { api, type ApiTeam, type ApiNavGroup } from '@/api'
import type { SidebarData, NavGroup, NavItem } from '@/components/layout/types'

const iconMap: Record<string, LucideIcon> = {
  LayoutDashboard,
  Users,
  Receipt,
  Settings,
  Command,
  ListTodo,
  UserCog,
  Palette,
  ShieldCheck,
  Package,
  Calendar,
  Wallet,
  FileText,
}

export function useNavigationQuery() {
  return useQuery({
    queryKey: ['navigation'],
    queryFn: async (): Promise<SidebarData> => {
      const data = await api.navigation.getNavigation()

      const teams = data.teams.map((t: ApiTeam) => ({
        name: t.name,
        logo: iconMap[t.logoName] || Command,
        plan: t.plan,
      }))

      const navGroups: NavGroup[] = data.navGroups.map((group: ApiNavGroup) => {
        const mappedItems: NavItem[] = []

        for (const item of group.items) {
          // Si es la ruta /tasks, mostrar como Calendario y Visitas
          const isCalendar = item.url === '/tasks'
          const title = isCalendar ? 'Calendario y Visitas' : item.title
          const Icon = isCalendar
            ? Calendar
            : item.iconName
              ? iconMap[item.iconName] || HelpCircle
              : undefined

          if (item.items && item.items.length > 0) {
            mappedItems.push({
              title,
              badge: item.badge,
              icon: Icon,
              items: item.items.map(
                (sub: {
                  title: string
                  url: string
                  iconName?: string
                  badge?: string
                }) => ({
                  title: sub.title,
                  url: sub.url,
                  badge: sub.badge,
                  icon: sub.iconName
                    ? iconMap[sub.iconName] || HelpCircle
                    : undefined,
                })
              ),
            } as NavItem)
          } else {
            mappedItems.push({
              title,
              url: item.url || '/',
              badge: item.badge,
              icon: Icon,
            } as NavItem)
          }

          // Justo después de Facturación (/facturas), insertar Gastos y Trimestre (/gastos)
          if (
            item.url === '/facturas' &&
            !group.items.some((i) => i.url === '/gastos')
          ) {
            mappedItems.push({
              title: 'Gastos y Trimestre',
              url: '/gastos',
              icon: Wallet,
            } as NavItem)
          }
        }

        return {
          title: group.title,
          items: mappedItems,
        }
      })

      return {
        user: {
          name: data.user.name,
          email: data.user.email,
          avatar: data.user.avatar,
        },
        teams,
        navGroups,
      }
    },
    staleTime: 1000 * 60 * 5, // 5 min
  })
}
