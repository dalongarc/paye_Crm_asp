import { useEffect, useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Switch } from '@/components/ui/switch'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { toast } from 'sonner'
import { AlertTriangle, RefreshCw } from 'lucide-react'
import {
  useCreateClienteMutation,
  useUpdateClienteMutation,
} from '@/hooks/use-clientes'
import {
  api,
  type ClienteDto,
  type CreateClienteRequest,
  type UpdateClienteRequest,
  ConcurrencyConflictError,
} from '@/api'

interface ClienteFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  cliente?: ClienteDto | null
  onSuccess?: () => void
}

export function ClienteFormDialog({
  open,
  onOpenChange,
  cliente,
  onSuccess,
}: ClienteFormDialogProps) {
  const isEdit = Boolean(cliente)
  const createMutation = useCreateClienteMutation()
  const updateMutation = useUpdateClienteMutation()

  // Form State
  const [nombreFiscal, setNombreFiscal] = useState('')
  const [nombreComercial, setNombreComercial] = useState('')
  const [cifNif, setCifNif] = useState('')
  const [email, setEmail] = useState('')
  const [telefono, setTelefono] = useState('')
  const [direccion, setDireccion] = useState('')
  const [codigoPostal, setCodigoPostal] = useState('')
  const [ciudad, setCiudad] = useState('')
  const [provincia, setProvincia] = useState('')
  const [activo, setActivo] = useState(true)
  const [notas, setNotas] = useState('')
  const [rowVersion, setRowVersion] = useState<number | string>(0)

  // Concurrency Conflict State
  const [conflictMessage, setConflictMessage] = useState<string | null>(null)
  const [isReloading, setIsReloading] = useState(false)

  // Reset or Populate on Open / Change
  useEffect(() => {
    if (open) {
      setConflictMessage(null)
      if (cliente) {
        setNombreFiscal(cliente.nombreFiscal || '')
        setNombreComercial(cliente.nombreComercial || '')
        setCifNif(cliente.cifNif || '')
        setEmail(cliente.email || '')
        setTelefono(cliente.telefono || '')
        setDireccion(cliente.direccion || '')
        setCodigoPostal(cliente.codigoPostal || '')
        setCiudad(cliente.ciudad || '')
        setProvincia(cliente.provincia || '')
        setActivo(cliente.activo ?? true)
        setNotas(cliente.notas || '')
        setRowVersion(cliente.rowVersion ?? 0)
      } else {
        setNombreFiscal('')
        setNombreComercial('')
        setCifNif('')
        setEmail('')
        setTelefono('')
        setDireccion('')
        setCodigoPostal('')
        setCiudad('')
        setProvincia('')
        setActivo(true)
        setNotas('')
        setRowVersion(0)
      }
    }
  }, [open, cliente])

  const handleReloadLatest = async () => {
    if (!cliente?.id) return
    setIsReloading(true)
    try {
      const fresh = await api.clientes.getClienteById(cliente.id)
      setNombreFiscal(fresh.nombreFiscal || '')
      setNombreComercial(fresh.nombreComercial || '')
      setCifNif(fresh.cifNif || '')
      setEmail(fresh.email || '')
      setTelefono(fresh.telefono || '')
      setDireccion(fresh.direccion || '')
      setCodigoPostal(fresh.codigoPostal || '')
      setCiudad(fresh.ciudad || '')
      setProvincia(fresh.provincia || '')
      setActivo(fresh.activo ?? true)
      setNotas(fresh.notas || '')
      setRowVersion(fresh.rowVersion)
      setConflictMessage(null)
      toast.info('Datos recargados con la versión más reciente del servidor.')
    } catch {
      toast.error('No se pudo recargar la versión reciente del cliente.')
    } finally {
      setIsReloading(false)
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setConflictMessage(null)

    if (!nombreFiscal.trim()) {
      toast.error('El nombre fiscal es obligatorio.')
      return
    }

    if (!cifNif.trim()) {
      toast.error('El CIF/NIF es obligatorio.')
      return
    }

    try {
      if (isEdit && cliente) {
        const updateData: UpdateClienteRequest = {
          nombreFiscal: nombreFiscal.trim(),
          nombreComercial: nombreComercial.trim() || null,
          cifNif: cifNif.trim().toUpperCase(),
          email: email.trim() || null,
          telefono: telefono.trim() || null,
          direccion: direccion.trim() || null,
          codigoPostal: codigoPostal.trim() || null,
          ciudad: ciudad.trim() || null,
          provincia: provincia.trim() || null,
          activo,
          notas: notas.trim() || null,
          rowVersion,
        }

        await updateMutation.mutateAsync({ id: cliente.id, data: updateData })
        toast.success(`Cliente "${nombreFiscal}" actualizado correctamente.`)
      } else {
        const createData: CreateClienteRequest = {
          nombreFiscal: nombreFiscal.trim(),
          nombreComercial: nombreComercial.trim() || null,
          cifNif: cifNif.trim().toUpperCase(),
          email: email.trim() || null,
          telefono: telefono.trim() || null,
          direccion: direccion.trim() || null,
          codigoPostal: codigoPostal.trim() || null,
          ciudad: ciudad.trim() || null,
          provincia: provincia.trim() || null,
          activo,
          notas: notas.trim() || null,
        }

        await createMutation.mutateAsync(createData)
        toast.success(`Cliente "${nombreFiscal}" creado correctamente.`)
      }

      onOpenChange(false)
      onSuccess?.()
    } catch (err: unknown) {
      if (err instanceof ConcurrencyConflictError || (err as { status?: number })?.status === 409) {
        setConflictMessage(
          'Conflicto de concurrencia (HTTP 409): El cliente ha sido modificado por otro usuario en otra sesión. Los datos que estás intentando guardar están basados en una versión obsoleta.'
        )
      } else {
        const msg = err instanceof Error ? err.message : 'Error al guardar los datos del cliente.'
        toast.error(msg)
      }
    }
  }

  const isPending = createMutation.isPending || updateMutation.isPending

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='max-h-[90vh] overflow-y-auto sm:max-w-[600px]'>
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>{isEdit ? 'Editar Cliente' : 'Nuevo Cliente'}</DialogTitle>
            <DialogDescription>
              {isEdit
                ? 'Modifica los datos del cliente. Los cambios se sincronizarán con el servidor.'
                : 'Completa los datos para registrar un nuevo cliente en el sistema.'}
            </DialogDescription>
          </DialogHeader>

          {/* Alerta de Conflicto 409 */}
          {conflictMessage && (
            <Alert variant='destructive' className='my-4' data-testid='alert-conflicto-409'>
              <AlertTriangle className='h-4 w-4' />
              <AlertTitle className='font-semibold'>Conflicto de Concurrencia</AlertTitle>
              <AlertDescription className='mt-2 space-y-3'>
                <p className='text-xs leading-relaxed'>{conflictMessage}</p>
                <div className='flex items-center gap-2 pt-1'>
                  <Button
                    type='button'
                    variant='outline'
                    size='sm'
                    className='h-8 text-xs gap-1.5 bg-background text-foreground'
                    onClick={handleReloadLatest}
                    disabled={isReloading}
                    data-testid='btn-recargar-conflicto'
                  >
                    <RefreshCw className={`h-3.5 w-3.5 ${isReloading ? 'animate-spin' : ''}`} />
                    <span>Recargar datos recientes del servidor</span>
                  </Button>
                </div>
              </AlertDescription>
            </Alert>
          )}

          <div className='grid gap-4 py-4'>
            {/* Nombre Fiscal y CIF/NIF */}
            <div className='grid grid-cols-1 sm:grid-cols-2 gap-4'>
              <div className='space-y-1.5'>
                <Label htmlFor='nombreFiscal'>
                  Nombre Fiscal <span className='text-destructive'>*</span>
                </Label>
                <Input
                  id='nombreFiscal'
                  data-testid='input-nombre-fiscal'
                  placeholder='Ej. Payé Soluciones S.L.'
                  value={nombreFiscal}
                  onChange={(e) => setNombreFiscal(e.target.value)}
                  disabled={isPending}
                  required
                />
              </div>

              <div className='space-y-1.5'>
                <Label htmlFor='cifNif'>
                  CIF / NIF <span className='text-destructive'>*</span>
                </Label>
                <Input
                  id='cifNif'
                  data-testid='input-cif-nif'
                  placeholder='Ej. B12345678'
                  value={cifNif}
                  onChange={(e) => setCifNif(e.target.value)}
                  disabled={isPending}
                  required
                />
              </div>
            </div>

            {/* Nombre Comercial y Email */}
            <div className='grid grid-cols-1 sm:grid-cols-2 gap-4'>
              <div className='space-y-1.5'>
                <Label htmlFor='nombreComercial'>Nombre Comercial</Label>
                <Input
                  id='nombreComercial'
                  data-testid='input-nombre-comercial'
                  placeholder='Ej. Payé Digital'
                  value={nombreComercial}
                  onChange={(e) => setNombreComercial(e.target.value)}
                  disabled={isPending}
                />
              </div>

              <div className='space-y-1.5'>
                <Label htmlFor='email'>Correo Electrónico</Label>
                <Input
                  id='email'
                  data-testid='input-email'
                  type='email'
                  placeholder='cliente@ejemplo.local'
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  disabled={isPending}
                />
              </div>
            </div>

            {/* Teléfono y Ciudad */}
            <div className='grid grid-cols-1 sm:grid-cols-2 gap-4'>
              <div className='space-y-1.5'>
                <Label htmlFor='telefono'>Teléfono</Label>
                <Input
                  id='telefono'
                  data-testid='input-telefono'
                  placeholder='+34 600 000 000'
                  value={telefono}
                  onChange={(e) => setTelefono(e.target.value)}
                  disabled={isPending}
                />
              </div>

              <div className='space-y-1.5'>
                <Label htmlFor='ciudad'>Ciudad</Label>
                <Input
                  id='ciudad'
                  data-testid='input-ciudad'
                  placeholder='Madrid'
                  value={ciudad}
                  onChange={(e) => setCiudad(e.target.value)}
                  disabled={isPending}
                />
              </div>
            </div>

            {/* Dirección y Código Postal */}
            <div className='grid grid-cols-1 sm:grid-cols-3 gap-4'>
              <div className='space-y-1.5 sm:col-span-2'>
                <Label htmlFor='direccion'>Dirección</Label>
                <Input
                  id='direccion'
                  placeholder='Calle Mayor, 12'
                  value={direccion}
                  onChange={(e) => setDireccion(e.target.value)}
                  disabled={isPending}
                />
              </div>

              <div className='space-y-1.5'>
                <Label htmlFor='codigoPostal'>Código Postal</Label>
                <Input
                  id='codigoPostal'
                  placeholder='28001'
                  value={codigoPostal}
                  onChange={(e) => setCodigoPostal(e.target.value)}
                  disabled={isPending}
                />
              </div>
            </div>

            {/* Estado Activo */}
            <div className='flex items-center justify-between rounded-lg border p-3'>
              <div className='space-y-0.5'>
                <Label htmlFor='activo' className='text-sm font-medium'>
                  Estado del Cliente
                </Label>
                <p className='text-xs text-muted-foreground'>
                  Los clientes activos están disponibles para nueva facturación y presupuestos.
                </p>
              </div>
              <Switch
                id='activo'
                checked={activo}
                onCheckedChange={setActivo}
                disabled={isPending}
              />
            </div>

            {/* Notas Internas */}
            <div className='space-y-1.5'>
              <Label htmlFor='notas'>Notas Internas</Label>
              <Textarea
                id='notas'
                placeholder='Observaciones de facturación, condiciones especiales...'
                value={notas}
                onChange={(e) => setNotas(e.target.value)}
                disabled={isPending}
                rows={3}
              />
            </div>
          </div>

          <DialogFooter className='gap-2 sm:gap-0'>
            <Button
              type='button'
              variant='outline'
              onClick={() => onOpenChange(false)}
              disabled={isPending}
            >
              Cancelar
            </Button>
            <Button
              type='submit'
              disabled={isPending}
              data-testid='btn-guardar-cliente'
            >
              {isPending
                ? 'Guardando...'
                : isEdit
                  ? 'Guardar Cambios'
                  : 'Crear Cliente'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
