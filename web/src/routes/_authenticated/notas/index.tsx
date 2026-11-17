import { useState, useMemo } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/card'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Switch } from '@/components/ui/switch'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  FileText,
  Plus,
  Search as SearchIcon,
  Archive,
  ArchiveRestore,
  Pencil,
  Trash2,
  Calendar,
  User,
  CheckCircle2,
} from 'lucide-react'
import { toast } from 'sonner'
import { notasApi, type NotaDto } from '@/api/notas-api'

export const Route = createFileRoute('/_authenticated/notas/')({
  component: NotasPage,
})

function NotasPage() {
  const queryClient = useQueryClient()
  const [searchTerm, setSearchTerm] = useState('')
  const [activeTab, setActiveTab] = useState<'todas' | 'activas' | 'archivadas'>('activas')

  // Modal Crear
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [newTitulo, setNewTitulo] = useState('')
  const [newContenido, setNewContenido] = useState('')
  const [newArchivada, setNewArchivada] = useState(false)

  // Modal Editar
  const [editingNota, setEditingNota] = useState<NotaDto | null>(null)
  const [editTitulo, setEditTitulo] = useState('')
  const [editContenido, setEditContenido] = useState('')
  const [editArchivada, setEditArchivada] = useState(false)

  // Modal Borrar
  const [deletingNota, setDeletingNota] = useState<NotaDto | null>(null)

  // Query Notas
  const estadoParam = activeTab === 'todas' ? undefined : activeTab
  const { data, isLoading } = useQuery({
    queryKey: ['notas', searchTerm, estadoParam],
    queryFn: () =>
      notasApi.getNotas({
        q: searchTerm.trim() || undefined,
        estado: estadoParam,
      }),
  })

  // Mutation Crear
  const createMutation = useMutation({
    mutationFn: (vars: { titulo: string; contenido: string; archivada: boolean }) =>
      notasApi.createNota(vars),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notas'] })
      toast.success('Nota creada correctamente')
      setIsCreateOpen(false)
      setNewTitulo('')
      setNewContenido('')
      setNewArchivada(false)
    },
    onError: (err: Error) => {
      toast.error(`Error al crear la nota: ${err.message}`)
    },
  })

  // Mutation Editar
  const updateMutation = useMutation({
    mutationFn: (vars: {
      id: string
      titulo: string
      contenido: string
      archivada: boolean
      rowVersion: number
    }) => notasApi.updateNota(vars.id, vars),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notas'] })
      toast.success('Nota actualizada correctamente')
      setEditingNota(null)
    },
    onError: (err: Error) => {
      toast.error(`Error al actualizar la nota: ${err.message}`)
    },
  })

  // Mutation Borrar
  const deleteMutation = useMutation({
    mutationFn: (id: string) => notasApi.deleteNota(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notas'] })
      toast.success('Nota eliminada correctamente')
      setDeletingNota(null)
    },
    onError: (err: Error) => {
      toast.error(`Error al eliminar la nota: ${err.message}`)
    },
  })

  const notesList = useMemo(() => data?.items ?? [], [data])

  const handleOpenEdit = (nota: NotaDto) => {
    setEditingNota(nota)
    setEditTitulo(nota.titulo)
    setEditContenido(nota.contenido)
    setEditArchivada(nota.archivada)
  }

  const handleToggleArchivada = (nota: NotaDto) => {
    updateMutation.mutate({
      id: nota.id,
      titulo: nota.titulo,
      contenido: nota.contenido,
      archivada: !nota.archivada,
      rowVersion: nota.rowVersion,
    })
  }

  const formatDate = (dateStr: string) => {
    try {
      const d = new Date(dateStr)
      return new Intl.DateTimeFormat('es-ES', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(d)
    } catch {
      return dateStr
    }
  }

  return (
    <>
      <Header>
        <div className='flex items-center gap-2'>
          <FileText className='h-5 w-5 text-primary' />
          <h1 className='text-base font-semibold'>Notas y Recordatorios</h1>
        </div>
        <div className='ml-auto flex items-center space-x-4'>
          <Search />
          <ThemeSwitch />
          <ProfileDropdown />
        </div>
      </Header>

      <Main className='space-y-6'>
        {/* Cabecera y acciones superiores */}
        <div className='flex flex-col sm:flex-row sm:items-center justify-between gap-4'>
          <div>
            <h2 className='text-2xl font-bold tracking-tight'>Gestión de Notas</h2>
            <p className='text-sm text-muted-foreground'>
              Anotaciones internas, actas de reuniones y recordatorios de gestión de la empresa.
            </p>
          </div>
          <Button
            onClick={() => setIsCreateOpen(true)}
            className='gap-2 shadow-xs'
          >
            <Plus className='h-4 w-4' />
            <span>Nueva Nota</span>
          </Button>
        </div>

        {/* Filtros: Búsqueda y Pestañas */}
        <div className='flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3'>
          <div className='relative flex-1 max-w-sm'>
            <SearchIcon className='absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground' />
            <Input
              type='search'
              placeholder='Buscar por título o contenido...'
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className='pl-8 h-9 text-xs'
            />
          </div>

          <Tabs
            value={activeTab}
            onValueChange={(v) => setActiveTab(v as 'todas' | 'activas' | 'archivadas')}
            className='w-auto'
          >
            <TabsList className='grid grid-cols-3 h-9'>
              <TabsTrigger value='activas' className='text-xs'>
                Activas
              </TabsTrigger>
              <TabsTrigger value='archivadas' className='text-xs'>
                Archivadas
              </TabsTrigger>
              <TabsTrigger value='todas' className='text-xs'>
                Todas
              </TabsTrigger>
            </TabsList>
          </Tabs>
        </div>

        {/* Listado de Notas */}
        {isLoading ? (
          <div className='grid sm:grid-cols-2 lg:grid-cols-3 gap-4'>
            {[1, 2, 3].map((i) => (
              <Card key={i} className='animate-pulse h-48 bg-muted/40' />
            ))}
          </div>
        ) : notesList.length === 0 ? (
          <Card className='p-12 text-center border-dashed'>
            <div className='flex flex-col items-center justify-center space-y-3'>
              <div className='p-3 bg-muted rounded-full'>
                <FileText className='h-8 w-8 text-muted-foreground' />
              </div>
              <h3 className='font-semibold text-lg'>No hay notas que mostrar</h3>
              <p className='text-sm text-muted-foreground max-w-md'>
                {searchTerm
                  ? 'No se encontraron notas que coincidan con la búsqueda.'
                  : activeTab === 'archivadas'
                    ? 'No tienes ninguna nota archivada.'
                    : 'Aún no has creado notas. Haz clic en "Nueva Nota" para añadir apuntes sobre clientes, operaciones o presupuestos.'}
              </p>
              {!searchTerm && activeTab !== 'archivadas' && (
                <Button
                  variant='outline'
                  size='sm'
                  onClick={() => setIsCreateOpen(true)}
                  className='gap-2 mt-2'
                >
                  <Plus className='h-4 w-4' />
                  Crear primera nota
                </Button>
              )}
            </div>
          </Card>
        ) : (
          <div className='grid sm:grid-cols-2 lg:grid-cols-3 gap-4'>
            {notesList.map((nota) => (
              <Card
                key={nota.id}
                className={`flex flex-col justify-between transition-all hover:shadow-sm border ${
                  nota.archivada
                    ? 'bg-muted/30 border-dashed opacity-80'
                    : 'bg-card border-border'
                }`}
              >
                <CardHeader className='pb-3'>
                  <div className='flex items-start justify-between gap-2'>
                    <CardTitle className='text-base font-semibold line-clamp-1'>
                      {nota.titulo}
                    </CardTitle>
                    {nota.archivada ? (
                      <Badge variant='secondary' className='text-[10px] h-5 px-1.5 shrink-0 gap-1'>
                        <Archive className='h-3 w-3' /> Archivada
                      </Badge>
                    ) : (
                      <Badge variant='outline' className='text-[10px] h-5 px-1.5 shrink-0 gap-1 border-emerald-500/30 text-emerald-600 dark:text-emerald-400'>
                        <CheckCircle2 className='h-3 w-3' /> Activa
                      </Badge>
                    )}
                  </div>
                  <CardDescription className='flex items-center gap-1.5 text-[11px] pt-1 text-muted-foreground'>
                    <Calendar className='h-3 w-3' />
                    <span>{formatDate(nota.createdAtUtc)}</span>
                  </CardDescription>
                </CardHeader>

                <CardContent className='flex-1 pb-4'>
                  <p className='text-xs text-foreground/80 whitespace-pre-wrap line-clamp-6 leading-relaxed'>
                    {nota.contenido}
                  </p>
                </CardContent>

                <div className='px-6 py-3 border-t bg-muted/15 flex items-center justify-between text-xs text-muted-foreground'>
                  <span className='flex items-center gap-1 text-[11px] truncate max-w-[120px]'>
                    <User className='h-3 w-3 shrink-0' />
                    <span className='truncate'>{nota.createdBy || 'Usuario'}</span>
                  </span>

                  <div className='flex items-center gap-1'>
                    <Button
                      variant='ghost'
                      size='icon'
                      className='h-7 w-7'
                      title={nota.archivada ? 'Desarchivar' : 'Archivar'}
                      onClick={() => handleToggleArchivada(nota)}
                    >
                      {nota.archivada ? (
                        <ArchiveRestore className='h-3.5 w-3.5' />
                      ) : (
                        <Archive className='h-3.5 w-3.5' />
                      )}
                    </Button>
                    <Button
                      variant='ghost'
                      size='icon'
                      className='h-7 w-7'
                      title='Editar'
                      onClick={() => handleOpenEdit(nota)}
                    >
                      <Pencil className='h-3.5 w-3.5' />
                    </Button>
                    <Button
                      variant='ghost'
                      size='icon'
                      className='h-7 w-7 text-destructive hover:text-destructive'
                      title='Eliminar'
                      onClick={() => setDeletingNota(nota)}
                    >
                      <Trash2 className='h-3.5 w-3.5' />
                    </Button>
                  </div>
                </div>
              </Card>
            ))}
          </div>
        )}

        {/* Modal Crear Nota */}
        <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
          <DialogContent className='sm:max-w-md'>
            <DialogHeader>
              <DialogTitle className='flex items-center gap-2 text-base'>
                <Plus className='h-4 w-4 text-primary' />
                <span>Nueva Nota</span>
              </DialogTitle>
              <DialogDescription className='text-xs'>
                Escribe un título y el contenido de tu anotación o recordatorio.
              </DialogDescription>
            </DialogHeader>

            <form
              onSubmit={(e) => {
                e.preventDefault()
                if (!newTitulo.trim() || !newContenido.trim()) {
                  toast.error('El título y contenido son obligatorios.')
                  return
                }
                createMutation.mutate({
                  titulo: newTitulo.trim(),
                  contenido: newContenido.trim(),
                  archivada: newArchivada,
                })
              }}
              className='space-y-4 py-2'
            >
              <div className='space-y-1.5'>
                <label className='text-xs font-medium'>Título</label>
                <Input
                  placeholder='Ej. Llamar a cliente para confirmar presupuesto'
                  value={newTitulo}
                  onChange={(e) => setNewTitulo(e.target.value)}
                  maxLength={150}
                  required
                />
              </div>

              <div className='space-y-1.5'>
                <label className='text-xs font-medium'>Contenido</label>
                <Textarea
                  placeholder='Detalles de la nota, acuerdos o recordatorios...'
                  rows={5}
                  value={newContenido}
                  onChange={(e) => setNewContenido(e.target.value)}
                  maxLength={4000}
                  required
                />
              </div>

              <div className='flex items-center justify-between rounded-lg border p-3 bg-muted/20'>
                <div className='space-y-0.5'>
                  <p className='text-xs font-medium'>Crear como archivada</p>
                  <p className='text-[11px] text-muted-foreground'>
                    Guardar directamente en el historial de archivadas.
                  </p>
                </div>
                <Switch
                  checked={newArchivada}
                  onCheckedChange={setNewArchivada}
                />
              </div>

              <DialogFooter className='pt-2'>
                <Button
                  type='button'
                  variant='outline'
                  size='sm'
                  onClick={() => setIsCreateOpen(false)}
                >
                  Cancelar
                </Button>
                <Button
                  type='submit'
                  size='sm'
                  disabled={createMutation.isPending}
                >
                  {createMutation.isPending ? 'Guardando...' : 'Crear Nota'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>

        {/* Modal Editar Nota */}
        <Dialog
          open={!!editingNota}
          onOpenChange={(open) => !open && setEditingNota(null)}
        >
          <DialogContent className='sm:max-w-md'>
            <DialogHeader>
              <DialogTitle className='flex items-center gap-2 text-base'>
                <Pencil className='h-4 w-4 text-primary' />
                <span>Editar Nota</span>
              </DialogTitle>
              <DialogDescription className='text-xs'>
                Modifica los datos de la nota. Los cambios se guardarán de inmediato.
              </DialogDescription>
            </DialogHeader>

            {editingNota && (
              <form
                onSubmit={(e) => {
                  e.preventDefault()
                  if (!editTitulo.trim() || !editContenido.trim()) {
                    toast.error('El título y contenido son obligatorios.')
                    return
                  }
                  updateMutation.mutate({
                    id: editingNota.id,
                    titulo: editTitulo.trim(),
                    contenido: editContenido.trim(),
                    archivada: editArchivada,
                    rowVersion: editingNota.rowVersion,
                  })
                }}
                className='space-y-4 py-2'
              >
                <div className='space-y-1.5'>
                  <label className='text-xs font-medium'>Título</label>
                  <Input
                    placeholder='Título de la nota'
                    value={editTitulo}
                    onChange={(e) => setEditTitulo(e.target.value)}
                    maxLength={150}
                    required
                  />
                </div>

                <div className='space-y-1.5'>
                  <label className='text-xs font-medium'>Contenido</label>
                  <Textarea
                    placeholder='Contenido de la nota...'
                    rows={5}
                    value={editContenido}
                    onChange={(e) => setEditContenido(e.target.value)}
                    maxLength={4000}
                    required
                  />
                </div>

                <div className='flex items-center justify-between rounded-lg border p-3 bg-muted/20'>
                  <div className='space-y-0.5'>
                    <p className='text-xs font-medium'>Nota archivada</p>
                    <p className='text-[11px] text-muted-foreground'>
                      Ocultar de la vista principal de notas activas.
                    </p>
                  </div>
                  <Switch
                    checked={editArchivada}
                    onCheckedChange={setEditArchivada}
                  />
                </div>

                <DialogFooter className='pt-2'>
                  <Button
                    type='button'
                    variant='outline'
                    size='sm'
                    onClick={() => setEditingNota(null)}
                  >
                    Cancelar
                  </Button>
                  <Button
                    type='submit'
                    size='sm'
                    disabled={updateMutation.isPending}
                  >
                    {updateMutation.isPending ? 'Guardando...' : 'Guardar Cambios'}
                  </Button>
                </DialogFooter>
              </form>
            )}
          </DialogContent>
        </Dialog>

        {/* Modal Confirmar Eliminación */}
        <AlertDialog
          open={!!deletingNota}
          onOpenChange={(open) => !open && setDeletingNota(null)}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>¿Eliminar esta nota?</AlertDialogTitle>
              <AlertDialogDescription>
                Esta acción eliminará la nota de forma permanente:{' '}
                <span className='font-semibold text-foreground'>
                  "{deletingNota?.titulo}"
                </span>
                . No se puede deshacer.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancelar</AlertDialogCancel>
              <AlertDialogAction
                onClick={() => deletingNota && deleteMutation.mutate(deletingNota.id)}
                className='bg-destructive text-destructive-foreground hover:bg-destructive/90'
              >
                Eliminar
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </Main>
    </>
  )
}
