import React, { useCallback, useEffect, useMemo, useState } from 'react'
import {
  ArrowLeft,
  Save,
  Download,
  Plus,
  Trash2,
  Eye,
  EyeOff,
  ChevronUp,
  ChevronDown,
  RotateCcw,
  AlertTriangle,
  Building2,
  Layers,
  Calculator,
  UserCheck,
  Search as SearchIcon,
  SlidersHorizontal,
  ZoomIn,
  RefreshCw,
  Sparkles,
  Wand2,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Switch } from '@/components/ui/switch'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
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
import { toast } from 'sonner'
import {
  ConcurrencyConflictError,
  type ClienteDto,
  type DocumentacionCompanySettingsDto,
  type DocumentoAiBorradorResponseDto,
  type DocumentoBloqueDto,
  type DocumentoComercialDetailDto,
  type DocumentoLineaInputDto,
  type SaveDocumentoComercialRequest,
  type TipoDocumentoComercial,
} from '@/api/types'
import { api } from '@/api'
import {
  ALLOWED_VAT_RATES,
  DEFAULT_LINE_VAT_RATE,
  calculateDocumentoTotals,
} from '../utils/totals-calculator'
import {
  PRESUPUESTO_DEFAULT_LINES,
  createInitialBlocksForType,
  getOriginalTemplateBlock,
} from '../config/default-templates'
import {
  DocumentoA4Renderer,
  formatCurrencyEs,
  type DocumentoA4LayoutOptions,
} from './documento-a4-renderer'
import { LightRichTextEditor } from './light-rich-text-editor'
import { OrigenDatosTab } from './origen-datos-tab'
import {
  decodeCorruptedHtmlEntities,
  refineRegeneratedBlockContent,
} from '../utils/html-sanitizer'

export interface DocumentoEditorViewProps {
  initialDocument?: DocumentoComercialDetailDto | null
  defaultTipo: TipoDocumentoComercial
  clientes: ClienteDto[]
  company: DocumentacionCompanySettingsDto
  isSaving?: boolean
  onSave: (
    dto: SaveDocumentoComercialRequest,
    existingId?: string
  ) => Promise<DocumentoComercialDetailDto | null>
  onExportPdf?: (id: string, fileName?: string) => Promise<void>
  onReloadLatest?: (id: string) => Promise<DocumentoComercialDetailDto | null>
  onBack: () => void
  onOpenCompanySettings: () => void
}

type EditorTab = 'datos' | 'lineas' | 'bloques' | 'origen'
type ZoomLevel = 'doc-zoom-70' | 'doc-zoom-85' | 'doc-zoom-100'

export const DocumentoEditorView: React.FC<DocumentoEditorViewProps> = ({
  initialDocument,
  defaultTipo,
  clientes,
  company,
  isSaving = false,
  onSave,
  onExportPdf,
  onReloadLatest,
  onBack,
  onOpenCompanySettings,
}) => {
  const tipo: TipoDocumentoComercial = initialDocument?.tipo ?? defaultTipo
  const isContrato = tipo === 1

  const todayIso = useMemo(() => new Date().toISOString().slice(0, 10), [])
  const defaultValidUntilIso = useMemo(() => {
    const d = new Date()
    d.setDate(d.getDate() + 30)
    return d.toISOString().slice(0, 10)
  }, [])

  // Estado de campos variables del documento
  const [clienteId, setClienteId] = useState<string>(
    initialDocument?.clienteId ?? clientes[0]?.id ?? ''
  )
  const [clientSearch, setClientSearch] = useState('')
  const [clienteNombre, setClienteNombre] = useState<string>(
    initialDocument?.clienteNombre ?? clientes[0]?.nombreFiscal ?? 'Alpe'
  )
  const [clienteCifNif, setClienteCifNif] = useState<string>(
    initialDocument?.clienteCifNif ?? clientes[0]?.cifNif ?? 'B12345678'
  )
  const [clienteDireccion, setClienteDireccion] = useState<string>(
    initialDocument?.clienteDireccion ??
      ([
        clientes[0]?.direccion,
        clientes[0]?.codigoPostal,
        clientes[0]?.ciudad,
        clientes[0]?.provincia,
      ]
        .filter(Boolean)
        .join(', ') ||
        'A Coruña')
  )
  const [clienteEmail, setClienteEmail] = useState<string>(
    initialDocument?.clienteEmail ?? clientes[0]?.email ?? ''
  )
  const [clienteTelefono, setClienteTelefono] = useState<string>(
    initialDocument?.clienteTelefono ?? clientes[0]?.telefono ?? ''
  )

  const [titulo, setTitulo] = useState<string>(
    initialDocument?.titulo ??
      (isContrato
        ? 'Contrato de diseño, desarrollo web y mantenimiento'
        : 'Web para aumentar venta online')
  )
  const [subtitulo, setSubtitulo] = useState<string>(
    initialDocument?.subtitulo ?? 'Diseño Web'
  )
  const [categoria, setCategoria] = useState<string>(
    initialDocument?.categoria ?? 'Diseño Web'
  )
  const [fechaEmision, setFechaEmision] = useState<string>(
    initialDocument?.fechaEmision?.slice(0, 10) ?? todayIso
  )
  const [fechaValidez, setFechaValidez] = useState<string>(
    initialDocument?.fechaValidez?.slice(0, 10) ?? defaultValidUntilIso
  )

  // Campos específicos de Contratos
  const [objetoContrato, setObjetoContrato] = useState<string>(
    initialDocument?.objetoContrato ??
      'Diseño, desarrollo e implantación de plataforma web corporativa y e-commerce.'
  )
  const [duracionContrato, setDuracionContrato] = useState<string>(
    initialDocument?.duracionContrato ?? '12 meses (prorrogable anualmente)'
  )
  const [precioTextoContrato, setPrecioTextoContrato] = useState<string>(
    initialDocument?.precioTextoContrato ?? '1.900,00 € + IVA (según anexo económico)'
  )
  const [formaPagoContrato, setFormaPagoContrato] = useState<string>(
    initialDocument?.formaPagoContrato ??
      '50% a la firma del contrato y 50% a la entrega y publicación.'
  )
  const [lugarFirma, setLugarFirma] = useState<string>(
    initialDocument?.lugarFirma ?? 'A Coruña'
  )

  // Bloques de secciones y cláusulas
  const [bloques, setBloques] = useState<DocumentoBloqueDto[]>(() => {
    if (initialDocument && initialDocument.bloques.length > 0) {
      return initialDocument.bloques.map((b: DocumentoBloqueDto) => ({ ...b }))
    }
    return createInitialBlocksForType(tipo)
  })

  // Líneas de concepto con IVA por defecto 21%
  const [lineas, setLineas] = useState<DocumentoLineaInputDto[]>(() => {
    if (initialDocument && initialDocument.lineas.length > 0) {
      return initialDocument.lineas.map((l) => ({
        descripcion: l.descripcion,
        cantidad: l.cantidad,
        precioUnitario: l.precioUnitario,
        descuentoPorcentaje: l.descuentoPorcentaje,
        ivaPorcentaje: l.ivaPorcentaje ?? DEFAULT_LINE_VAT_RATE,
      }))
    }
    return PRESUPUESTO_DEFAULT_LINES.map((l) => ({ ...l }))
  })

  // Opciones adicionales de layout (desactivadas por defecto salvo el bloque de desglose en presupuestos)
  const [layoutOptions, setLayoutOptions] = useState<DocumentoA4LayoutOptions>({
    mostrarCabeceraEmpresa: false,
    mostrarCodigoEnPortada: false,
    mostrarCenefaPortada: false,
    mostrarDatosFiscalesPie: false,
    evitarPartirBloques: true,
  })

  const [currentDocId, setCurrentDocId] = useState<string | undefined>(
    initialDocument?.id
  )
  const [currentNumeroDocumento, setCurrentNumeroDocumento] = useState<
    string | undefined
  >(initialDocument?.numeroDocumento)
  const [isExportingPdf, setIsExportingPdf] = useState(false)
  const [rowVersion, setRowVersion] = useState<number>(initialDocument?.rowVersion ?? 1)
  const [activeTab, setActiveTab] = useState<EditorTab>('bloques')
  const [selectedBlockId, setSelectedBlockId] = useState<string | null>(
    () => bloques[0]?.id ?? null
  )
  const [isStructureCollapsed, setIsStructureCollapsed] = useState<boolean>(false)
  const [zoomLevel, setZoomLevel] = useState<ZoomLevel>('doc-zoom-85')
  const [isDirty, setIsDirty] = useState(false)
  const [showExitConfirm, setShowExitConfirm] = useState(false)
  const [concurrencyConflictMessage, setConcurrencyConflictMessage] = useState<
    string | null
  >(null)

  // Fase 5: Estado de Origen de Datos e IA (Gemini)
  const [textoOrigenDatos, setTextoOrigenDatos] = useState<string>(
    initialDocument?.textoOrigenDatos ?? ''
  )
  const [nombreArchivoOrigen, setNombreArchivoOrigen] = useState<string>(
    initialDocument?.nombreArchivoOrigen ?? ''
  )
  const [tratoCliente, setTratoCliente] = useState<string>(
    'Tuteo cercano y profesional (tú)'
  )
  const [instruccionesExtra, setInstruccionesExtra] = useState<string>('')
  const [blockExtraInstruction, setBlockExtraInstruction] = useState<string>('')
  const [isRegeneratingBlock, setIsRegeneratingBlock] = useState<boolean>(false)
  const [undoAiSnapshot, setUndoAiSnapshot] = useState<{
    titulo: string
    subtitulo: string
    categoria: string
    bloques: DocumentoBloqueDto[]
    lineas: DocumentoLineaInputDto[]
  } | null>(null)

  // Aviso nativo del navegador si hay cambios sin guardar
  useEffect(() => {
    if (!isDirty) return
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [isDirty])

  const markDirty = () => {
    if (!isDirty) setIsDirty(true)
  }

  // Filtrado de clientes en el selector con búsqueda
  const filteredClientes = useMemo(() => {
    const q = clientSearch.trim().toLowerCase()
    if (!q) return clientes
    return clientes.filter(
      (c) =>
        c.nombreFiscal.toLowerCase().includes(q) ||
        c.cifNif.toLowerCase().includes(q) ||
        (c.email && c.email.toLowerCase().includes(q))
    )
  }, [clientes, clientSearch])

  const handleSelectCliente = (id: string) => {
    setClienteId(id)
    const found = clientes.find((c) => c.id === id)
    if (!found) return
    setClienteNombre(found.nombreFiscal)
    setClienteCifNif(found.cifNif)
    setClienteDireccion(
      [found.direccion, found.codigoPostal, found.ciudad, found.provincia]
        .filter(Boolean)
        .join(', ') || 'Dirección fiscal registrada'
    )
    setClienteEmail(found.email || '')
    setClienteTelefono(found.telefono || '')
    markDirty()
    toast.success(`Datos fiscales de ${found.nombreFiscal} rellenados en la plantilla`)
  }

  // Cálculos en tiempo real idénticos al servidor
  const calculatedTotals = useMemo(() => calculateDocumentoTotals(lineas), [lineas])

  // Gestión de líneas de presupuesto
  const handleAddLine = () => {
    setLineas((prev) => [
      ...prev,
      {
        descripcion: 'Nuevo concepto o entregable',
        cantidad: 1,
        precioUnitario: 100,
        descuentoPorcentaje: 0,
        ivaPorcentaje: DEFAULT_LINE_VAT_RATE, // 21% por defecto
      },
    ])
    markDirty()
  }

  const handleUpdateLine = (
    index: number,
    field: keyof DocumentoLineaInputDto,
    value: string | number
  ) => {
    setLineas((prev) =>
      prev.map((item, idx) => (idx === index ? { ...item, [field]: value } : item))
    )
    markDirty()
  }

  const handleRemoveLine = (index: number) => {
    if (lineas.length <= 1) {
      toast.warning('El documento debe conservar al menos una línea económica.')
      return
    }
    setLineas((prev) => prev.filter((_, idx) => idx !== index))
    markDirty()
  }

  // Gestión de bloques (Canva-style)
  const sortedBlocks = useMemo(
    () => [...bloques].sort((a, b) => a.orden - b.orden),
    [bloques]
  )

  const selectedBlock = useMemo(
    () => sortedBlocks.find((b) => b.id === selectedBlockId) ?? sortedBlocks[0] ?? null,
    [sortedBlocks, selectedBlockId]
  )

  const selectedOriginalTemplateBlock = useMemo(
    () =>
      selectedBlock
        ? getOriginalTemplateBlock(tipo, selectedBlock.templateBlockId ?? selectedBlock.id)
        : undefined,
    [selectedBlock, tipo]
  )

  const handleUpdateBlock = (blockId: string, patch: Partial<DocumentoBloqueDto>) => {
    setBloques((prev) =>
      prev.map((b) => (b.id === blockId ? { ...b, ...patch } : b))
    )
    markDirty()
  }

  const handleMoveBlock = (blockId: string, direction: -1 | 1) => {
    const ordered = [...bloques].sort((a, b) => a.orden - b.orden)
    const idx = ordered.findIndex((b) => b.id === blockId)
    const swapIdx = idx + direction
    if (idx < 0 || swapIdx < 0 || swapIdx >= ordered.length) return

    const temp = ordered[idx]
    ordered[idx] = ordered[swapIdx]
    ordered[swapIdx] = temp

    const renumbered = ordered.map((b, i) => ({ ...b, orden: i + 1 }))
    setBloques(renumbered)
    markDirty()
  }

  const handleAddCustomBlock = (
    tipoBloque: 'pill-section' | 'highlight-cost-box' | 'timeline-steps' = 'pill-section'
  ) => {
    const nextOrder = bloques.length + 1
    const newId = `custom-blk-${Date.now()}`

    let newBlock: DocumentoBloqueDto

    if (tipoBloque === 'highlight-cost-box') {
      newBlock = {
        id: newId,
        templateBlockId: null,
        tipoBloque: 'highlight-cost-box',
        numeroPastilla: null,
        titulo: 'Resumen de costes.',
        contenidoHtml: `<ul>
  <li><strong>Web - Shopify</strong>
    <ul>
      <li>Creación y configuración inicial de Shopify</li>
      <li>Subida de 50 artículos</li>
      <li>Creación de imágenes publicitarias para la web</li>
      <li>Implementación de SEO en base al conocimiento recogido de las redes sociales.</li>
    </ul>
  </li>
  <li><strong>Impresión automática</strong>
    <ul>
      <li>Servicio web que imprime el pedido y la etiqueta de forma instantánea y automática</li>
    </ul>
  </li>
</ul>`,
        visible: true,
        orden: nextOrder,
        esPersonalizado: true,
      }
      toast.success('Caja destacada de costes añadida (con fondo ámbar y estrella)')
    } else if (tipoBloque === 'timeline-steps') {
      newBlock = {
        id: newId,
        templateBlockId: null,
        tipoBloque: 'timeline-steps',
        numeroPastilla: `${nextOrder}.`,
        titulo: 'FASES Y CRONOGRAMA',
        contenidoHtml: `<p class="doc-timeline-lead">Planificación estimada de ejecución y entregas:</p>
<div class="doc-timeline-step">
  <strong>Fase 1 · Configuración y diseño inicial</strong>
  <p>Reunión de alineación, entrega de accesos y diseño de la estructura base.</p>
</div>
<div class="doc-timeline-step">
  <strong>Fase 2 · Desarrollo e integración</strong>
  <p>Conexión de catálogo, pasarela de pago y automatización de procesos.</p>
</div>`,
        visible: true,
        orden: nextOrder,
        esPersonalizado: true,
      }
      toast.success('Línea temporal de fases añadida')
    } else {
      newBlock = {
        id: newId,
        templateBlockId: null,
        tipoBloque: 'pill-section',
        numeroPastilla: `${nextOrder}.`,
        titulo: 'NUEVA CLÁUSULA O SECCIÓN',
        contenidoHtml:
          '<p>Escribe aquí las condiciones o detalles específicos para este cliente.</p>',
        visible: true,
        orden: nextOrder,
        esPersonalizado: true,
      }
      toast.success('Sección estándar añadida')
    }

    setBloques((prev) => [...prev, newBlock])
    setSelectedBlockId(newId)
    setActiveTab('bloques')
    markDirty()
  }

  const handleDeleteBlock = (blockId: string) => {
    if (bloques.length <= 1) {
      toast.warning('El documento debe tener al menos un bloque.')
      return
    }
    setBloques((prev) =>
      prev
        .filter((b) => b.id !== blockId)
        .map((b, idx) => ({ ...b, orden: idx + 1 }))
    )
    if (selectedBlockId === blockId) {
      setSelectedBlockId(bloques.find((b) => b.id !== blockId)?.id ?? null)
    }
    markDirty()
  }

  const handleRestoreSingleBlock = (blockId: string) => {
    const target = bloques.find((b) => b.id === blockId)
    if (!target) return
    const orig = getOriginalTemplateBlock(tipo, target.templateBlockId ?? target.id)
    if (!orig) return
    handleUpdateBlock(blockId, {
      titulo: orig.titulo,
      numeroPastilla: orig.numeroPastilla,
      contenidoHtml: orig.contenidoHtml,
    })
    toast.success(`Bloque "${orig.titulo}" restablecido al texto original`)
  }

  const handleRestoreAllTemplateBlocks = () => {
    const fresh = createInitialBlocksForType(tipo)
    setBloques(fresh)
    setSelectedBlockId(fresh[0]?.id ?? null)
    markDirty()
    toast.success('Todas las secciones se han restablecido a la plantilla estándar')
  }

  const pricingTableBlock = useMemo(
    () => bloques.find((b) => b.tipoBloque === 'pricing-table'),
    [bloques]
  )

  const handleTogglePricingTableVisibility = (checked: boolean) => {
    if (pricingTableBlock) {
      handleUpdateBlock(pricingTableBlock.id, { visible: checked })
    }
  }

  // Fase 5: Aplicar borrador generado por IA (Reemplazar o Añadir al final) con opción de Deshacer
  const handleApplyAiDraft = (
    draft: DocumentoAiBorradorResponseDto,
    mode: 'replace' | 'append'
  ) => {
    setUndoAiSnapshot({
      titulo,
      subtitulo,
      categoria,
      bloques: sortedBlocks.map((b) => ({ ...b })),
      lineas: lineas.map((l) => ({ ...l })),
    })

    const generatedBlocks: DocumentoBloqueDto[] = draft.bloques.map((b, idx) => ({
      id: `ai-blk-${Date.now()}-${idx + 1}`,
      templateBlockId: null,
      tipoBloque: 'pill-section',
      numeroPastilla: `${idx + 1}.`,
      titulo: decodeCorruptedHtmlEntities(b.titulo).toUpperCase(),
      contenidoHtml: refineRegeneratedBlockContent({
        bloqueTitulo: b.titulo,
        bloqueContenidoActual: '',
        returnedHtml: b.contenido,
        modo: 'regenerar',
        instruccionesExtra: null,
        textoOrigen: textoOrigenDatos.trim() || null,
      }),
      visible: true,
      orden: idx + 1,
      esPersonalizado: true,
    }))

    if (mode === 'replace') {
      if (draft.titulo?.trim()) setTitulo(draft.titulo.trim())
      if (draft.subtitulo?.trim()) {
        setSubtitulo(draft.subtitulo.trim())
        setCategoria(draft.subtitulo.trim())
      }

      if (isContrato) {
        // En contratos se conservan las cláusulas legales estándar de la plantilla tal como están
        const legalClauses = sortedBlocks.filter(
          (b) =>
            !b.esPersonalizado &&
            b.id !== 'cont-expositivo' &&
            b.id !== 'cont-clausula-1'
        )
        const combined = [...generatedBlocks, ...legalClauses].map((b, i) => ({
          ...b,
          numeroPastilla:
            b.tipoBloque === 'pill-section' ? `${i + 1}.` : b.numeroPastilla,
          orden: i + 1,
        }))
        setBloques(combined)
        setSelectedBlockId(combined[0]?.id ?? null)
      } else {
        // En presupuestos se conserva el bloque de tabla económica al final
        const pricing = sortedBlocks.filter((b) => b.tipoBloque === 'pricing-table')
        const combined = [...generatedBlocks, ...pricing].map((b, i) => ({
          ...b,
          numeroPastilla:
            b.tipoBloque === 'pill-section' ? `${i + 1}.` : b.numeroPastilla,
          orden: i + 1,
        }))
        setBloques(combined)
        setSelectedBlockId(combined[0]?.id ?? null)
      }

      if (!isContrato && draft.lineasSugeridas && draft.lineasSugeridas.length > 0) {
        setLineas(
          draft.lineasSugeridas.map((ls) => ({
            descripcion: ls.descripcion,
            cantidad: ls.cantidad > 0 ? ls.cantidad : 1,
            precioUnitario: 0, // Nunca se inventan importes
            descuentoPorcentaje: 0,
            ivaPorcentaje: DEFAULT_LINE_VAT_RATE,
          }))
        )
      }
    } else {
      // Añadir al final (antes del bloque de tabla de precios o firmas si existen)
      const tailSpecial = sortedBlocks.filter(
        (b) => b.tipoBloque === 'pricing-table' || b.tipoBloque === 'signatures'
      )
      const regular = sortedBlocks.filter(
        (b) => b.tipoBloque !== 'pricing-table' && b.tipoBloque !== 'signatures'
      )
      const combined = [...regular, ...generatedBlocks, ...tailSpecial].map((b, i) => ({
        ...b,
        numeroPastilla:
          b.tipoBloque === 'pill-section' ? `${i + 1}.` : b.numeroPastilla,
        orden: i + 1,
      }))
      setBloques(combined)
      setSelectedBlockId(generatedBlocks[0]?.id ?? combined[0]?.id ?? null)

      if (!isContrato && draft.lineasSugeridas && draft.lineasSugeridas.length > 0) {
        setLineas((prev) => [
          ...prev,
          ...draft.lineasSugeridas.map((ls) => ({
            descripcion: ls.descripcion,
            cantidad: ls.cantidad > 0 ? ls.cantidad : 1,
            precioUnitario: 0,
            descuentoPorcentaje: 0,
            ivaPorcentaje: DEFAULT_LINE_VAT_RATE,
          })),
        ])
      }
    }

    markDirty()
    toast.success(
      mode === 'replace'
        ? 'Borrador de IA aplicado al documento. Puedes editar cualquier bloque o deshacer.'
        : 'Bloques de IA añadidos al final del documento.'
    )
  }

  const handleUndoLastAiApply = () => {
    if (!undoAiSnapshot) return
    setTitulo(undoAiSnapshot.titulo)
    setSubtitulo(undoAiSnapshot.subtitulo)
    setCategoria(undoAiSnapshot.categoria)
    setBloques(undoAiSnapshot.bloques)
    setLineas(undoAiSnapshot.lineas)
    setSelectedBlockId(undoAiSnapshot.bloques[0]?.id ?? null)
    setUndoAiSnapshot(null)
    markDirty()
    toast.info('Se ha deshecho la aplicación del borrador de IA.')
  }

  const handleRegenerateSingleBlock = async (
    block: DocumentoBloqueDto,
    modo: 'regenerar' | 'simplificar'
  ) => {
    setIsRegeneratingBlock(true)
    try {
      const updated = await api.documentacion.regenerarBloque({
        tipo,
        documentoId: initialDocument?.id ?? null,
        bloqueTitulo: block.titulo,
        bloqueContenidoActual: block.contenidoHtml,
        modo,
        instruccionesExtra: blockExtraInstruction.trim() || null,
        textoOrigen: textoOrigenDatos.trim() || null,
        tratoCliente,
        eliminarSecretosDetectados: true,
      })
      const refinedHtml = refineRegeneratedBlockContent({
        bloqueTitulo: block.titulo,
        bloqueContenidoActual: block.contenidoHtml,
        returnedHtml: updated.contenido,
        modo,
        instruccionesExtra: blockExtraInstruction.trim() || null,
        textoOrigen: textoOrigenDatos.trim() || null,
      })
      handleUpdateBlock(block.id, {
        contenidoHtml: refinedHtml,
      })
      toast.success(
        modo === 'simplificar'
          ? `Bloque "${block.titulo}" simplificado en lenguaje claro.`
          : `Bloque "${block.titulo}" regenerado con IA.`
      )
    } catch (err: unknown) {
      const msg =
        err instanceof Error
          ? err.message
          : 'No se pudo regenerar el bloque con IA.'
      toast.error(msg)
    } finally {
      setIsRegeneratingBlock(false)
    }
  }

  // Si los clientes se cargan de forma asíncrona tras abrir un documento nuevo y aún no había cliente seleccionado, preseleccionar el primero disponible
  useEffect(() => {
    if (!initialDocument && !clienteId && clientes.length > 0) {
      const first = clientes[0]
      setClienteId(first.id)
      setClienteNombre(first.nombreFiscal)
      setClienteCifNif(first.cifNif)
      setClienteDireccion(
        [first.direccion, first.codigoPostal, first.ciudad, first.provincia]
          .filter(Boolean)
          .join(', ') || 'Dirección fiscal registrada'
      )
      setClienteEmail(first.email || '')
      setClienteTelefono(first.telefono || '')
    }
  }, [initialDocument, clienteId, clientes])

  // Guardado con manejo explícito de conflicto de concurrencia (RowVersion 409)
  const handleSave = useCallback(async (): Promise<DocumentoComercialDetailDto | null> => {
    if (!titulo.trim()) {
      toast.error('El título del documento es obligatorio.')
      setActiveTab('datos')
      return null
    }

    setConcurrencyConflictMessage(null)

    const normalizedClienteId = clienteId.trim() ? clienteId.trim() : null

    const requestDto: SaveDocumentoComercialRequest = {
      tipo,
      clienteId: normalizedClienteId,
      titulo: titulo.trim(),
      subtitulo: subtitulo.trim(),
      categoria: categoria.trim() || subtitulo.trim(),
      clienteNombreOverride: clienteNombre.trim(),
      clienteCifNifOverride: clienteCifNif.trim(),
      clienteDireccionOverride: clienteDireccion.trim(),
      clienteEmailOverride: clienteEmail.trim(),
      clienteTelefonoOverride: clienteTelefono.trim(),
      fechaEmision,
      fechaValidez: isContrato ? null : fechaValidez || null,
      objetoContrato: isContrato ? objetoContrato : null,
      duracionContrato: isContrato ? duracionContrato : null,
      precioTextoContrato: isContrato ? precioTextoContrato : null,
      formaPagoContrato: isContrato ? formaPagoContrato : null,
      lugarFirma: isContrato ? lugarFirma : null,
      bloques: sortedBlocks,
      lineas: lineas.map((l) => ({
        ...l,
        ivaPorcentaje: l.ivaPorcentaje ?? DEFAULT_LINE_VAT_RATE,
      })),
      textoOrigenDatos: textoOrigenDatos.trim() || null,
      nombreArchivoOrigen: nombreArchivoOrigen.trim() || null,
      rowVersion,
    }

    try {
      const saved = await onSave(requestDto, currentDocId)
      if (saved) {
        setCurrentDocId(saved.id)
        setCurrentNumeroDocumento(saved.numeroDocumento)
        setRowVersion(saved.rowVersion)
        setIsDirty(false)
        return saved
      }
      return null
    } catch (err: unknown) {
      if (err instanceof ConcurrencyConflictError) {
        setConcurrencyConflictMessage(err.message)
        toast.error(err.message)
        return null
      }
      const msg =
        err instanceof Error ? err.message : 'No se pudo guardar el documento.'
      if (msg.toLowerCase().includes('modificado') || msg.includes('409')) {
        setConcurrencyConflictMessage(msg)
      }
      toast.error(msg)
      return null
    }
  }, [
    clienteId,
    titulo,
    tipo,
    subtitulo,
    categoria,
    clienteNombre,
    clienteCifNif,
    clienteDireccion,
    clienteEmail,
    clienteTelefono,
    fechaEmision,
    isContrato,
    fechaValidez,
    objetoContrato,
    duracionContrato,
    precioTextoContrato,
    formaPagoContrato,
    lugarFirma,
    sortedBlocks,
    lineas,
    textoOrigenDatos,
    nombreArchivoOrigen,
    rowVersion,
    onSave,
    currentDocId,
  ])

  // Descarga exclusiva del PDF vectorial generado en el servidor (guarda automáticamente si hay cambios pendientes)
  const handleDownloadServerPdf = useCallback(async () => {
    if (!onExportPdf || isExportingPdf) return
    setIsExportingPdf(true)
    try {
      let targetId = currentDocId
      let targetNumero = currentNumeroDocumento
      if (!targetId || isDirty) {
        const saved = await handleSave()
        if (!saved) {
          return
        }
        targetId = saved.id
        targetNumero = saved.numeroDocumento
      }
      await onExportPdf(targetId, targetNumero)
    } finally {
      setIsExportingPdf(false)
    }
  }, [
    onExportPdf,
    isExportingPdf,
    currentDocId,
    currentNumeroDocumento,
    isDirty,
    handleSave,
  ])

  // Intercepta Ctrl+P / Cmd+P en el editor para lanzar siempre el PDF del servidor y nunca imprimir el shell de la aplicación
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'p') {
        e.preventDefault()
        e.stopPropagation()
        void handleDownloadServerPdf()
      }
    }
    window.addEventListener('keydown', handleKeyDown, { capture: true })
    return () =>
      window.removeEventListener('keydown', handleKeyDown, { capture: true })
  }, [handleDownloadServerPdf])

  const handleReloadAfterConflict = async () => {
    if (!initialDocument?.id || !onReloadLatest) return
    const latest = await onReloadLatest(initialDocument.id)
    if (!latest) return
    setTitulo(latest.titulo)
    setSubtitulo(latest.subtitulo)
    setCategoria(latest.categoria)
    setFechaEmision(latest.fechaEmision.slice(0, 10))
    setFechaValidez(latest.fechaValidez?.slice(0, 10) ?? defaultValidUntilIso)
    setObjetoContrato(latest.objetoContrato ?? '')
    setDuracionContrato(latest.duracionContrato ?? '')
    setPrecioTextoContrato(latest.precioTextoContrato ?? '')
    setFormaPagoContrato(latest.formaPagoContrato ?? '')
    setLugarFirma(latest.lugarFirma ?? 'A Coruña')
    setBloques(latest.bloques)
    setLineas(
      latest.lineas.map((l) => ({
        descripcion: l.descripcion,
        cantidad: l.cantidad,
        precioUnitario: l.precioUnitario,
        descuentoPorcentaje: l.descuentoPorcentaje,
        ivaPorcentaje: l.ivaPorcentaje,
      }))
    )
    setRowVersion(latest.rowVersion)
    setTextoOrigenDatos(latest.textoOrigenDatos ?? '')
    setNombreArchivoOrigen(latest.nombreArchivoOrigen ?? '')
    setConcurrencyConflictMessage(null)
    setIsDirty(false)
    toast.success('Se ha cargado la última versión guardada en el servidor.')
  }

  const handleRequestBack = () => {
    if (isDirty) {
      setShowExitConfirm(true)
      return
    }
    onBack()
  }

  const previewDocumentData = useMemo(
    () => ({
      tipo,
      numeroDocumento:
        currentNumeroDocumento ??
        (isContrato ? 'CONT-2026-AUTO' : 'PRES-2026-AUTO'),
      titulo,
      subtitulo,
      categoria: categoria || subtitulo,
      clienteNombre,
      clienteCifNif,
      clienteDireccion,
      clienteEmail,
      clienteTelefono,
      fechaEmision,
      fechaValidez,
      objetoContrato,
      duracionContrato,
      precioTextoContrato,
      formaPagoContrato,
      lugarFirma,
      bloques: sortedBlocks,
      lineas,
      ...layoutOptions,
    }),
    [
      tipo,
      currentNumeroDocumento,
      isContrato,
      titulo,
      subtitulo,
      categoria,
      clienteNombre,
      clienteCifNif,
      clienteDireccion,
      clienteEmail,
      clienteTelefono,
      fechaEmision,
      fechaValidez,
      objetoContrato,
      duracionContrato,
      precioTextoContrato,
      formaPagoContrato,
      lugarFirma,
      sortedBlocks,
      lineas,
      layoutOptions,
    ]
  )

  return (
    <div className='flex flex-col gap-3'>
      {/* BARRA SUPERIOR DEL EDITOR VISUAL TIPO CANVA */}
      <div className='flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border/80 bg-card px-4 py-2.5 shadow-2xs print:hidden'>
        <div className='flex flex-wrap items-center gap-2.5'>
          <Button
            type='button'
            variant='ghost'
            size='sm'
            className='h-8 gap-1.5 text-xs'
            onClick={handleRequestBack}
          >
            <ArrowLeft className='h-3.5 w-3.5' />
            <span>Volver al listado</span>
          </Button>

          <span className='h-4 w-px bg-border' />

          <Badge variant='outline' className='font-mono text-xs'>
            {previewDocumentData.numeroDocumento}
          </Badge>

          <span className='text-sm font-semibold text-foreground truncate max-w-[260px]'>
            {clienteNombre} · {titulo}
          </span>

          {isDirty && (
            <Badge
              variant='outline'
              className='border-amber-500/40 bg-amber-500/10 text-amber-700 dark:text-amber-300 text-[11px] gap-1'
              data-testid='badge-cambios-sin-guardar'
            >
              <AlertTriangle className='h-3 w-3' />
              <span>Cambios sin guardar</span>
            </Badge>
          )}
        </div>

        <div className='flex flex-wrap items-center gap-2'>
          <Button
            type='button'
            variant='outline'
            size='sm'
            className='h-8 text-xs gap-1.5'
            onClick={onOpenCompanySettings}
          >
            <Building2 className='h-3.5 w-3.5' />
            <span className='hidden sm:inline'>Empresa y Pie Legal</span>
          </Button>

          <Button
            type='button'
            variant='outline'
            size='sm'
            className='h-8 text-xs gap-1.5'
            disabled={isSaving || isExportingPdf}
            onClick={() => void handleDownloadServerPdf()}
            data-testid='btn-descargar-pdf'
            title='Guarda automáticamente si hay cambios pendientes y descarga el PDF A4 del servidor (Ctrl+P)'
          >
            <Download className='h-3.5 w-3.5' />
            <span>{isExportingPdf ? 'Generando PDF...' : 'Descargar PDF'}</span>
          </Button>

          <Button
            type='button'
            size='sm'
            className='h-8 text-xs gap-1.5'
            disabled={isSaving || isExportingPdf}
            onClick={() => void handleSave()}
            data-testid='btn-guardar-documento'
          >
            <Save className='h-3.5 w-3.5' />
            <span>{isSaving ? 'Guardando...' : 'Guardar documento'}</span>
          </Button>
        </div>
      </div>

      {/* BANNER DE CONFLICTO DE CONCURRENCIA (RowVersion 409) */}
      {concurrencyConflictMessage && (
        <div
          role='alert'
          data-testid='alert-concurrency-conflict'
          className='flex flex-wrap items-center justify-between gap-3 rounded-xl border border-destructive/50 bg-destructive/10 px-4 py-3 text-xs text-destructive print:hidden'
        >
          <div className='flex items-center gap-2.5'>
            <AlertTriangle className='h-4 w-4 shrink-0' />
            <div>
              <p className='font-semibold'>
                Conflicto de concurrencia detectado (RowVersion)
              </p>
              <p className='text-destructive/90'>{concurrencyConflictMessage}</p>
            </div>
          </div>
          {initialDocument?.id && onReloadLatest && (
            <Button
              type='button'
              variant='outline'
              size='sm'
              className='h-8 text-xs gap-1.5 border-destructive/40 bg-background text-foreground'
              onClick={handleReloadAfterConflict}
            >
              <RefreshCw className='h-3.5 w-3.5' />
              <span>Recargar última versión del servidor</span>
            </Button>
          )}
        </div>
      )}

      {/* SPLIT VIEW: PANEL IZQUIERDO DE CONTROLES (5 COLS) + VISTA PREVIA A4 EN VIVO (7 COLS) */}
      <div className='grid grid-cols-1 xl:grid-cols-12 gap-4 items-start'>
        {/* PANEL IZQUIERDO: VARIABLES, LÍNEAS E IVA, Y GESTOR DE BLOQUES */}
        <Card className='xl:col-span-5 border border-border/80 rounded-xl shadow-2xs overflow-hidden print:hidden'>
          {/* Pestañas del panel izquierdo */}
          <div
            role='tablist'
            className='grid grid-cols-2 sm:grid-cols-4 border-b border-border/70 bg-muted/30 p-1 gap-1'
          >
            <button
              type='button'
              role='tab'
              aria-selected={activeTab === 'bloques'}
              onClick={() => setActiveTab('bloques')}
              className={`inline-flex items-center justify-center gap-1 rounded-lg py-2 text-[11px] font-medium transition-all ${
                activeTab === 'bloques'
                  ? 'bg-background text-foreground shadow-xs'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              <Layers className='h-3.5 w-3.5 shrink-0' />
              <span className='truncate'>Cláusulas ({bloques.length})</span>
            </button>

            <button
              type='button'
              role='tab'
              aria-selected={activeTab === 'datos'}
              onClick={() => setActiveTab('datos')}
              className={`inline-flex items-center justify-center gap-1 rounded-lg py-2 text-[11px] font-medium transition-all ${
                activeTab === 'datos'
                  ? 'bg-background text-foreground shadow-xs'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              <UserCheck className='h-3.5 w-3.5 shrink-0' />
              <span className='truncate'>Cliente y Datos</span>
            </button>

            <button
              type='button'
              role='tab'
              aria-selected={activeTab === 'lineas'}
              onClick={() => setActiveTab('lineas')}
              className={`inline-flex items-center justify-center gap-1 rounded-lg py-2 text-[11px] font-medium transition-all ${
                activeTab === 'lineas'
                  ? 'bg-background text-foreground shadow-xs'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              <Calculator className='h-3.5 w-3.5 shrink-0' />
              <span className='truncate'>Importes e IVA ({lineas.length})</span>
            </button>

            <button
              type='button'
              role='tab'
              data-testid='tab-origen-datos'
              aria-selected={activeTab === 'origen'}
              onClick={() => setActiveTab('origen')}
              className={`inline-flex items-center justify-center gap-1 rounded-lg py-2 text-[11px] font-medium transition-all ${
                activeTab === 'origen'
                  ? 'bg-background text-amber-600 dark:text-amber-400 shadow-xs'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              <Sparkles className='h-3.5 w-3.5 shrink-0 text-amber-500' />
              <span className='truncate'>Origen de datos</span>
            </button>
          </div>

          <div className='p-4 space-y-4 max-h-[calc(100vh-210px)] overflow-y-auto'>
            {/* ================================================================
                PESTAÑA 1: SECCIONES Y CLÁUSULAS (ESTILO CANVA)
                ================================================================ */}
            {activeTab === 'bloques' && (
              <div className='space-y-3'>
                <div className='rounded-xl border border-border/75 bg-background/80 p-3 space-y-2.5'>
                  <div className='flex flex-wrap items-center justify-between gap-2'>
                    <button
                      type='button'
                      onClick={() => setIsStructureCollapsed((prev) => !prev)}
                      className='flex items-center gap-2 text-left group cursor-pointer focus:outline-none'
                      title={
                        isStructureCollapsed
                          ? 'Desplegar lista de bloques del documento'
                          : 'Colapsar lista de bloques para editar con más espacio'
                      }
                      data-testid='btn-toggle-estructura-bloques'
                    >
                      <span className='inline-flex h-6 w-6 items-center justify-center rounded-md border border-border/80 bg-muted/40 text-muted-foreground group-hover:text-foreground group-hover:bg-muted transition-colors'>
                        {isStructureCollapsed ? (
                          <ChevronDown className='h-3.5 w-3.5' />
                        ) : (
                          <ChevronUp className='h-3.5 w-3.5' />
                        )}
                      </span>
                      <div>
                        <div className='flex items-center gap-1.5'>
                          <h3 className='text-xs font-semibold uppercase tracking-wider text-foreground'>
                            Estructura del Documento A4
                          </h3>
                          <Badge variant='secondary' className='h-4 px-1.5 text-[10px]'>
                            {sortedBlocks.length} bloques
                          </Badge>
                        </div>
                        <p className='text-[11px] text-muted-foreground'>
                          {isStructureCollapsed
                            ? `Lista plegada · Editando: ${selectedBlock?.numeroPastilla ?? ''} ${selectedBlock?.titulo ?? ''}`
                            : 'Haz clic en un bloque o en la cabecera para plegar la lista.'}
                        </p>
                      </div>
                    </button>

                    <div className='flex items-center gap-1.5'>
                      <Button
                        type='button'
                        variant='ghost'
                        size='sm'
                        className='h-7 px-2 text-[11px] gap-1 text-muted-foreground hover:text-foreground'
                        onClick={() => setIsStructureCollapsed((prev) => !prev)}
                      >
                        {isStructureCollapsed ? (
                          <>
                            <ChevronDown className='h-3 w-3' />
                            <span>Ver lista</span>
                          </>
                        ) : (
                          <>
                            <ChevronUp className='h-3 w-3' />
                            <span>Colapsar</span>
                          </>
                        )}
                      </Button>
                      <Button
                        type='button'
                        variant='outline'
                        size='sm'
                        className='h-7 px-2 text-[11px] gap-1'
                        onClick={handleRestoreAllTemplateBlocks}
                        title='Restaurar todas las cláusulas a la plantilla original'
                      >
                        <RotateCcw className='h-3 w-3' />
                        <span className='hidden sm:inline'>Restaurar plantilla</span>
                      </Button>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button
                            type='button'
                            size='sm'
                            className='h-7 px-2.5 text-[11px] gap-1'
                            data-testid='btn-add-custom-block'
                          >
                            <Plus className='h-3 w-3' />
                            <span>Añadir bloque</span>
                            <ChevronDown className='h-3 w-3 opacity-60' />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align='end' className='w-64'>
                          <DropdownMenuItem
                            onClick={() => handleAddCustomBlock('pill-section')}
                            className='cursor-pointer py-2'
                          >
                            <div className='flex flex-col gap-0.5'>
                              <span className='font-semibold text-xs flex items-center gap-1.5'>
                                📄 Sección estándar
                              </span>
                              <span className='text-[10px] text-muted-foreground'>
                                Pastilla ámbar numerada con texto enriquecido
                              </span>
                            </div>
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onClick={() => handleAddCustomBlock('highlight-cost-box')}
                            className='cursor-pointer py-2'
                          >
                            <div className='flex flex-col gap-0.5'>
                              <span className='font-semibold text-xs text-amber-700 dark:text-amber-400 flex items-center gap-1.5'>
                                ⭐ Caja destacada de costes
                              </span>
                              <span className='text-[10px] text-muted-foreground'>
                                Fondo ámbar, estrella roja y total automático + IVA
                              </span>
                            </div>
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onClick={() => handleAddCustomBlock('timeline-steps')}
                            className='cursor-pointer py-2'
                          >
                            <div className='flex flex-col gap-0.5'>
                              <span className='font-semibold text-xs flex items-center gap-1.5'>
                                ⏱️ Línea temporal / Fases
                              </span>
                              <span className='text-[10px] text-muted-foreground'>
                                Planificación en pasos temporales
                              </span>
                            </div>
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </div>

                  {/* Cuando está colapsado, mostramos pastillas compactas en una sola fila para cambiar de bloque sin ocupar espacio */}
                  {isStructureCollapsed ? (
                    <div className='flex items-center gap-1 overflow-x-auto pt-1 pb-0.5'>
                      {sortedBlocks.map((blk, idx) => {
                        const isSelected = selectedBlock?.id === blk.id
                        return (
                          <button
                            key={blk.id}
                            type='button'
                            onClick={() => setSelectedBlockId(blk.id)}
                            className={`inline-flex items-center gap-1 rounded-lg border px-2 py-1 text-[11px] font-medium whitespace-nowrap transition-colors cursor-pointer ${
                              isSelected
                                ? 'border-amber-500/80 bg-amber-500/15 text-amber-900 dark:text-amber-200 font-semibold'
                                : 'border-border/60 bg-muted/20 text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                            } ${!blk.visible ? 'opacity-50 line-through' : ''}`}
                            title={blk.titulo}
                          >
                            <span className='font-bold text-amber-700 dark:text-amber-400'>
                              {blk.tipoBloque === 'highlight-cost-box'
                                ? '⭐'
                                : blk.numeroPastilla || `${idx + 1}.`}
                            </span>
                            <span className='max-w-[130px] truncate'>{blk.titulo}</span>
                          </button>
                        )
                      })}
                    </div>
                  ) : (
                    /* Lista completa de bloques reordenables */
                    <div className='space-y-1.5 pt-1'>
                      {sortedBlocks.map((blk, idx) => {
                        const isSelected = selectedBlock?.id === blk.id
                        return (
                          <div
                            key={blk.id}
                            onClick={() => setSelectedBlockId(blk.id)}
                            className={`flex items-center justify-between gap-2 rounded-xl border px-3 py-2 cursor-pointer transition-all ${
                              isSelected
                                ? 'border-amber-500/70 bg-amber-500/10 shadow-2xs'
                                : 'border-border/70 bg-background hover:bg-muted/40'
                            } ${!blk.visible ? 'opacity-55' : ''}`}
                          >
                            <div className='flex items-center gap-2 min-w-0'>
                              <span className='inline-flex h-5 min-w-5 items-center justify-center rounded-md bg-amber-500/20 px-1.5 text-[11px] font-bold text-amber-800 dark:text-amber-300'>
                                {blk.tipoBloque === 'highlight-cost-box'
                                  ? '⭐'
                                  : blk.numeroPastilla || idx + 1}
                              </span>
                              <div className='min-w-0'>
                                <p className='text-xs font-semibold truncate text-foreground'>
                                  {blk.titulo}
                                </p>
                                <p className='text-[10px] text-muted-foreground truncate'>
                                  {blk.tipoBloque === 'highlight-cost-box'
                                    ? '⭐ Caja destacada de costes'
                                    : blk.tipoBloque === 'timeline-steps'
                                    ? '⏱️ Línea temporal / Fases'
                                    : blk.tipoBloque === 'pricing-table'
                                    ? '📊 Tabla de precios'
                                    : blk.tipoBloque === 'signatures'
                                    ? '✍️ Firmas'
                                    : blk.esPersonalizado
                                    ? 'Bloque personalizado'
                                    : 'Sección estándar de plantilla'}
                                </p>
                              </div>
                            </div>

                            <div
                              className='flex items-center gap-0.5 shrink-0'
                              onClick={(e) => e.stopPropagation()}
                            >
                              <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='h-6 w-6 p-0'
                                disabled={idx === 0}
                                title='Subir bloque'
                                onClick={() => handleMoveBlock(blk.id, -1)}
                              >
                                <ChevronUp className='h-3.5 w-3.5' />
                              </Button>
                              <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='h-6 w-6 p-0'
                                disabled={idx === sortedBlocks.length - 1}
                                title='Bajar bloque'
                                onClick={() => handleMoveBlock(blk.id, 1)}
                              >
                                <ChevronDown className='h-3.5 w-3.5' />
                              </Button>
                              <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='h-6 w-6 p-0'
                                title={blk.visible ? 'Ocultar bloque' : 'Mostrar bloque'}
                                onClick={() =>
                                  handleUpdateBlock(blk.id, { visible: !blk.visible })
                                }
                              >
                                {blk.visible ? (
                                  <Eye className='h-3.5 w-3.5 text-emerald-600' />
                                ) : (
                                  <EyeOff className='h-3.5 w-3.5 text-muted-foreground' />
                                )}
                              </Button>
                              <Button
                                type='button'
                                variant='ghost'
                                size='sm'
                                className='h-6 w-6 p-0 text-destructive hover:text-destructive'
                                title='Eliminar bloque'
                                onClick={() => handleDeleteBlock(blk.id)}
                              >
                                <Trash2 className='h-3.5 w-3.5' />
                              </Button>
                            </div>
                          </div>
                        )
                      })}
                    </div>
                  )}
                </div>

                {/* Editor del bloque seleccionado */}
                {selectedBlock && (
                  <div className='rounded-xl border border-border/80 bg-muted/15 p-3.5 space-y-3'>
                    <div className='flex flex-wrap items-center justify-between gap-2 pb-1 border-b border-border/50'>
                      <span className='text-xs font-semibold text-foreground truncate max-w-[200px]'>
                        Editando bloque: {selectedBlock.titulo || '(Sin título)'}
                      </span>
                      {selectedBlock.tipoBloque === 'pricing-table' ||
                      selectedBlock.tipoBloque === 'signatures' ? (
                        <Badge variant='outline' className='text-[10px] bg-muted/50'>
                          {selectedBlock.tipoBloque === 'pricing-table'
                            ? 'Tabla de precios (Sistema)'
                            : 'Firmas (Sistema)'}
                        </Badge>
                      ) : (
                        <div className='flex items-center gap-1.5'>
                          <Label className='text-[11px] text-muted-foreground whitespace-nowrap'>
                            Tipo de bloque:
                          </Label>
                          <Select
                            value={selectedBlock.tipoBloque}
                            onValueChange={(val) => {
                              const isHighlight = val === 'highlight-cost-box'
                              handleUpdateBlock(selectedBlock.id, {
                                tipoBloque: val,
                                ...(isHighlight &&
                                (!selectedBlock.titulo ||
                                  selectedBlock.titulo === 'NUEVA CLÁUSULA O SECCIÓN')
                                  ? {
                                      titulo: 'Resumen de costes.',
                                      numeroPastilla: null,
                                    }
                                  : {}),
                              })
                            }}
                          >
                            <SelectTrigger className='h-7 text-xs w-[190px]'>
                              <SelectValue placeholder='Selecciona tipo' />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value='pill-section'>
                                📄 Sección estándar (pastilla)
                              </SelectItem>
                              <SelectItem value='highlight-cost-box'>
                                ⭐ Caja destacada (ámbar + estrella)
                              </SelectItem>
                              <SelectItem value='timeline-steps'>
                                ⏱️ Línea temporal / Fases
                              </SelectItem>
                            </SelectContent>
                          </Select>
                        </div>
                      )}
                    </div>

                    {selectedBlock.tipoBloque === 'highlight-cost-box' && (
                      <div className='rounded-lg border border-amber-500/40 bg-amber-500/10 p-2.5 text-xs text-amber-900 dark:text-amber-200 flex items-start gap-2'>
                        <Sparkles className='h-4 w-4 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5' />
                        <div className='space-y-0.5 text-[11px] leading-relaxed'>
                          <span className='font-bold block'>
                            Caja destacada de costes (fondo ámbar con estrella roja)
                          </span>
                          <span>
                            Este bloque se muestra en el A4 y PDF con esquinas redondeadas, fondo ámbar y la estrella roja corporativa. Los conceptos se organizan en listas y la línea <strong>• Total: {formatCurrencyEs(calculatedTotals.baseImponible)} + IVA</strong> se calcula automáticamente al final según los conceptos del presupuesto.
                          </span>
                        </div>
                      </div>
                    )}

                    <div className='grid grid-cols-12 gap-2'>
                      {selectedBlock.tipoBloque !== 'highlight-cost-box' ? (
                        <>
                          <div className='col-span-3 space-y-1'>
                            <Label className='text-[11px]'>Numeración</Label>
                            <Input
                              value={selectedBlock.numeroPastilla ?? ''}
                              onChange={(e) =>
                                handleUpdateBlock(selectedBlock.id, {
                                  numeroPastilla: e.target.value,
                                })
                              }
                              placeholder='1.'
                              className='h-8 text-xs font-bold'
                            />
                          </div>
                          <div className='col-span-9 space-y-1'>
                            <Label className='text-[11px]'>Título de la pastilla ámbar</Label>
                            <Input
                              value={selectedBlock.titulo}
                              onChange={(e) =>
                                handleUpdateBlock(selectedBlock.id, {
                                  titulo: e.target.value,
                                })
                              }
                              placeholder='INTRODUCCIÓN'
                              className='h-8 text-xs font-semibold'
                            />
                          </div>
                        </>
                      ) : (
                        <div className='col-span-12 space-y-1'>
                          <Label className='text-[11px]'>Título de la caja destacada</Label>
                          <Input
                            value={selectedBlock.titulo}
                            onChange={(e) =>
                              handleUpdateBlock(selectedBlock.id, {
                                titulo: e.target.value,
                              })
                            }
                            placeholder='Resumen de costes.'
                            className='h-8 text-xs font-semibold'
                          />
                        </div>
                      )}
                    </div>

                    <div className='space-y-1'>
                      <Label className='text-[11px]'>
                        Contenido enriquecido (Negrita, Cursiva, Listas, Tablas)
                      </Label>
                      <LightRichTextEditor
                        value={selectedBlock.contenidoHtml}
                        onChange={(cleanHtml) =>
                          handleUpdateBlock(selectedBlock.id, {
                            contenidoHtml: cleanHtml,
                          })
                        }
                        originalHtml={selectedOriginalTemplateBlock?.contenidoHtml}
                        onRestoreOriginal={() =>
                          handleRestoreSingleBlock(selectedBlock.id)
                        }
                      />
                    </div>

                    {/* Acciones de IA por bloque: Regenerar este bloque / Hacerlo más sencillo */}
                    {selectedBlock.tipoBloque !== 'pricing-table' &&
                      selectedBlock.tipoBloque !== 'signatures' && (
                        <div className='rounded-lg border border-amber-500/30 bg-amber-500/5 p-2.5 space-y-2'>
                          <div className='flex flex-wrap items-center justify-between gap-1.5'>
                            <span className='text-[11px] font-semibold flex items-center gap-1 text-amber-700 dark:text-amber-300'>
                              <Sparkles className='h-3 w-3' />
                              Asistente IA para este bloque
                            </span>
                            <div className='flex items-center gap-1.5'>
                              <Button
                                type='button'
                                variant='outline'
                                size='sm'
                                className='h-6 px-2 text-[11px] gap-1'
                                disabled={isRegeneratingBlock}
                                onClick={() =>
                                  handleRegenerateSingleBlock(selectedBlock, 'regenerar')
                                }
                                data-testid='btn-regenerar-este-bloque'
                              >
                                <RefreshCw className='h-3 w-3' />
                                <span>Regenerar este bloque</span>
                              </Button>
                              <Button
                                type='button'
                                variant='outline'
                                size='sm'
                                className='h-6 px-2 text-[11px] gap-1'
                                disabled={isRegeneratingBlock}
                                onClick={() =>
                                  handleRegenerateSingleBlock(
                                    selectedBlock,
                                    'simplificar'
                                  )
                                }
                                data-testid='btn-simplificar-este-bloque'
                              >
                                <Wand2 className='h-3 w-3' />
                                <span>Hacerlo más sencillo</span>
                              </Button>
                            </div>
                          </div>
                          <Input
                            value={blockExtraInstruction}
                            onChange={(e) => setBlockExtraInstruction(e.target.value)}
                            placeholder='Opcional: instrucción extra para este bloque (ej. «más corto», «tono más cercano»)...'
                            className='h-7 text-[11px]'
                            data-testid='input-instruccion-bloque-ia'
                          />
                        </div>
                      )}
                  </div>
                )}
              </div>
            )}

            {/* ================================================================
                PESTAÑA 4: ORIGEN DE DATOS Y GENERACIÓN DE TEXTO POR IA (GEMINI)
                ================================================================ */}
            {activeTab === 'origen' && (
              <OrigenDatosTab
                tipo={tipo}
                documentoId={initialDocument?.id}
                textoOrigen={textoOrigenDatos}
                nombreArchivoOrigen={nombreArchivoOrigen}
                tratoCliente={tratoCliente}
                instruccionesExtra={instruccionesExtra}
                canUndoAiApply={Boolean(undoAiSnapshot)}
                onChangeTextoOrigen={(text, fileName) => {
                  setTextoOrigenDatos(text)
                  if (fileName !== undefined) setNombreArchivoOrigen(fileName)
                  markDirty()
                }}
                onChangeTratoCliente={setTratoCliente}
                onChangeInstruccionesExtra={setInstruccionesExtra}
                onClearOrigenDatos={() => {
                  setTextoOrigenDatos('')
                  setNombreArchivoOrigen('')
                  markDirty()
                  toast.info('Texto de origen borrado del documento.')
                }}
                onApplyDraft={handleApplyAiDraft}
                onUndoLastAiApply={handleUndoLastAiApply}
              />
            )}

            {/* ================================================================
                PESTAÑA 2: ASOCIACIÓN A CLIENTE, METADATOS Y AJUSTES VISUALES
                ================================================================ */}
            {activeTab === 'datos' && (
              <div className='space-y-4'>
                {/* Selector de cliente con buscador */}
                <div className='rounded-xl border border-border/80 bg-muted/15 p-3.5 space-y-3'>
                  <div className='flex items-center justify-between'>
                    <Label className='text-xs font-semibold'>
                      1. Asociar Cliente (autorrelleno automático)
                    </Label>
                    <Badge variant='secondary' className='text-[10px]'>
                      {clientes.length} clientes disponibles
                    </Badge>
                  </div>

                  <div className='relative'>
                    <SearchIcon className='absolute left-2.5 top-2 h-3.5 w-3.5 text-muted-foreground' />
                    <Input
                      value={clientSearch}
                      onChange={(e) => setClientSearch(e.target.value)}
                      placeholder='Buscar cliente por nombre, CIF/NIF o email...'
                      className='pl-8 h-8 text-xs'
                    />
                  </div>

                  <select
                    aria-label='Seleccionar cliente'
                    value={clienteId}
                    onChange={(e) => handleSelectCliente(e.target.value)}
                    className='w-full h-8 rounded-md border border-input bg-background px-2.5 text-xs font-medium'
                    data-testid='select-cliente-documento'
                  >
                    <option value=''>— Selecciona un cliente del CRM —</option>
                    {filteredClientes.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.nombreFiscal} ({c.cifNif})
                      </option>
                    ))}
                  </select>

                  <div className='grid grid-cols-1 sm:grid-cols-2 gap-2 pt-1'>
                    <div className='space-y-1'>
                      <Label className='text-[11px]'>Nombre en Portada / Cliente</Label>
                      <Input
                        value={clienteNombre}
                        onChange={(e) => {
                          setClienteNombre(e.target.value)
                          markDirty()
                        }}
                        className='h-8 text-xs font-semibold'
                      />
                    </div>
                    <div className='space-y-1'>
                      <Label className='text-[11px]'>CIF / NIF del Cliente</Label>
                      <Input
                        value={clienteCifNif}
                        onChange={(e) => {
                          setClienteCifNif(e.target.value)
                          markDirty()
                        }}
                        className='h-8 text-xs font-mono'
                      />
                    </div>
                    <div className='space-y-1 sm:col-span-2'>
                      <Label className='text-[11px]'>Domicilio Fiscal</Label>
                      <Input
                        value={clienteDireccion}
                        onChange={(e) => {
                          setClienteDireccion(e.target.value)
                          markDirty()
                        }}
                        className='h-8 text-xs'
                      />
                    </div>
                  </div>
                </div>

                {/* Datos generales de portada y fechas */}
                <div className='rounded-xl border border-border/80 bg-muted/15 p-3.5 space-y-3'>
                  <Label className='text-xs font-semibold'>
                    2. Portada y Fechas del Documento
                  </Label>

                  <div className='grid grid-cols-1 sm:grid-cols-2 gap-2.5'>
                    <div className='space-y-1 sm:col-span-2'>
                      <Label className='text-[11px]'>Título principal en portada</Label>
                      <Input
                        value={titulo}
                        onChange={(e) => {
                          setTitulo(e.target.value)
                          markDirty()
                        }}
                        placeholder='Web para aumentar venta online'
                        className='h-8 text-xs font-semibold'
                      />
                    </div>

                    <div className='space-y-1'>
                      <Label className='text-[11px]'>
                        Categoría en cursiva (Portada)
                      </Label>
                      <Input
                        value={categoria}
                        onChange={(e) => {
                          setCategoria(e.target.value)
                          setSubtitulo(e.target.value)
                          markDirty()
                        }}
                        placeholder='Diseño Web'
                        className='h-8 text-xs italic'
                      />
                    </div>

                    <div className='space-y-1'>
                      <Label className='text-[11px]'>Fecha de emisión</Label>
                      <Input
                        type='date'
                        value={fechaEmision}
                        onChange={(e) => {
                          setFechaEmision(e.target.value)
                          markDirty()
                        }}
                        className='h-8 text-xs'
                      />
                    </div>

                    {!isContrato && (
                      <div className='space-y-1'>
                        <Label className='text-[11px]'>Válido hasta</Label>
                        <Input
                          type='date'
                          value={fechaValidez}
                          onChange={(e) => {
                            setFechaValidez(e.target.value)
                            markDirty()
                          }}
                          className='h-8 text-xs'
                        />
                      </div>
                    )}
                  </div>
                </div>

                {/* Campos específicos de CONTRATOS */}
                {isContrato && (
                  <div className='rounded-xl border border-border/80 bg-muted/15 p-3.5 space-y-2.5'>
                    <Label className='text-xs font-semibold'>
                      3. Variables Contractuales y Firma
                    </Label>
                    <div className='space-y-1'>
                      <Label className='text-[11px]'>Objeto del contrato</Label>
                      <Input
                        value={objetoContrato}
                        onChange={(e) => {
                          setObjetoContrato(e.target.value)
                          markDirty()
                        }}
                        className='h-8 text-xs'
                      />
                    </div>
                    <div className='grid grid-cols-1 sm:grid-cols-2 gap-2'>
                      <div className='space-y-1'>
                        <Label className='text-[11px]'>Duración / Vigencia</Label>
                        <Input
                          value={duracionContrato}
                          onChange={(e) => {
                            setDuracionContrato(e.target.value)
                            markDirty()
                          }}
                          className='h-8 text-xs'
                        />
                      </div>
                      <div className='space-y-1'>
                        <Label className='text-[11px]'>Lugar de firma</Label>
                        <Input
                          value={lugarFirma}
                          onChange={(e) => {
                            setLugarFirma(e.target.value)
                            markDirty()
                          }}
                          className='h-8 text-xs'
                        />
                      </div>
                      <div className='space-y-1'>
                        <Label className='text-[11px]'>Precio pactado (texto)</Label>
                        <Input
                          value={precioTextoContrato}
                          onChange={(e) => {
                            setPrecioTextoContrato(e.target.value)
                            markDirty()
                          }}
                          className='h-8 text-xs'
                        />
                      </div>
                      <div className='space-y-1'>
                        <Label className='text-[11px]'>Forma de pago</Label>
                        <Input
                          value={formaPagoContrato}
                          onChange={(e) => {
                            setFormaPagoContrato(e.target.value)
                            markDirty()
                          }}
                          className='h-8 text-xs'
                        />
                      </div>
                    </div>
                  </div>
                )}

                {/* Ajustes de elementos adicionales (desactivados por defecto salvo desglose de presupuesto) */}
                <div className='rounded-xl border border-border/80 bg-muted/15 p-3.5 space-y-3'>
                  <div className='flex items-center gap-1.5'>
                    <SlidersHorizontal className='h-3.5 w-3.5 text-amber-500' />
                    <Label className='text-xs font-semibold'>
                      Elementos adicionales opcionales (Fidelidad 1:1 con PDF)
                    </Label>
                  </div>
                  <p className='text-[11px] text-muted-foreground'>
                    Por defecto están desactivados para replicar exactamente las páginas de{' '}
                    <code>Alpe_Web.pdf</code>, salvo el desglose económico en presupuestos.
                  </p>

                  <div className='space-y-2.5 text-xs'>
                    {!isContrato && pricingTableBlock && (
                      <div className='flex items-center justify-between gap-2'>
                        <span>Tabla de desglose económico e IVA (Presupuesto)</span>
                        <Switch
                          checked={pricingTableBlock.visible}
                          onCheckedChange={handleTogglePricingTableVisibility}
                        />
                      </div>
                    )}

                    <div className='flex items-center justify-between gap-2'>
                      <span>Cabecera superior con datos de empresa y código</span>
                      <Switch
                        checked={Boolean(layoutOptions.mostrarCabeceraEmpresa)}
                        onCheckedChange={(v) =>
                          setLayoutOptions((prev) => ({
                            ...prev,
                            mostrarCabeceraEmpresa: v,
                          }))
                        }
                      />
                    </div>

                    <div className='flex items-center justify-between gap-2'>
                      <span>Mostrar código correlativo en portada negra</span>
                      <Switch
                        checked={Boolean(layoutOptions.mostrarCodigoEnPortada)}
                        onCheckedChange={(v) =>
                          setLayoutOptions((prev) => ({
                            ...prev,
                            mostrarCodigoEnPortada: v,
                          }))
                        }
                      />
                    </div>

                    <div className='flex items-center justify-between gap-2'>
                      <span>Mostrar cenefa étnica horizontal en portada</span>
                      <Switch
                        checked={Boolean(layoutOptions.mostrarCenefaPortada)}
                        onCheckedChange={(v) =>
                          setLayoutOptions((prev) => ({
                            ...prev,
                            mostrarCenefaPortada: v,
                          }))
                        }
                      />
                    </div>

                    <div className='flex items-center justify-between gap-2'>
                      <span>Mostrar dirección e IBAN en el pie de página</span>
                      <Switch
                        checked={Boolean(layoutOptions.mostrarDatosFiscalesPie)}
                        onCheckedChange={(v) =>
                          setLayoutOptions((prev) => ({
                            ...prev,
                            mostrarDatosFiscalesPie: v,
                          }))
                        }
                      />
                    </div>

                    <div className='flex items-center justify-between gap-2 pt-2 border-t border-border/50'>
                      <div className='flex flex-col gap-0.5'>
                        <span className='font-semibold text-xs'>Ajustar bloques enteros por página</span>
                        <span className='text-[10px] text-muted-foreground'>
                          Evita cortar cláusulas o secciones a la mitad cuando quepan en una hoja
                        </span>
                      </div>
                      <Switch
                        checked={Boolean(layoutOptions.evitarPartirBloques ?? true)}
                        onCheckedChange={(v) =>
                          setLayoutOptions((prev) => ({
                            ...prev,
                            evitarPartirBloques: v,
                          }))
                        }
                      />
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* ================================================================
                PESTAÑA 3: TABLA DE LÍNEAS DE CONCEPTO E IVA (21% POR DEFECTO)
                ================================================================ */}
            {activeTab === 'lineas' && (
              <div className='space-y-4'>
                <div className='flex items-center justify-between'>
                  <div>
                    <h3 className='text-xs font-semibold uppercase tracking-wider text-muted-foreground'>
                      Líneas de Concepto (IVA 21% por defecto)
                    </h3>
                    <p className='text-[11px] text-muted-foreground'>
                      Admite varios tipos de IVA (21%, 10%, 4%, 0%) y descuento por línea.
                    </p>
                  </div>
                  <Button
                    type='button'
                    size='sm'
                    className='h-7 px-2.5 text-xs gap-1'
                    onClick={handleAddLine}
                    data-testid='btn-add-linea-presupuesto'
                  >
                    <Plus className='h-3.5 w-3.5' />
                    <span>Añadir línea</span>
                  </Button>
                </div>

                <div className='space-y-2.5'>
                  {lineas.map((linea, idx) => {
                    const calcRow = calculatedTotals.lineas[idx]
                    return (
                      <div
                        key={`editor-line-${idx}`}
                        className='rounded-xl border border-border/80 bg-muted/15 p-3 space-y-2'
                      >
                        <div className='flex items-center justify-between gap-2'>
                          <span className='text-[11px] font-bold text-muted-foreground'>
                            Línea #{idx + 1}
                          </span>
                          <div className='flex items-center gap-2'>
                            <span className='font-mono text-xs font-semibold'>
                              Base: {formatCurrencyEs(calcRow?.baseImponible ?? 0)}
                            </span>
                            <Button
                              type='button'
                              variant='ghost'
                              size='sm'
                              className='h-6 w-6 p-0 text-destructive'
                              onClick={() => handleRemoveLine(idx)}
                            >
                              <Trash2 className='h-3.5 w-3.5' />
                            </Button>
                          </div>
                        </div>

                        <Input
                          value={linea.descripcion}
                          onChange={(e) =>
                            handleUpdateLine(idx, 'descripcion', e.target.value)
                          }
                          placeholder='Descripción del concepto o servicio...'
                          className='h-8 text-xs'
                        />

                        <div className='grid grid-cols-4 gap-2'>
                          <div className='space-y-1'>
                            <Label className='text-[10px]'>Cantidad</Label>
                            <Input
                              type='number'
                              step='0.01'
                              min='0'
                              value={linea.cantidad}
                              onChange={(e) =>
                                handleUpdateLine(
                                  idx,
                                  'cantidad',
                                  parseFloat(e.target.value) || 0
                                )
                              }
                              className='h-7 text-xs font-mono'
                            />
                          </div>

                          <div className='space-y-1'>
                            <Label className='text-[10px]'>Precio Ud. (€)</Label>
                            <Input
                              type='number'
                              step='0.01'
                              min='0'
                              value={linea.precioUnitario}
                              onChange={(e) =>
                                handleUpdateLine(
                                  idx,
                                  'precioUnitario',
                                  parseFloat(e.target.value) || 0
                                )
                              }
                              className='h-7 text-xs font-mono'
                            />
                          </div>

                          <div className='space-y-1'>
                            <Label className='text-[10px]'>Dto. (%)</Label>
                            <Input
                              type='number'
                              step='0.5'
                              min='0'
                              max='100'
                              value={linea.descuentoPorcentaje}
                              onChange={(e) =>
                                handleUpdateLine(
                                  idx,
                                  'descuentoPorcentaje',
                                  parseFloat(e.target.value) || 0
                                )
                              }
                              className='h-7 text-xs font-mono'
                            />
                          </div>

                          <div className='space-y-1'>
                            <Label className='text-[10px]'>Tipo IVA</Label>
                            <select
                              aria-label={`Tipo de IVA línea ${idx + 1}`}
                              value={linea.ivaPorcentaje}
                              onChange={(e) =>
                                handleUpdateLine(
                                  idx,
                                  'ivaPorcentaje',
                                  Number(e.target.value)
                                )
                              }
                              className='w-full h-7 rounded-md border border-input bg-background px-2 text-xs font-mono'
                            >
                              {ALLOWED_VAT_RATES.map((rate: number) => (
                                <option key={rate} value={rate}>
                                  {rate}%
                                </option>
                              ))}
                            </select>
                          </div>
                        </div>
                      </div>
                    )
                  })}
                </div>

                {/* Resumen de Totales e IVA agrupado */}
                <div className='rounded-xl border border-amber-500/40 bg-amber-500/10 p-3.5 space-y-1.5 text-xs'>
                  <div className='flex justify-between'>
                    <span className='text-muted-foreground'>Subtotal bruto:</span>
                    <span className='font-mono font-medium'>
                      {formatCurrencyEs(calculatedTotals.subtotalBruto)}
                    </span>
                  </div>
                  {calculatedTotals.totalDescuento > 0 && (
                    <div className='flex justify-between text-amber-700 dark:text-amber-300'>
                      <span>Descuento total:</span>
                      <span className='font-mono'>
                        -{formatCurrencyEs(calculatedTotals.totalDescuento)}
                      </span>
                    </div>
                  )}
                  <div className='flex justify-between font-semibold'>
                    <span>Base Imponible Total:</span>
                    <span className='font-mono'>
                      {formatCurrencyEs(calculatedTotals.baseImponible)}
                    </span>
                  </div>
                  {calculatedTotals.desgloseIva.map((vat) => (
                    <div
                      key={vat.ivaPorcentaje}
                      className='flex justify-between text-[11px] text-muted-foreground'
                    >
                      <span>
                        {vat.etiqueta} (sobre {formatCurrencyEs(vat.baseImponible)}):
                      </span>
                      <span className='font-mono'>{formatCurrencyEs(vat.cuotaIva)}</span>
                    </div>
                  ))}
                  <div className='flex justify-between border-t border-amber-500/30 pt-2 text-sm font-bold text-foreground'>
                    <span>TOTAL (IVA incluido):</span>
                    <span className='font-mono'>
                      {formatCurrencyEs(calculatedTotals.totalGeneral)}
                    </span>
                  </div>
                </div>
              </div>
            )}
          </div>
        </Card>

        {/* PANEL DERECHO: VISTA PREVIA A4 EN VIVO IDÉNTICA AL PDF FINAL */}
        <div className='xl:col-span-7 flex flex-col gap-2'>
          <div className='flex items-center justify-between rounded-xl border border-border/80 bg-card px-3.5 py-2 text-xs print:hidden'>
            <div className='flex items-center gap-2'>
              <span className='h-2 w-2 rounded-full bg-emerald-500' />
              <span className='font-semibold'>
                Vista Previa A4 en Vivo (210mm × 297mm)
              </span>
              <span className='text-muted-foreground hidden sm:inline'>
                · Haz clic en cualquier sección para editarla
              </span>
            </div>

            <div className='flex items-center gap-2'>
              <Button
                type='button'
                variant='outline'
                size='sm'
                className={`h-7 px-2.5 text-[11px] gap-1.5 transition-colors ${
                  layoutOptions.evitarPartirBloques !== false
                    ? 'border-amber-500/70 bg-amber-500/10 text-amber-900 dark:text-amber-200 font-semibold'
                    : 'text-muted-foreground'
                }`}
                onClick={() => {
                  const nextVal = layoutOptions.evitarPartirBloques === false
                  setLayoutOptions((prev) => ({
                    ...prev,
                    evitarPartirBloques: nextVal,
                  }))
                  markDirty()
                  if (nextVal) {
                    toast.success('Páginas reajustadas: bloques enteros por página sin cortes')
                  } else {
                    toast.info('Modo continuo activado: los bloques fluyen ocupando todo el alto')
                  }
                }}
                title='Reajustar el diseño para intentar que cada bloque quede íntegro en una página'
              >
                <Layers className='h-3.5 w-3.5 text-amber-600 dark:text-amber-400' />
                <span>Reajustar páginas</span>
              </Button>

              <div className='h-4 w-px bg-border/60 mx-0.5' />

              <div className='flex items-center gap-1'>
                <ZoomIn className='h-3.5 w-3.5 text-muted-foreground mr-0.5' />
                {(
                  [
                    { id: 'doc-zoom-70', label: '70%' },
                    { id: 'doc-zoom-85', label: '85%' },
                    { id: 'doc-zoom-100', label: '100%' },
                  ] as { id: ZoomLevel; label: string }[]
                ).map((z) => (
                  <button
                    key={z.id}
                    type='button'
                    onClick={() => setZoomLevel(z.id)}
                    className={`rounded-md px-2 py-0.5 text-[11px] font-medium transition-colors ${
                      zoomLevel === z.id
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:bg-muted'
                    }`}
                  >
                    {z.label}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className='rounded-2xl border border-border/80 bg-zinc-900/90 overflow-auto max-h-[calc(100vh-175px)] print:border-0 print:bg-transparent print:max-h-none print:overflow-visible'>
            <div className={zoomLevel}>
              <DocumentoA4Renderer
                document={previewDocumentData}
                company={company}
                calculatedTotals={calculatedTotals}
                interactive
                selectedBlockId={selectedBlock?.id}
                onSelectBlock={(blockId) => {
                  setSelectedBlockId(blockId)
                  setActiveTab('bloques')
                }}
              />
            </div>
          </div>
        </div>
      </div>

      {/* DIÁLOGO DE CONFIRMACIÓN SI HAY CAMBIOS SIN GUARDAR */}
      <AlertDialog open={showExitConfirm} onOpenChange={setShowExitConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>¿Salir sin guardar los cambios?</AlertDialogTitle>
            <AlertDialogDescription>
              Has realizado modificaciones en las cláusulas o importes de este documento
              que aún no se han guardado. Si sales ahora, perderás los cambios recientes.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Seguir editando</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setShowExitConfirm(false)
                onBack()
              }}
            >
              Salir sin guardar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
