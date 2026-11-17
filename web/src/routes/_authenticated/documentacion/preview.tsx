import z from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import {
  PreviewShowcase,
  type PreviewShowcaseMode,
} from '@/features/documentacion/preview/preview-showcase'

const previewSearchSchema = z.object({
  mode: z
    .enum([
      'presupuesto-corto',
      'presupuesto-largo',
      'contrato-corto',
      'contrato-largo',
    ])
    .optional()
    .catch(undefined),
  clean: z.union([z.string(), z.number(), z.boolean()]).optional().catch(undefined),
})

export const Route = createFileRoute('/_authenticated/documentacion/preview')({
  validateSearch: previewSearchSchema,
  component: DocumentacionPreviewPage,
})

function DocumentacionPreviewPage() {
  const search = Route.useSearch() as {
    mode?: PreviewShowcaseMode
    clean?: string | number | boolean
  }
  const initialMode: PreviewShowcaseMode = search.mode ?? 'presupuesto-corto'
  const isClean =
    search.clean === '1' || search.clean === 1 || search.clean === true

  return <PreviewShowcase initialMode={initialMode} cleanMode={isClean} />
}
