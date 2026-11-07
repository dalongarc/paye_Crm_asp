import {
  Bar,
  BarChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { DashboardMesEvolucion } from '@/api'
import { formatCurrency } from '@/lib/utils'

interface OverviewProps {
  data: DashboardMesEvolucion[]
}

interface CustomTooltipProps {
  active?: boolean
  payload?: Array<{ value: number }>
  label?: string
}

function CustomTooltip({ active, payload, label }: CustomTooltipProps) {
  if (active && payload && payload.length) {
    const total = payload[0]?.value ?? 0
    return (
      <div className='rounded-lg border bg-popover px-3 py-2 text-popover-foreground shadow-md'>
        <p className='text-xs font-medium text-muted-foreground'>{label}</p>
        <p className='text-sm font-bold text-foreground'>{formatCurrency(total)}</p>
      </div>
    )
  }
  return null
}

export function Overview({ data }: OverviewProps) {
  if (!data || data.length === 0) {
    return (
      <div className='flex h-[320px] items-center justify-center text-sm text-muted-foreground'>
        Sin datos de facturación para el periodo seleccionado
      </div>
    )
  }

  return (
    <ResponsiveContainer width='100%' height={320}>
      <BarChart data={data} margin={{ top: 10, right: 10, left: -10, bottom: 0 }}>
        <XAxis
          dataKey='mes'
          stroke='currentColor'
          className='text-muted-foreground'
          fontSize={12}
          tickLine={false}
          axisLine={false}
        />
        <YAxis
          stroke='currentColor'
          className='text-muted-foreground'
          fontSize={12}
          tickLine={false}
          axisLine={false}
          width={65}
          tickFormatter={(value) => `${value.toLocaleString('es-ES')} €`}
        />
        <Tooltip
          content={<CustomTooltip />}
          cursor={{ fill: 'rgba(120, 120, 120, 0.08)' }}
        />
        <Bar
          dataKey='total'
          fill='currentColor'
          radius={[6, 6, 0, 0]}
          className='fill-primary'
        />
      </BarChart>
    </ResponsiveContainer>
  )
}
