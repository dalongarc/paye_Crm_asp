import { createFileRoute } from '@tanstack/react-router'
import { DesignShowcase } from '@/features/design'

export const Route = createFileRoute('/_authenticated/design')({
  component: DesignShowcase,
})
