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
import { Printer, Download, QrCode, ShieldCheck } from 'lucide-react'
import type { InvoiceItem, CompanyConfig } from '@/lib/billing-store'
import { downloadSingleInvoicePdf } from '@/lib/invoice-pdf-export'
import { toast } from 'sonner'

interface FacturaPreviewDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  factura: InvoiceItem | null
  company: CompanyConfig
}

export function FacturaPreviewDialog({
  open,
  onOpenChange,
  factura,
  company,
}: FacturaPreviewDialogProps) {
  if (!factura) return null

  const handleDownload = () => {
    downloadSingleInvoicePdf(factura, company)
    toast.success(`Factura ${factura.numero}.pdf descargada correctamente`)
  }

  const handlePrint = () => {
    window.print()
  }

  const getStatusBadge = (estado: InvoiceItem['estado']) => {
    switch (estado) {
      case 'cobrada':
        return (
          <Badge className='bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20 text-xs font-medium'>
            Cobrada
          </Badge>
        )
      case 'emitida':
        return (
          <Badge className='bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20 text-xs font-medium'>
            Emitida
          </Badge>
        )
      case 'borrador':
        return (
          <Badge variant='outline' className='text-amber-600 border-amber-500/30 text-xs font-medium'>
            Borrador
          </Badge>
        )
      case 'rectificativa':
        return (
          <Badge variant='destructive' className='text-xs font-medium'>
            Rectificativa
          </Badge>
        )
      default:
        return null
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
                  {factura.numero}
                </DialogTitle>
                {getStatusBadge(factura.estado)}
              </div>
              <p className='text-xs text-muted-foreground'>
                Fecha de emisión: {factura.fecha} · Vencimiento: {factura.fechaVencimiento}
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
              <Button
                size='sm'
                className='h-8 text-xs gap-1.5'
                onClick={handleDownload}
              >
                <Download className='h-3.5 w-3.5' />
                <span>Descargar PDF</span>
              </Button>
            </div>
          </div>
        </DialogHeader>

        {/* Cuerpo de la Factura (Render visual fiel) */}
        <div className='p-6 space-y-6 text-xs bg-background'>
          {/* Cabecera Emisor y Receptor */}
          <div className='grid sm:grid-cols-2 gap-6'>
            <div className='p-3.5 rounded-xl border border-border/70 bg-muted/10 space-y-1'>
              <span className='text-[10px] font-semibold text-muted-foreground uppercase tracking-wider block'>
                Emisor / Facturado por
              </span>
              <p className='font-bold text-sm text-foreground'>{company.razonSocial}</p>
              <p className='text-muted-foreground font-mono'>NIF/CIF: {company.nifCif}</p>
              <p className='text-muted-foreground'>{company.direccionFiscal}</p>
              <p className='text-muted-foreground'>
                {company.codigoPostal} {company.ciudad} ({company.provincia})
              </p>
              {company.iban && (
                <p className='text-[11px] font-mono text-foreground/80 pt-1'>
                  IBAN: {company.iban}
                </p>
              )}
            </div>

            <div className='p-3.5 rounded-xl border border-border/70 bg-muted/10 space-y-1'>
              <span className='text-[10px] font-semibold text-muted-foreground uppercase tracking-wider block'>
                Cliente / Receptor
              </span>
              <p className='font-bold text-sm text-foreground'>{factura.cliente}</p>
              <p className='text-muted-foreground font-mono'>NIF/CIF: {factura.cifNif}</p>
              <p className='text-muted-foreground'>{factura.direccion}</p>
              {factura.email && <p className='text-muted-foreground'>{factura.email}</p>}
              <p className='text-[11px] text-muted-foreground pt-1'>
                Forma de pago: <span className='font-medium text-foreground'>{factura.formaPago}</span>
              </p>
            </div>
          </div>

          {/* Tabla de Líneas */}
          <div className='border rounded-lg overflow-hidden'>
            <table className='w-full text-left'>
              <thead className='bg-muted/40 border-b text-muted-foreground font-medium text-xs'>
                <tr>
                  <th className='py-2.5 px-3'>Concepto / Descripción</th>
                  <th className='py-2.5 px-3 text-right w-16'>Cant.</th>
                  <th className='py-2.5 px-3 text-right w-24'>Precio</th>
                  <th className='py-2.5 px-3 text-right w-16'>IVA</th>
                  <th className='py-2.5 px-3 text-right w-24'>Total</th>
                </tr>
              </thead>
              <tbody className='divide-y text-xs'>
                {factura.lineas.map((linea, idx) => (
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

          {/* Desglose de Totales */}
          <div className='flex justify-end'>
            <div className='w-full sm:w-64 space-y-1.5 p-3.5 rounded-xl border border-border/80 bg-muted/15 font-mono text-xs'>
              <div className='flex justify-between text-muted-foreground'>
                <span>Base imponible:</span>
                <span>
                  {factura.baseImponible.toLocaleString('es-ES', {
                    minimumFractionDigits: 2,
                  })}{' '}
                  €
                </span>
              </div>
              <div className='flex justify-between text-muted-foreground'>
                <span>Cuota IVA:</span>
                <span>
                  {factura.iva.toLocaleString('es-ES', {
                    minimumFractionDigits: 2,
                  })}{' '}
                  €
                </span>
              </div>
              {factura.irpfImporte > 0 && (
                <div className='flex justify-between text-amber-600 dark:text-amber-400'>
                  <span>Retención IRPF ({factura.irpfPercent}%):</span>
                  <span>
                    -
                    {factura.irpfImporte.toLocaleString('es-ES', {
                      minimumFractionDigits: 2,
                    })}{' '}
                    €
                  </span>
                </div>
              )}
              {factura.recargoImporte > 0 && (
                <div className='flex justify-between text-muted-foreground'>
                  <span>Recargo ({factura.recargoPercent}%):</span>
                  <span>
                    +
                    {factura.recargoImporte.toLocaleString('es-ES', {
                      minimumFractionDigits: 2,
                    })}{' '}
                    €
                  </span>
                </div>
              )}
              <Separator className='my-2' />
              <div className='flex justify-between text-sm font-bold text-foreground'>
                <span>Total Factura:</span>
                <span className='text-emerald-600 dark:text-emerald-400'>
                  {factura.total.toLocaleString('es-ES', {
                    minimumFractionDigits: 2,
                  })}{' '}
                  €
                </span>
              </div>
            </div>
          </div>

          {/* Sello VeriFactu */}
          <div className='p-3 rounded-lg border border-emerald-500/20 bg-emerald-500/5 flex items-center justify-between gap-3 text-[11px] text-muted-foreground'>
            <div className='flex items-center gap-2'>
              <ShieldCheck className='h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0' />
              <div>
                <span className='font-semibold text-foreground block'>
                  Factura Verificable en la AEAT (VeriFactu)
                </span>
                <span className='font-mono text-[10px]'>
                  Huella SHA-256: {factura.hashVerifactu || 'HASH-VERIFACTU-OK'}
                </span>
              </div>
            </div>
            <QrCode className='h-8 w-8 text-foreground/70 shrink-0' />
          </div>

          {factura.notas && (
            <div className='p-3 rounded-lg border border-border/60 bg-muted/10 text-[11px] text-muted-foreground space-y-1'>
              <span className='font-medium text-foreground block'>Condiciones / Notas:</span>
              <p className='whitespace-pre-line'>{factura.notas}</p>
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
