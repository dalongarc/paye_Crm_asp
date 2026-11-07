import { Logo } from '@/assets/logo'

type AuthLayoutProps = {
  children: React.ReactNode
}

export function AuthLayout({ children }: AuthLayoutProps) {
  return (
    <div className='container grid h-svh max-w-none items-center justify-center'>
      <div className='mx-auto flex w-full flex-col justify-center space-y-2 py-8 sm:p-8'>
        <div className='mb-4 flex items-center justify-center gap-2'>
          <Logo className='h-6 w-6' />
          <h1 className='text-xl font-semibold tracking-tight'>Payé Base</h1>
        </div>
        {children}
      </div>
    </div>
  )
}
