import { Outlet, useLocation } from '@tanstack/react-router'
import {
  Building2,
  Percent,
  FileSpreadsheet,
  ShieldCheck,
  LockKeyhole,
} from 'lucide-react'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'
import { SidebarNav } from './components/sidebar-nav'

const sidebarNavItems = [
  {
    title: 'Empresa y Logo (WebP)',
    href: '/settings',
    icon: <Building2 size={18} />,
  },
  {
    title: 'Impuestos y Catálogo',
    href: '/settings/account',
    icon: <Percent size={18} />,
  },
  {
    title: 'Plantillas de Factura',
    href: '/settings/appearance',
    icon: <FileSpreadsheet size={18} />,
  },
  {
    title: 'VeriFactu / AEAT',
    href: '/settings/notifications',
    icon: <ShieldCheck size={18} />,
  },
  {
    title: 'Cuenta e Identity',
    href: '/settings/display',
    icon: <LockKeyhole size={18} />,
  },
]

export function Settings() {
  const { pathname } = useLocation()
  const activePane =
    sidebarNavItems.find((i) => i.href === pathname)?.title ??
    'Empresa y Logo (WebP)'

  return (
    <>
      <Header>
        <div className='me-auto flex items-center gap-2 min-w-0'>
          <span className='text-muted-foreground text-sm font-medium'>
            Configuración
          </span>
          <span className='text-muted-foreground text-xs'>/</span>
          <span className='text-foreground text-sm font-semibold tracking-tight truncate'>
            {activePane}
          </span>
        </div>
        <Search />
        <ThemeSwitch />
        <ProfileDropdown />
      </Header>

      <Main>
        <div className='flex flex-1 flex-col gap-6 lg:flex-row lg:gap-8 items-start'>
          <aside className='w-full lg:w-60 shrink-0'>
            <SidebarNav items={sidebarNavItems} />
          </aside>
          <div className='flex-1 w-full min-w-0'>
            <Outlet />
          </div>
        </div>
      </Main>
    </>
  )
}
