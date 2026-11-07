import { SearchIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useSearch } from '@/context/search-provider'
import { Button } from './ui/button'

export function Search({
  className = '',
  placeholder = 'Buscar...',
  ...props
}: React.ComponentProps<'button'> & { placeholder?: string }) {
  const { setOpen } = useSearch()
  return (
    <Button
      {...props}
      variant='outline'
      className={cn(
        'group relative h-8 w-full flex-1 justify-start rounded-lg border-border/70 bg-muted/40 text-xs font-normal text-muted-foreground shadow-none hover:bg-muted/80 hover:text-foreground sm:w-40 sm:pe-12 md:flex-none lg:w-48 xl:w-56',
        className
      )}
      aria-keyshortcuts='Meta+K Control+K'
      onClick={() => setOpen(true)}
    >
      <SearchIcon
        aria-hidden='true'
        className='absolute inset-s-2.5 top-1/2 -translate-y-1/2 text-muted-foreground/80'
        size={14}
      />
      <span className='ms-5'>{placeholder}</span>
      <kbd className='pointer-events-none absolute inset-e-1.5 top-1/2 -translate-y-1/2 hidden h-5 items-center gap-0.5 rounded border border-border/70 bg-background/80 px-1.5 font-mono text-[11px] font-medium text-muted-foreground select-none sm:flex'>
        <span className='text-xs'>⌘</span>K
      </kbd>
    </Button>
  )
}
