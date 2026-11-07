import { Link } from '@tanstack/react-router'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'
import { useDashboardSummaryQuery } from '@/hooks/use-dashboard'
import { formatCurrency } from '@/lib/utils'
import { Overview } from './components/overview'
import { RecentInvoices } from './components/recent-invoices'
import { ServicesDistribution } from './components/services-distribution'
import {
  Euro,
  Clock,
  Users,
  CheckCircle2,
  TrendingUp,
  Receipt,
  ArrowUpRight,
  AlertCircle,
  Settings,
  Shield,
} from 'lucide-react'

export function Dashboard() {
  const { data, isLoading, isError, refetch } = useDashboardSummaryQuery()

  return (
    <>
      {/* ===== Header Superior ===== */}
      <Header>
        <div className='flex items-center gap-2 me-auto min-w-0'>
          <span className='font-semibold text-sm tracking-tight text-muted-foreground truncate'>
            Payé Base
          </span>
          <span className='text-muted-foreground text-xs'>/</span>
          <span className='text-foreground text-sm font-semibold'>
            Dashboard
          </span>
          {data && (
            <Badge variant='secondary' className='text-[11px] font-normal ms-1'>
              {data.periodo}
            </Badge>
          )}
        </div>
        <Search />
        <ThemeSwitch />
        <ProfileDropdown />
      </Header>

      {/* ===== Contenido Principal ===== */}
      <Main>

        {/* Estado de Error */}
        {isError && (
          <Alert variant='destructive' className='mb-6'>
            <AlertCircle className='h-4 w-4' />
            <AlertTitle>Error al cargar el panel</AlertTitle>
            <AlertDescription className='flex items-center justify-between gap-4 mt-1'>
              <span>
                No se pudieron obtener las métricas del servidor. Comprueba tu
                conexión.
              </span>
              <Button
                variant='outline'
                size='sm'
                onClick={() => refetch()}
                className='shrink-0'
              >
                Reintentar
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {/* Estado de Carga (Skeletons) */}
        {isLoading && (
          <div className='space-y-6'>
            <div className='grid gap-4 sm:grid-cols-2 lg:grid-cols-4'>
              {Array.from({ length: 4 }).map((_, i) => (
                <Skeleton key={i} className='h-[118px] w-full rounded-xl' />
              ))}
            </div>
            <div className='grid grid-cols-1 gap-4 lg:grid-cols-7'>
              <Skeleton className='col-span-1 lg:col-span-4 h-[380px] rounded-xl' />
              <Skeleton className='col-span-1 lg:col-span-3 h-[380px] rounded-xl' />
            </div>
          </div>
        )}

        {/* Contenido con Datos */}
        {data && (
          <div className='space-y-6'>
            {/* Grid de 4 KPIs Principales */}
            <div className='grid gap-4 sm:grid-cols-2 lg:grid-cols-4'>
              {/* Facturación del Mes */}
              <Card>
                <CardHeader className='flex flex-row items-center justify-between pb-2'>
                  <CardTitle className='text-xs font-semibold uppercase tracking-wider text-muted-foreground'>
                    Facturado (Mes)
                  </CardTitle>
                  <div className='flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-primary'>
                    <Euro className='h-4 w-4' />
                  </div>
                </CardHeader>
                <CardContent className='space-y-1.5'>
                  <div className='text-2xl font-bold tracking-tight text-foreground'>
                    {formatCurrency(data.kpis.totalFacturadoMes)}
                  </div>
                  <div className='flex items-center gap-1.5 text-xs text-emerald-600 dark:text-emerald-400 font-medium'>
                    <TrendingUp className='h-3.5 w-3.5 shrink-0' />
                    <span>+{data.kpis.facturadoVariacionPorcentaje}% vs. mes anterior</span>
                  </div>
                  <p className='text-[11px] text-muted-foreground pt-0.5 border-t border-border/50'>
                    Base: {formatCurrency(data.kpis.baseImponibleMes)} · IVA: {formatCurrency(data.kpis.ivaMes)}
                  </p>
                </CardContent>
              </Card>

              {/* Pendiente de Cobro */}
              <Card>
                <CardHeader className='flex flex-row items-center justify-between pb-2'>
                  <CardTitle className='text-xs font-semibold uppercase tracking-wider text-muted-foreground'>
                    Pendiente de Cobro
                  </CardTitle>
                  <div className='flex h-8 w-8 items-center justify-center rounded-lg bg-amber-500/10 text-amber-600 dark:text-amber-400'>
                    <Clock className='h-4 w-4' />
                  </div>
                </CardHeader>
                <CardContent className='space-y-1.5'>
                  <div className='text-2xl font-bold tracking-tight text-foreground'>
                    {formatCurrency(data.kpis.pendienteCobro)}
                  </div>
                  <p className='text-xs text-amber-600 dark:text-amber-400 font-medium'>
                    {data.kpis.facturasPendientesCount} facturas pendientes
                  </p>
                  <p className='text-[11px] text-muted-foreground pt-0.5 border-t border-border/50'>
                    2 vencen en los próximos 7 días
                  </p>
                </CardContent>
              </Card>

              {/* Clientes Activos */}
              <Card>
                <CardHeader className='flex flex-row items-center justify-between pb-2'>
                  <CardTitle className='text-xs font-semibold uppercase tracking-wider text-muted-foreground'>
                    Clientes Activos
                  </CardTitle>
                  <div className='flex h-8 w-8 items-center justify-center rounded-lg bg-blue-500/10 text-blue-600 dark:text-blue-400'>
                    <Users className='h-4 w-4' />
                  </div>
                </CardHeader>
                <CardContent className='space-y-1.5'>
                  <div className='text-2xl font-bold tracking-tight text-foreground'>
                    {data.kpis.clientesActivos}
                  </div>
                  <p className='text-xs text-blue-600 dark:text-blue-400 font-medium'>
                    +{data.kpis.clientesNuevosMes} nuevos este mes
                  </p>
                  <p className='text-[11px] text-muted-foreground pt-0.5 border-t border-border/50'>
                    Servicios de desarrollo, WP y VPS
                  </p>
                </CardContent>
              </Card>

              {/* Efectividad de Cobro */}
              <Card>
                <CardHeader className='flex flex-row items-center justify-between pb-2'>
                  <CardTitle className='text-xs font-semibold uppercase tracking-wider text-muted-foreground'>
                    Efectividad de Cobro
                  </CardTitle>
                  <div className='flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'>
                    <CheckCircle2 className='h-4 w-4' />
                  </div>
                </CardHeader>
                <CardContent className='space-y-1.5'>
                  <div className='text-2xl font-bold tracking-tight text-foreground'>
                    {data.kpis.efectividadCobroPorcentaje}%
                  </div>
                  <p className='text-xs text-muted-foreground font-medium'>
                    Promedio: {data.kpis.diasPromedioCobro} días tras emisión
                  </p>
                  <p className='text-[11px] text-muted-foreground pt-0.5 border-t border-border/50'>
                    Cumplimiento puntual en el trimestre
                  </p>
                </CardContent>
              </Card>
            </div>

            {/* Fila Principal: Gráfico de Evolución + Facturas Recientes */}
            <div className='grid grid-cols-1 gap-4 lg:grid-cols-7'>
              <Card className='col-span-1 lg:col-span-4'>
                <CardHeader>
                  <CardTitle className='text-base font-semibold'>
                    Evolución de Facturación
                  </CardTitle>
                  <CardDescription>
                    Total facturado mensual (IVA incluido) en los últimos 6 meses.
                  </CardDescription>
                </CardHeader>
                <CardContent className='ps-2'>
                  <Overview data={data.evolucion} />
                </CardContent>
              </Card>

              <Card className='col-span-1 lg:col-span-3'>
                <CardHeader className='flex flex-row items-center justify-between space-y-0'>
                  <div>
                    <CardTitle className='text-base font-semibold'>
                      Últimas Facturas
                    </CardTitle>
                    <CardDescription>
                      Actividad de emisión y cobro reciente.
                    </CardDescription>
                  </div>
                  <Button asChild variant='ghost' size='sm' className='text-xs'>
                    <Link to='/facturas' className='gap-1'>
                      <span>Ver todas</span>
                      <ArrowUpRight className='h-3 w-3' />
                    </Link>
                  </Button>
                </CardHeader>
                <CardContent>
                  <RecentInvoices facturas={data.ultimasFacturas} />
                </CardContent>
              </Card>
            </div>

            {/* Fila Secundaria: Distribución de Servicios y Accesos Rápidos */}
            <div className='grid grid-cols-1 gap-4 lg:grid-cols-7'>
              <Card className='col-span-1 lg:col-span-4'>
                <CardHeader>
                  <CardTitle className='text-base font-semibold'>
                    Servicios & Vencimientos
                  </CardTitle>
                  <CardDescription>
                    Distribución de clientes por servicio y facturas pendientes próximas.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <ServicesDistribution
                    servicios={data.servicios}
                    vencimientos={data.proximosVencimientos}
                  />
                </CardContent>
              </Card>

              <Card className='col-span-1 lg:col-span-3'>
                <CardHeader>
                  <CardTitle className='text-base font-semibold'>
                    Acciones de Gestión
                  </CardTitle>
                  <CardDescription>
                    Accesos directos para la operativa del negocio.
                  </CardDescription>
                </CardHeader>
                <CardContent className='space-y-3'>
                  <Link
                    to='/facturas'
                    className='group flex items-center justify-between rounded-lg border border-border/80 p-3 hover:border-primary/50 hover:bg-muted/50 transition-colors'
                  >
                    <div className='flex items-center gap-3'>
                      <div className='flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary'>
                        <Receipt className='h-4 w-4' />
                      </div>
                      <div>
                        <p className='text-sm font-medium text-foreground group-hover:text-primary transition-colors'>
                          Emisión de Facturas
                        </p>
                        <p className='text-xs text-muted-foreground'>
                          Crear factura con desglose de IVA y serie
                        </p>
                      </div>
                    </div>
                    <ArrowUpRight className='h-4 w-4 text-muted-foreground group-hover:text-primary transition-colors' />
                  </Link>

                  <Link
                    to='/clientes'
                    className='group flex items-center justify-between rounded-lg border border-border/80 p-3 hover:border-primary/50 hover:bg-muted/50 transition-colors'
                  >
                    <div className='flex items-center gap-3'>
                      <div className='flex h-9 w-9 items-center justify-center rounded-md bg-blue-500/10 text-blue-600 dark:text-blue-400'>
                        <Users className='h-4 w-4' />
                      </div>
                      <div>
                        <p className='text-sm font-medium text-foreground group-hover:text-primary transition-colors'>
                          Fichas de Clientes
                        </p>
                        <p className='text-xs text-muted-foreground'>
                          Datos fiscales, dirección y credenciales de servicios
                        </p>
                      </div>
                    </div>
                    <ArrowUpRight className='h-4 w-4 text-muted-foreground group-hover:text-primary transition-colors' />
                  </Link>

                  <Link
                    to='/settings'
                    className='group flex items-center justify-between rounded-lg border border-border/80 p-3 hover:border-primary/50 hover:bg-muted/50 transition-colors'
                  >
                    <div className='flex items-center gap-3'>
                      <div className='flex h-9 w-9 items-center justify-center rounded-md bg-muted text-muted-foreground'>
                        <Settings className='h-4 w-4' />
                      </div>
                      <div>
                        <p className='text-sm font-medium text-foreground group-hover:text-primary transition-colors'>
                          Ajustes de Facturación
                        </p>
                        <p className='text-xs text-muted-foreground'>
                          Datos de emisor, series y retenciones
                        </p>
                      </div>
                    </div>
                    <ArrowUpRight className='h-4 w-4 text-muted-foreground group-hover:text-primary transition-colors' />
                  </Link>

                  <div className='rounded-lg border border-border/60 bg-muted/30 p-3 text-xs text-muted-foreground flex items-center gap-2'>
                    <Shield className='h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0' />
                    <span>
                      Sesión activa bajo esquema de seguridad estricto y cookies HttpOnly.
                    </span>
                  </div>
                </CardContent>
              </Card>
            </div>
          </div>
        )}
      </Main>
    </>
  )
}
