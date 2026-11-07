import { useSearch } from '@tanstack/react-router'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { AuthLayout } from '../auth-layout'
import { UserAuthForm } from './components/user-auth-form'

export function SignIn() {
  const { redirect } = useSearch({ from: '/(auth)/sign-in' })

  return (
    <AuthLayout>
      <Card className='w-full max-w-sm gap-4 shadow-sm border-border/80'>
        <CardHeader className='space-y-1.5 pb-2'>
          <CardTitle className='text-xl font-semibold tracking-tight'>
            Iniciar Sesión
          </CardTitle>
          <CardDescription className='text-sm text-muted-foreground'>
            Introduce tus credenciales corporativas para acceder al sistema.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <UserAuthForm redirectTo={redirect} />
        </CardContent>
        <CardFooter className='pt-0 pb-4'>
          <p className='w-full text-center text-xs text-muted-foreground'>
            Acceso restringido. Las cuentas son creadas exclusivamente por un administrador.
          </p>
        </CardFooter>
      </Card>
    </AuthLayout>
  )
}
