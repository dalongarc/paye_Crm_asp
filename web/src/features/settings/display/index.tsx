import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { LockKeyhole, Shield, KeyRound } from 'lucide-react'
import { useCurrentUserQuery } from '@/hooks/use-auth'

export function SettingsDisplay() {
  const { data: user } = useCurrentUserQuery()

  return (
    <div className='space-y-5'>
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <CardTitle className='text-sm font-semibold flex items-center gap-2'>
            <LockKeyhole className='h-4 w-4 text-primary' />
            <span>Cuenta de Usuario y Seguridad (ASP.NET Core Identity)</span>
          </CardTitle>
          <CardDescription className='text-xs'>
            Sesión autenticada mediante cookie segura HttpOnly, SameSite=Lax y protección Antiforgery.
          </CardDescription>
        </CardHeader>

        <CardContent className='space-y-4'>
          <div className='grid sm:grid-cols-3 gap-3 p-4 rounded-lg border bg-muted/20 text-xs'>
            <div>
              <span className='text-[10px] uppercase font-semibold text-muted-foreground block'>
                Usuario Activo
              </span>
              <span className='font-semibold text-sm text-foreground mt-0.5 block'>
                {user?.name || 'Propietario'}
              </span>
            </div>
            <div>
              <span className='text-[10px] uppercase font-semibold text-muted-foreground block'>
                Correo Electrónico
              </span>
              <span className='font-mono text-foreground mt-0.5 block'>
                {user?.email || 'propietario@payedigital.local'}
              </span>
            </div>
            <div>
              <span className='text-[10px] uppercase font-semibold text-muted-foreground block'>
                Rol del Sistema
              </span>
              <Badge className='mt-1 text-xs'>
                {user?.role || 'Propietario'}
              </Badge>
            </div>
          </div>

          <div className='rounded-lg border p-3.5 space-y-2 text-xs'>
            <div className='flex items-center gap-2 font-semibold text-foreground'>
              <Shield className='h-4 w-4 text-emerald-500' />
              <span>Permisos Efectivos Concedidos</span>
            </div>
            <div className='flex flex-wrap gap-1.5 pt-1'>
              {(
                user?.permissions ?? [
                  'Clientes.Ver',
                  'Clientes.Crear',
                  'Clientes.Editar',
                  'Clientes.Eliminar',
                  'Facturacion.Ver',
                  'Facturacion.Crear',
                  'Facturacion.Emitir',
                  'Facturacion.Configurar',
                ]
              ).map((perm) => (
                <Badge
                  key={perm}
                  variant='outline'
                  className='font-mono text-[10px]'
                >
                  {perm}
                </Badge>
              ))}
            </div>
          </div>

          <div className='rounded-lg border p-3.5 flex items-center justify-between text-xs bg-muted/10'>
            <div className='flex items-center gap-2.5'>
              <KeyRound className='h-4 w-4 text-muted-foreground' />
              <div>
                <p className='font-medium text-foreground'>
                  Política de Contraseñas y Bloqueo Automático
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Bloqueo tras 5 intentos fallidos · Hashing PBKDF2 gestionado por ASP.NET Core Identity
                </p>
              </div>
            </div>
            <Badge
              variant='outline'
              className='border-emerald-500/40 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 text-[10px]'
            >
              Protegido
            </Badge>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
