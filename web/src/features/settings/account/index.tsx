import { useState } from 'react'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import {
  Percent,
  Plus,
  Check,
  Package,
} from 'lucide-react'
import { toast } from 'sonner'
import {
  useBillingStore,
  billingActions,
  type TaxRateItem,
} from '@/lib/billing-store'

export function SettingsAccount() {
  const { taxes, catalog } = useBillingStore()

  // Nuevo Impuesto
  const [taxCode, setTaxCode] = useState('')
  const [taxName, setTaxName] = useState('')
  const [taxPercent, setTaxPercent] = useState<number>(21)
  const [taxKind, setTaxKind] = useState<TaxRateItem['kind']>('iva')

  // Nuevo Producto / Servicio en Catálogo
  const [catSku, setCatSku] = useState('')
  const [catConcepto, setCatConcepto] = useState('')
  const [catTipo, setCatTipo] = useState<'servicio' | 'producto'>('servicio')
  const [catPrecio, setCatPrecio] = useState<number>(100)
  const catIva = 21

  const handleAddTax = (e: React.FormEvent) => {
    e.preventDefault()
    if (!taxCode.trim() || !taxName.trim()) {
      toast.error('Introduce el código y el nombre del impuesto.')
      return
    }
    billingActions.addTax({
      code: taxCode.trim().toUpperCase(),
      name: taxName.trim(),
      percentage: taxPercent,
      kind: taxKind,
      isDefault: false,
      active: true,
    })
    setTaxCode('')
    setTaxName('')
    toast.success(`Impuesto "${taxName.trim()}" añadido y disponible en facturas.`)
  }

  const handleAddCatalog = (e: React.FormEvent) => {
    e.preventDefault()
    if (!catConcepto.trim()) {
      toast.error('Introduce el concepto del servicio o producto.')
      return
    }
    billingActions.addCatalogItem({
      sku: catSku.trim().toUpperCase() || `ITEM-${catalog.length + 1}`,
      concepto: catConcepto.trim(),
      categoria: catTipo,
      precioUnitario: catPrecio,
      ivaPercent: catIva,
    })
    setCatSku('')
    setCatConcepto('')
    toast.success(`"${catConcepto.trim()}" añadido al selector de la plantilla de factura.`)
  }

  return (
    <div className='space-y-5'>
      {/* Catálogo de Impuestos (IVA, IRPF, Recargo de Equivalencia) */}
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <CardTitle className='text-sm font-semibold flex items-center gap-2'>
            <Percent className='h-4 w-4 text-primary' />
            <span>Configuración de Impuestos (IVA, IRPF y Recargo de Equivalencia)</span>
          </CardTitle>
          <CardDescription className='text-xs'>
            Marca cuál es el tipo predeterminado para que al abrir una nueva factura se calcule automáticamente.
          </CardDescription>
        </CardHeader>

        <CardContent className='space-y-4'>
          <div className='grid md:grid-cols-3 gap-4'>
            {(['iva', 'irpf', 'recargo'] as const).map((kind) => {
              const title =
                kind === 'iva'
                  ? 'Tipos de IVA (+)'
                  : kind === 'irpf'
                    ? 'Retenciones IRPF (-)'
                    : 'Recargo Equivalencia (+)'
              const list = taxes.filter((t) => t.kind === kind)
              return (
                <div key={kind} className='border rounded-lg p-3 space-y-2 bg-muted/15'>
                  <span className='text-xs font-semibold uppercase tracking-wider text-foreground block'>
                    {title}
                  </span>
                  <div className='space-y-1.5'>
                    {list.map((t) => (
                      <div
                        key={t.id}
                        className='flex items-center justify-between gap-2 p-2 rounded border bg-card text-xs'
                      >
                        <div>
                          <span className='font-medium text-foreground block'>
                            {t.name}
                          </span>
                          <span className='font-mono text-[11px] text-muted-foreground'>
                            {t.code} · {t.percentage}%
                          </span>
                        </div>
                        <Button
                          type='button'
                          size='sm'
                          variant={t.isDefault ? 'default' : 'outline'}
                          className='h-7 text-[11px] px-2 gap-1'
                          onClick={() => {
                            billingActions.toggleTaxDefault(t.id)
                            toast.success(
                              `${t.name} establecido como predeterminado en nuevas facturas.`
                            )
                          }}
                        >
                          {t.isDefault && <Check className='h-3 w-3' />}
                          <span>{t.isDefault ? 'Por defecto' : 'Usar por defecto'}</span>
                        </Button>
                      </div>
                    ))}
                  </div>
                </div>
              )
            })}
          </div>

          {/* Formulario para añadir nuevo tipo impositivo */}
          <form
            onSubmit={handleAddTax}
            className='grid sm:grid-cols-5 gap-2.5 items-end bg-muted/20 p-3 rounded-lg border'
          >
            <div>
              <label className='text-[10px] uppercase font-semibold text-muted-foreground'>
                Tipo
              </label>
              <select
                value={taxKind}
                onChange={(e) => setTaxKind(e.target.value as TaxRateItem['kind'])}
                className='h-8 w-full rounded-md border border-input bg-background px-2 text-xs mt-0.5'
              >
                <option value='iva'>IVA (+)</option>
                <option value='irpf'>Retención IRPF (-)</option>
                <option value='recargo'>Recargo Eq. (+)</option>
              </select>
            </div>
            <div>
              <label className='text-[10px] uppercase font-semibold text-muted-foreground'>
                Código Fiscal
              </label>
              <Input
                placeholder='Ej. IGIC7'
                value={taxCode}
                onChange={(e) => setTaxCode(e.target.value)}
                className='h-8 text-xs font-mono mt-0.5 bg-background'
              />
            </div>
            <div className='sm:col-span-2'>
              <label className='text-[10px] uppercase font-semibold text-muted-foreground'>
                Nombre descriptivo
              </label>
              <Input
                placeholder='Ej. IGIC General (7%)'
                value={taxName}
                onChange={(e) => setTaxName(e.target.value)}
                className='h-8 text-xs mt-0.5 bg-background'
              />
            </div>
            <div className='flex items-end gap-2'>
              <div className='flex-1'>
                <label className='text-[10px] uppercase font-semibold text-muted-foreground'>
                  %
                </label>
                <Input
                  type='number'
                  step='0.1'
                  value={taxPercent}
                  onChange={(e) => setTaxPercent(parseFloat(e.target.value) || 0)}
                  className='h-8 text-xs font-mono mt-0.5 bg-background'
                />
              </div>
              <Button type='submit' size='sm' className='h-8 text-xs gap-1 shrink-0'>
                <Plus className='h-3.5 w-3.5' />
                <span>Añadir</span>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {/* Catálogo de Servicios y Productos para insertar rápido en Facturas */}
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <CardTitle className='text-sm font-semibold flex items-center gap-2'>
            <Package className='h-4 w-4 text-primary' />
            <span>Catálogo de Servicios y Productos (Para Nueva Factura)</span>
          </CardTitle>
          <CardDescription className='text-xs'>
            Estos conceptos aparecen en el desplegable al crear o editar una factura para insertarlos con un clic.
          </CardDescription>
        </CardHeader>

        <CardContent className='space-y-4'>
          <div className='border rounded-lg overflow-hidden'>
            <table className='w-full text-left text-xs'>
              <thead className='bg-muted/40 border-b text-muted-foreground font-medium'>
                <tr>
                  <th className='py-2 px-3'>SKU / Ref</th>
                  <th className='py-2 px-3'>Concepto / Servicio / Producto</th>
                  <th className='py-2 px-3'>Tipo</th>
                  <th className='py-2 px-3 text-right'>Precio Neto</th>
                  <th className='py-2 px-3 text-right'>IVA</th>
                </tr>
              </thead>
              <tbody className='divide-y'>
                {catalog.map((c) => (
                  <tr key={c.id}>
                    <td className='py-2 px-3 font-mono text-muted-foreground'>
                      {c.sku}
                    </td>
                    <td className='py-2 px-3 font-medium text-foreground'>
                      {c.concepto}
                    </td>
                    <td className='py-2 px-3'>
                      <Badge variant='secondary' className='text-[10px] capitalize'>
                        {c.categoria}
                      </Badge>
                    </td>
                    <td className='py-2 px-3 text-right font-mono font-semibold'>
                      {c.precioUnitario.toFixed(2)} €
                    </td>
                    <td className='py-2 px-3 text-right font-mono'>
                      {c.ivaPercent}%
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <form
            onSubmit={handleAddCatalog}
            className='grid sm:grid-cols-6 gap-2.5 items-end bg-muted/20 p-3 rounded-lg border'
          >
            <div>
              <label className='text-[10px] uppercase font-semibold text-muted-foreground'>
                Tipo
              </label>
              <select
                value={catTipo}
                onChange={(e) =>
                  setCatTipo(e.target.value as 'servicio' | 'producto')
                }
                className='h-8 w-full rounded-md border border-input bg-background px-2 text-xs mt-0.5'
              >
                <option value='servicio'>Servicio</option>
                <option value='producto'>Producto</option>
              </select>
            </div>
            <div>
              <label className='text-[10px] uppercase font-semibold text-muted-foreground'>
                Referencia
              </label>
              <Input
                placeholder='SRV-01'
                value={catSku}
                onChange={(e) => setCatSku(e.target.value)}
                className='h-8 text-xs font-mono mt-0.5 bg-background'
              />
            </div>
            <div className='sm:col-span-2'>
              <label className='text-[10px] uppercase font-semibold text-muted-foreground'>
                Descripción del servicio / producto
              </label>
              <Input
                placeholder='Ej. Mantenimiento mensual software'
                value={catConcepto}
                onChange={(e) => setCatConcepto(e.target.value)}
                className='h-8 text-xs mt-0.5 bg-background'
              />
            </div>
            <div>
              <label className='text-[10px] uppercase font-semibold text-muted-foreground'>
                Precio (€)
              </label>
              <Input
                type='number'
                step='0.01'
                value={catPrecio}
                onChange={(e) => setCatPrecio(parseFloat(e.target.value) || 0)}
                className='h-8 text-xs font-mono mt-0.5 bg-background'
              />
            </div>
            <Button type='submit' size='sm' className='h-8 text-xs gap-1'>
              <Plus className='h-3.5 w-3.5' />
              <span>Añadir al Catálogo</span>
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
