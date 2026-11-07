import { useState, useMemo } from 'react'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Calendar as CalendarIcon,
  ChevronLeft,
  ChevronRight,
  Plus,
  Clock,
  MapPin,
  User,
  CheckCircle2,
  Circle,
  Trash2,
} from 'lucide-react'
import { toast } from 'sonner'
import {
  useBillingStore,
  billingActions,
  type CalendarEventItem,
} from '@/lib/billing-store'

const MONTH_NAMES = [
  'Enero',
  'Febrero',
  'Marzo',
  'Abril',
  'Mayo',
  'Junio',
  'Julio',
  'Agosto',
  'Septiembre',
  'Octubre',
  'Noviembre',
  'Diciembre',
]

const WEEKDAYS = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom']

const EVENT_TYPE_META: Record<
  CalendarEventItem['tipo'],
  { label: string; badgeClass: string; dotClass: string }
> = {
  visita: {
    label: 'Visita a Cliente',
    badgeClass:
      'border-blue-500/30 bg-blue-500/10 text-blue-600 dark:text-blue-400',
    dotClass: 'bg-blue-500',
  },
  reunion: {
    label: 'Reunión / Cita',
    badgeClass:
      'border-emerald-500/30 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400',
    dotClass: 'bg-emerald-500',
  },
  tarea: {
    label: 'Tarea Operativa',
    badgeClass:
      'border-amber-500/30 bg-amber-500/10 text-amber-600 dark:text-amber-400',
    dotClass: 'bg-amber-500',
  },
  fiscal: {
    label: 'Hacienda / Fiscal',
    badgeClass:
      'border-purple-500/30 bg-purple-500/10 text-purple-600 dark:text-purple-400',
    dotClass: 'bg-purple-500',
  },
}

export function Tasks() {
  const { eventos } = useBillingStore()

  // Estado del mes visible (por defecto Noviembre 2026: monthIndex = 10)
  const [currentYear, setCurrentYear] = useState(2026)
  const [currentMonth, setCurrentMonth] = useState(10) // 0-indexed -> 10 = Noviembre
  const [selectedDate, setSelectedDate] = useState('2026-11-06')
  const [filterType, setFilterType] = useState<string>('todos')

  // Estado del modal de añadir tarea / visita
  const [modalOpen, setModalOpen] = useState(false)
  const [newTitulo, setNewTitulo] = useState('')
  const [newTipo, setNewTipo] = useState<CalendarEventItem['tipo']>('visita')
  const [newFecha, setNewFecha] = useState('2026-11-06')
  const [newHoraInicio, setNewHoraInicio] = useState('10:00')
  const [newHoraFin, setNewHoraFin] = useState('11:00')
  const [newCliente, setNewCliente] = useState('')
  const [newUbicacion, setNewUbicacion] = useState('')
  const [newNotas, setNewNotas] = useState('')

  const handlePrevMonth = () => {
    if (currentMonth === 0) {
      setCurrentMonth(11)
      setCurrentYear((y) => y - 1)
    } else {
      setCurrentMonth((m) => m - 1)
    }
  }

  const handleNextMonth = () => {
    if (currentMonth === 11) {
      setCurrentMonth(0)
      setCurrentYear((y) => y + 1)
    } else {
      setCurrentMonth((m) => m + 1)
    }
  }

  const handleToday = () => {
    setCurrentYear(2026)
    setCurrentMonth(10)
    setSelectedDate('2026-11-06')
  }

  // Generar las celdas del calendario mensual (Lunes a Domingo)
  const calendarDays = useMemo(() => {
    const firstDayOfMonth = new Date(currentYear, currentMonth, 1)
    const daysInMonth = new Date(currentYear, currentMonth + 1, 0).getDate()

    // Convertir getDay() (0=Dom..6=Sáb) a Lunes=0..Domingo=6
    const startOffset = (firstDayOfMonth.getDay() + 6) % 7

    const cells: {
      dateStr: string
      dayNumber: number
      isCurrentMonth: boolean
    }[] = []

    const prevMonthDays = new Date(currentYear, currentMonth, 0).getDate()
    for (let i = startOffset - 1; i >= 0; i--) {
      const d = prevMonthDays - i
      const m = currentMonth === 0 ? 12 : currentMonth
      const y = currentMonth === 0 ? currentYear - 1 : currentYear
      const dateStr = `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`
      cells.push({ dateStr, dayNumber: d, isCurrentMonth: false })
    }

    for (let d = 1; d <= daysInMonth; d++) {
      const m = currentMonth + 1
      const dateStr = `${currentYear}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`
      cells.push({ dateStr, dayNumber: d, isCurrentMonth: true })
    }

    const remaining = (7 - (cells.length % 7)) % 7
    for (let d = 1; d <= remaining; d++) {
      const m = currentMonth === 11 ? 1 : currentMonth + 2
      const y = currentMonth === 11 ? currentYear + 1 : currentYear
      const dateStr = `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`
      cells.push({ dateStr, dayNumber: d, isCurrentMonth: false })
    }

    return cells
  }, [currentYear, currentMonth])

  const filteredEvents = useMemo(() => {
    return eventos.filter((e) =>
      filterType === 'todos' ? true : e.tipo === filterType
    )
  }, [eventos, filterType])

  const eventsByDate = useMemo(() => {
    const map: Record<string, CalendarEventItem[]> = {}
    for (const ev of filteredEvents) {
      if (!map[ev.fecha]) map[ev.fecha] = []
      map[ev.fecha].push(ev)
    }
    for (const k of Object.keys(map)) {
      map[k].sort((a, b) => a.horaInicio.localeCompare(b.horaInicio))
    }
    return map
  }, [filteredEvents])

  const selectedDayEvents = eventsByDate[selectedDate] ?? []

  const handleOpenAddModal = (dateStr?: string) => {
    setNewFecha(dateStr ?? selectedDate)
    setNewTitulo('')
    setNewTipo('visita')
    setNewHoraInicio('10:00')
    setNewHoraFin('11:00')
    setNewCliente('')
    setNewUbicacion('')
    setNewNotas('')
    setModalOpen(true)
  }

  const handleSaveNewEvent = (e: React.FormEvent) => {
    e.preventDefault()
    if (!newTitulo.trim()) {
      toast.error('Introduce un título para la visita o tarea.')
      return
    }
    billingActions.addCalendarEvent({
      fecha: newFecha,
      horaInicio: newHoraInicio,
      horaFin: newHoraFin,
      titulo: newTitulo.trim(),
      tipo: newTipo,
      cliente: newCliente.trim() || undefined,
      ubicacion: newUbicacion.trim() || undefined,
      notas: newNotas.trim() || undefined,
      completado: false,
    })
    setSelectedDate(newFecha)
    setModalOpen(false)
    toast.success(`Evento "${newTitulo.trim()}" añadido al calendario el ${newFecha}`)
  }

  return (
    <>
      <Header>
        <div className='me-auto flex items-center gap-2.5 min-w-0'>
          <span className='text-foreground text-sm font-semibold tracking-tight'>
            Calendario y Visitas
          </span>
          <Badge variant='secondary' className='text-xs font-medium px-2 py-0 h-5 rounded-full'>
            {eventos.length} programadas
          </Badge>
        </div>
        <Search />
        <ThemeSwitch />
        <ProfileDropdown />
      </Header>

      <Main>
        <div className='flex flex-1 flex-col gap-4'>
          {/* Barra Superior del Calendario con Segmented Control macOS */}
          <div className='flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-card border border-border/70 px-4 py-2.5 rounded-xl shadow-2xs'>
            <div className='flex items-center gap-2.5'>
              <div className='inline-flex items-center rounded-lg border border-border/70 bg-background p-0.5 shadow-2xs'>
                <Button
                  type='button'
                  variant='ghost'
                  size='icon'
                  className='h-7 w-7 rounded-md'
                  onClick={handlePrevMonth}
                  aria-label='Mes anterior'
                >
                  <ChevronLeft className='h-4 w-4' />
                </Button>
                <Button
                  type='button'
                  variant='ghost'
                  size='sm'
                  className='h-7 text-xs font-medium px-2.5 rounded-md'
                  onClick={handleToday}
                >
                  Hoy
                </Button>
                <Button
                  type='button'
                  variant='ghost'
                  size='icon'
                  className='h-7 w-7 rounded-md'
                  onClick={handleNextMonth}
                  aria-label='Mes siguiente'
                >
                  <ChevronRight className='h-4 w-4' />
                </Button>
              </div>

              <span className='text-base font-semibold tracking-tight text-foreground ms-1'>
                {MONTH_NAMES[currentMonth]} {currentYear}
              </span>
            </div>

            {/* Segmented Control de tipos de evento + Único botón primario (buttons.md) */}
            <div className='flex flex-wrap items-center gap-2.5'>
              <div
                role='tablist'
                aria-label='Filtrar por tipo de evento'
                className='inline-flex items-center rounded-lg bg-muted/70 p-0.5 border border-border/50 overflow-x-auto'
              >
                {[
                  { id: 'todos', label: 'Todos' },
                  { id: 'visita', label: 'Visitas' },
                  { id: 'reunion', label: 'Reuniones' },
                  { id: 'tarea', label: 'Tareas' },
                  { id: 'fiscal', label: 'Fiscal / AEAT' },
                ].map((f) => (
                  <button
                    key={f.id}
                    type='button'
                    role='tab'
                    aria-selected={filterType === f.id}
                    onClick={() => setFilterType(f.id)}
                    className={`px-3 py-1 rounded-md text-xs font-medium transition-all shrink-0 ${
                      filterType === f.id
                        ? 'bg-background text-foreground shadow-xs'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    {f.label}
                  </button>
                ))}
              </div>

              <Button
                size='sm'
                className='h-8 text-xs font-medium gap-1.5 shadow-2xs'
                onClick={() => handleOpenAddModal()}
                data-testid='btn-nueva-visita'
              >
                <Plus className='h-3.5 w-3.5' />
                <span>Nueva Visita / Tarea</span>
              </Button>
            </div>
          </div>

          {/* Layout Principal: Cuadrícula Mensual (8 col) + Agenda del Día Seleccionado (4 col) */}
          <div className='grid grid-cols-1 lg:grid-cols-12 gap-4 items-start'>
            {/* CUADRÍCULA MENSUAL */}
            <div className='lg:col-span-8 xl:col-span-9'>
              <Card className='border border-border/70 shadow-2xs rounded-xl overflow-hidden'>
                {/* Cabecera Días de la Semana */}
                <div className='grid grid-cols-7 border-b border-border/60 bg-muted/20'>
                  {WEEKDAYS.map((day) => (
                    <div
                      key={day}
                      className='py-2 text-center text-xs font-medium text-muted-foreground border-r border-border/50 last:border-r-0'
                    >
                      {day}
                    </div>
                  ))}
                </div>

                {/* Celdas de los días del mes */}
                <div className='grid grid-cols-7 divide-x divide-y'>
                  {calendarDays.map((cell) => {
                    const dayEvents = eventsByDate[cell.dateStr] ?? []
                    const isSelected = cell.dateStr === selectedDate
                    const isToday = cell.dateStr === '2026-11-06'

                    return (
                      <div
                        key={cell.dateStr}
                        onClick={() => setSelectedDate(cell.dateStr)}
                        onDoubleClick={() => handleOpenAddModal(cell.dateStr)}
                        className={`min-h-[112px] p-2 cursor-pointer transition-colors flex flex-col gap-1 ${
                          !cell.isCurrentMonth
                            ? 'bg-muted/15 text-muted-foreground/50'
                            : 'bg-card hover:bg-muted/20'
                        } ${
                          isSelected
                            ? 'ring-2 ring-inset ring-primary bg-primary/5'
                            : ''
                        }`}
                      >
                        <div className='flex items-center justify-between'>
                          <span
                            className={`inline-flex h-6 w-6 items-center justify-center rounded-full text-xs font-semibold ${
                              isToday
                                ? 'bg-primary text-primary-foreground'
                                : cell.isCurrentMonth
                                  ? 'text-foreground'
                                  : 'text-muted-foreground/50'
                            }`}
                          >
                            {cell.dayNumber}
                          </span>
                          {dayEvents.length > 0 && (
                            <span className='text-[10px] font-mono text-muted-foreground'>
                              {dayEvents.length}
                            </span>
                          )}
                        </div>

                        <div className='flex flex-col gap-1 mt-0.5 overflow-hidden'>
                          {dayEvents.slice(0, 3).map((ev) => {
                            const meta = EVENT_TYPE_META[ev.tipo]
                            return (
                              <div
                                key={ev.id}
                                className={`px-1.5 py-0.5 rounded border text-[10px] leading-tight truncate flex items-center gap-1 ${
                                  meta.badgeClass
                                } ${ev.completado ? 'opacity-50 line-through' : ''}`}
                              >
                                <span
                                  className={`h-1.5 w-1.5 rounded-full shrink-0 ${meta.dotClass}`}
                                />
                                <span className='font-mono font-semibold shrink-0'>
                                  {ev.horaInicio}
                                </span>
                                <span className='truncate font-medium'>
                                  {ev.titulo}
                                </span>
                              </div>
                            )
                          })}
                          {dayEvents.length > 3 && (
                            <span className='text-[10px] text-muted-foreground ps-1'>
                              +{dayEvents.length - 3} más...
                            </span>
                          )}
                        </div>
                      </div>
                    )
                  })}
                </div>
              </Card>
            </div>

            {/* PANEL DERECHO: Agenda detallada del día seleccionado */}
            <div className='lg:col-span-4 xl:col-span-3 flex flex-col gap-3'>
              <Card className='border border-border/80 shadow-xs'>
                <div className='p-3.5 border-b bg-muted/20 flex items-center justify-between'>
                  <div className='flex items-center gap-2'>
                    <CalendarIcon className='h-4 w-4 text-primary' />
                    <div>
                      <span className='text-xs font-semibold uppercase tracking-wider block'>
                        Agenda del Día
                      </span>
                      <span className='text-[11px] font-mono text-muted-foreground'>
                        {selectedDate}
                      </span>
                    </div>
                  </div>

                  <Button
                    size='sm'
                    variant='outline'
                    className='h-7 text-xs gap-1 px-2'
                    onClick={() => handleOpenAddModal(selectedDate)}
                  >
                    <Plus className='h-3 w-3' />
                    <span>Añadir</span>
                  </Button>
                </div>

                <CardContent className='p-3.5 space-y-3'>
                  {selectedDayEvents.length === 0 ? (
                    <div className='py-10 text-center space-y-2'>
                      <p className='text-xs text-muted-foreground'>
                        No hay visitas ni tareas programadas para el {selectedDate}.
                      </p>
                      <Button
                        size='sm'
                        variant='outline'
                        className='h-8 text-xs gap-1.5'
                        onClick={() => handleOpenAddModal(selectedDate)}
                      >
                        <Plus className='h-3.5 w-3.5' />
                        <span>Programar visita este día</span>
                      </Button>
                    </div>
                  ) : (
                    selectedDayEvents.map((ev) => {
                      const meta = EVENT_TYPE_META[ev.tipo]
                      return (
                        <div
                          key={ev.id}
                          className='rounded-lg border p-3 space-y-2 bg-card hover:bg-muted/20 transition-colors'
                        >
                          <div className='flex items-start justify-between gap-2'>
                            <Badge
                              variant='outline'
                              className={`text-[10px] px-1.5 py-0 h-4 ${meta.badgeClass}`}
                            >
                              {meta.label}
                            </Badge>
                            <div className='flex items-center gap-1'>
                              <button
                                type='button'
                                onClick={() => billingActions.toggleCalendarEvent(ev.id)}
                                className='text-muted-foreground hover:text-emerald-500 transition-colors'
                                title={
                                  ev.completado
                                    ? 'Marcar como pendiente'
                                    : 'Marcar como completada'
                                }
                              >
                                {ev.completado ? (
                                  <CheckCircle2 className='h-4 w-4 text-emerald-500' />
                                ) : (
                                  <Circle className='h-4 w-4' />
                                )}
                              </button>
                              <button
                                type='button'
                                onClick={() => {
                                  billingActions.deleteCalendarEvent(ev.id)
                                  toast.info('Evento eliminado del calendario')
                                }}
                                className='text-muted-foreground hover:text-destructive transition-colors'
                                title='Eliminar evento'
                              >
                                <Trash2 className='h-3.5 w-3.5' />
                              </button>
                            </div>
                          </div>

                          <p
                            className={`text-xs font-semibold text-foreground ${
                              ev.completado ? 'line-through text-muted-foreground' : ''
                            }`}
                          >
                            {ev.titulo}
                          </p>

                          <div className='space-y-1 text-[11px] text-muted-foreground'>
                            <div className='flex items-center gap-1.5 font-mono'>
                              <Clock className='h-3 w-3 text-primary' />
                              <span>
                                {ev.horaInicio} - {ev.horaFin}
                              </span>
                            </div>
                            {ev.cliente && (
                              <div className='flex items-center gap-1.5'>
                                <User className='h-3 w-3 text-primary' />
                                <span className='font-medium text-foreground'>
                                  {ev.cliente}
                                </span>
                              </div>
                            )}
                            {ev.ubicacion && (
                              <div className='flex items-center gap-1.5'>
                                <MapPin className='h-3 w-3 text-primary' />
                                <span>{ev.ubicacion}</span>
                              </div>
                            )}
                            {ev.notas && (
                              <p className='text-[11px] bg-muted/30 p-2 rounded border mt-1.5'>
                                {ev.notas}
                              </p>
                            )}
                          </div>
                        </div>
                      )
                    })
                  )}
                </CardContent>
              </Card>
            </div>
          </div>
        </div>
      </Main>

      {/* Modal con Fondo Blur para añadir Visita / Tarea / Cita */}
      <Dialog open={modalOpen} onOpenChange={setModalOpen}>
        <DialogContent className='sm:max-w-[480px]'>
          <DialogHeader>
            <DialogTitle>Programar Visita, Cita o Tarea</DialogTitle>
            <DialogDescription>
              Añade una visita comercial a cliente, recordatorio operativo o vencimiento fiscal en el calendario.
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={handleSaveNewEvent} className='space-y-3.5'>
            <div>
              <label className='text-xs font-medium text-foreground'>
                Título de la visita o tarea *
              </label>
              <Input
                placeholder='Ej. Visita comercial e instalación lector TPV'
                value={newTitulo}
                onChange={(e) => setNewTitulo(e.target.value)}
                className='h-9 text-xs mt-1'
                data-testid='input-calendario-titulo'
              />
            </div>

            <div className='grid grid-cols-2 gap-3'>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Tipo de Evento
                </label>
                <select
                  value={newTipo}
                  onChange={(e) =>
                    setNewTipo(e.target.value as CalendarEventItem['tipo'])
                  }
                  className='h-9 w-full rounded-md border border-input bg-background px-2.5 text-xs mt-1'
                >
                  <option value='visita'>Visita a Cliente</option>
                  <option value='reunion'>Reunión / Cita</option>
                  <option value='tarea'>Tarea Operativa</option>
                  <option value='fiscal'>Hacienda / Fiscal</option>
                </select>
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Fecha
                </label>
                <Input
                  type='date'
                  value={newFecha}
                  onChange={(e) => setNewFecha(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
            </div>

            <div className='grid grid-cols-2 gap-3'>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Hora Inicio
                </label>
                <Input
                  type='time'
                  value={newHoraInicio}
                  onChange={(e) => setNewHoraInicio(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Hora Fin
                </label>
                <Input
                  type='time'
                  value={newHoraFin}
                  onChange={(e) => setNewHoraFin(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
            </div>

            <div className='grid grid-cols-2 gap-3'>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Cliente (Opcional)
                </label>
                <Input
                  placeholder='Ej. Darytravel S.L.'
                  value={newCliente}
                  onChange={(e) => setNewCliente(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Ubicación / Dirección
                </label>
                <Input
                  placeholder='Ej. Calle Mayor 12'
                  value={newUbicacion}
                  onChange={(e) => setNewUbicacion(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
            </div>

            <div>
              <label className='text-xs font-medium text-foreground'>
                Notas adicionales
              </label>
              <Input
                placeholder='Materiales a llevar, acuerdos o detalles...'
                value={newNotas}
                onChange={(e) => setNewNotas(e.target.value)}
                className='h-9 text-xs mt-1'
              />
            </div>

            <DialogFooter className='pt-2'>
              <Button
                type='button'
                variant='outline'
                size='sm'
                onClick={() => setModalOpen(false)}
              >
                Cancelar
              </Button>
              <Button
                type='submit'
                size='sm'
                data-testid='btn-guardar-visita'
              >
                Guardar en Calendario
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}
