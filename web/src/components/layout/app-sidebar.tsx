import { useLayout } from '@/context/layout-provider'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarRail,
} from '@/components/ui/sidebar'
// import { AppTitle } from './app-title'
import { useNavigationQuery } from '@/hooks/use-navigation'
import { sidebarData as fallbackData } from './data/sidebar-data'
import { NavGroup } from './nav-group'
import { NavUser } from './nav-user'
import { TeamSwitcher } from './team-switcher'

export function AppSidebar() {
  const { collapsible, variant } = useLayout()
  const { data: navData } = useNavigationQuery()
  const activeData = navData || fallbackData

  return (
    <Sidebar collapsible={collapsible} variant={variant}>
      <SidebarHeader>
        <TeamSwitcher teams={activeData.teams} />
      </SidebarHeader>
      <SidebarContent>
        {activeData.navGroups.map((props) => (
          <NavGroup key={props.title} {...props} />
        ))}
      </SidebarContent>
      <SidebarFooter>
        <NavUser user={activeData.user} />
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  )
}
