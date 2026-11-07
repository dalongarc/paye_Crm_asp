import { useState } from 'react'
import { z } from 'zod'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate } from '@tanstack/react-router'
import {
  AlertCircle,
  Clock,
  FlaskConical,
  KeyRound,
  Loader2,
  LogIn,
  ShieldAlert,
  UserX,
  WifiOff,
} from 'lucide-react'
import { toast } from 'sonner'
import { AuthError, type AuthErrorCode } from '@/api'
import { useDemoSandboxLoginMutation, useLoginMutation, usePublicAuthConfigQuery } from '@/hooks/use-auth'
import { cn } from '@/lib/utils'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { PasswordInput } from '@/components/password-input'

const formSchema = z.object({
  email: z.string().min(1, 'Introduce tu correo corporativo.').email('Formato de correo no válido.'),
  password: z.string().min(1, 'Introduce tu contraseña.'),
})

interface UserAuthFormProps extends React.HTMLAttributes<HTMLFormElement> {
  redirectTo?: string
}

interface ErrorState {
  code: AuthErrorCode
  title: string
  message: string
}

export function UserAuthForm({
  className,
  redirectTo,
  ...props
}: UserAuthFormProps) {
  const [activeError, setActiveError] = useState<ErrorState | null>(null)
  const navigate = useNavigate()
  const loginMutation = useLoginMutation()
  const demoSandboxMutation = useDemoSandboxLoginMutation()
  const { data: publicConfig } = usePublicAuthConfigQuery()

  const isMocks = import.meta.env.VITE_USE_MOCKS === 'true'

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  })

  function mapAuthError(err: unknown): ErrorState {
    if (err instanceof AuthError) {
      switch (err.code) {
        case 'LOCKED_OUT':
          return {
            code: 'LOCKED_OUT',
            title: 'Cuenta bloqueada temporalmente',
            message:
              err.message ||
              'Cuenta bloqueada tras 5 intentos fallidos. Inténtalo de nuevo en 15 minutos.',
          }
        case 'RATE_LIMITED':
          return {
            code: 'RATE_LIMITED',
            title: 'Demasiadas peticiones (429)',
            message:
              err.message ||
              'Has superado el límite de intentos permitidos. Por favor, espera unos minutos.',
          }
        case 'INACTIVE':
          return {
            code: 'INACTIVE',
            title: 'Cuenta desactivada',
            message:
              err.message ||
              'Esta cuenta de usuario ha sido desactivada por el administrador.',
          }
        case 'NETWORK_ERROR':
          return {
            code: 'NETWORK_ERROR',
            title: 'Error de red o conexión',
            message:
              err.message ||
              'No se pudo conectar con el servidor. Comprueba tu conexión a internet.',
          }
        case 'BAD_CREDENTIALS':
        default:
          return {
            code: 'BAD_CREDENTIALS',
            title: 'Credenciales incorrectas',
            message:
              err.message ||
              'El correo electrónico o la contraseña ingresados son incorrectos.',
          }
      }
    }
    return {
      code: 'NETWORK_ERROR',
      title: 'Error del sistema',
      message: 'Ocurrió un error inesperado al procesar la solicitud.',
    }
  }

  function getErrorIcon(code: AuthErrorCode) {
    switch (code) {
      case 'LOCKED_OUT':
        return <ShieldAlert className='h-4 w-4' />
      case 'RATE_LIMITED':
        return <Clock className='h-4 w-4' />
      case 'INACTIVE':
        return <UserX className='h-4 w-4' />
      case 'NETWORK_ERROR':
        return <WifiOff className='h-4 w-4' />
      case 'BAD_CREDENTIALS':
      default:
        return <AlertCircle className='h-4 w-4' />
    }
  }

  async function onSubmit(data: z.infer<typeof formSchema>) {
    setActiveError(null)
    try {
      const result = await loginMutation.mutateAsync({
        email: data.email,
        password: data.password,
      })

      if (result.status === 'requires2fa') {
        toast.info('Se requiere verificación de doble factor (2FA)')
        navigate({
          to: '/otp',
          search: { email: result.email },
        })
        return
      }

      toast.success('Sesión iniciada correctamente')
      const targetPath = redirectTo || '/'
      navigate({ to: targetPath, replace: true })
    } catch (err) {
      const mapped = mapAuthError(err)
      setActiveError(mapped)
    }
  }

  async function handleDemoSandboxLogin() {
    setActiveError(null)
    try {
      await demoSandboxMutation.mutateAsync()
      toast.success('Sesión iniciada en Sandbox Demo Aislado')
      const targetPath = redirectTo || '/'
      navigate({ to: targetPath, replace: true })
    } catch (err) {
      setActiveError(mapAuthError(err))
    }
  }

  // Helper para autocompletar casos de prueba en desarrollo/mocks
  function fillMockUser(email: string, password = 'paye123') {
    form.setValue('email', email, { shouldValidate: true })
    form.setValue('password', password, { shouldValidate: true })
    setActiveError(null)
  }

  const isSubmitting = loginMutation.isPending || demoSandboxMutation.isPending

  return (
    <div className='grid gap-4'>
      {/* Banner de error visible y diseñado */}
      {activeError && (
        <Alert variant='destructive' className='border-destructive/50 bg-destructive/10 text-destructive dark:bg-destructive/20'>
          {getErrorIcon(activeError.code)}
          <AlertTitle className='font-semibold'>{activeError.title}</AlertTitle>
          <AlertDescription className='text-xs leading-relaxed text-destructive/90 dark:text-destructive'>
            {activeError.message}
          </AlertDescription>
        </Alert>
      )}

      <Form {...form}>
        <form
          onSubmit={form.handleSubmit(onSubmit)}
          className={cn('grid gap-3', className)}
          {...props}
        >
          <FormField
            control={form.control}
            name='email'
            render={({ field }) => (
              <FormItem>
                <FormLabel>Correo corporativo</FormLabel>
                <FormControl>
                  <Input
                    type='email'
                    placeholder='usuario@empresa.com'
                    autoComplete='email'
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name='password'
            render={({ field }) => (
              <FormItem className='relative'>
                <FormLabel>Contraseña</FormLabel>
                <FormControl>
                  <PasswordInput
                    placeholder='••••••••'
                    autoComplete='current-password'
                    {...field}
                  />
                </FormControl>
                <FormMessage />
                <Link
                  to='/forgot-password'
                  className='absolute inset-e-0 -top-0.5 text-xs font-medium text-muted-foreground hover:text-primary transition-colors'
                >
                  ¿Olvidaste tu contraseña?
                </Link>
              </FormItem>
            )}
          />

          <Button
            type='submit'
            data-testid='btn-login'
            className='mt-2 w-full'
            disabled={isSubmitting}
          >
            {isSubmitting ? (
              <Loader2 className='me-2 h-4 w-4 animate-spin' />
            ) : (
              <LogIn className='me-2 h-4 w-4' />
            )}
            Iniciar Sesión
          </Button>

          {/* Botón opcional de Modo Demo Aislado (Solo si la API lo habilita) */}
          {publicConfig?.demoSandboxEnabled && (
            <div className='mt-2 space-y-2'>
              <div className='relative my-1'>
                <div className='absolute inset-0 flex items-center'>
                  <span className='w-full border-t border-border/60' />
                </div>
                <div className='relative flex justify-center text-[10px] uppercase tracking-wider'>
                  <span className='bg-card px-2 text-muted-foreground font-medium'>
                    Entorno de Demostración
                  </span>
                </div>
              </div>

              <Button
                type='button'
                variant='outline'
                className='w-full border-amber-500/40 bg-amber-500/5 hover:bg-amber-500/10 text-amber-900 dark:text-amber-200'
                disabled={isSubmitting}
                onClick={handleDemoSandboxLogin}
              >
                <FlaskConical className='me-2 h-4 w-4 text-amber-600 dark:text-amber-400' />
                Entrar al Modo Demo Aislado
              </Button>
            </div>
          )}

          {/* Selector de pruebas mágicas para desarrollo/mocks */}
          {isMocks && (
            <div className='mt-3 rounded-md border border-dashed border-border p-2.5 bg-muted/40 text-xs'>
              <div className='flex items-center gap-1.5 font-medium text-muted-foreground mb-2'>
                <KeyRound className='h-3.5 w-3.5' />
                <span>Simular usuarios mágicos:</span>
              </div>
              <div className='flex flex-wrap gap-1'>
                <button
                  type='button'
                  onClick={() => fillMockUser('admin@paye.local', 'paye123')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent'
                >
                  admin (ok)
                </button>
                <button
                  type='button'
                  onClick={() => fillMockUser('dosfa@paye.local', 'paye123')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent text-primary'
                >
                  2fa
                </button>
                <button
                  type='button'
                  onClick={() => fillMockUser('bloqueado@paye.local', 'paye123')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent text-destructive'
                >
                  bloqueado
                </button>
                <button
                  type='button'
                  onClick={() => fillMockUser('limitado@paye.local', 'paye123')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent text-amber-600 dark:text-amber-400'
                >
                  429 límite
                </button>
                <button
                  type='button'
                  onClick={() => fillMockUser('desactivado@paye.local', 'paye123')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent text-destructive'
                >
                  desactivado
                </button>
                <button
                  type='button'
                  onClick={() => fillMockUser('error@paye.local', 'paye123')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent text-muted-foreground'
                >
                  error red
                </button>
                <button
                  type='button'
                  onClick={() => fillMockUser('admin@paye.local', 'wrong_pass')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent text-destructive'
                >
                  clave errónea
                </button>
              </div>
            </div>
          )}
        </form>
      </Form>
    </div>
  )
}
