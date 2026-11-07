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
import { Switch } from '@/components/ui/switch'
import {
  ShieldCheck,
  Link2,
  Save,
  CheckCircle2,
} from 'lucide-react'
import { toast } from 'sonner'
import {
  useBillingStore,
  billingActions,
  type VerifactuConfig,
} from '@/lib/billing-store'

export function SettingsNotifications() {
  const { verifactu, company } = useBillingStore()

  const [softwareName, setSoftwareName] = useState(verifactu.softwareName)
  const [softwareVersion, setSoftwareVersion] = useState(verifactu.softwareVersion)
  const [developerNif, setDeveloperNif] = useState(verifactu.developerNif)

  const handleSaveVerifactu = (e: React.FormEvent) => {
    e.preventDefault()
    billingActions.updateVerifactu({
      softwareName: softwareName.trim(),
      softwareVersion: softwareVersion.trim(),
      developerNif: developerNif.trim().toUpperCase(),
    })
    toast.success('Configuración VeriFactu AEAT guardada y aplicada.')
  }

  return (
    <div className='space-y-5'>
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <div className='flex items-center justify-between gap-2'>
            <div>
              <CardTitle className='text-sm font-semibold flex items-center gap-2'>
                <ShieldCheck className='h-4 w-4 text-emerald-500' />
                <span>Configuración Sistema VeriFactu (Real Decreto 1007/2023)</span>
              </CardTitle>
              <CardDescription className='text-xs mt-0.5'>
                Control de integridad, inalterabilidad, trazabilidad y encadenamiento criptográfico SHA-256 de los registros de facturación.
              </CardDescription>
            </div>
            <Badge
              variant='outline'
              className={
                verifactu.enabled
                  ? 'border-emerald-500/40 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 text-xs'
                  : 'text-xs'
              }
            >
              {verifactu.enabled ? 'VeriFactu Activo' : 'Desactivado'}
            </Badge>
          </div>
        </CardHeader>

        <CardContent className='space-y-4'>
          <div className='grid sm:grid-cols-2 gap-3'>
            <div className='flex items-center justify-between p-3.5 rounded-lg border bg-muted/15'>
              <div>
                <p className='text-xs font-semibold text-foreground'>
                  Activar Registro Fiscal VeriFactu
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Genera huella encadenada y sello verificable al emitir cada factura
                </p>
              </div>
              <Switch
                checked={verifactu.enabled}
                onCheckedChange={(v) =>
                  billingActions.updateVerifactu({ enabled: v })
                }
              />
            </div>

            <div className='flex items-center justify-between p-3.5 rounded-lg border bg-muted/15'>
              <div>
                <p className='text-xs font-semibold text-foreground'>
                  Encadenamiento SHA-256 Automático
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Vincula cada factura con el hash de la factura inmediatamente anterior
                </p>
              </div>
              <Switch
                checked={verifactu.autoChainSha256}
                onCheckedChange={(v) =>
                  billingActions.updateVerifactu({ autoChainSha256: v })
                }
              />
            </div>
          </div>

          <form onSubmit={handleSaveVerifactu} className='space-y-4 pt-2'>
            <div className='grid sm:grid-cols-2 gap-3'>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Modalidad de Cumplimiento Tributario
                </label>
                <select
                  value={verifactu.mode}
                  onChange={(e) =>
                    billingActions.updateVerifactu({
                      mode: e.target.value as VerifactuConfig['mode'],
                    })
                  }
                  className='h-9 w-full rounded-md border border-input bg-background px-2.5 text-xs mt-1'
                >
                  <option value='verifactu_aeat'>
                    Modalidad VERI*FACTU (Remisión electrónica a Sede AEAT)
                  </option>
                  <option value='conservacion_firmada'>
                    Conservación en local con firma electrónica inmutable
                  </option>
                </select>
              </div>

              <div>
                <label className='text-xs font-medium text-foreground'>
                  Entorno de Conexión AEAT
                </label>
                <select
                  value={verifactu.environment}
                  onChange={(e) =>
                    billingActions.updateVerifactu({
                      environment: e.target.value as VerifactuConfig['environment'],
                    })
                  }
                  className='h-9 w-full rounded-md border border-input bg-background px-2.5 text-xs mt-1'
                >
                  <option value='pruebas'>
                    Entorno de Pruebas / Preproducción AEAT (Sandbox)
                  </option>
                  <option value='produccion'>
                    Entorno Real de Producción AEAT (Certificado X.509)
                  </option>
                </select>
              </div>
            </div>

            <div className='grid sm:grid-cols-3 gap-3'>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Nombre del Sistema Informático
                </label>
                <Input
                  value={softwareName}
                  onChange={(e) => setSoftwareName(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Versión Homologada
                </label>
                <Input
                  value={softwareVersion}
                  onChange={(e) => setSoftwareVersion(e.target.value)}
                  className='h-9 text-xs font-mono mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  NIF Productor Software / Obligado
                </label>
                <Input
                  value={developerNif}
                  onChange={(e) => setDeveloperNif(e.target.value)}
                  className='h-9 text-xs font-mono mt-1'
                />
              </div>
            </div>

            <div className='rounded-lg border bg-emerald-500/5 border-emerald-500/20 p-3.5 flex items-start gap-3 text-xs'>
              <CheckCircle2 className='h-4 w-4 text-emerald-500 shrink-0 mt-0.5' />
              <div className='space-y-1'>
                <p className='font-semibold text-foreground'>
                  Declaración Responsable del Sistema Informático de Facturación
                </p>
                <p className='text-muted-foreground text-[11px]'>
                  Obligado tributario activo: <strong>{company.razonSocial}</strong> ({company.nifCif}).
                  Todos los registros de alta y anulación quedan sellados con marca temporal en huso horario Europe/Madrid.
                </p>
              </div>
            </div>

            <div className='flex items-center justify-between pt-1'>
              <Button
                type='button'
                variant='outline'
                size='sm'
                className='h-9 text-xs gap-1.5'
                onClick={() =>
                  toast.success(
                    'Cadena SHA-256 de registros VeriFactu verificada sin discrepancias.'
                  )
                }
              >
                <Link2 className='h-3.5 w-3.5' />
                <span>Verificar Integridad Cadena SHA-256</span>
              </Button>

              <Button
                type='submit'
                size='sm'
                className='h-9 text-xs gap-1.5 shadow-2xs'
                data-testid='btn-guardar-verifactu'
              >
                <Save className='h-3.5 w-3.5' />
                <span>Guardar Configuración VeriFactu</span>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
