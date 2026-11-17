import { useEffect, useState } from 'react'
import { CheckCircle2, MonitorDown } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

interface BeforeInstallPromptEvent extends Event {
  readonly platforms: string[]
  readonly userChoice: Promise<{
    outcome: 'accepted' | 'dismissed'
    platform: string
  }>
  prompt(): Promise<void>
}

let deferredPromptGlobal: BeforeInstallPromptEvent | null = null

if (typeof window !== 'undefined') {
  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault()
    deferredPromptGlobal = e as BeforeInstallPromptEvent
    window.dispatchEvent(new CustomEvent('paye-pwa-prompt-ready'))
  })
}

type PwaInstallButtonProps = {
  variant?: 'sidebar' | 'compact'
}

export function PwaInstallButton({ variant = 'sidebar' }: PwaInstallButtonProps) {
  const [deferredPrompt, setDeferredPrompt] =
    useState<BeforeInstallPromptEvent | null>(deferredPromptGlobal)
  const [isStandalone, setIsStandalone] = useState(false)
  const [helpOpen, setHelpOpen] = useState(false)

  useEffect(() => {
    const checkStandalone = () => {
      const standalone =
        window.matchMedia('(display-mode: standalone)').matches ||
        window.matchMedia('(display-mode: window-controls-overlay)').matches
      setIsStandalone(standalone)
    }

    checkStandalone()

    const onPromptReady = () => {
      setDeferredPrompt(deferredPromptGlobal)
    }

    const onAppInstalled = () => {
      deferredPromptGlobal = null
      setDeferredPrompt(null)
      setIsStandalone(true)
      toast.success('Payé CRM instalada correctamente como aplicación de escritorio.')
    }

    window.addEventListener('paye-pwa-prompt-ready', onPromptReady)
    window.addEventListener('appinstalled', onAppInstalled)

    return () => {
      window.removeEventListener('paye-pwa-prompt-ready', onPromptReady)
      window.removeEventListener('appinstalled', onAppInstalled)
    }
  }, [])

  if (isStandalone) {
    return null
  }

  const handleInstallClick = async () => {
    if (deferredPrompt) {
      await deferredPrompt.prompt()
      const choice = await deferredPrompt.userChoice
      if (choice.outcome === 'accepted') {
        deferredPromptGlobal = null
        setDeferredPrompt(null)
      }
      return
    }
    setHelpOpen(true)
  }

  return (
    <>
      {variant === 'compact' ? (
        <Button
          type='button'
          variant='outline'
          size='sm'
          onClick={handleInstallClick}
          className='gap-2 text-xs'
        >
          <MonitorDown className='size-3.5' />
          <span>Instalar App en Windows 11</span>
        </Button>
      ) : (
        <div className='px-2 py-1 group-data-[collapsible=icon]:px-0'>
          <Button
            type='button'
            variant='outline'
            size='sm'
            onClick={handleInstallClick}
            className='w-full justify-start gap-2 border-primary/30 bg-primary/5 text-xs font-medium hover:bg-primary/10 group-data-[collapsible=icon]:size-8 group-data-[collapsible=icon]:justify-center group-data-[collapsible=icon]:p-0'
            title='Instalar Payé CRM como aplicación de escritorio (PWA)'
          >
            <MonitorDown className='size-4 shrink-0 text-primary' />
            <span className='truncate group-data-[collapsible=icon]:hidden'>
              Instalar App Escritorio
            </span>
          </Button>
        </div>
      )}

      <Dialog open={helpOpen} onOpenChange={setHelpOpen}>
        <DialogContent className='sm:max-w-md'>
          <DialogHeader>
            <DialogTitle className='flex items-center gap-2'>
              <MonitorDown className='size-5 text-primary' />
              Instalar Payé CRM en Windows 11
            </DialogTitle>
            <DialogDescription>
              Ejecuta Payé CRM en su propia ventana nativa, con icono en la barra de
              tareas y menú de Inicio de Windows 11.
            </DialogDescription>
          </DialogHeader>
          <div className='space-y-3 text-sm'>
            <div className='rounded-lg border bg-muted/40 p-3 space-y-2'>
              <p className='font-medium text-foreground flex items-center gap-1.5'>
                <CheckCircle2 className='size-4 text-primary' />
                Opción 1: Desde el navegador (Edge / Chrome)
              </p>
              <p className='text-xs text-muted-foreground leading-relaxed'>
                Haz clic en el icono <strong>Instalar Payé CRM</strong> situado a la
                derecha de la barra de direcciones (o en el menú <strong>⋯ → Aplicaciones → Instalar este sitio como una aplicación</strong>).
              </p>
            </div>
            <div className='rounded-lg border bg-muted/40 p-3 space-y-2'>
              <p className='font-medium text-foreground flex items-center gap-1.5'>
                <CheckCircle2 className='size-4 text-primary' />
                Opción 2: Instalador nativo de Windows 11 (.exe)
              </p>
              <p className='text-xs text-muted-foreground leading-relaxed'>
                Ejecuta <code>installer\PayeCRM-Setup-Win11.exe</code> incluido en el
                proyecto para instalar los accesos directos en el Escritorio, Menú Inicio
                y Configuración de Windows 11.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button type='button' onClick={() => setHelpOpen(false)}>
              Entendido
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
