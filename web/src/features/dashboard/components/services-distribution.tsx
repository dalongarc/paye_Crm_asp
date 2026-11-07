import type {
  DashboardServicioDistribucion,
  DashboardVencimientoProximo,
} from '@/api'
import { Badge } from '@/components/ui/badge'
import { formatCurrency } from '@/lib/utils'
import { AlertCircle, Layers } from 'lucide-react'

interface ServicesDistributionProps {
  servicios: DashboardServicioDistribucion[]
  vencimientos: DashboardVencimientoProximo[]
}

export function ServicesDistribution({
  servicios,
  vencimientos,
}: ServicesDistributionProps) {
  return (
    <div className='space-y-6'>
      <div className='space-y-4'>
        <div className='flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground'>
          <Layers className='h-3.5 w-3.5' />
          <span>Servicios Activos por Volumen</span>
        </div>

        <div className='space-y-3.5'>
          {servicios.map((srv) => (
            <div key={srv.nombre} className='space-y-1.5'>
              <div className='flex items-center justify-between text-xs'>
                <span className='font-medium text-foreground'>{srv.nombre}</span>
                <span className='text-muted-foreground'>
                  {srv.clientesCount} clientes ({srv.porcentaje}%)
                </span>
              </div>
              <svg className='h-2 w-full overflow-hidden rounded-full bg-muted'>
                <rect
                  height='100%'
                  width={`${srv.porcentaje}%`}
                  rx='4'
                  className='fill-primary transition-all'
                />
              </svg>
            </div>
          ))}
        </div>
      </div>

      {vencimientos && vencimientos.length > 0 && (
        <div className='space-y-3 pt-2 border-t border-border/60'>
          <div className='flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-600 dark:text-amber-400'>
            <AlertCircle className='h-3.5 w-3.5' />
            <span>Próximos Vencimientos (7 días)</span>
          </div>

          <div className='space-y-2'>
            {vencimientos.map((item) => (
              <div
                key={item.id}
                className='flex items-center justify-between rounded-md border border-amber-500/20 bg-amber-500/5 p-2.5 text-xs'
              >
                <div className='min-w-0 pr-2'>
                  <p className='truncate font-medium text-foreground'>
                    {item.clienteNombre}
                  </p>
                  <p className='text-[11px] text-muted-foreground font-mono'>
                    {item.numero}
                  </p>
                </div>
                <div className='text-right shrink-0'>
                  <p className='font-semibold text-foreground'>
                    {formatCurrency(item.importe)}
                  </p>
                  <Badge
                    variant='outline'
                    className='mt-0.5 border-amber-500/30 bg-amber-500/10 text-[10px] text-amber-700 dark:text-amber-400'
                  >
                    Vence en {item.diasRestantes} días
                  </Badge>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
