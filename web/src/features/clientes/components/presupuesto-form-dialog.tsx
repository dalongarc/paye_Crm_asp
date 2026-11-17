import { useState, useEffect } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Plus, Trash2, FileText } from 'lucide-react'
import { toast } from 'sonner'
import type { ClienteDto } from '@/api'
import {
  billingActions,
  getNextQuoteNumber,
  type QuoteItem,
  type LineaFactura,
} from '@/lib/billing-store'

interface PresupuestoFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  cliente: ClienteDto | null
  existingPresupuestos: QuoteItem[]
}

export function PresupuestoFormDialog({
  open,
  onOpenChange,
  cliente,
  existingPresupuestos,
}: PresupuestoFormDialogProps) {
  const [numero, setNumero] = useState('P2026-01')
  const [fecha, setFecha] = useState(() => new Date().toISOString().split('T')[0])
  const [fechaValidez, setFechaValidez] = useState(() => {
    const d = new Date()
    d.setDate(d.getDate() + 30)
    return d.toISOString().split('T')[0]
  })
  const [notas, setNotas] = useState('Presupuesto válido durante 30 días. Forma de pago: 50% al aceptar y 50% a la entrega.')
  const [manualNombre, setManualNombre] = useState('')
  const [manualCifNif, setManualCifNif] = useState('')
  const [lineas, setLineas] = useState<LineaFactura[]>([
    {
      id: 'pl-1',
      concepto: 'Servicio comercial / propuesta de proyecto',
      cantidad: 1,
      precioUnitario: 350,
      ivaPercent: 21,
      total: 350,
    },
  ])

  useEffect(() => {
    if (open) {
      const nextNum = getNextQuoteNumber(existingPresupuestos, new Date().getFullYear(), 'P')
      setNumero(nextNum)
      const today = new Date().toISOString().split('T')[0]
      setFecha(today)
      const validDate = new Date()
      validDate.setDate(validDate.getDate() + 30)
      setFechaValidez(validDate.toISOString().split('T')[0])
      setManualNombre(cliente?.nombreFiscal || '')
      setManualCifNif(cliente?.cifNif || '')
      setLineas([
        {
          id: `pl-${Date.now()}`,
          concepto: 'Propuesta comercial o servicios a medida',
          cantidad: 1,
          precioUnitario: 250,
          ivaPercent: 21,
          total: 250,
        },
      ])
    }
  }, [open, existingPresupuestos, cliente])

  const handleUpdateLine = (id: string, patch: Partial<LineaFactura>) => {
    setLineas((prev) =>
      prev.map((l) => {
        if (l.id !== id) return l
        const updated = { ...l, ...patch }
        const cant = typeof updated.cantidad === 'number' ? updated.cantidad : 0
        const precio = typeof updated.precioUnitario === 'number' ? updated.precioUnitario : 0
        updated.total = Math.round(cant * precio * 100) / 100
        return updated
      })
    )
  }

  const handleAddLine = () => {
    setLineas((prev) => [
      ...prev,
      {
        id: `pl-${Date.now()}-${prev.length}`,
        concepto: '',
        cantidad: 1,
        precioUnitario: 0,
        ivaPercent: 21,
        total: 0,
      },
    ])
  }

  const handleRemoveLine = (id: string) => {
    if (lineas.length <= 1) {
      toast.error('El presupuesto debe contener al menos una línea.')
      return
    }
    setLineas((prev) => prev.filter((l) => l.id !== id))
  }

  // Totals
  let baseImponible = 0
  let ivaTotal = 0
  for (const l of lineas) {
    const net = Math.round(l.cantidad * l.precioUnitario * 100) / 100
    baseImponible += net
    ivaTotal += Math.round(net * (l.ivaPercent / 100) * 100) / 100
  }
  const total = Math.round((baseImponible + ivaTotal) * 100) / 100

  const handleSave = () => {
    const validLines = lineas.filter((l) => l.concepto.trim().length > 0)
    if (validLines.length === 0) {
      toast.error('Escribe al menos un concepto en las líneas del presupuesto.')
      return
    }

    const direccionCompleta = cliente
      ? [
          cliente.direccion,
          cliente.codigoPostal,
          cliente.ciudad,
          cliente.provincia,
        ]
          .filter(Boolean)
          .join(', ')
      : ''

    const resolvedNombre = (cliente?.nombreFiscal || manualNombre || 'Cliente sin asignar').trim()
    const resolvedCif = (cliente?.cifNif || manualCifNif || '').trim()

    const quote: QuoteItem = {
      id: `quote-${Date.now()}`,
      numero,
      serie: 'P',
      fecha,
      fechaValidez,
      clienteId: cliente?.id,
      cliente: resolvedNombre,
      cifNif: resolvedCif,
      direccion: direccionCompleta || 'Dirección pendiente de confirmar',
      email: cliente?.email || '',
      baseImponible,
      iva: ivaTotal,
      total,
      estado: 'pendiente',
      notas,
      lineas: validLines,
    }

    billingActions.saveQuote(quote)
    toast.success(`Presupuesto ${quote.numero} creado para ${resolvedNombre}`)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='max-w-2xl sm:max-w-3xl max-h-[90vh] overflow-y-auto'>
        <DialogHeader>
          <div className='flex items-center gap-2'>
            <FileText className='h-5 w-5 text-emerald-600 dark:text-emerald-400' />
            <DialogTitle className='text-base font-semibold'>
              {cliente
                ? `Nuevo Presupuesto para ${cliente.nombreFiscal}`
                : 'Nuevo Presupuesto (Sin cliente CRM)'}
            </DialogTitle>
          </div>
        </DialogHeader>

        <div className='space-y-4 text-xs py-2'>
          {!cliente && (
            <div className='grid sm:grid-cols-2 gap-3 p-3.5 rounded-xl border border-border/70 bg-muted/10'>
              <div>
                <Label className='text-xs text-muted-foreground'>
                  Nombre del Destinatario / Cliente
                </Label>
                <Input
                  value={manualNombre}
                  onChange={(e) => setManualNombre(e.target.value)}
                  placeholder='Ej. Empresa o particular'
                  className='h-8 text-xs mt-1'
                />
              </div>
              <div>
                <Label className='text-xs text-muted-foreground'>
                  CIF / NIF (Opcional)
                </Label>
                <Input
                  value={manualCifNif}
                  onChange={(e) => setManualCifNif(e.target.value)}
                  placeholder='Sin CIF/NIF si es borrador'
                  className='h-8 text-xs font-mono mt-1'
                />
              </div>
            </div>
          )}
          {/* Fila Metadatos */}
          <div className='grid sm:grid-cols-3 gap-3 p-3.5 rounded-xl border border-border/70 bg-muted/10'>
            <div>
              <Label className='text-xs text-muted-foreground'>Número de Presupuesto</Label>
              <Input
                value={numero}
                onChange={(e) => setNumero(e.target.value)}
                className='h-8 text-xs font-mono font-semibold mt-1'
              />
            </div>
            <div>
              <Label className='text-xs text-muted-foreground'>Fecha de Emisión</Label>
              <Input
                type='date'
                value={fecha}
                onChange={(e) => setFecha(e.target.value)}
                className='h-8 text-xs font-mono mt-1'
              />
            </div>
            <div>
              <Label className='text-xs text-muted-foreground'>Válido Hasta</Label>
              <Input
                type='date'
                value={fechaValidez}
                onChange={(e) => setFechaValidez(e.target.value)}
                className='h-8 text-xs font-mono mt-1'
              />
            </div>
          </div>

          {/* Líneas de Concepto */}
          <div className='space-y-2'>
            <div className='flex items-center justify-between'>
              <Label className='text-xs font-semibold text-foreground'>
                Líneas de propuesta comercial
              </Label>
              <Button
                type='button'
                variant='outline'
                size='sm'
                onClick={handleAddLine}
                className='h-7 text-xs gap-1'
              >
                <Plus className='h-3.5 w-3.5' />
                <span>Añadir concepto</span>
              </Button>
            </div>

            <div className='border rounded-lg overflow-hidden'>
              <table className='w-full text-left text-xs'>
                <thead className='bg-muted/40 border-b text-muted-foreground font-medium'>
                  <tr>
                    <th className='py-2 px-3'>Descripción / Concepto</th>
                    <th className='py-2 px-2 text-right w-16'>Cant.</th>
                    <th className='py-2 px-2 text-right w-24'>Precio (€)</th>
                    <th className='py-2 px-2 text-right w-16'>IVA %</th>
                    <th className='py-2 px-2 text-right w-20'>Total</th>
                    <th className='py-2 px-2 w-10'></th>
                  </tr>
                </thead>
                <tbody className='divide-y'>
                  {lineas.map((linea) => (
                    <tr key={linea.id} className='hover:bg-muted/10'>
                      <td className='py-1.5 px-2'>
                        <Input
                          placeholder='Descripción del servicio...'
                          value={linea.concepto}
                          onChange={(e) =>
                            handleUpdateLine(linea.id, { concepto: e.target.value })
                          }
                          className='h-7 text-xs'
                        />
                      </td>
                      <td className='py-1.5 px-2'>
                        <Input
                          type='number'
                          min='1'
                          value={linea.cantidad}
                          onChange={(e) =>
                            handleUpdateLine(linea.id, {
                              cantidad: parseFloat(e.target.value) || 0,
                            })
                          }
                          className='h-7 text-xs font-mono text-right'
                        />
                      </td>
                      <td className='py-1.5 px-2'>
                        <Input
                          type='number'
                          step='0.01'
                          min='0'
                          value={linea.precioUnitario}
                          onChange={(e) =>
                            handleUpdateLine(linea.id, {
                              precioUnitario: parseFloat(e.target.value) || 0,
                            })
                          }
                          className='h-7 text-xs font-mono text-right'
                        />
                      </td>
                      <td className='py-1.5 px-2'>
                        <select
                          value={linea.ivaPercent}
                          onChange={(e) =>
                            handleUpdateLine(linea.id, {
                              ivaPercent: parseFloat(e.target.value) || 0,
                            })
                          }
                          className='h-7 rounded-md border border-input bg-background px-1 text-xs text-right font-mono w-full'
                        >
                          <option value='21'>21%</option>
                          <option value='10'>10%</option>
                          <option value='4'>4%</option>
                          <option value='0'>0%</option>
                        </select>
                      </td>
                      <td className='py-1.5 px-2 text-right font-mono font-semibold'>
                        {linea.total.toLocaleString('es-ES', { minimumFractionDigits: 2 })}{' '}
                        €
                      </td>
                      <td className='py-1.5 px-2 text-center'>
                        <Button
                          type='button'
                          variant='ghost'
                          size='icon'
                          className='h-6 w-6 text-muted-foreground hover:text-destructive'
                          onClick={() => handleRemoveLine(linea.id)}
                        >
                          <Trash2 className='h-3.5 w-3.5' />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Totales y Notas */}
          <div className='grid sm:grid-cols-2 gap-4 items-start pt-2'>
            <div>
              <Label className='text-xs text-muted-foreground'>
                Condiciones y Notas del Presupuesto
              </Label>
              <Textarea
                rows={3}
                value={notas}
                onChange={(e) => setNotas(e.target.value)}
                placeholder='Condiciones de entrega, validez de la oferta...'
                className='text-xs mt-1'
              />
            </div>

            <div className='p-3.5 rounded-xl border border-border/80 bg-muted/15 space-y-1.5 font-mono text-xs'>
              <div className='flex justify-between text-muted-foreground'>
                <span>Base Imponible:</span>
                <span>
                  {baseImponible.toLocaleString('es-ES', { minimumFractionDigits: 2 })} €
                </span>
              </div>
              <div className='flex justify-between text-muted-foreground'>
                <span>Total IVA:</span>
                <span>
                  {ivaTotal.toLocaleString('es-ES', { minimumFractionDigits: 2 })} €
                </span>
              </div>
              <div className='flex justify-between text-sm font-bold text-foreground border-t pt-1.5'>
                <span>Total Presupuesto:</span>
                <span className='text-emerald-600 dark:text-emerald-400'>
                  {total.toLocaleString('es-ES', { minimumFractionDigits: 2 })} €
                </span>
              </div>
            </div>
          </div>
        </div>

        <DialogFooter className='gap-2 sm:gap-0'>
          <Button variant='outline' size='sm' onClick={() => onOpenChange(false)}>
            Cancelar
          </Button>
          <Button size='sm' onClick={handleSave} className='gap-1.5'>
            <FileText className='h-3.5 w-3.5' />
            <span>Guardar Presupuesto</span>
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
