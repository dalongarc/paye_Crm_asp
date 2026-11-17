import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { Printer, Receipt, ArrowRight } from 'lucide-react'
import type { QuoteItem, CompanyConfig } from '@/lib/billing-store'
import { billingActions } from '@/lib/billing-store'
import { toast } from 'sonner'

interface PresupuestoPreviewDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  presupuesto: QuoteItem | null
  company: CompanyConfig
  onConvertedToInvoice?: () => void
}

export function PresupuestoPreviewDialog({
  open,
  onOpenChange,
  presupuesto,
  company,
  onConvertedToInvoice,
}: PresupuestoPreviewDialogProps) {
  if (!presupuesto) return null

  const handlePrint = () => {
    window.print()
  }

  const handleConvertToInvoice = () => {
    const inv = billingActions.convertQuoteToInvoice(presupuesto.id)
    if (inv) {
      toast.success(
        `Presupuesto ${presupuesto.numero} convertido en Factura ${inv.numero} correctamente`
      )
      onConvertedToInvoice?.()
      onOpenChange(false)
    }
  }

  const getStatusBadge = (estado: QuoteItem['estado']) => {
    switch (estado) {
      case 'facturado':
        return (
          <Badge className='bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20 text-xs font-medium'>
            Convertido en Factura
          </Badge>
        )
      case 'aceptado':
        return (
          <Badge className='bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20 text-xs font-medium'>
            Aceptado
          </Badge>
        )
      case 'rechazado':
        return (
          <Badge variant='destructive' className='text-xs font-medium'>
            Rechazado
          </Badge>
        )
      case 'pendiente':
      default:
        return (
          <Badge variant='outline' className='text-amber-600 border-amber-500/30 text-xs font-medium'>
            Pendiente de aprobación
          </Badge>
        )
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='max-w-2xl sm:max-w-3xl max-h-[90vh] overflow-y-auto p-0 gap-0'>
        <DialogHeader className='p-6 pb-4 border-b border-border/60 bg-muted/20'>
          <div className='flex items-center justify-between gap-4 pe-6'>
            <div className='space-y-1'>
              <div className='flex items-center gap-2'>
                <DialogTitle className='text-lg font-bold font-mono tracking-tight text-foreground'>
                  {presupuesto.numero}
                </DialogTitle>
                {getStatusBadge(presupuesto.estado)}
              </div>
              <p className='text-xs text-muted-foreground'>
                Fecha: {presupuesto.fecha} · Válido hasta: {presupuesto.fechaValidez}
              </p>
            </div>
            <div className='flex items-center gap-2'>
              <Button
                variant='outline'
                size='sm'
                className='h-8 text-xs gap-1.5'
                onClick={handlePrint}
              >
                <Printer className='h-3.5 w-3.5' />
                <span>Imprimir</span>
              </Button>
              {presupuesto.estado !== 'facturado' && (
                <Button
                  size='sm'
                  className='h-8 text-xs gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white'
                  onClick={handleConvertToInvoice}
                >
                  <Receipt className='h-3.5 w-3.5' />
                  <span>Convertir a Factura</span>
                  <ArrowRight className='h-3 w-3' />
                </Button>
              )}
            </div>
          </div>
        </DialogHeader>

        {/* Contenido Visual del Presupuesto */}
        <div className='p-6 space-y-6 text-xs bg-background'>
          <div className='grid sm:grid-cols-2 gap-6'>
            <div className='p-3.5 rounded-xl border border-border/70 bg-muted/10 space-y-1'>
              <span className='text-[10px] font-semibold text-muted-foreground uppercase tracking-wider block'>
                Emisor / Empresa
              </span>
              <p className='font-bold text-sm text-foreground'>{company.razonSocial}</p>
              <p className='text-muted-foreground font-mono'>NIF/CIF: {company.nifCif}</p>
              <p className='text-muted-foreground'>{company.direccionFiscal}</p>
              <p className='text-muted-foreground'>
                {company.codigoPostal} {company.ciudad} ({company.provincia})
              </p>
            </div>

            <div className='p-3.5 rounded-xl border border-border/70 bg-muted/10 space-y-1'>
              <span className='text-[10px] font-semibold text-muted-foreground uppercase tracking-wider block'>
                Cliente / Destinatario
              </span>
              <p className='font-bold text-sm text-foreground'>{presupuesto.cliente}</p>
              <p className='text-muted-foreground font-mono'>NIF/CIF: {presupuesto.cifNif}</p>
              <p className='text-muted-foreground'>{presupuesto.direccion}</p>
              {presupuesto.email && (
                <p className='text-muted-foreground'>{presupuesto.email}</p>
              )}
            </div>
          </div>

          <div className='border rounded-lg overflow-hidden'>
            <table className='w-full text-left'>
              <thead className='bg-muted/40 border-b text-muted-foreground font-medium text-xs'>
                <tr>
                  <th className='py-2.5 px-3'>Descripción de la Propuesta</th>
                  <th className='py-2.5 px-3 text-right w-16'>Cant.</th>
                  <th className='py-2.5 px-3 text-right w-24'>Precio</th>
                  <th className='py-2.5 px-3 text-right w-16'>IVA</th>
                  <th className='py-2.5 px-3 text-right w-24'>Total</th>
                </tr>
              </thead>
              <tbody className='divide-y text-xs'>
                {presupuesto.lineas.map((linea, idx) => (
                  <tr key={linea.id || idx} className='hover:bg-muted/10'>
                    <td className='py-2 px-3 font-medium text-foreground'>
                      {linea.concepto}
                    </td>
                    <td className='py-2 px-3 text-right font-mono'>{linea.cantidad}</td>
                    <td className='py-2 px-3 text-right font-mono'>
                      {linea.precioUnitario.toLocaleString('es-ES', {
                        minimumFractionDigits: 2,
                      })}{' '}
                      €
                    </td>
                    <td className='py-2 px-3 text-right font-mono text-muted-foreground'>
                      {linea.ivaPercent}%
                    </td>
                    <td className='py-2 px-3 text-right font-mono font-semibold'>
                      {linea.total.toLocaleString('es-ES', {
                        minimumFractionDigits: 2,
                      })}{' '}
                      €
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className='flex justify-end'>
            <div className='w-full sm:w-64 space-y-1.5 p-3.5 rounded-xl border border-border/80 bg-muted/15 font-mono text-xs'>
              <div className='flex justify-between text-muted-foreground'>
                <span>Base Imponible:</span>
                <span>
                  {presupuesto.baseImponible.toLocaleString('es-ES', {
                    minimumFractionDigits: 2,
                  })}{' '}
                  €
                </span>
              </div>
              <div className='flex justify-between text-muted-foreground'>
                <span>Total IVA:</span>
                <span>
                  {presupuesto.iva.toLocaleString('es-ES', {
                    minimumFractionDigits: 2,
                  })}{' '}
                  €
                </span>
              </div>
              <Separator className='my-2' />
              <div className='flex justify-between text-sm font-bold text-foreground'>
                <span>Total Presupuesto:</span>
                <span className='text-emerald-600 dark:text-emerald-400'>
                  {presupuesto.total.toLocaleString('es-ES', {
                    minimumFractionDigits: 2,
                  })}{' '}
                  €
                </span>
              </div>
            </div>
          </div>

          {presupuesto.notas && (
            <div className='p-3 rounded-lg border border-border/60 bg-muted/10 text-[11px] text-muted-foreground space-y-1'>
              <span className='font-medium text-foreground block'>Condiciones del Presupuesto:</span>
              <p className='whitespace-pre-line'>{presupuesto.notas}</p>
            </div>
          )}
        </div>

        <DialogFooter className='p-4 border-t border-border/60 bg-muted/20'>
          <Button variant='outline' size='sm' onClick={() => onOpenChange(false)}>
            Cerrar
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
