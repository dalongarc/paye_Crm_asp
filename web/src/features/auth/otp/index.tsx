import { Link, useSearch } from '@tanstack/react-router'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { AuthLayout } from '../auth-layout'
import { OtpForm } from './components/otp-form'

export function Otp() {
  const { email } = useSearch({ from: '/(auth)/otp' })

  return (
    <AuthLayout>
      <Card className='w-full max-w-sm gap-4 shadow-sm border-border/80'>
        <CardHeader className='space-y-1.5 pb-2 text-center sm:text-start'>
          <CardTitle className='text-lg font-semibold tracking-tight'>
            Verificación de Doble Factor (2FA)
          </CardTitle>
          <CardDescription className='text-sm text-muted-foreground'>
            Introduce el código temporal TOTP de 6 dígitos generado por tu aplicación autenticadora
            {email ? (
              <> para la cuenta <span className='font-medium text-foreground'>{email}</span>.</>
            ) : (
              '.'
            )}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <OtpForm email={email || 'usuario@paye.local'} />
        </CardContent>
        <CardFooter className='pt-0 pb-4'>
          <p className='w-full text-center text-xs text-muted-foreground'>
            ¿Problemas con el código?{' '}
            <Link
              to='/sign-in'
              className='underline underline-offset-4 hover:text-primary font-medium'
            >
              Volver al inicio de sesión
            </Link>
          </p>
        </CardFooter>
      </Card>
    </AuthLayout>
  )
}
