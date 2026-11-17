'use client'

import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { toast } from 'sonner'
import { useDeleteUserMutation } from '@/hooks/use-users'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { type User } from '../data/schema'

type UserDeleteDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow: User
}

export function UsersDeleteDialog({
  open,
  onOpenChange,
  currentRow,
}: UserDeleteDialogProps) {
  const [value, setValue] = useState('')
  const deleteMutation = useDeleteUserMutation()

  const handleDelete = async () => {
    if (value.trim() !== currentRow.username) return

    try {
      await deleteMutation.mutateAsync(currentRow.id)
      toast.success(`Usuario ${currentRow.username} eliminado correctamente.`)
      onOpenChange(false)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error al eliminar el usuario.'
      toast.error(msg)
    }
  }

  return (
    <ConfirmDialog
      open={open}
      onOpenChange={onOpenChange}
      form='users-delete-form'
      disabled={value.trim() !== currentRow.username}
      title={
        <span className='text-destructive'>
          <AlertTriangle
            className='me-1 inline-block stroke-destructive'
            size={18}
          />{' '}
          Eliminar Usuario
        </span>
      }
      desc={
        <form
          id='users-delete-form'
          onSubmit={(e) => {
            e.preventDefault()
            handleDelete()
          }}
          className='space-y-4'
        >
          <p className='mb-2'>
            ¿Estás seguro de que deseas eliminar al usuario{' '}
            <span className='font-bold'>{currentRow.username}</span>?
            <br />
            Esta acción eliminará de forma permanente al usuario del sistema. Esta acción no se puede deshacer.
          </p>

          <Label className='my-2'>
            Nombre de usuario:
            <Input
              value={value}
              onChange={(e) => setValue(e.target.value)}
              placeholder='Escribe el nombre de usuario para confirmar.'
              autoFocus
            />
          </Label>

          <Alert variant='destructive'>
            <AlertTitle>¡Atención!</AlertTitle>
            <AlertDescription>
              Por favor ten cuidado, esta operación no se puede revertir.
            </AlertDescription>
          </Alert>
        </form>
      }
      confirmText='Eliminar'
      destructive
    />
  )
}
