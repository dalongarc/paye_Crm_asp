import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { toast } from 'sonner'
import { useDeleteClienteMutation } from '@/hooks/use-clientes'
import type { ClienteDto } from '@/api'

interface ClienteDeleteDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  cliente: ClienteDto | null
  onSuccess?: () => void
}

export function ClienteDeleteDialog({
  open,
  onOpenChange,
  cliente,
  onSuccess,
}: ClienteDeleteDialogProps) {
  const deleteMutation = useDeleteClienteMutation()

  const handleDelete = async () => {
    if (!cliente) return

    try {
      await deleteMutation.mutateAsync(cliente.id)
      toast.success(`Cliente "${cliente.nombreFiscal}" eliminado correctamente.`)
      onOpenChange(false)
      onSuccess?.()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error al eliminar el cliente'
      toast.error(msg)
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>¿Eliminar cliente?</AlertDialogTitle>
          <AlertDialogDescription>
            Esta acción realizará un borrado lógico del cliente{' '}
            <strong className='text-foreground'>{cliente?.nombreFiscal}</strong> ({cliente?.cifNif}). Podrás consultar facturas históricas pero ya no aparecerá como cliente activo.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={deleteMutation.isPending}>
            Cancelar
          </AlertDialogCancel>
          <AlertDialogAction
            onClick={(e) => {
              e.preventDefault()
              handleDelete()
            }}
            disabled={deleteMutation.isPending}
            className='bg-destructive text-destructive-foreground hover:bg-destructive/90'
          >
            {deleteMutation.isPending ? 'Eliminando...' : 'Eliminar Cliente'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
