import { useEffect } from 'react'
import { Check, Moon, Sun, Laptop } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useTheme } from '@/context/theme-provider'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

export function ThemeSwitch() {
  const { theme, setTheme, resolvedTheme } = useTheme()

  /* Actualizar la etiqueta meta theme-color con el color de superficie correspondiente */
  useEffect(() => {
    const metaThemeColor = document.querySelector("meta[name='theme-color']")
    if (metaThemeColor) {
      // Superficie oscura tintada u superficie clara tintada (sin blanco puro ni negro puro)
      const themeColor = resolvedTheme === 'dark' ? '#0d1527' : '#f4f7fb'
      metaThemeColor.setAttribute('content', themeColor)
    }
  }, [resolvedTheme])

  return (
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger asChild>
        <Button
          variant='ghost'
          size='icon'
          className='scale-95 rounded-full'
          title='Cambiar modo de iluminación (Claro / Oscuro / Sistema)'
        >
          <Sun className='size-[1.2rem] scale-100 rotate-0 transition-all dark:scale-0 dark:-rotate-90' />
          <Moon className='absolute size-[1.2rem] scale-0 rotate-90 transition-all dark:scale-100 dark:rotate-0' />
          <span className='sr-only'>Alternar tema</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align='end' className='w-40'>
        <DropdownMenuItem onClick={() => setTheme('light')} className='gap-2'>
          <Sun className='h-4 w-4 text-amber-500' />
          <span>Modo Claro</span>
          <Check
            size={14}
            className={cn('ms-auto text-primary', theme !== 'light' && 'hidden')}
          />
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setTheme('dark')} className='gap-2'>
          <Moon className='h-4 w-4 text-indigo-400' />
          <span>Modo Oscuro</span>
          <Check
            size={14}
            className={cn('ms-auto text-primary', theme !== 'dark' && 'hidden')}
          />
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setTheme('system')} className='gap-2'>
          <Laptop className='h-4 w-4 text-muted-foreground' />
          <span>Sistema</span>
          <Check
            size={14}
            className={cn('ms-auto text-primary', theme !== 'system' && 'hidden')}
          />
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
