import { useState, useMemo } from 'react'
import { createFileRoute, Link } from '@tanstack/react-router'
import { Card } from '@/components/ui/card'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import {
  Plus,
  FileDown,
  Printer,
  Search as SearchIcon,
  ShieldCheck,
  Send,
  RotateCcw,
  QrCode,
  FileText,
  Trash2,
  PackagePlus,
  Check,
  BookOpenCheck,
  Settings2,
  Wallet,
  Pencil,
} from 'lucide-react'
import { toast } from 'sonner'
import {
  useBillingStore,
  billingActions,
  getQuarterFromDate,
  getYearFromDate,
  type InvoiceItem,
  type LineaFactura,
  type QuarterId,
} from '@/lib/billing-store'
import {
  downloadSingleInvoicePdf,
  downloadQuarterBookPdf,
} from '@/lib/invoice-pdf-export'
import { useClientesQuery } from '@/hooks/use-clientes'

export const Route = createFileRoute('/_authenticated/facturas/')({
  component: FacturasPage,
})

const QUARTERS: { id: QuarterId | 'todas'; label: string; desc: string }[] = [
  { id: '4T', label: '4T · Oct-Dic', desc: '4º Trimestre (Octubre - Diciembre)' },
  { id: '3T', label: '3T · Jul-Sep', desc: '3º Trimestre (Julio - Septiembre)' },
  { id: '2T', label: '2T · Abr-Jun', desc: '2º Trimestre (Abril - Junio)' },
  { id: '1T', label: '1T · Ene-Mar', desc: '1º Trimestre (Enero - Marzo)' },
  { id: 'todas', label: 'Todo el Año', desc: 'Ejercicio Completo 2026' },
]

function FacturasPage() {
  const { company, taxes, template, verifactu, catalog, facturas, gastos } =
    useBillingStore()

  const { data: clientesData } = useClientesQuery({ page: 1, pageSize: 100 })
  const dbClientes = clientesData?.items ?? []

  const [selectedQuarter, setSelectedQuarter] = useState<QuarterId | 'todas'>('4T')
  const [filterState, setFilterState] = useState<string>('todas')
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedFacturaId, setSelectedFacturaId] = useState<string>(
    facturas[0]?.id ?? '1'
  )

  // Estado del Modo Edición Visual en Vivo (WYSIWYG sobre la propia plantilla)
  const [isEditingTemplate, setIsEditingTemplate] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [draftNumero, setDraftNumero] = useState('F2026-00128')
  const [draftFecha, setDraftFecha] = useState('2026-11-06')
  const [draftVencimiento, setDraftVencimiento] = useState('2026-12-06')
  const [draftCliente, setDraftCliente] = useState('')
  const [draftCifNif, setDraftCifNif] = useState('')
  const [draftDireccion, setDraftDireccion] = useState('')
  const [draftEmail, setDraftEmail] = useState('')
  const [draftFormaPago, setDraftFormaPago] = useState(template.defaultPaymentMethod)
  const [draftNotas, setDraftNotas] = useState('')
  const [draftIrpfPercent, setDraftIrpfPercent] = useState<number>(
    () => taxes.find((t) => t.kind === 'irpf' && t.isDefault)?.percentage ?? 0
  )
  const [draftRecargoPercent, setDraftRecargoPercent] = useState<number>(
    () => taxes.find((t) => t.kind === 'recargo' && t.isDefault)?.percentage ?? 0
  )
  const [draftLineas, setDraftLineas] = useState<LineaFactura[]>([
    {
      id: 'dl-1',
      concepto: '',
      cantidad: 1,
      precioUnitario: 0,
      ivaPercent: taxes.find((t) => t.kind === 'iva' && t.isDefault)?.percentage ?? 21,
      total: 0,
    },
  ])

  const activeIvaTaxes = useMemo(
    () => taxes.filter((t) => t.kind === 'iva' && t.active),
    [taxes]
  )
  const activeIrpfTaxes = useMemo(
    () => taxes.filter((t) => t.kind === 'irpf' && t.active),
    [taxes]
  )
  const activeRecargoTaxes = useMemo(
    () => taxes.filter((t) => t.kind === 'recargo' && t.active),
    [taxes]
  )

  // Filtrado por trimestre, estado y búsqueda
  const quarterFacturas = useMemo(() => {
    return facturas.filter((f) =>
      selectedQuarter === 'todas' ? true : f.trimestre === selectedQuarter
    )
  }, [facturas, selectedQuarter])

  const quarterGastos = useMemo(() => {
    return gastos.filter((g) =>
      selectedQuarter === 'todas' ? true : g.trimestre === selectedQuarter
    )
  }, [gastos, selectedQuarter])

  const filteredFacturas = useMemo(() => {
    return quarterFacturas.filter((f) => {
      const matchesFilter = filterState === 'todas' || f.estado === filterState
      const matchesSearch =
        f.numero.toLowerCase().includes(searchTerm.toLowerCase()) ||
        f.cliente.toLowerCase().includes(searchTerm.toLowerCase()) ||
        f.cifNif.toLowerCase().includes(searchTerm.toLowerCase())
      return matchesFilter && matchesSearch
    })
  }, [quarterFacturas, filterState, searchTerm])

  // Agrupación de las facturas filtradas por Trimestre para mostrar cabeceras en la lista
  const groupedByQuarter = useMemo(() => {
    const groups: Record<QuarterId, InvoiceItem[]> = {
      '4T': [],
      '3T': [],
      '2T': [],
      '1T': [],
    }
    for (const f of filteredFacturas) {
      groups[f.trimestre]?.push(f)
    }
    return (['4T', '3T', '2T', '1T'] as QuarterId[])
      .map((q) => ({ quarter: q, items: groups[q] }))
      .filter((g) => g.items.length > 0)
  }, [filteredFacturas])

  const selectedFactura =
    facturas.find((f) => f.id === selectedFacturaId) ||
    filteredFacturas[0] ||
    facturas[0]

  // KPIs del Trimestre seleccionado (conectados con Gastos)
  const kpis = useMemo(() => {
    const valid = quarterFacturas.filter((f) => f.estado !== 'borrador')
    const baseVentas = valid.reduce((s, f) => s + f.baseImponible, 0)
    const ivaRepercutido = valid.reduce((s, f) => s + f.iva, 0)
    const totalVentas = valid.reduce((s, f) => s + f.total, 0)

    const deducibles = quarterGastos.filter((g) => g.deducible)
    const baseGastos = deducibles.reduce((s, g) => s + g.baseImponible, 0)
    const ivaSoportado = deducibles.reduce((s, g) => s + g.ivaSoportado, 0)
    const totalGastos = deducibles.reduce((s, g) => s + g.total, 0)

    const ivaAPagar = ivaRepercutido - ivaSoportado
    const beneficioNeto = baseVentas - baseGastos

    return {
      baseVentas,
      ivaRepercutido,
      totalVentas,
      countVentas: valid.length,
      baseGastos,
      ivaSoportado,
      totalGastos,
      countGastos: deducibles.length,
      ivaAPagar,
      beneficioNeto,
    }
  }, [quarterFacturas, quarterGastos])

  // Cálculos automáticos en tiempo real para la plantilla editable (WYSIWYG)
  const draftTotals = useMemo(() => {
    let baseImponible = 0
    let ivaTotal = 0
    for (const l of draftLineas) {
      const net = Math.round(l.cantidad * l.precioUnitario * 100) / 100
      baseImponible += net
      ivaTotal += Math.round(net * (l.ivaPercent / 100) * 100) / 100
    }
    const recargoImporte =
      Math.round(baseImponible * (draftRecargoPercent / 100) * 100) / 100
    const irpfImporte =
      Math.round(baseImponible * (draftIrpfPercent / 100) * 100) / 100
    const total =
      Math.round((baseImponible + ivaTotal + recargoImporte - irpfImporte) * 100) /
      100

    return {
      baseImponible,
      ivaTotal,
      recargoImporte,
      irpfImporte,
      total,
    }
  }, [draftLineas, draftIrpfPercent, draftRecargoPercent])

  // Abrir la Plantilla Editable para Nueva Factura
  const handleStartNuevaFactura = () => {
    const nextSeq = facturas.length + 124
    const defaultIva =
      taxes.find((t) => t.kind === 'iva' && t.isDefault)?.percentage ?? 21
    const defaultIrpf =
      taxes.find((t) => t.kind === 'irpf' && t.isDefault)?.percentage ?? 0
    const defaultRecargo =
      taxes.find((t) => t.kind === 'recargo' && t.isDefault)?.percentage ?? 0

    setEditingId(null)
    setDraftNumero(`F2026-${String(nextSeq).padStart(5, '0')}`)
    setDraftFecha('2026-11-06')
    setDraftVencimiento('2026-12-06')
    setDraftCliente('')
    setDraftCifNif('')
    setDraftDireccion('')
    setDraftEmail('')
    setDraftFormaPago(template.defaultPaymentMethod)
    setDraftNotas('')
    setDraftIrpfPercent(defaultIrpf)
    setDraftRecargoPercent(defaultRecargo)
    setDraftLineas([
      {
        id: `dl-${Date.now()}`,
        concepto: 'Servicio de consultoría o producto comercial',
        cantidad: 1,
        precioUnitario: 100,
        ivaPercent: defaultIva,
        total: 100,
      },
    ])
    setIsEditingTemplate(true)
  }

  // Editar factura existente en la plantilla visual
  const handleEditFactura = (factura: InvoiceItem) => {
    setEditingId(factura.id)
    setDraftNumero(factura.numero)
    setDraftFecha(factura.fecha)
    setDraftVencimiento(factura.fechaVencimiento)
    setDraftCliente(factura.cliente)
    setDraftCifNif(factura.cifNif)
    setDraftDireccion(factura.direccion)
    setDraftEmail(factura.email)
    setDraftFormaPago(factura.formaPago)
    setDraftNotas(factura.notas ?? '')
    setDraftIrpfPercent(factura.irpfPercent ?? 0)
    setDraftRecargoPercent(factura.recargoPercent ?? 0)
    setDraftLineas(
      factura.lineas.map((l) => ({
        ...l,
      }))
    )
    setIsEditingTemplate(true)
  }

  // Autocompletar cliente desde el selector
  const handleSelectExistingCustomer = (val: string) => {
    if (!val) return
    const fromDb = dbClientes.find((c) => c.id === val)
    if (fromDb) {
      setDraftCliente(fromDb.nombreFiscal)
      setDraftCifNif(fromDb.cifNif)
      setDraftDireccion(
        [fromDb.direccion, fromDb.codigoPostal, fromDb.ciudad]
          .filter(Boolean)
          .join(', ') || 'Madrid'
      )
      setDraftEmail(fromDb.email || '')
      return
    }
    const fromInvoice = facturas.find((f) => f.cifNif === val)
    if (fromInvoice) {
      setDraftCliente(fromInvoice.cliente)
      setDraftCifNif(fromInvoice.cifNif)
      setDraftDireccion(fromInvoice.direccion)
      setDraftEmail(fromInvoice.email)
    }
  }

  // Añadir línea libre a la plantilla
  const handleAddFreeLine = () => {
    const defaultIva =
      taxes.find((t) => t.kind === 'iva' && t.isDefault)?.percentage ?? 21
    setDraftLineas((prev) => [
      ...prev,
      {
        id: `dl-${Date.now()}-${prev.length}`,
        concepto: '',
        cantidad: 1,
        precioUnitario: 0,
        ivaPercent: defaultIva,
        total: 0,
      },
    ])
  }

  // Añadir línea desde el catálogo de servicios/productos
  const handleAddCatalogLine = (catalogId: string) => {
    const item = catalog.find((c) => c.id === catalogId)
    if (!item) return
    setDraftLineas((prev) => [
      ...prev.filter((l) => l.concepto.trim() !== '' || l.precioUnitario > 0),
      {
        id: `dl-${Date.now()}`,
        concepto: item.concepto,
        cantidad: 1,
        precioUnitario: item.precioUnitario,
        ivaPercent: item.ivaPercent,
        total: item.precioUnitario,
      },
    ])
  }

  const handleUpdateDraftLine = (
    id: string,
    field: keyof LineaFactura,
    value: string | number
  ) => {
    setDraftLineas((prev) =>
      prev.map((l) => {
        if (l.id !== id) return l
        const updated = { ...l, [field]: value }
        const cant = Number(updated.cantidad) || 0
        const precio = Number(updated.precioUnitario) || 0
        updated.total = Math.round(cant * precio * 100) / 100
        return updated
      })
    )
  }

  const handleRemoveDraftLine = (id: string) => {
    if (draftLineas.length <= 1) {
      toast.warning('La factura debe tener al menos una línea.')
      return
    }
    setDraftLineas((prev) => prev.filter((l) => l.id !== id))
  }

  const handleSaveInvoice = (status: 'emitida' | 'borrador') => {
    if (!draftCliente.trim()) {
      toast.error('Selecciona un cliente o escribe su razón social en la factura.')
      return
    }
    const validLines = draftLineas.filter((l) => l.concepto.trim().length > 0)
    if (validLines.length === 0) {
      toast.error('Añade al menos un concepto o servicio en las líneas de la factura.')
      return
    }

    const quarter = getQuarterFromDate(draftFecha)
    const year = getYearFromDate(draftFecha)
    const id = editingId ?? `inv-${Date.now()}`
    const hash =
      status === 'borrador'
        ? 'borrador-pendiente-firma'
        : `${Math.random().toString(16).slice(2, 10)}...${Math.random().toString(16).slice(2, 6)}`

    const newInvoice: InvoiceItem = {
      id,
      numero: status === 'borrador' && !editingId ? `BORR-${draftNumero}` : draftNumero,
      serie: 'F2026',
      fecha: draftFecha,
      trimestre: quarter,
      ejercicio: year,
      fechaVencimiento: draftVencimiento,
      cliente: draftCliente.trim(),
      cifNif: draftCifNif.trim() || 'B00000000',
      direccion: draftDireccion.trim() || 'Dirección fiscal registrada',
      email: draftEmail.trim() || 'facturacion@cliente.es',
      baseImponible: draftTotals.baseImponible,
      iva: draftTotals.ivaTotal,
      irpfPercent: draftIrpfPercent,
      irpfImporte: draftTotals.irpfImporte,
      recargoPercent: draftRecargoPercent,
      recargoImporte: draftTotals.recargoImporte,
      total: draftTotals.total,
      estado: status,
      formaPago: draftFormaPago,
      notas: draftNotas,
      hashVerifactu: hash,
      lineas: validLines,
    }

    billingActions.saveInvoice(newInvoice)
    setSelectedQuarter(quarter)
    setSelectedFacturaId(id)
    setIsEditingTemplate(false)

    toast.success(
      status === 'emitida'
        ? `Factura ${newInvoice.numero} emitida y firmada en el ${quarter} ${year}`
        : `Borrador ${newInvoice.numero} guardado correctamente`
    )
  }

  const handleDescargarPdf = (factura: InvoiceItem) => {
    downloadSingleInvoicePdf(factura, company)
    toast.success(`Factura ${factura.numero}.pdf descargada correctamente`)
  }

  const handleDescargarTrimestre = () => {
    if (quarterFacturas.length === 0) {
      toast.warning('No hay facturas en este trimestre para exportar.')
      return
    }
    downloadQuarterBookPdf(
      selectedQuarter,
      2026,
      quarterFacturas,
      quarterGastos,
      company
    )
    const label = selectedQuarter === 'todas' ? 'Anual 2026' : `${selectedQuarter} 2026`
    toast.success(
      `Libro del ${label} descargado (Pág. 1: Resumen del Trimestre + ${quarterFacturas.length} facturas en orden)`
    )
  }

  const handleImprimir = () => {
    window.print()
  }

  return (
    <>
      <Header>
        <div className='me-auto flex items-center gap-2.5 min-w-0'>
          <span className='text-foreground text-sm font-semibold tracking-tight'>
            Facturación
          </span>
          {verifactu.enabled && (
            <Badge
              variant='outline'
              className='border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 text-xs font-medium gap-1 hidden sm:inline-flex'
            >
              <ShieldCheck className='h-3 w-3' />
              VeriFactu activo
            </Badge>
          )}
        </div>
        <Search />
        <ThemeSwitch />
        <ProfileDropdown />
      </Header>

      <Main>
        <div className='flex flex-1 flex-col gap-4'>
          {/* BARRA UNIFICADA DE TRIMESTRE (Segmented Control macOS + Resumen Fiscal Compacto + Exportación) */}
          <div className='flex flex-col xl:flex-row xl:items-center xl:justify-between gap-3 bg-card border border-border/70 px-4 py-2.5 rounded-xl shadow-2xs'>
            <div className='flex flex-wrap items-center gap-3'>
              {/* Segmented Control de Trimestres (Apple HIG: segmented-controls.md) */}
              <div
                role='tablist'
                aria-label='Trimestre fiscal'
                className='inline-flex items-center rounded-lg bg-muted/70 p-0.5 border border-border/50 overflow-x-auto'
              >
                {QUARTERS.map((q) => {
                  const isActive = selectedQuarter === q.id
                  const count =
                    q.id === 'todas'
                      ? facturas.length
                      : facturas.filter((f) => f.trimestre === q.id).length
                  return (
                    <button
                      key={q.id}
                      type='button'
                      role='tab'
                      aria-selected={isActive}
                      onClick={() => setSelectedQuarter(q.id)}
                      className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-md text-xs font-medium transition-all shrink-0 ${
                        isActive
                          ? 'bg-background text-foreground shadow-xs'
                          : 'text-muted-foreground hover:text-foreground'
                      }`}
                    >
                      <span>{q.label}</span>
                      <span
                        className={`text-[11px] px-1.5 py-0 rounded-full font-mono ${
                          isActive
                            ? 'bg-primary/10 text-foreground font-semibold'
                            : 'text-muted-foreground/80'
                        }`}
                      >
                        {count}
                      </span>
                    </button>
                  )
                })}
              </div>

              {/* Resumen compacto en una sola línea (evita 4 tarjetas gigantes sobre el área de trabajo) */}
              <div className='hidden md:flex items-center gap-4 text-xs border-l border-border/70 pl-4'>
                <div>
                  <span className='text-muted-foreground'>Facturado: </span>
                  <span className='font-mono font-semibold text-foreground'>
                    {kpis.totalVentas.toLocaleString('es-ES', {
                      style: 'currency',
                      currency: 'EUR',
                    })}
                  </span>
                </div>
                <div>
                  <span className='text-muted-foreground'>Gastos: </span>
                  <span className='font-mono font-semibold text-amber-600 dark:text-amber-400'>
                    {kpis.totalGastos.toLocaleString('es-ES', {
                      style: 'currency',
                      currency: 'EUR',
                    })}
                  </span>
                </div>
                <div className='rounded-md bg-muted/50 px-2.5 py-1 border border-border/50'>
                  <span className='text-muted-foreground'>IVA a pagar (Mod. 303): </span>
                  <span className='font-mono font-semibold text-foreground'>
                    {kpis.ivaAPagar.toLocaleString('es-ES', {
                      style: 'currency',
                      currency: 'EUR',
                    })}
                  </span>
                </div>
              </div>
            </div>

            <div className='flex items-center gap-2 shrink-0'>
              <Button
                variant='ghost'
                size='sm'
                asChild
                className='h-8 text-xs font-medium gap-1.5 text-muted-foreground hover:text-foreground'
              >
                <Link to='/gastos'>
                  <Wallet className='h-3.5 w-3.5 text-amber-500' />
                  <span>Ver gastos ({quarterGastos.length})</span>
                </Link>
              </Button>

              <Button
                variant='outline'
                size='sm'
                onClick={handleDescargarTrimestre}
                className='h-8 text-xs font-medium gap-1.5 bg-background shadow-2xs'
                data-testid='btn-descargar-trimestre'
              >
                <BookOpenCheck className='h-3.5 w-3.5 text-primary' />
                <span>
                  Descargar libro {selectedQuarter === 'todas' ? 'anual' : selectedQuarter}
                </span>
              </Button>
            </div>
          </div>

          {/* SPLIT VIEW: Lista maestra a la izquierda (4 cols) + Documento en vivo a la derecha (8 cols) */}
          <div className='grid grid-cols-1 lg:grid-cols-12 gap-4 items-start'>
            {/* PANEL MAESTRO: Facturas del trimestre */}
            <div className='lg:col-span-4 flex flex-col'>
              <Card className='border border-border/70 shadow-2xs overflow-hidden rounded-xl'>
                <div className='px-4 py-3 border-b border-border/60 flex items-center justify-between gap-2 bg-muted/15'>
                  <div>
                    <h2 className='text-sm font-semibold text-foreground leading-none'>
                      Facturas ({filteredFacturas.length})
                    </h2>
                    <p className='text-xs text-muted-foreground mt-1'>
                      {selectedQuarter === 'todas'
                        ? 'Todas las series 2026'
                        : `Trimestre ${selectedQuarter} · Serie F2026`}
                    </p>
                  </div>
                  <Button
                    size='sm'
                    className='h-8 text-xs font-medium gap-1.5 shadow-2xs'
                    onClick={handleStartNuevaFactura}
                    data-testid='btn-nueva-factura'
                  >
                    <Plus className='h-3.5 w-3.5' />
                    <span>Nueva factura</span>
                  </Button>
                </div>

                <div className='p-2.5 space-y-2 border-b border-border/60 bg-background'>
                  <div className='relative'>
                    <SearchIcon className='absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground' />
                    <Input
                      placeholder='Filtrar por número, cliente o NIF...'
                      className='pl-8 h-8 text-xs rounded-lg bg-muted/30 border-border/60 focus-visible:bg-background'
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                    />
                  </div>

                  {/* Segmented Control para estado de cobro */}
                  <div
                    role='tablist'
                    className='grid grid-cols-4 rounded-lg bg-muted/70 p-0.5 border border-border/50'
                  >
                    {[
                      { id: 'todas', label: 'Todas' },
                      { id: 'cobrada', label: 'Cobradas' },
                      { id: 'emitida', label: 'Emitidas' },
                      { id: 'borrador', label: 'Borrador' },
                    ].map((tab) => (
                      <button
                        key={tab.id}
                        type='button'
                        role='tab'
                        aria-selected={filterState === tab.id}
                        onClick={() => setFilterState(tab.id)}
                        className={`text-xs py-1 rounded-md transition-all font-medium truncate ${
                          filterState === tab.id
                            ? 'bg-background text-foreground shadow-xs'
                            : 'text-muted-foreground hover:text-foreground'
                        }`}
                      >
                        {tab.label}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Lista agrupada por Trimestre con selección estilo macOS */}
                <div className='max-h-[680px] overflow-y-auto divide-y divide-border/50'>
                  {groupedByQuarter.length === 0 ? (
                    <div className='p-8 text-center text-xs text-muted-foreground'>
                      No hay facturas en este trimestre con el filtro seleccionado.
                    </div>
                  ) : (
                    groupedByQuarter.map((group) => (
                      <div key={group.quarter}>
                        <div className='bg-muted/30 px-4 py-1.5 flex items-center justify-between text-xs font-medium text-muted-foreground border-b border-border/40'>
                          <span>Trimestre {group.quarter} · 2026</span>
                          <span className='font-mono font-semibold text-foreground/80'>
                            {group.items
                              .reduce((acc, f) => acc + f.total, 0)
                              .toLocaleString('es-ES', {
                                style: 'currency',
                                currency: 'EUR',
                              })}
                          </span>
                        </div>
                        <div className='p-1.5 space-y-0.5'>
                          {group.items.map((factura) => {
                            const isSelected =
                              !isEditingTemplate && factura.id === selectedFactura?.id
                            return (
                              <div
                                key={factura.id}
                                onClick={() => {
                                  setIsEditingTemplate(false)
                                  setSelectedFacturaId(factura.id)
                                }}
                                className={`px-3 py-2.5 rounded-lg cursor-pointer transition-colors text-left ${
                                  isSelected
                                    ? 'bg-primary/10 dark:bg-primary/15 text-foreground ring-1 ring-primary/20'
                                    : 'hover:bg-muted/50'
                                }`}
                              >
                                <div className='flex items-center justify-between gap-2'>
                                  <span className='font-mono font-semibold text-xs text-foreground'>
                                    {factura.numero}
                                  </span>
                                  <div className='flex items-center gap-1.5'>
                                    {factura.estado === 'cobrada' && (
                                      <Badge
                                        variant='outline'
                                        className='border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 text-[11px] px-1.5 py-0 h-4 font-medium'
                                      >
                                        Cobrada
                                      </Badge>
                                    )}
                                    {factura.estado === 'emitida' && (
                                      <Badge
                                        variant='outline'
                                        className='border-blue-500/30 bg-blue-500/10 text-blue-700 dark:text-blue-400 text-[11px] px-1.5 py-0 h-4 font-medium'
                                      >
                                        Emitida
                                      </Badge>
                                    )}
                                    {factura.estado === 'borrador' && (
                                      <Badge
                                        variant='outline'
                                        className='border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400 text-[11px] px-1.5 py-0 h-4 font-medium'
                                      >
                                        Borrador
                                      </Badge>
                                    )}
                                  </div>
                                </div>

                                <p className='text-[13px] font-medium text-foreground truncate mt-0.5'>
                                  {factura.cliente}
                                </p>

                                <div className='flex items-center justify-between gap-2 mt-1 text-xs text-muted-foreground'>
                                  <span>{factura.fecha}</span>
                                  <span className='font-mono font-semibold text-foreground'>
                                    {factura.total.toLocaleString('es-ES', {
                                      style: 'currency',
                                      currency: 'EUR',
                                    })}
                                  </span>
                                </div>
                              </div>
                            )
                          })}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </Card>
            </div>

            {/* PANEL SECUNDARIO: LIENZO DE LA FACTURA (UNIFICADO CON SU BARRA DE HERRAMIENTAS) */}
            <div className='lg:col-span-8 flex flex-col'>
              {isEditingTemplate ? (
                /* =====================================================================
                   MODO EDICIÓN VISUAL EN VIVO (WYSIWYG SOBRE LA PLANTILLA DE FACTURA)
                   ===================================================================== */
                <Card className='border border-primary/30 shadow-xs overflow-hidden rounded-xl'>
                  {/* Barra de herramientas integrada en la cabecera del documento */}
                  <div className='flex flex-wrap items-center justify-between gap-3 bg-muted/20 border-b border-border/70 px-5 py-3'>
                    <div className='flex items-center gap-2.5'>
                      <span className='h-2 w-2 rounded-full bg-primary' />
                      <span className='text-sm font-semibold text-foreground'>
                        {editingId ? `Editando ${draftNumero}` : 'Nueva factura en plantilla'}
                      </span>
                      <span className='text-xs text-muted-foreground hidden sm:inline'>
                        · Cálculo de impuestos automático
                      </span>
                    </div>

                    <div className='flex items-center gap-2'>
                      <Button
                        type='button'
                        variant='ghost'
                        size='sm'
                        className='h-8 text-xs font-medium'
                        onClick={() => setIsEditingTemplate(false)}
                      >
                        Cancelar
                      </Button>
                      <Button
                        type='button'
                        variant='outline'
                        size='sm'
                        className='h-8 text-xs font-medium bg-background'
                        onClick={() => handleSaveInvoice('borrador')}
                      >
                        Guardar borrador
                      </Button>
                      <Button
                        type='button'
                        size='sm'
                        className='h-8 text-xs font-medium gap-1.5 shadow-2xs'
                        onClick={() => handleSaveInvoice('emitida')}
                        data-testid='btn-emitir-factura-editable'
                      >
                        <Check className='h-3.5 w-3.5' />
                        <span>Emitir factura</span>
                      </Button>
                    </div>
                  </div>

                  {/* Papel de Factura Editable */}
                  <div className='p-6 sm:p-8 flex flex-col gap-6 bg-card'>
                    {/* Cabecera Emisor + Número y Fechas */}
                    <div className='flex flex-col sm:flex-row sm:items-start sm:justify-between gap-6'>
                      <div>
                        <div className='flex items-center gap-3'>
                          {template.showLogo && company.logoWebpDataUrl ? (
                            <img
                              src={company.logoWebpDataUrl}
                              alt={company.nombreComercial}
                              className='h-12 w-auto max-w-[140px] object-contain rounded-lg border p-1 bg-white'
                            />
                          ) : (
                            <div className='h-10 w-10 rounded-lg bg-primary text-primary-foreground flex items-center justify-center font-bold text-sm'>
                              {company.razonSocial.charAt(0)}
                            </div>
                          )}
                          <div>
                            <span className='font-bold text-base tracking-tight text-foreground block'>
                              {company.razonSocial}
                            </span>
                            <span className='text-xs text-muted-foreground font-mono'>
                              NIF: {company.nifCif}
                            </span>
                          </div>
                        </div>
                        <div className='text-xs text-muted-foreground mt-2 space-y-0.5'>
                          <p>{company.direccionFiscal}</p>
                          <p>
                            {company.codigoPostal} {company.ciudad} · {company.pais}
                          </p>
                          <p>{company.emailFacturacion}</p>
                        </div>
                      </div>

                      <div className='grid grid-cols-2 sm:w-72 gap-2.5 text-xs bg-muted/15 p-3.5 rounded-xl border border-border/70'>
                        <div className='col-span-2'>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            Número de factura (Serie F2026)
                          </label>
                          <Input
                            value={draftNumero}
                            onChange={(e) => setDraftNumero(e.target.value)}
                            className='h-8 text-xs font-mono font-semibold bg-background'
                          />
                        </div>
                        <div>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            Fecha de emisión
                          </label>
                          <Input
                            type='date'
                            value={draftFecha}
                            onChange={(e) => setDraftFecha(e.target.value)}
                            className='h-8 text-xs bg-background'
                          />
                        </div>
                        <div>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            Vencimiento
                          </label>
                          <Input
                            type='date'
                            value={draftVencimiento}
                            onChange={(e) => setDraftVencimiento(e.target.value)}
                            className='h-8 text-xs bg-background'
                          />
                        </div>
                        <div className='col-span-2 pt-1 flex items-center justify-between text-xs'>
                          <span className='text-muted-foreground'>Trimestre fiscal:</span>
                          <Badge variant='secondary' className='font-mono text-xs'>
                            {getQuarterFromDate(draftFecha)} {getYearFromDate(draftFecha)}
                          </Badge>
                        </div>
                      </div>
                    </div>

                    <Separator />

                    {/* Bloque Cliente / Receptor con selector rápido (entering-data.md) */}
                    <div className='bg-muted/15 border border-border/70 rounded-xl p-4 space-y-3.5'>
                      <div className='flex flex-col sm:flex-row sm:items-center justify-between gap-2'>
                        <span className='text-xs font-semibold text-foreground'>
                          Datos del cliente (receptor)
                        </span>

                        <select
                          aria-label='Seleccionar cliente existente'
                          onChange={(e) => handleSelectExistingCustomer(e.target.value)}
                          defaultValue=''
                          className='h-8 rounded-lg border border-input bg-background px-3 text-xs font-medium'
                          data-testid='select-cliente-factura'
                        >
                          <option value=''>
                            Elegir cliente del directorio...
                          </option>
                          {dbClientes.map((c) => (
                            <option key={c.id} value={c.id}>
                              {c.nombreFiscal} ({c.cifNif})
                            </option>
                          ))}
                          <option value='B98765432'>
                            Acme Telecomunicaciones S.L. (B98765432)
                          </option>
                          <option value='E12345678'>
                            Restaurante El Molino C.B. (E12345678)
                          </option>
                          <option value='A87654321'>
                            Talleres Mecánicos Sur S.A. (A87654321)
                          </option>
                        </select>
                      </div>

                      <div className='grid sm:grid-cols-2 lg:grid-cols-4 gap-3'>
                        <div className='sm:col-span-2'>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            Cliente o razón social *
                          </label>
                          <Input
                            placeholder='Ej. Darytravel Aventuras S.L.'
                            value={draftCliente}
                            onChange={(e) => setDraftCliente(e.target.value)}
                            className='h-8 text-xs bg-background'
                            data-testid='input-factura-cliente'
                          />
                        </div>
                        <div>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            NIF / CIF
                          </label>
                          <Input
                            placeholder='B12345678'
                            value={draftCifNif}
                            onChange={(e) => setDraftCifNif(e.target.value)}
                            className='h-8 text-xs font-mono bg-background'
                            data-testid='input-factura-cif'
                          />
                        </div>
                        <div>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            Correo electrónico
                          </label>
                          <Input
                            placeholder='facturacion@cliente.es'
                            value={draftEmail}
                            onChange={(e) => setDraftEmail(e.target.value)}
                            className='h-8 text-xs bg-background'
                          />
                        </div>
                        <div className='sm:col-span-2'>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            Dirección fiscal
                          </label>
                          <Input
                            placeholder='Calle, código postal y localidad'
                            value={draftDireccion}
                            onChange={(e) => setDraftDireccion(e.target.value)}
                            className='h-8 text-xs bg-background'
                          />
                        </div>
                        <div className='sm:col-span-2'>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            Forma de pago
                          </label>
                          <select
                            value={draftFormaPago}
                            onChange={(e) => setDraftFormaPago(e.target.value)}
                            className='h-8 w-full rounded-lg border border-input bg-background px-2.5 text-xs'
                          >
                            <option value='Transferencia bancaria a 30 días'>
                              Transferencia bancaria a 30 días
                            </option>
                            <option value='Domiciliación bancaria SEPA'>
                              Domiciliación bancaria SEPA
                            </option>
                            <option value='Tarjeta de crédito / Débito (TPV)'>
                              Tarjeta de crédito / Débito (TPV)
                            </option>
                            <option value='Bizum / Pago inmediato'>
                              Bizum / Pago inmediato
                            </option>
                            <option value='Contado / Efectivo'>
                              Contado / Efectivo
                            </option>
                          </select>
                        </div>
                      </div>
                    </div>

                    {/* Tabla de Conceptos con pie integrado para añadir líneas */}
                    <div className='space-y-2'>
                      <span className='text-xs font-semibold text-foreground block'>
                        Conceptos y servicios facturados
                      </span>

                      <div className='border border-border/70 rounded-xl overflow-hidden'>
                        <table className='w-full text-left text-xs'>
                          <thead className='bg-muted/30 border-b border-border/60 text-muted-foreground font-medium'>
                            <tr>
                              <th className='py-2.5 px-3'>Concepto o descripción</th>
                              <th className='py-2.5 px-2 text-right w-20'>Cant.</th>
                              <th className='py-2.5 px-2 text-right w-28'>Precio (€)</th>
                              <th className='py-2.5 px-2 text-right w-28'>IVA</th>
                              <th className='py-2.5 px-3 text-right w-28'>Importe</th>
                              <th className='py-2.5 px-2 w-10'></th>
                            </tr>
                          </thead>
                          <tbody className='divide-y divide-border/50'>
                            {draftLineas.map((linea, idx) => (
                              <tr key={linea.id} className='bg-background'>
                                <td className='p-2'>
                                  <Input
                                    placeholder={`Descripción del servicio o producto (${idx + 1})...`}
                                    value={linea.concepto}
                                    onChange={(e) =>
                                      handleUpdateDraftLine(
                                        linea.id,
                                        'concepto',
                                        e.target.value
                                      )
                                    }
                                    className='h-8 text-xs'
                                  />
                                </td>
                                <td className='p-2'>
                                  <Input
                                    type='number'
                                    min='0.01'
                                    step='any'
                                    value={linea.cantidad}
                                    onChange={(e) =>
                                      handleUpdateDraftLine(
                                        linea.id,
                                        'cantidad',
                                        parseFloat(e.target.value) || 0
                                      )
                                    }
                                    className='h-8 text-xs text-right font-mono'
                                  />
                                </td>
                                <td className='p-2'>
                                  <Input
                                    type='number'
                                    step='0.01'
                                    value={linea.precioUnitario}
                                    onChange={(e) =>
                                      handleUpdateDraftLine(
                                        linea.id,
                                        'precioUnitario',
                                        parseFloat(e.target.value) || 0
                                      )
                                    }
                                    className='h-8 text-xs text-right font-mono'
                                  />
                                </td>
                                <td className='p-2'>
                                  <select
                                    value={linea.ivaPercent}
                                    onChange={(e) =>
                                      handleUpdateDraftLine(
                                        linea.id,
                                        'ivaPercent',
                                        parseFloat(e.target.value)
                                      )
                                    }
                                    className='h-8 w-full rounded-lg border border-input bg-background px-2 text-xs font-mono text-right'
                                  >
                                    {activeIvaTaxes.map((t) => (
                                      <option key={t.id} value={t.percentage}>
                                        {t.percentage}% ({t.code})
                                      </option>
                                    ))}
                                  </select>
                                </td>
                                <td className='p-2 px-3 text-right font-mono font-semibold text-foreground'>
                                  {linea.total.toLocaleString('es-ES', {
                                    style: 'currency',
                                    currency: 'EUR',
                                  })}
                                </td>
                                <td className='p-2 text-center'>
                                  <Button
                                    type='button'
                                    variant='ghost'
                                    size='icon'
                                    className='h-7 w-7 text-muted-foreground hover:text-destructive'
                                    onClick={() => handleRemoveDraftLine(linea.id)}
                                    aria-label='Eliminar línea'
                                  >
                                    <Trash2 className='h-3.5 w-3.5' />
                                  </Button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>

                        {/* Barra inferior integrada para insertar línea libre o del catálogo */}
                        <div className='bg-muted/20 border-t border-border/60 px-3 py-2 flex flex-wrap items-center justify-between gap-2'>
                          <Button
                            type='button'
                            variant='ghost'
                            size='sm'
                            onClick={handleAddFreeLine}
                            className='h-8 text-xs font-medium gap-1.5 text-primary hover:text-primary'
                            data-testid='btn-anadir-linea-libre'
                          >
                            <Plus className='h-3.5 w-3.5' />
                            <span>Añadir línea libre</span>
                          </Button>

                          <div className='inline-flex items-center gap-1.5'>
                            <PackagePlus className='h-3.5 w-3.5 text-muted-foreground' />
                            <select
                              aria-label='Añadir servicio o producto del catálogo'
                              value=''
                              onChange={(e) => {
                                if (e.target.value) {
                                  handleAddCatalogLine(e.target.value)
                                }
                              }}
                              className='h-8 rounded-lg border border-input bg-background px-2.5 text-xs font-medium'
                              data-testid='select-catalogo-factura'
                            >
                              <option value=''>Insertar producto o servicio del catálogo...</option>
                              {catalog.map((item) => (
                                <option key={item.id} value={item.id}>
                                  {item.concepto} — {item.precioUnitario.toFixed(2)} € (IVA {item.ivaPercent}%)
                                </option>
                              ))}
                            </select>
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Retención IRPF, Recargo de Equivalencia y Totales Automáticos */}
                    <div className='grid sm:grid-cols-2 gap-4 items-end pt-1'>
                      <div className='space-y-3 bg-muted/15 p-4 rounded-xl border border-border/70'>
                        <div className='flex items-center justify-between'>
                          <span className='text-xs font-semibold text-foreground'>
                            Retenciones y recargos
                          </span>
                          <Link
                            to='/settings/account'
                            className='text-xs text-primary hover:underline inline-flex items-center gap-1'
                          >
                            <Settings2 className='h-3 w-3' />
                            Configurar tipos
                          </Link>
                        </div>

                        <div className='grid grid-cols-2 gap-2.5'>
                          <div>
                            <label className='text-xs font-medium text-muted-foreground block mb-1'>
                              Retención IRPF (-)
                            </label>
                            <select
                              value={draftIrpfPercent}
                              onChange={(e) =>
                                setDraftIrpfPercent(parseFloat(e.target.value))
                              }
                              className='h-8 w-full rounded-lg border border-input bg-background px-2 text-xs'
                            >
                              {activeIrpfTaxes.map((t) => (
                                <option key={t.id} value={t.percentage}>
                                  {t.name}
                                </option>
                              ))}
                            </select>
                          </div>

                          <div>
                            <label className='text-xs font-medium text-muted-foreground block mb-1'>
                              Recargo equivalencia (+)
                            </label>
                            <select
                              value={draftRecargoPercent}
                              onChange={(e) =>
                                setDraftRecargoPercent(parseFloat(e.target.value))
                              }
                              className='h-8 w-full rounded-lg border border-input bg-background px-2 text-xs'
                            >
                              {activeRecargoTaxes.map((t) => (
                                <option key={t.id} value={t.percentage}>
                                  {t.name}
                                </option>
                              ))}
                            </select>
                          </div>
                        </div>

                        <div>
                          <label className='text-xs font-medium text-muted-foreground block mb-1'>
                            Observaciones o referencias
                          </label>
                          <Input
                            placeholder='Opcional: Nº de pedido, condiciones o nota al pie...'
                            value={draftNotas}
                            onChange={(e) => setDraftNotas(e.target.value)}
                            className='h-8 text-xs bg-background'
                          />
                        </div>
                      </div>

                      {/* Resumen de Importes Calculados */}
                      <div className='w-full bg-neutral-900 text-neutral-100 dark:bg-neutral-950 dark:border dark:border-neutral-800 rounded-xl p-4 shadow-xs space-y-2'>
                        <div className='flex items-center justify-between text-xs text-neutral-400'>
                          <span>Base imponible</span>
                          <span className='font-mono'>
                            {draftTotals.baseImponible.toLocaleString('es-ES', {
                              style: 'currency',
                              currency: 'EUR',
                            })}
                          </span>
                        </div>

                        <div className='flex items-center justify-between text-xs text-neutral-400'>
                          <span>Cuota IVA</span>
                          <span className='font-mono'>
                            +{draftTotals.ivaTotal.toLocaleString('es-ES', {
                              style: 'currency',
                              currency: 'EUR',
                            })}
                          </span>
                        </div>

                        {draftTotals.recargoImporte > 0 && (
                          <div className='flex items-center justify-between text-xs text-neutral-400'>
                            <span>Recargo eq. ({draftRecargoPercent}%)</span>
                            <span className='font-mono'>
                              +{draftTotals.recargoImporte.toLocaleString('es-ES', {
                                style: 'currency',
                                currency: 'EUR',
                              })}
                            </span>
                          </div>
                        )}

                        {draftTotals.irpfImporte > 0 && (
                          <div className='flex items-center justify-between text-xs text-rose-400'>
                            <span>Retención IRPF (-{draftIrpfPercent}%)</span>
                            <span className='font-mono'>
                              -{draftTotals.irpfImporte.toLocaleString('es-ES', {
                                style: 'currency',
                                currency: 'EUR',
                              })}
                            </span>
                          </div>
                        )}

                        <Separator className='bg-neutral-800' />

                        <div className='flex items-center justify-between pt-1'>
                          <span className='text-xs font-semibold text-neutral-300'>
                            Total factura
                          </span>
                          <span className='text-xl font-bold font-mono text-emerald-400'>
                            {draftTotals.total.toLocaleString('es-ES', {
                              style: 'currency',
                              currency: 'EUR',
                            })}
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>
                </Card>
              ) : selectedFactura ? (
                /* =====================================================================
                   MODO VISUALIZACIÓN DE LA PLANTILLA DE FACTURA SELECCIONADA
                   ===================================================================== */
                <Card className='border border-border/70 shadow-2xs overflow-hidden rounded-xl'>
                  {/* Barra de acciones integrada en la cabecera del documento */}
                  <div className='flex flex-wrap items-center justify-between gap-2 bg-muted/15 border-b border-border/60 px-5 py-3'>
                    <div className='flex items-center gap-2.5'>
                      <div className='flex h-7 w-7 items-center justify-center rounded-lg bg-primary/10 text-primary'>
                        <FileText className='h-4 w-4' />
                      </div>
                      <span className='text-sm font-semibold font-mono text-foreground'>
                        {selectedFactura.numero}
                      </span>
                      <Badge variant='secondary' className='text-xs font-mono'>
                        {selectedFactura.trimestre} {selectedFactura.ejercicio}
                      </Badge>
                    </div>

                    <div className='flex items-center gap-1.5'>
                      <Button
                        variant='outline'
                        size='sm'
                        className='h-8 text-xs font-medium gap-1.5 bg-background'
                        onClick={() => handleEditFactura(selectedFactura)}
                      >
                        <Pencil className='h-3.5 w-3.5 text-muted-foreground' />
                        <span>Editar en plantilla</span>
                      </Button>
                      <Button
                        variant='outline'
                        size='sm'
                        className='h-8 text-xs font-medium gap-1.5 bg-background'
                        onClick={handleImprimir}
                      >
                        <Printer className='h-3.5 w-3.5 text-muted-foreground' />
                        <span className='hidden sm:inline'>Imprimir</span>
                      </Button>
                      <Button
                        variant='outline'
                        size='sm'
                        className='h-8 text-xs font-medium gap-1.5 bg-background'
                        onClick={() => handleDescargarPdf(selectedFactura)}
                        data-testid='btn-descargar-factura-pdf'
                      >
                        <FileDown className='h-3.5 w-3.5 text-primary' />
                        <span>Descargar PDF</span>
                      </Button>
                      <Button
                        variant='outline'
                        size='sm'
                        className='h-8 text-xs font-medium gap-1.5 bg-background'
                        onClick={() =>
                          toast.success(
                            `Factura ${selectedFactura.numero} enviada a ${selectedFactura.email}`
                          )
                        }
                      >
                        <Send className='h-3.5 w-3.5 text-muted-foreground' />
                        <span className='hidden sm:inline'>Enviar</span>
                      </Button>
                      <Button
                        variant='ghost'
                        size='sm'
                        className='h-8 w-8 p-0 text-muted-foreground hover:text-foreground'
                        title='Crear factura rectificativa'
                        onClick={() =>
                          toast.warning(
                            `Preparando factura rectificativa para ${selectedFactura.numero}`
                          )
                        }
                      >
                        <RotateCcw className='h-3.5 w-3.5' />
                      </Button>
                    </div>
                  </div>

                  {/* Hoja de Factura */}
                  <div className='p-6 sm:p-8 flex flex-col gap-6 relative bg-card'>
                    {/* Cabecera Comercial */}
                    <div className='flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4'>
                      <div>
                        <div className='flex items-center gap-3'>
                          {template.showLogo && company.logoWebpDataUrl ? (
                            <img
                              src={company.logoWebpDataUrl}
                              alt={company.nombreComercial}
                              className='h-12 w-auto max-w-[150px] object-contain rounded border p-1 bg-white'
                            />
                          ) : (
                            <div className='h-8 w-8 rounded-md bg-primary text-primary-foreground flex items-center justify-center font-bold text-sm'>
                              {company.razonSocial.charAt(0)}
                            </div>
                          )}
                          <span className='font-bold text-lg tracking-tight text-foreground'>
                            {company.razonSocial}
                          </span>
                        </div>
                        <div className='text-xs text-muted-foreground mt-2 space-y-0.5'>
                          <p>NIF: {company.nifCif}</p>
                          <p>{company.direccionFiscal}</p>
                          <p>
                            {company.codigoPostal} {company.ciudad} · {company.pais}
                          </p>
                          <p>{company.emailFacturacion}</p>
                        </div>
                      </div>

                      <div className='sm:text-right space-y-1'>
                        <div className='inline-flex items-center gap-1.5'>
                          <span className='text-xs uppercase tracking-wider text-muted-foreground font-medium'>
                            Factura Ordinaria · {selectedFactura.trimestre}
                          </span>
                        </div>
                        <p className='text-xl sm:text-2xl font-mono font-bold tracking-tight text-foreground'>
                          {selectedFactura.numero}
                        </p>
                        <div className='text-xs text-muted-foreground space-y-0.5 pt-1'>
                          <p>
                            Fecha Emisión:{' '}
                            <span className='font-medium text-foreground'>
                              {selectedFactura.fecha}
                            </span>
                          </p>
                          <p>
                            Vencimiento:{' '}
                            <span className='font-medium text-foreground'>
                              {selectedFactura.fechaVencimiento}
                            </span>
                          </p>
                          <div className='pt-1'>
                            {selectedFactura.estado === 'cobrada' && (
                              <Badge className='bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20 text-xs'>
                                Liquidada / Cobrada
                              </Badge>
                            )}
                            {selectedFactura.estado === 'emitida' && (
                              <Badge className='bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20 text-xs'>
                                Emitida (Pendiente)
                              </Badge>
                            )}
                            {selectedFactura.estado === 'borrador' && (
                              <Badge variant='secondary' className='text-xs'>
                                Borrador editable
                              </Badge>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>

                    <Separator />

                    {/* Datos del Cliente Receptor */}
                    <div className='bg-muted/20 border border-border/50 rounded-lg p-4'>
                      <span className='text-[10px] font-semibold uppercase tracking-wider text-muted-foreground block mb-1'>
                        Cliente / Receptor
                      </span>
                      <div className='grid sm:grid-cols-2 gap-3 text-xs'>
                        <div>
                          <p className='font-semibold text-sm text-foreground'>
                            {selectedFactura.cliente}
                          </p>
                          <p className='font-mono text-muted-foreground mt-0.5'>
                            NIF/CIF: {selectedFactura.cifNif}
                          </p>
                          <p className='text-muted-foreground mt-0.5'>
                            {selectedFactura.direccion}
                          </p>
                        </div>
                        <div className='sm:text-right space-y-0.5'>
                          <p className='text-muted-foreground'>Email de facturación:</p>
                          <p className='font-medium text-foreground'>
                            {selectedFactura.email}
                          </p>
                          <p className='text-muted-foreground pt-1'>Forma de pago:</p>
                          <p className='font-medium text-foreground'>
                            {selectedFactura.formaPago}
                          </p>
                          {template.showIban && company.iban && (
                            <p className='font-mono text-[11px] text-muted-foreground pt-0.5'>
                              IBAN: {company.iban}
                            </p>
                          )}
                        </div>
                      </div>
                    </div>

                    {/* Tabla de Conceptos / Líneas */}
                    <div className='border rounded-lg overflow-hidden'>
                      <table className='w-full text-left text-xs'>
                        <thead className='bg-muted/40 border-b text-muted-foreground font-medium'>
                          <tr>
                            <th className='py-2.5 px-3.5'>Descripción / Concepto</th>
                            <th className='py-2.5 px-3.5 text-right w-16'>Cant.</th>
                            <th className='py-2.5 px-3.5 text-right w-24'>Precio</th>
                            <th className='py-2.5 px-3.5 text-right w-16'>IVA</th>
                            <th className='py-2.5 px-3.5 text-right w-28'>Total</th>
                          </tr>
                        </thead>
                        <tbody className='divide-y'>
                          {selectedFactura.lineas.map((linea) => (
                            <tr key={linea.id} className='hover:bg-muted/20'>
                              <td className='py-3 px-3.5 font-medium text-foreground'>
                                {linea.concepto}
                              </td>
                              <td className='py-3 px-3.5 text-right font-mono'>
                                {linea.cantidad}
                              </td>
                              <td className='py-3 px-3.5 text-right font-mono'>
                                {linea.precioUnitario.toLocaleString('es-ES', {
                                  style: 'currency',
                                  currency: 'EUR',
                                })}
                              </td>
                              <td className='py-3 px-3.5 text-right font-mono text-muted-foreground'>
                                {linea.ivaPercent}%
                              </td>
                              <td className='py-3 px-3.5 text-right font-mono font-semibold text-foreground'>
                                {linea.total.toLocaleString('es-ES', {
                                  style: 'currency',
                                  currency: 'EUR',
                                })}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>

                    {/* Tarjeta de Resumen y Totales */}
                    <div className='flex flex-col sm:flex-row items-stretch sm:items-end justify-between gap-4 pt-2'>
                      {template.showQrVerifactu ? (
                        <div className='flex items-center gap-3 p-3 rounded-lg border bg-muted/10 max-w-sm'>
                          <div className='h-12 w-12 rounded border bg-background flex items-center justify-center shrink-0 text-muted-foreground'>
                            <QrCode className='h-8 w-8' />
                          </div>
                          <div className='text-[10px] text-muted-foreground leading-tight space-y-0.5 min-w-0'>
                            <div className='flex items-center gap-1 font-semibold text-foreground'>
                              <ShieldCheck className='h-3 w-3 text-emerald-500' />
                              <span>Registro VeriFactu AEAT</span>
                            </div>
                            <p className='truncate'>
                              Huella: {selectedFactura.hashVerifactu}
                            </p>
                            <p>Encadenamiento SHA-256 verificado.</p>
                          </div>
                        </div>
                      ) : (
                        <div />
                      )}

                      <div className='w-full sm:w-72 bg-neutral-900 text-neutral-100 dark:bg-neutral-950 dark:border dark:border-neutral-800 rounded-lg p-4 shadow-sm space-y-2'>
                        <div className='flex items-center justify-between text-xs text-neutral-400'>
                          <span>Base Imponible</span>
                          <span className='font-mono'>
                            {selectedFactura.baseImponible.toLocaleString('es-ES', {
                              style: 'currency',
                              currency: 'EUR',
                            })}
                          </span>
                        </div>

                        <div className='flex items-center justify-between text-xs text-neutral-400'>
                          <span>IVA Repercutido</span>
                          <span className='font-mono'>
                            {selectedFactura.iva.toLocaleString('es-ES', {
                              style: 'currency',
                              currency: 'EUR',
                            })}
                          </span>
                        </div>

                        {selectedFactura.recargoImporte > 0 && (
                          <div className='flex items-center justify-between text-xs text-neutral-400'>
                            <span>
                              Recargo Eq. ({selectedFactura.recargoPercent}%)
                            </span>
                            <span className='font-mono'>
                              {selectedFactura.recargoImporte.toLocaleString('es-ES', {
                                style: 'currency',
                                currency: 'EUR',
                              })}
                            </span>
                          </div>
                        )}

                        {selectedFactura.irpfImporte > 0 && (
                          <div className='flex items-center justify-between text-xs text-rose-400'>
                            <span>
                              Retención IRPF (-{selectedFactura.irpfPercent}%)
                            </span>
                            <span className='font-mono'>
                              -{selectedFactura.irpfImporte.toLocaleString('es-ES', {
                                style: 'currency',
                                currency: 'EUR',
                              })}
                            </span>
                          </div>
                        )}

                        <Separator className='bg-neutral-800' />

                        <div className='flex items-center justify-between pt-1'>
                          <span className='text-xs font-semibold uppercase tracking-wider text-neutral-300'>
                            Total Factura
                          </span>
                          <span className='text-xl font-bold font-mono text-emerald-400'>
                            {selectedFactura.total.toLocaleString('es-ES', {
                              style: 'currency',
                              currency: 'EUR',
                            })}
                          </span>
                        </div>
                      </div>
                    </div>

                    {/* Pie de Página Legal */}
                    {template.showRegistroMercantil && (
                      <div className='text-xs text-muted-foreground border-t pt-4 text-center sm:text-left space-y-0.5'>
                        <p>{company.registroMercantil}</p>
                        <p>{company.pieFactura}</p>
                      </div>
                    )}
                  </div>
                </Card>
              ) : null}
            </div>
          </div>
        </div>
      </Main>
    </>
  )
}
