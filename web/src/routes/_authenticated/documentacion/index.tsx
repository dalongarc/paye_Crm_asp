import { useMemo, useState } from 'react'
import z from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  FileSignature,
  FileText,
  FileCheck2,
  Plus,
  Search as SearchIcon,
  Pencil,
  Copy,
  Trash2,
  Download,
  Building2,
  Eye,
} from 'lucide-react'
import { toast } from 'sonner'
import { api } from '@/api'
import { useClientesQuery } from '@/hooks/use-clientes'
import type {
  DocumentacionCompanySettingsDto,
  DocumentoComercialDetailDto,
  SaveDocumentoComercialRequest,
  TipoDocumentoComercial,
  UpdateDocumentacionCompanySettingsRequest,
} from '@/api/types'
import {
  DEFAULT_LEGAL_NOTICE_TEXT,
  DEFAULT_LEGAL_NOTICE_TITLE,
  DOCUMENTACION_ASSETS,
} from '@/features/documentacion/config/default-templates'
import { DocumentoEditorView } from '@/features/documentacion/components/documento-editor-view'
import { CompanySettingsDialog } from '@/features/documentacion/components/company-settings-dialog'
import {
  formatCurrencyEs,
  formatDateEs,
} from '@/features/documentacion/components/documento-a4-renderer'

const documentacionSearchSchema = z.object({
  tab: z.enum(['presupuestos', 'contratos']).optional().catch(undefined),
  clienteId: z.string().optional().catch(undefined),
})

export const Route = createFileRoute('/_authenticated/documentacion/')({
  validateSearch: documentacionSearchSchema,
  component: DocumentacionPage,
})

const FALLBACK_COMPANY: DocumentacionCompanySettingsDto = {
  id: 'default-company',
  nombreEmpresa: 'Payé Digital',
  cifNif: '',
  direccion: '',
  telefono: '',
  email: '',
  web: 'www.payedigital.com',
  iban: '',
  logoUrl: DOCUMENTACION_ASSETS.logoHorizontalDark,
  textoLegalTitulo: DEFAULT_LEGAL_NOTICE_TITLE,
  textoLegalContenido: DEFAULT_LEGAL_NOTICE_TEXT,
  rowVersion: 1,
}

function DocumentacionPage() {
  const searchParams = Route.useSearch() as {
    tab?: 'presupuestos' | 'contratos'
    clienteId?: string
  }
  const queryClient = useQueryClient()

  const [activeTipo, setActiveTipo] = useState<TipoDocumentoComercial>(
    searchParams.tab === 'contratos' ? 1 : 2
  )
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedClienteFilter, setSelectedClienteFilter] = useState<string>(
    searchParams.clienteId ?? ''
  )

  // Estado del modo Editor en vivo (Canva-style)
  const [isEditorOpen, setIsEditorOpen] = useState(false)
  const [editingDocument, setEditingDocument] =
    useState<DocumentoComercialDetailDto | null>(null)
  const [isCompanyDialogOpen, setIsCompanyDialogOpen] = useState(false)

  // Consultas a la API real/mock
  const { data: clientesResult } = useClientesQuery({ page: 1, pageSize: 200 })
  const clientes = useMemo(() => clientesResult?.items ?? [], [clientesResult])

  const { data: companySettings } = useQuery({
    queryKey: ['documentacion', 'company-settings'],
    queryFn: () => api.documentacion.getCompanySettings(),
  })

  const effectiveCompany = companySettings ?? FALLBACK_COMPANY

  const { data: documentosResult, isLoading } = useQuery({
    queryKey: [
      'documentacion',
      'list',
      activeTipo,
      searchQuery,
      selectedClienteFilter,
    ],
    queryFn: () =>
      api.documentacion.getDocumentos({
        tipo: activeTipo,
        search: searchQuery.trim() || undefined,
        clienteId: selectedClienteFilter || undefined,
        page: 1,
        pageSize: 50,
      }),
  })

  const documentos = documentosResult?.items ?? []

  // Mutaciones
  const saveCompanyMutation = useMutation({
    mutationFn: (dto: UpdateDocumentacionCompanySettingsRequest) =>
      api.documentacion.updateCompanySettings(dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['documentacion', 'company-settings'] })
      toast.success('Configuración de empresa y pie legal actualizada')
    },
    onError: (err: unknown) => {
      toast.error(
        err instanceof Error ? err.message : 'No se pudo guardar la configuración'
      )
    },
  })

  const duplicateMutation = useMutation({
    mutationFn: (id: string) => api.documentacion.duplicateDocumento(id),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['documentacion', 'list'] })
      toast.success(`Documento duplicado como ${created.numeroDocumento}`)
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Error al duplicar documento')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.documentacion.deleteDocumento(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['documentacion', 'list'] })
      toast.success('Documento eliminado correctamente')
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Error al eliminar documento')
    },
  })

  const handleOpenNewDocument = (tipo: TipoDocumentoComercial) => {
    setActiveTipo(tipo)
    setEditingDocument(null)
    setIsEditorOpen(true)
  }

  const handleOpenEditDocument = async (id: string) => {
    try {
      const detail = await api.documentacion.getDocumentoById(id)
      if (!detail) {
        toast.error('No se encontró el documento solicitado.')
        return
      }
      setActiveTipo(detail.tipo)
      setEditingDocument(detail)
      setIsEditorOpen(true)
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Error al abrir el documento')
    }
  }

  const handleSaveDocument = async (
    dto: SaveDocumentoComercialRequest,
    existingId?: string
  ): Promise<DocumentoComercialDetailDto | null> => {
    if (existingId) {
      const updated = await api.documentacion.updateDocumento(existingId, dto)
      setEditingDocument(updated)
      queryClient.invalidateQueries({ queryKey: ['documentacion', 'list'] })
      toast.success(`Documento ${updated.numeroDocumento} guardado correctamente`)
      return updated
    } else {
      const created = await api.documentacion.createDocumento(dto)
      setEditingDocument(created)
      queryClient.invalidateQueries({ queryKey: ['documentacion', 'list'] })
      toast.success(`Documento ${created.numeroDocumento} creado correctamente`)
      return created
    }
  }

  const handleExportPdf = async (id: string, numeroDocumento?: string) => {
    const toastId = toast.loading(
      `Generando PDF vectorial A4 (${numeroDocumento ?? 'documento'})...`
    )
    try {
      await api.documentacion.exportDocumentoPdf(id, numeroDocumento)
      toast.success(`PDF ${numeroDocumento ?? ''} generado y descargado`, {
        id: toastId,
      })
    } catch (err: unknown) {
      toast.error(
        err instanceof Error
          ? err.message
          : 'No se pudo exportar el PDF en el servidor.',
        { id: toastId }
      )
    }
  }

  return (
    <>
      <Header className='print:hidden'>
        <div className='me-auto flex items-center gap-2.5 min-w-0'>
          <FileSignature className='h-4 w-4 text-amber-500 shrink-0' />
          <span className='text-foreground text-sm font-semibold tracking-tight'>
            Documentación · Contratos y Presupuestos
          </span>
        </div>
        <Search />
        <ThemeSwitch />
        <ProfileDropdown />
      </Header>

      <Main>
        {isEditorOpen ? (
          <DocumentoEditorView
            initialDocument={editingDocument}
            defaultTipo={activeTipo}
            clientes={clientes}
            company={effectiveCompany}
            onSave={handleSaveDocument}
            onExportPdf={handleExportPdf}
            onReloadLatest={(id) => api.documentacion.getDocumentoById(id)}
            onBack={() => setIsEditorOpen(false)}
            onOpenCompanySettings={() => setIsCompanyDialogOpen(true)}
          />
        ) : (
          <div className='flex flex-1 flex-col gap-4'>
            {/* BARRA SUPERIOR: PESTAÑAS CONTRATOS / PRESUPUESTOS + ACCIONES */}
            <div className='flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border/80 bg-card px-4 py-3 shadow-2xs'>
              <div className='flex flex-wrap items-center gap-3'>
                <div
                  role='tablist'
                  aria-label='Tipo de documento'
                  className='inline-flex items-center rounded-lg bg-muted/70 p-0.5 border border-border/60'
                >
                  <button
                    type='button'
                    role='tab'
                    aria-selected={activeTipo === 2}
                    onClick={() => setActiveTipo(2)}
                    className={`inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-md text-xs font-semibold transition-all ${
                      activeTipo === 2
                        ? 'bg-background text-foreground shadow-xs'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    <FileText className='h-3.5 w-3.5 text-amber-500' />
                    <span>Presupuestos</span>
                  </button>

                  <button
                    type='button'
                    role='tab'
                    aria-selected={activeTipo === 1}
                    onClick={() => setActiveTipo(1)}
                    className={`inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-md text-xs font-semibold transition-all ${
                      activeTipo === 1
                        ? 'bg-background text-foreground shadow-xs'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    <FileCheck2 className='h-3.5 w-3.5 text-amber-500' />
                    <span>Contratos</span>
                  </button>
                </div>

                <span className='text-xs text-muted-foreground hidden md:inline'>
                  Plantillas A4 con edición en vivo e identidad visual Payé Digital
                </span>
              </div>

              <div className='flex flex-wrap items-center gap-2'>
                <Button
                  type='button'
                  variant='outline'
                  size='sm'
                  asChild
                  className='h-8 text-xs gap-1.5'
                >
                  <a href='/documentacion/preview'>
                    <Eye className='h-3.5 w-3.5 text-amber-500' />
                    <span>Vista previa de verificación A4</span>
                  </a>
                </Button>

                <Button
                  type='button'
                  variant='outline'
                  size='sm'
                  className='h-8 text-xs gap-1.5'
                  onClick={() => setIsCompanyDialogOpen(true)}
                >
                  <Building2 className='h-3.5 w-3.5' />
                  <span>Datos de Empresa y Pie Legal</span>
                </Button>

                <Button
                  type='button'
                  size='sm'
                  className='h-8 text-xs gap-1.5'
                  onClick={() => handleOpenNewDocument(activeTipo)}
                  data-testid='btn-nuevo-documento'
                >
                  <Plus className='h-3.5 w-3.5' />
                  <span>
                    {activeTipo === 1 ? 'Nuevo Contrato' : 'Nuevo Presupuesto'}
                  </span>
                </Button>
              </div>
            </div>

            {/* BARRA DE FILTROS: BUSCADOR + FILTRO POR CLIENTE */}
            <Card className='border border-border/80 rounded-xl p-3.5 shadow-2xs'>
              <div className='grid grid-cols-1 md:grid-cols-12 gap-3 items-center'>
                <div className='md:col-span-8 relative'>
                  <SearchIcon className='absolute left-3 top-2.5 h-3.5 w-3.5 text-muted-foreground' />
                  <Input
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder='Buscar por número (PRES-2026-001), título o cliente...'
                    className='pl-8 h-8 text-xs'
                  />
                </div>

                <div className='md:col-span-4'>
                  <select
                    aria-label='Filtrar por cliente'
                    value={selectedClienteFilter}
                    onChange={(e) => setSelectedClienteFilter(e.target.value)}
                    className='w-full h-8 rounded-md border border-input bg-background px-2.5 text-xs'
                  >
                    <option value=''>Todos los clientes</option>
                    {clientes.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.nombreFiscal} ({c.cifNif})
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </Card>

            {/* TABLA DE LISTADO DE CONTRATOS / PRESUPUESTOS */}
            <Card className='border border-border/80 rounded-xl overflow-hidden shadow-2xs'>
              {isLoading ? (
                <div className='p-10 text-center text-xs text-muted-foreground'>
                  Cargando documentos...
                </div>
              ) : documentos.length === 0 ? (
                <div className='p-12 text-center space-y-3'>
                  <p className='text-sm font-semibold text-foreground'>
                    No hay {activeTipo === 1 ? 'contratos' : 'presupuestos'} que coincidan
                    con el filtro
                  </p>
                  <p className='text-xs text-muted-foreground max-w-md mx-auto'>
                    Crea un documento nuevo a partir de la plantilla A4 predefinida con
                    todas las secciones y cláusulas estándar ya colocadas.
                  </p>
                  <Button
                    type='button'
                    size='sm'
                    className='h-8 text-xs gap-1.5'
                    onClick={() => handleOpenNewDocument(activeTipo)}
                  >
                    <Plus className='h-3.5 w-3.5' />
                    <span>
                      Crear primer {activeTipo === 1 ? 'Contrato' : 'Presupuesto'}
                    </span>
                  </Button>
                </div>
              ) : (
                <div className='overflow-x-auto'>
                  <table className='w-full text-left border-collapse text-xs'>
                    <thead>
                      <tr className='border-b border-border/70 bg-muted/30 text-[11px] uppercase tracking-wider text-muted-foreground'>
                        <th className='py-2.5 px-4 font-semibold'>Número</th>
                        <th className='py-2.5 px-4 font-semibold'>Cliente</th>
                        <th className='py-2.5 px-4 font-semibold'>Título / Proyecto</th>
                        <th className='py-2.5 px-4 font-semibold'>Emisión</th>
                        {activeTipo === 2 && (
                          <th className='py-2.5 px-4 font-semibold'>Validez</th>
                        )}
                        <th className='py-2.5 px-4 font-semibold text-right'>
                          Base Imponible
                        </th>
                        <th className='py-2.5 px-4 font-semibold text-right'>
                          Total (IVA incl.)
                        </th>
                        <th className='py-2.5 px-4 font-semibold'>Versión</th>
                        <th className='py-2.5 px-4 font-semibold text-right'>
                          Acciones
                        </th>
                      </tr>
                    </thead>
                    <tbody className='divide-y divide-border/60'>
                      {documentos.map((doc) => (
                        <tr
                          key={doc.id}
                          className='hover:bg-muted/30 transition-colors'
                        >
                          <td className='py-3 px-4 font-mono font-semibold text-foreground'>
                            {doc.numeroDocumento}
                          </td>
                          <td className='py-3 px-4'>
                            <div className='font-medium text-foreground'>
                              {doc.clienteNombre}
                            </div>
                            <div className='text-[11px] text-muted-foreground font-mono'>
                              {doc.clienteCifNif}
                            </div>
                          </td>
                          <td className='py-3 px-4'>
                            <div className='font-medium text-foreground'>
                              {doc.titulo}
                            </div>
                            {doc.subtitulo && (
                              <div className='text-[11px] text-muted-foreground italic'>
                                {doc.subtitulo}
                              </div>
                            )}
                          </td>
                          <td className='py-3 px-4 font-mono'>
                            {formatDateEs(doc.fechaEmision)}
                          </td>
                          {activeTipo === 2 && (
                            <td className='py-3 px-4 font-mono text-muted-foreground'>
                              {formatDateEs(doc.fechaValidez)}
                            </td>
                          )}
                          <td className='py-3 px-4 text-right font-mono'>
                            {formatCurrencyEs(doc.baseImponible)}
                          </td>
                          <td className='py-3 px-4 text-right font-mono font-semibold text-foreground'>
                            {formatCurrencyEs(doc.totalGeneral)}
                          </td>
                          <td className='py-3 px-4'>
                            <Badge variant='outline' className='font-mono text-[10px]'>
                              v{doc.rowVersion}
                            </Badge>
                          </td>
                          <td className='py-3 px-4 text-right'>
                            <div className='inline-flex items-center justify-end gap-1'>
                              <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='h-7 px-2 text-xs gap-1'
                                title='Editar documento en plantilla A4'
                                onClick={() => void handleOpenEditDocument(doc.id)}
                              >
                                <Pencil className='h-3.5 w-3.5' />
                                <span className='hidden lg:inline'>Editar</span>
                              </Button>

                              <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='h-7 px-2 text-xs gap-1'
                                title='Descargar PDF A4 generado en el servidor'
                                onClick={() =>
                                  void handleExportPdf(doc.id, doc.numeroDocumento)
                                }
                              >
                                <Download className='h-3.5 w-3.5' />
                                <span className='hidden lg:inline'>Descargar PDF</span>
                              </Button>

                              <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='h-7 px-2 text-xs gap-1'
                                title='Duplicar con nueva numeración correlativa'
                                onClick={() => duplicateMutation.mutate(doc.id)}
                              >
                                <Copy className='h-3.5 w-3.5' />
                              </Button>

                              <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='h-7 px-2 text-xs text-destructive hover:text-destructive'
                                title='Eliminar documento'
                                onClick={() => {
                                  if (
                                    window.confirm(
                                      `¿Seguro que deseas eliminar ${doc.numeroDocumento}?`
                                    )
                                  ) {
                                    deleteMutation.mutate(doc.id)
                                  }
                                }}
                              >
                                <Trash2 className='h-3.5 w-3.5' />
                              </Button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </Card>
          </div>
        )}

        <CompanySettingsDialog
          open={isCompanyDialogOpen}
          onOpenChange={setIsCompanyDialogOpen}
          settings={effectiveCompany}
          isSaving={saveCompanyMutation.isPending}
          onSave={async (dto) => {
            await saveCompanyMutation.mutateAsync(dto)
          }}
        />
      </Main>
    </>
  )
}
