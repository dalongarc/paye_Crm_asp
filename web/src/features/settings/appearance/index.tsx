import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Switch } from '@/components/ui/switch'
import {
  FileSpreadsheet,
  Check,
  Sun,
  Moon,
} from 'lucide-react'
import { toast } from 'sonner'
import { useTheme } from '@/context/theme-provider'
import {
  useBillingStore,
  billingActions,
  type InvoiceTemplateConfig,
} from '@/lib/billing-store'

export function SettingsAppearance() {
  const { template, company } = useBillingStore()
  const { theme, setTheme } = useTheme()

  const styles: {
    id: InvoiceTemplateConfig['style']
    title: string
    desc: string
  }[] = [
    {
      id: 'modern',
      title: 'Moderna Minimalista (Pepa / Linear)',
      desc: 'Cabecera limpia, bloque de totales en alto contraste oscuro y tipografía monoespaciada para importes.',
    },
    {
      id: 'classic',
      title: 'Clásica Corporativa A4',
      desc: 'Estructura tradicional de despacho profesional con recuadros delimitados para cliente y bancos.',
    },
    {
      id: 'compact',
      title: 'Compacta Comercial / Ticket A5',
      desc: 'Diseñada para comercios minoristas y entregas rápidas con aprovechamiento denso de espacio.',
    },
  ]

  return (
    <div className='space-y-5'>
      {/* Selector de Diseño de Plantilla de Factura */}
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <CardTitle className='text-sm font-semibold flex items-center gap-2'>
            <FileSpreadsheet className='h-4 w-4 text-primary' />
            <span>Diseño de Plantillas de Factura</span>
          </CardTitle>
          <CardDescription className='text-xs'>
            Elige el diseño visual y los bloques informativos que se muestran tanto en la vista en vivo como al descargar el PDF.
          </CardDescription>
        </CardHeader>

        <CardContent className='space-y-5'>
          <div className='grid md:grid-cols-3 gap-3'>
            {styles.map((s) => {
              const active = template.style === s.id
              return (
                <div
                  key={s.id}
                  onClick={() => {
                    billingActions.updateTemplate({ style: s.id })
                    toast.success(`Plantilla "${s.title}" activada.`)
                  }}
                  className={`cursor-pointer rounded-lg border p-3.5 transition-all space-y-2 ${
                    active
                      ? 'border-primary bg-primary/5 ring-1 ring-primary'
                      : 'hover:bg-muted/30'
                  }`}
                >
                  <div className='flex items-center justify-between'>
                    <span className='text-xs font-semibold text-foreground'>
                      {s.title}
                    </span>
                    {active && (
                      <Badge className='h-5 px-1.5 text-[10px] gap-1'>
                        <Check className='h-3 w-3' /> Activa
                      </Badge>
                    )}
                  </div>
                  <p className='text-[11px] text-muted-foreground leading-relaxed'>
                    {s.desc}
                  </p>
                </div>
              )
            })}
          </div>

          {/* Interruptores de Elementos Visibles en la Factura */}
          <div className='grid sm:grid-cols-2 gap-3 pt-2'>
            <div className='flex items-center justify-between p-3 rounded-lg border bg-muted/15'>
              <div>
                <p className='text-xs font-medium text-foreground'>
                  Mostrar Logotipo WebP en Cabecera
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Incrusta el logo comprimido de {company.nombreComercial}
                </p>
              </div>
              <Switch
                checked={template.showLogo}
                onCheckedChange={(v) =>
                  billingActions.updateTemplate({ showLogo: v })
                }
              />
            </div>

            <div className='flex items-center justify-between p-3 rounded-lg border bg-muted/15'>
              <div>
                <p className='text-xs font-medium text-foreground'>
                  Mostrar Cuenta Bancaria IBAN
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Visible junto a la forma de pago ({company.iban.slice(0, 12)}...)
                </p>
              </div>
              <Switch
                checked={template.showIban}
                onCheckedChange={(v) =>
                  billingActions.updateTemplate({ showIban: v })
                }
              />
            </div>

            <div className='flex items-center justify-between p-3 rounded-lg border bg-muted/15'>
              <div>
                <p className='text-xs font-medium text-foreground'>
                  Sello QR y Huella VeriFactu AEAT
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Imprime el código QR verificable y el hash SHA-256
                </p>
              </div>
              <Switch
                checked={template.showQrVerifactu}
                onCheckedChange={(v) =>
                  billingActions.updateTemplate({ showQrVerifactu: v })
                }
              />
            </div>

            <div className='flex items-center justify-between p-3 rounded-lg border bg-muted/15'>
              <div>
                <p className='text-xs font-medium text-foreground'>
                  Pie de Registro Mercantil y RGPD
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Texto legal al pie del documento
                </p>
              </div>
              <Switch
                checked={template.showRegistroMercantil}
                onCheckedChange={(v) =>
                  billingActions.updateTemplate({ showRegistroMercantil: v })
                }
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Selector de Tema de la Aplicación (Claro / Oscuro) */}
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <CardTitle className='text-sm font-semibold'>
            Tema Visual de la Interfaz
          </CardTitle>
          <CardDescription className='text-xs'>
            Alterna entre el modo Claro de oficina y el modo Oscuro de bajo contraste.
          </CardDescription>
        </CardHeader>
        <CardContent className='flex items-center gap-3'>
          <Button
            type='button'
            variant={theme === 'light' ? 'default' : 'outline'}
            size='sm'
            className='h-9 text-xs gap-2'
            onClick={() => setTheme('light')}
          >
            <Sun className='h-4 w-4' />
            <span>Modo Claro</span>
          </Button>
          <Button
            type='button'
            variant={theme === 'dark' ? 'default' : 'outline'}
            size='sm'
            className='h-9 text-xs gap-2'
            onClick={() => setTheme('dark')}
          >
            <Moon className='h-4 w-4' />
            <span>Modo Oscuro</span>
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
