import { useState } from 'react'
import { z } from 'zod'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from '@tanstack/react-router'
import { AlertCircle, CheckCircle2, KeyRound, Loader2, ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'
import { useVerify2faMutation } from '@/hooks/use-auth'
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
import {
  InputOTP,
  InputOTPGroup,
  InputOTPSlot,
  InputOTPSeparator,
} from '@/components/ui/input-otp'

const formSchema = z.object({
  otp: z
    .string()
    .min(6, 'Introduce el código completo de 6 dígitos.')
    .max(6, 'El código debe tener exactamente 6 dígitos.'),
})

interface OtpFormProps extends React.HTMLAttributes<HTMLFormElement> {
  email?: string
}

export function OtpForm({ email = 'usuario@paye.local', className, ...props }: OtpFormProps) {
  const navigate = useNavigate()
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const verify2faMutation = useVerify2faMutation()
  const isMocks = import.meta.env.VITE_USE_MOCKS === 'true'

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: { otp: '' },
  })

  // eslint-disable-next-line react-hooks/incompatible-library
  const otp = form.watch('otp')

  async function onSubmit(data: z.infer<typeof formSchema>) {
    setErrorMessage(null)
    try {
      await verify2faMutation.mutateAsync({
        email,
        code: data.otp,
      })
      toast.success('Doble factor verificado con éxito')
      navigate({ to: '/', replace: true })
    } catch (err: unknown) {
      if (err instanceof Error) {
        setErrorMessage(err.message)
      } else {
        setErrorMessage('Código de verificación incorrecto.')
      }
    }
  }

  function setQuickCode(code: string) {
    form.setValue('otp', code, { shouldValidate: true })
    setErrorMessage(null)
  }

  const isPending = verify2faMutation.isPending

  return (
    <div className='grid gap-4'>
      {errorMessage && (
        <Alert variant='destructive' className='border-destructive/50 bg-destructive/10 text-destructive dark:bg-destructive/20'>
          <AlertCircle className='h-4 w-4' />
          <AlertTitle className='font-semibold'>Código 2FA Inválido</AlertTitle>
          <AlertDescription className='text-xs leading-relaxed'>
            {errorMessage}
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
            name='otp'
            render={({ field }) => (
              <FormItem className='flex flex-col items-center gap-2'>
                <FormLabel className='sr-only'>Código 2FA de 6 dígitos</FormLabel>
                <FormControl>
                  <InputOTP
                    maxLength={6}
                    {...field}
                    containerClassName='justify-between gap-1 sm:gap-2'
                  >
                    <InputOTPGroup>
                      <InputOTPSlot index={0} className='h-11 w-10 sm:w-11 text-base font-semibold' />
                      <InputOTPSlot index={1} className='h-11 w-10 sm:w-11 text-base font-semibold' />
                      <InputOTPSlot index={2} className='h-11 w-10 sm:w-11 text-base font-semibold' />
                    </InputOTPGroup>
                    <InputOTPSeparator />
                    <InputOTPGroup>
                      <InputOTPSlot index={3} className='h-11 w-10 sm:w-11 text-base font-semibold' />
                      <InputOTPSlot index={4} className='h-11 w-10 sm:w-11 text-base font-semibold' />
                      <InputOTPSlot index={5} className='h-11 w-10 sm:w-11 text-base font-semibold' />
                    </InputOTPGroup>
                  </InputOTP>
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <Button
            className='mt-2 w-full'
            disabled={otp.length < 6 || isPending}
          >
            {isPending ? (
              <Loader2 className='me-2 h-4 w-4 animate-spin' />
            ) : (
              <ShieldCheck className='me-2 h-4 w-4' />
            )}
            Verificar Código
          </Button>

          {/* Helper para simular códigos 2FA en modo mocks */}
          {isMocks && (
            <div className='mt-2 rounded-md border border-dashed border-border p-2 bg-muted/40 text-xs'>
              <div className='flex items-center gap-1.5 font-medium text-muted-foreground mb-1.5'>
                <KeyRound className='h-3.5 w-3.5' />
                <span>Simular códigos TOTP:</span>
              </div>
              <div className='flex gap-1.5'>
                <button
                  type='button'
                  onClick={() => setQuickCode('123456')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent text-primary flex items-center gap-1'
                >
                  <CheckCircle2 className='h-3 w-3' />
                  123456 (válido)
                </button>
                <button
                  type='button'
                  onClick={() => setQuickCode('000000')}
                  className='rounded bg-background px-2 py-0.5 text-[11px] font-medium border hover:bg-accent text-destructive flex items-center gap-1'
                >
                  <AlertCircle className='h-3 w-3' />
                  000000 (inválido)
                </button>
              </div>
            </div>
          )}
        </form>
      </Form>
    </div>
  )
}
