import { createFileRoute, redirect } from '@tanstack/react-router'

export const Route = createFileRoute('/_authenticated/trimestre')({
  beforeLoad: () => {
    throw redirect({ to: '/gastos' })
  },
})
