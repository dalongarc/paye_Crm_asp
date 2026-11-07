import { Link } from '@tanstack/react-router'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { AuthLayout } from '../auth-layout'
import { ForgotPasswordForm } from './components/forgot-password-form'

export function ForgotPassword() {
  return (
    <AuthLayout>
      <Card className='max-w-sm gap-4 sm:min-w-sm'>
        <CardHeader>
          <CardTitle className='text-lg tracking-tight'>
            Recuperar Contraseña
          </CardTitle>
          <CardDescription>
            Introduce tu correo corporativo y te enviaremos las instrucciones
            para restablecer tu acceso.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ForgotPasswordForm />
        </CardContent>
        <CardFooter>
          <p className='mx-auto px-8 text-center text-sm text-balance text-muted-foreground'>
            ¿Recordaste tu contraseña?{' '}
            <Link
              to='/sign-in'
              className='underline underline-offset-4 hover:text-primary font-medium'
            >
              Volver a Iniciar Sesión
            </Link>
          </p>
        </CardFooter>
      </Card>
    </AuthLayout>
  )
}
