import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import type { DashboardFacturaReciente } from '@/api'
import { formatCurrency, getDisplayNameInitials } from '@/lib/utils'

interface RecentInvoicesProps {
  facturas: DashboardFacturaReciente[]
}

export function RecentInvoices({ facturas }: RecentInvoicesProps) {
  if (!facturas || facturas.length === 0) {
    return (
      <div className='flex h-[240px] items-center justify-center text-sm text-muted-foreground'>
        No hay facturas registradas recientemente.
      </div>
    )
  }

  return (
    <div className='divide-y divide-border/60'>
      {facturas.map((factura) => (
        <div
          key={factura.id}
          className='flex flex-col gap-2 py-3.5 first:pt-0 last:pb-0 sm:flex-row sm:items-center sm:justify-between sm:gap-4'
        >
          <div className='flex items-center gap-3 min-w-0'>
            <Avatar className='h-9 w-9 shrink-0 border border-border/60'>
              <AvatarFallback className='text-xs font-semibold bg-muted text-foreground'>
                {getDisplayNameInitials(factura.clienteNombre)}
              </AvatarFallback>
            </Avatar>
            <div className='min-w-0 space-y-0.5'>
              <div className='flex items-center gap-2'>
                <p className='truncate text-sm font-medium leading-none text-foreground'>
                  {factura.clienteNombre}
                </p>
                <span className='text-xs text-muted-foreground font-mono'>
                  {factura.numero}
                </span>
              </div>
              <p className='truncate text-xs text-muted-foreground'>
                {factura.servicio}
              </p>
            </div>
          </div>

          <div className='flex items-center justify-between gap-3 sm:justify-end shrink-0'>
            <div className='text-right'>
              <p className='text-sm font-semibold tracking-tight text-foreground'>
                {formatCurrency(factura.importe)}
              </p>
              <p className='text-[11px] text-muted-foreground'>{factura.fecha}</p>
            </div>

            <Badge
              variant='outline'
              className={
                factura.estado === 'cobrada'
                  ? 'border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 capitalize text-[11px]'
                  : factura.estado === 'pendiente'
                    ? 'border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400 capitalize text-[11px]'
                    : 'border-rose-500/30 bg-rose-500/10 text-rose-700 dark:text-rose-400 capitalize text-[11px]'
              }
            >
              {factura.estado}
            </Badge>
          </div>
        </div>
      ))}
    </div>
  )
}
