import { useEffect, useState } from 'react'
import { cn } from '@/lib/utils'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'

type HeaderProps = React.HTMLAttributes<HTMLElement> & {
  fixed?: boolean
  ref?: React.Ref<HTMLElement>
}

export function Header({ className, fixed, children, ...props }: HeaderProps) {
  const [offset, setOffset] = useState(0)

  useEffect(() => {
    const onScroll = () => {
      setOffset(document.body.scrollTop || document.documentElement.scrollTop)
    }

    // Add scroll listener to the body
    document.addEventListener('scroll', onScroll, { passive: true })

    // Clean up the event listener on unmount
    return () => document.removeEventListener('scroll', onScroll)
  }, [])

  return (
    <header
      className={cn(
        'sticky top-0 z-40 h-14 border-b border-border/60 bg-background/80 backdrop-blur-md transition-shadow',
        fixed && 'header-fixed peer/header w-[inherit]',
        offset > 10 ? 'shadow-xs' : 'shadow-none',
        className
      )}
      {...props}
    >
      <div className='relative flex h-full items-center gap-3 px-4 sm:px-6'>
        <SidebarTrigger variant='ghost' className='h-8 w-8 text-muted-foreground hover:text-foreground' />
        <Separator orientation='vertical' className='h-4 bg-border/80' />
        {children}
      </div>
    </header>
  )
}
