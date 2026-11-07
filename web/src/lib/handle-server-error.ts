import { AxiosError } from 'axios'
import { toast } from 'sonner'

import { AuthError, ConcurrencyConflictError } from '@/api'

export function handleServerError(error: unknown) {
  if (error instanceof AuthError || error instanceof ConcurrencyConflictError) {
    // Gestionados directamente en sus respectivos formularios/vistas
    return
  }

  if (import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.log(error)
  }

  let errMsg = 'Ocurrió un error inesperado en el servidor.'

  if (
    error &&
    typeof error === 'object' &&
    'status' in error &&
    Number(error.status) === 204
  ) {
    errMsg = 'No content.'
  }

  if (error instanceof AxiosError) {
    const title = error.response?.data?.title
    if (typeof title === 'string' && title.length > 0) {
      errMsg = title
    }
  }

  toast.error(errMsg)
}
