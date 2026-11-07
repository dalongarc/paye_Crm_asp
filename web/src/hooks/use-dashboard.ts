import { useQuery } from '@tanstack/react-query'
import { api, type DashboardSummary } from '@/api'

export function useDashboardSummaryQuery() {
  return useQuery<DashboardSummary>({
    queryKey: ['dashboard', 'summary'],
    queryFn: () => api.dashboard.getSummary(),
    staleTime: 1000 * 60 * 2, // 2 minutos
  })
}
