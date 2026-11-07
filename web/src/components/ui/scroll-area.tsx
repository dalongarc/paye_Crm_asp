import * as React from 'react'
import { cn } from '@/lib/utils'

interface ScrollAreaProps extends React.HTMLAttributes<HTMLDivElement> {
  orientation?: 'vertical' | 'horizontal'
  type?: 'auto' | 'always' | 'scroll' | 'hover'
}

function ScrollArea({
  className,
  children,
  orientation = 'vertical',
  type: _type,
  ...props
}: ScrollAreaProps) {
  return (
    <div
      data-slot='scroll-area'
      className={cn(
        'relative overflow-auto',
        orientation === 'horizontal' ? 'overflow-x-auto' : 'overflow-y-auto',
        className
      )}
      {...props}
    >
      <div
        data-slot='scroll-area-viewport'
        className='size-full rounded-[inherit] transition-[color,box-shadow] outline-none'
      >
        {children}
      </div>
    </div>
  )
}

function ScrollBar({
  className,
  orientation = 'vertical',
  ...props
}: React.HTMLAttributes<HTMLDivElement> & {
  orientation?: 'vertical' | 'horizontal'
}) {
  return (
    <div
      data-slot='scroll-area-scrollbar'
      data-orientation={orientation}
      className={cn('hidden', className)}
      {...props}
    />
  )
}

export { ScrollArea, ScrollBar }
