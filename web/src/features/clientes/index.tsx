import { useState, useMemo } from 'react'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'
import { ClienteFormDialog } from './components/cliente-form-dialog'
import { ClienteDeleteDialog } from './components/cliente-delete-dialog'
import { useClientesQuery } from '@/hooks/use-clientes'
import type { ClienteDto } from '@/api'
import {
  Users,
  UserPlus,
  Search as SearchIcon,
  Pencil,
  Trash2,
  Mail,
  Phone,
  Building2,
  MapPin,
  Receipt,
  FileText,
  CheckCircle2,
  LayoutGrid,
  List,
  ShieldCheck,
} from 'lucide-react'
import { toast } from 'sonner'

function getInitials(nombre: string): string {
  const parts = nombre.trim().split(/\s+/)
  if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase()
  return (parts[0][0] + parts[1][0]).toUpperCase()
}

// Colores de acento pastel sutiles para los avatares
const avatarColorMap = [
  'bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20',
  'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20',
  'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20',
  'bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/20',
  'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/20',
  'bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border-cyan-500/20',
]

function getAvatarColor(id: string): string {
  let hash = 0
  for (let i = 0; i < id.length; i++) {
    hash = (hash << 5) - hash + id.charCodeAt(i)
  }
  return avatarColorMap[Math.abs(hash) % avatarColorMap.length]
}

export function Clientes() {
  const [searchTerm, setSearchTerm] = useState('')
  const [formDialogOpen, setFormDialogOpen] = useState(false)
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [selectedCliente, setSelectedCliente] = useState<ClienteDto | null>(null)
  const [activeTab, setActiveTab] = useState<'fiscal' | 'facturas' | 'pedidos' | 'auditoria'>('fiscal')
  const [viewMode, setViewMode] = useState<'workstation' | 'table'>('workstation')

  const { data, isLoading, refetch } = useClientesQuery({
    searchTerm: searchTerm.trim() || undefined,
    pageSize: 50,
  })

  const clientes = data?.items || []

  // Seleccionar por defecto el primer cliente si no hay ninguno seleccionado
  const activeCliente = useMemo(() => {
    if (selectedCliente && clientes.some((c) => c.id === selectedCliente.id)) {
      return clientes.find((c) => c.id === selectedCliente.id) || selectedCliente
    }
    return clientes[0] || null
  }, [clientes, selectedCliente])

  const handleOpenCreate = () => {
    setSelectedCliente(null)
    setFormDialogOpen(true)
  }

  const handleOpenEdit = (cliente: ClienteDto) => {
    setSelectedCliente(cliente)
    setFormDialogOpen(true)
  }

  const handleOpenDelete = (cliente: ClienteDto) => {
    setSelectedCliente(cliente)
    setDeleteDialogOpen(true)
  }

  const handleNuevaFacturaParaCliente = (nombre: string) => {
    toast.success(`Iniciando factura borrador vinculada a ${nombre}`)
  }

  return (
    <>
      <Header>
        <div className='flex items-center gap-2 me-auto min-w-0'>
          <span className='text-foreground text-sm font-semibold tracking-tight'>
            Clientes
          </span>
          {data && (
            <Badge variant='secondary' className='text-xs font-medium px-2 py-0 h-5 rounded-full'>
              {data.totalCount}
            </Badge>
          )}
        </div>

        {/* Segmented Control (macOS HIG: segmented-controls.md) */}
        <div
          role='tablist'
          aria-label='Modo de visualización'
          className='inline-flex items-center rounded-lg bg-muted/70 p-0.5 border border-border/50'
        >
          <button
            type='button'
            role='tab'
            aria-selected={viewMode === 'workstation'}
            onClick={() => setViewMode('workstation')}
            className={`inline-flex items-center gap-1.5 px-3 py-1 text-xs font-medium rounded-md transition-all ${
              viewMode === 'workstation'
                ? 'bg-background text-foreground shadow-xs'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <LayoutGrid className='h-3.5 w-3.5' />
            <span className='hidden sm:inline'>Ficha</span>
          </button>
          <button
            type='button'
            role='tab'
            aria-selected={viewMode === 'table'}
            onClick={() => setViewMode('table')}
            className={`inline-flex items-center gap-1.5 px-3 py-1 text-xs font-medium rounded-md transition-all ${
              viewMode === 'table'
                ? 'bg-background text-foreground shadow-xs'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <List className='h-3.5 w-3.5' />
            <span className='hidden sm:inline'>Tabla</span>
          </button>
        </div>

        <Search />
        <ThemeSwitch />
        <ProfileDropdown />
      </Header>

      <Main>
        <div className='flex flex-1 flex-col gap-4'>
          {/* SPLIT VIEW MASTER–DETAIL (Apple HIG: split-views.md) */}
          {viewMode === 'workstation' ? (
            <div className='grid grid-cols-1 lg:grid-cols-12 gap-4 items-start'>
              {/* PANEL MAESTRO: Directorio de clientes */}
              <div className='lg:col-span-4 flex flex-col'>
                <Card className='border border-border/70 shadow-2xs overflow-hidden rounded-xl'>
                  <div className='px-4 py-3 border-b border-border/60 bg-muted/15 flex items-center justify-between gap-2'>
                    <div>
                      <h2 className='text-sm font-semibold text-foreground leading-none'>
                        Directorio de clientes
                      </h2>
                      <p className='text-xs text-muted-foreground mt-1'>
                        {clientes.length} {clientes.length === 1 ? 'cliente registrado' : 'clientes registrados'}
                      </p>
                    </div>

                    <Button
                      size='sm'
                      variant='outline'
                      onClick={handleOpenCreate}
                      className='gap-1.5 h-8 text-xs font-medium bg-background hover:bg-muted/60 shadow-2xs'
                      data-testid='btn-nuevo-cliente'
                    >
                      <UserPlus className='h-3.5 w-3.5 text-primary' />
                      <span>Nuevo cliente</span>
                    </Button>
                  </div>

                  {/* Campo de búsqueda del listado */}
                  <div className='p-2.5 border-b border-border/60 bg-background'>
                    <div className='relative'>
                      <SearchIcon className='absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground' />
                      <Input
                        placeholder='Filtrar por nombre o NIF/CIF...'
                        className='pl-8 h-8 text-xs rounded-lg bg-muted/30 border-border/60 focus-visible:bg-background'
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        data-testid='input-buscar-cliente'
                      />
                    </div>
                  </div>

                  {/* Lista maestra con estado de selección persistente */}
                  <div className='divide-y divide-border/50 max-h-[660px] overflow-y-auto p-1.5 space-y-0.5'>
                    {isLoading ? (
                      Array.from({ length: 4 }).map((_, i) => (
                        <div key={i} className='p-3 space-y-2'>
                          <Skeleton className='h-4 w-32' />
                          <Skeleton className='h-3 w-24' />
                        </div>
                      ))
                    ) : clientes.length === 0 ? (
                      <div className='p-8 text-center text-xs text-muted-foreground space-y-2'>
                        <Users className='h-8 w-8 mx-auto text-muted-foreground/50' />
                        <p className='font-medium text-foreground'>Sin clientes en el directorio</p>
                        <p>Añade tu primer cliente para emitir facturas y presupuestos.</p>
                      </div>
                    ) : (
                      clientes.map((cliente) => {
                        const isSelected = activeCliente?.id === cliente.id
                        const initials = getInitials(cliente.nombreFiscal)
                        const avatarClass = getAvatarColor(cliente.id)

                        return (
                          <div
                            key={cliente.id}
                            data-testid={`cliente-row-${cliente.id}`}
                            onClick={() => setSelectedCliente(cliente)}
                            className={`group px-3 py-2.5 rounded-lg cursor-pointer transition-colors flex items-center justify-between gap-3 text-left ${
                              isSelected
                                ? 'bg-primary/10 dark:bg-primary/15 text-foreground ring-1 ring-primary/20'
                                : 'hover:bg-muted/50 text-foreground/90'
                            }`}
                          >
                            <div className='flex items-center gap-3 min-w-0'>
                              <div
                                className={`flex h-9 w-9 items-center justify-center rounded-lg border font-semibold text-xs shrink-0 ${avatarClass}`}
                              >
                                {initials}
                              </div>
                              <div className='min-w-0'>
                                <p className='font-semibold text-[13px] text-foreground truncate leading-snug'>
                                  {cliente.nombreFiscal}
                                </p>
                                <p className='text-xs text-muted-foreground font-mono truncate mt-0.5'>
                                  {cliente.cifNif}
                                </p>
                              </div>
                            </div>

                            <div className='flex items-center gap-1 shrink-0'>
                              <span
                                className={`h-2 w-2 rounded-full ${
                                  cliente.activo ? 'bg-emerald-500' : 'bg-muted-foreground/40'
                                }`}
                                title={cliente.activo ? 'Cliente activo' : 'Cliente inactivo'}
                              />
                              <div className='flex items-center opacity-0 group-hover:opacity-100 focus-within:opacity-100 transition-opacity ms-1'>
                                <Button
                                  variant='ghost'
                                  size='icon'
                                  className='h-7 w-7 text-muted-foreground hover:text-foreground'
                                  onClick={(e) => {
                                    e.stopPropagation()
                                    handleOpenEdit(cliente)
                                  }}
                                  data-testid={`btn-editar-cliente-${cliente.id}`}
                                  aria-label={`Editar ${cliente.nombreFiscal}`}
                                  title='Editar ficha'
                                >
                                  <Pencil className='h-3.5 w-3.5' />
                                </Button>
                                <Button
                                  variant='ghost'
                                  size='icon'
                                  className='h-7 w-7 text-muted-foreground hover:text-destructive'
                                  onClick={(e) => {
                                    e.stopPropagation()
                                    handleOpenDelete(cliente)
                                  }}
                                  data-testid={`btn-eliminar-cliente-${cliente.id}`}
                                  aria-label={`Eliminar ${cliente.nombreFiscal}`}
                                  title='Eliminar cliente'
                                >
                                  <Trash2 className='h-3.5 w-3.5' />
                                </Button>
                              </div>
                            </div>
                          </div>
                        )
                      })
                    )}
                  </div>
                </Card>
              </div>

              {/* PANEL SECUNDARIO: Ficha detallada del cliente (8 columnas) */}
              <div className='lg:col-span-8 flex flex-col gap-4'>
                {activeCliente ? (
                  <Card className='border border-border/70 shadow-2xs overflow-hidden rounded-xl'>
                    {/* Cabecera de la ficha con jerarquía clara de 1 solo botón primario (buttons.md) */}
                    <div className='p-5 bg-muted/10 border-b border-border/60 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4'>
                      <div className='flex items-center gap-3.5'>
                        <div
                          className={`flex h-12 w-12 items-center justify-center rounded-xl border text-base font-bold shrink-0 ${getAvatarColor(
                            activeCliente.id
                          )}`}
                        >
                          {getInitials(activeCliente.nombreFiscal)}
                        </div>
                        <div>
                          <div className='flex items-center gap-2.5 flex-wrap'>
                            <h2 className='text-lg font-semibold tracking-tight text-foreground'>
                              {activeCliente.nombreFiscal}
                            </h2>
                            {activeCliente.activo ? (
                              <Badge
                                variant='outline'
                                className='border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 text-xs font-medium'
                              >
                                Activo
                              </Badge>
                            ) : (
                              <Badge variant='secondary' className='text-xs'>
                                Inactivo
                              </Badge>
                            )}
                          </div>
                          <p className='text-xs text-muted-foreground font-mono mt-0.5'>
                            NIF/CIF: {activeCliente.cifNif}
                            {activeCliente.nombreComercial && (
                              <span className='font-sans ms-2 text-foreground/80 font-medium'>
                                · {activeCliente.nombreComercial}
                              </span>
                            )}
                          </p>
                        </div>
                      </div>

                      {/* Acciones de la ficha: Editar (secundario) + Emitir factura (único botón primario) */}
                      <div className='flex items-center gap-2'>
                        <Button
                          variant='outline'
                          size='sm'
                          className='h-8 text-xs font-medium gap-1.5'
                          onClick={() => handleOpenEdit(activeCliente)}
                        >
                          <Pencil className='h-3.5 w-3.5 text-muted-foreground' />
                          <span>Editar ficha</span>
                        </Button>
                        <Button
                          size='sm'
                          className='h-8 text-xs font-medium gap-1.5 shadow-2xs'
                          onClick={() => handleNuevaFacturaParaCliente(activeCliente.nombreFiscal)}
                        >
                          <Receipt className='h-3.5 w-3.5' />
                          <span>Emitir factura</span>
                        </Button>
                      </div>
                    </div>

                    {/* Métricas con etiquetas legibles en sentence-case (typography.md) */}
                    <div className='grid grid-cols-3 divide-x divide-border/60 border-b border-border/60 bg-background px-2 py-3.5 text-center'>
                      <div className='px-3'>
                        <span className='text-xs text-muted-foreground font-medium block'>
                          Facturación anual (2026)
                        </span>
                        <p className='text-base font-semibold font-mono text-foreground mt-0.5'>
                          3.420,00 €
                        </p>
                      </div>
                      <div className='px-3'>
                        <span className='text-xs text-muted-foreground font-medium block'>
                          Facturas emitidas
                        </span>
                        <p className='text-base font-semibold font-mono text-foreground mt-0.5'>
                          4 documentos
                        </p>
                      </div>
                      <div className='px-3'>
                        <span className='text-xs text-muted-foreground font-medium block'>
                          Estado de cobro
                        </span>
                        <p className='text-xs font-semibold text-emerald-600 dark:text-emerald-400 mt-1 inline-flex items-center justify-center gap-1'>
                          <CheckCircle2 className='h-3.5 w-3.5' />
                          Al corriente
                        </p>
                      </div>
                    </div>

                    {/* Segmented Control para las subvistas de la ficha (segmented-controls.md) */}
                    <div className='px-6 pt-4 pb-2 flex items-center justify-between'>
                      <div
                        role='tablist'
                        className='inline-flex items-center rounded-lg bg-muted/70 p-0.5 border border-border/50 overflow-x-auto'
                      >
                        {[
                          { id: 'fiscal', label: 'Datos fiscales', icon: Building2 },
                          { id: 'facturas', label: 'Facturas y tickets', icon: Receipt },
                          { id: 'pedidos', label: 'Presupuestos', icon: FileText },
                          { id: 'auditoria', label: 'Actividad', icon: ShieldCheck },
                        ].map((tab) => {
                          const Icon = tab.icon
                          const isActive = activeTab === tab.id
                          return (
                            <button
                              key={tab.id}
                              type='button'
                              role='tab'
                              aria-selected={isActive}
                              onClick={() => setActiveTab(tab.id as typeof activeTab)}
                              className={`flex items-center gap-1.5 py-1.5 px-3 text-xs font-medium rounded-md transition-all shrink-0 ${
                                isActive
                                  ? 'bg-background text-foreground shadow-xs'
                                  : 'text-muted-foreground hover:text-foreground'
                              }`}
                            >
                              <Icon className='h-3.5 w-3.5' />
                              <span>{tab.label}</span>
                            </button>
                          )
                        })}
                      </div>
                    </div>

                    {/* Contenido de la subvista seleccionada */}
                    <div className='p-6 pt-3'>
                      {activeTab === 'fiscal' && (
                        <div className='space-y-4'>
                          <div className='grid sm:grid-cols-2 gap-4 text-xs'>
                            <div className='p-4 rounded-xl border border-border/70 bg-muted/10 space-y-2'>
                              <span className='text-xs font-medium text-muted-foreground block'>
                                Identificación fiscal
                              </span>
                              <p className='font-semibold text-sm text-foreground'>
                                {activeCliente.nombreFiscal}
                              </p>
                              <p className='font-mono text-xs text-muted-foreground'>
                                NIF/CIF: {activeCliente.cifNif}
                              </p>
                              <p className='text-xs text-muted-foreground'>
                                Régimen aplicable: IVA General (21%)
                              </p>
                            </div>

                            <div className='p-4 rounded-xl border border-border/70 bg-muted/10 space-y-2'>
                              <span className='text-xs font-medium text-muted-foreground block'>
                                Contacto comercial
                              </span>
                              <div className='flex items-center gap-2 text-xs text-foreground'>
                                <Mail className='h-3.5 w-3.5 text-muted-foreground shrink-0' />
                                <span>{activeCliente.email || 'Sin correo electrónico registrado'}</span>
                              </div>
                              <div className='flex items-center gap-2 text-xs text-foreground'>
                                <Phone className='h-3.5 w-3.5 text-muted-foreground shrink-0' />
                                <span>{activeCliente.telefono || 'Sin teléfono registrado'}</span>
                              </div>
                            </div>
                          </div>

                          <div className='p-4 rounded-xl border border-border/70 bg-muted/10 space-y-1.5 text-xs'>
                            <span className='text-xs font-medium text-muted-foreground flex items-center gap-1.5'>
                              <MapPin className='h-3.5 w-3.5 text-muted-foreground' />
                              Domicilio fiscal y de facturación
                            </span>
                            <p className='text-foreground font-medium text-sm'>
                              {activeCliente.direccion || 'Dirección postal no especificada'}
                            </p>
                            <p className='text-muted-foreground text-xs'>
                              {[
                                activeCliente.codigoPostal,
                                activeCliente.ciudad,
                                activeCliente.provincia,
                              ]
                                .filter(Boolean)
                                .join(' · ') || 'España'}
                            </p>
                          </div>

                          {activeCliente.notas && (
                            <div className='p-4 rounded-xl border border-border/70 bg-muted/5 space-y-1.5 text-xs'>
                              <span className='text-xs font-medium text-muted-foreground block'>
                                Notas internas
                              </span>
                              <p className='text-foreground/90 leading-relaxed whitespace-pre-line'>
                                {activeCliente.notas}
                              </p>
                            </div>
                          )}
                        </div>
                      )}

                      {activeTab === 'facturas' && (
                        <div className='space-y-3 text-xs'>
                          <div className='border rounded-lg overflow-hidden'>
                            <table className='w-full text-left'>
                              <thead className='bg-muted/40 border-b text-muted-foreground font-medium'>
                                <tr>
                                  <th className='py-2 px-3'>Número</th>
                                  <th className='py-2 px-3'>Fecha</th>
                                  <th className='py-2 px-3 text-right'>Importe</th>
                                  <th className='py-2 px-3 text-right'>Estado</th>
                                </tr>
                              </thead>
                              <tbody className='divide-y'>
                                <tr className='hover:bg-muted/20'>
                                  <td className='py-2.5 px-3 font-mono font-medium'>F2026-00124</td>
                                  <td className='py-2.5 px-3 text-muted-foreground'>05/11/2026</td>
                                  <td className='py-2.5 px-3 text-right font-mono font-semibold'>1.512,50 €</td>
                                  <td className='py-2.5 px-3 text-right'>
                                    <Badge className='bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20 text-[10px]'>
                                      Cobrada
                                    </Badge>
                                  </td>
                                </tr>
                                <tr className='hover:bg-muted/20'>
                                  <td className='py-2.5 px-3 font-mono font-medium'>F2026-00089</td>
                                  <td className='py-2.5 px-3 text-muted-foreground'>12/10/2026</td>
                                  <td className='py-2.5 px-3 text-right font-mono font-semibold'>1.907,50 €</td>
                                  <td className='py-2.5 px-3 text-right'>
                                    <Badge className='bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20 text-[10px]'>
                                      Cobrada
                                    </Badge>
                                  </td>
                                </tr>
                              </tbody>
                            </table>
                          </div>
                        </div>
                      )}

                      {activeTab === 'pedidos' && (
                        <div className='p-6 text-center text-xs text-muted-foreground space-y-2 border border-dashed rounded-lg'>
                          <FileText className='h-8 w-8 mx-auto text-muted-foreground/50' />
                          <p>No hay presupuestos pendientes para este cliente.</p>
                          <Button size='sm' variant='outline' className='text-xs h-7 mt-1'>
                            Crear Presupuesto
                          </Button>
                        </div>
                      )}

                      {activeTab === 'auditoria' && (
                        <div className='p-4 rounded-lg border bg-muted/10 space-y-2 text-xs font-mono text-muted-foreground'>
                          <p>ID Registro: {activeCliente.id}</p>
                          <p>RowVersion (xmin): {String(activeCliente.rowVersion)}</p>
                          <p>Sincronización: Activa con PostgreSQL 17</p>
                        </div>
                      )}
                    </div>
                  </Card>
                ) : (
                  <Card className='border border-border/80 p-12 text-center text-xs text-muted-foreground'>
                    Selecciona un cliente del directorio para visualizar su ficha.
                  </Card>
                )}
              </div>
            </div>
          ) : (
            /* VISTA 2: TABLA COMPACTA CLÁSICA */
            <Card className='border border-border/80 shadow-xs'>
              <CardHeader className='pb-3'>
                <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
                  <div>
                    <CardTitle className='text-base font-semibold'>Directorio Tabular</CardTitle>
                    <CardDescription>
                      Listado sincronizado en tiempo real con la base de datos PostgreSQL.
                    </CardDescription>
                  </div>

                  <div className='flex items-center gap-2 w-full sm:w-auto'>
                    <div className='relative flex-1 sm:w-64'>
                      <SearchIcon className='absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground' />
                      <Input
                        placeholder='Buscar por nombre o CIF...'
                        className='pl-8 h-8 text-xs'
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                      />
                    </div>
                    <Button
                      size='sm'
                      onClick={handleOpenCreate}
                      className='gap-1.5 shadow-2xs h-8 text-xs shrink-0'
                      data-testid='btn-nuevo-cliente'
                    >
                      <UserPlus className='h-3.5 w-3.5' />
                      <span>Nuevo Cliente</span>
                    </Button>
                  </div>
                </div>
              </CardHeader>

              <CardContent className='p-0'>
                <div className='border-t'>
                  <Table>
                    <TableHeader>
                      <TableRow className='hover:bg-transparent'>
                        <TableHead className='w-[300px]'>Cliente / Empresa</TableHead>
                        <TableHead>CIF / NIF</TableHead>
                        <TableHead>Contacto</TableHead>
                        <TableHead>Ubicación</TableHead>
                        <TableHead>Estado</TableHead>
                        <TableHead className='text-right'>Acciones</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {isLoading ? (
                        Array.from({ length: 4 }).map((_, i) => (
                          <TableRow key={i}>
                            <TableCell>
                              <Skeleton className='h-5 w-40' />
                            </TableCell>
                            <TableCell>
                              <Skeleton className='h-4 w-20' />
                            </TableCell>
                            <TableCell>
                              <Skeleton className='h-4 w-32' />
                            </TableCell>
                            <TableCell>
                              <Skeleton className='h-4 w-24' />
                            </TableCell>
                            <TableCell>
                              <Skeleton className='h-5 w-16' />
                            </TableCell>
                            <TableCell className='text-right'>
                              <Skeleton className='h-8 w-20 ms-auto' />
                            </TableCell>
                          </TableRow>
                        ))
                      ) : clientes.length === 0 ? (
                        <TableRow>
                          <TableCell colSpan={6} className='h-36 text-center text-muted-foreground'>
                            No se encontraron clientes registrados.
                          </TableCell>
                        </TableRow>
                      ) : (
                        clientes.map((cliente) => (
                          <TableRow key={cliente.id} className='group'>
                            <TableCell>
                              <div className='flex items-center gap-2.5'>
                                <div
                                  className={`flex h-8 w-8 items-center justify-center rounded-lg border font-semibold text-xs shrink-0 ${getAvatarColor(
                                    cliente.id
                                  )}`}
                                >
                                  {getInitials(cliente.nombreFiscal)}
                                </div>
                                <div className='min-w-0'>
                                  <p className='font-medium text-sm text-foreground truncate'>
                                    {cliente.nombreFiscal}
                                  </p>
                                  {cliente.nombreComercial && (
                                    <p className='text-xs text-muted-foreground truncate'>
                                      {cliente.nombreComercial}
                                    </p>
                                  )}
                                </div>
                              </div>
                            </TableCell>

                            <TableCell>
                              <span className='font-mono text-xs font-medium'>{cliente.cifNif}</span>
                            </TableCell>

                            <TableCell>
                              <div className='space-y-0.5 text-xs text-muted-foreground'>
                                {cliente.email && <div>{cliente.email}</div>}
                                {cliente.telefono && <div>{cliente.telefono}</div>}
                              </div>
                            </TableCell>

                            <TableCell>
                              <span className='text-xs text-muted-foreground'>
                                {[cliente.ciudad, cliente.provincia].filter(Boolean).join(', ') || '—'}
                              </span>
                            </TableCell>

                            <TableCell>
                              {cliente.activo ? (
                                <Badge
                                  variant='outline'
                                  className='border-emerald-500/40 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 text-[11px]'
                                >
                                  Activo
                                </Badge>
                              ) : (
                                <Badge variant='secondary' className='text-[11px] text-muted-foreground'>
                                  Inactivo
                                </Badge>
                              )}
                            </TableCell>

                            <TableCell className='text-right'>
                              <div className='flex items-center justify-end gap-1'>
                                <Button
                                  variant='ghost'
                                  size='icon'
                                  className='h-8 w-8 text-muted-foreground hover:text-foreground'
                                  onClick={() => handleOpenEdit(cliente)}
                                >
                                  <Pencil className='h-4 w-4' />
                                </Button>
                                <Button
                                  variant='ghost'
                                  size='icon'
                                  className='h-8 w-8 text-muted-foreground hover:text-destructive'
                                  onClick={() => handleOpenDelete(cliente)}
                                >
                                  <Trash2 className='h-4 w-4' />
                                </Button>
                              </div>
                            </TableCell>
                          </TableRow>
                        ))
                      )}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </Main>

      {/* Diálogos de Formulario y Eliminación */}
      <ClienteFormDialog
        open={formDialogOpen}
        onOpenChange={setFormDialogOpen}
        cliente={selectedCliente}
        onSuccess={() => refetch()}
      />

      <ClienteDeleteDialog
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        cliente={selectedCliente}
        onSuccess={() => refetch()}
      />
    </>
  )
}
