import { cn } from '@/lib/utils'

type MainProps = React.HTMLAttributes<HTMLElement> & {
  fixed?: boolean
  fluid?: boolean
  ref?: React.Ref<HTMLElement>
}

export function Main({ fixed, className, fluid = true, ...props }: MainProps) {
  return (
    <main
      data-layout={fixed ? 'fixed' : 'auto'}
      className={cn(
        'px-4 sm:px-6 py-6 w-full',

        // If layout is fixed, make the main container flex and grow
        fixed && 'flex grow flex-col overflow-hidden',

        // If layout is explicitly not fluid, limit max-width and center
        !fluid &&
          '@7xl/content:mx-auto @7xl/content:max-w-7xl',
        className
      )}
      {...props}
    />
  )
}
