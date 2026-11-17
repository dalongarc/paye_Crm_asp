import { useState } from 'react'
import { type Table } from '@tanstack/react-table'
import { Trash2, UserX, UserCheck, Mail } from 'lucide-react'
import { toast } from 'sonner'
import { sleep } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { DataTableBulkActions as BulkActionsToolbar } from '@/components/data-table'
import { type User } from '../data/schema'
import { UsersMultiDeleteDialog } from './users-multi-delete-dialog'

type DataTableBulkActionsProps<TData> = {
  table: Table<TData>
}

export function DataTableBulkActions<TData>({
  table,
}: DataTableBulkActionsProps<TData>) {
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const selectedRows = table.getFilteredSelectedRowModel().rows

  const handleBulkStatusChange = (status: 'active' | 'inactive') => {
    const selectedUsers = selectedRows.map((row) => row.original as User)
    toast.promise(sleep(1000), {
      loading: `${status === 'active' ? 'Activando' : 'Desactivando'} usuarios...`,
      success: () => {
        table.resetRowSelection()
        return `${status === 'active' ? 'Activados' : 'Desactivados'} ${selectedUsers.length} usuario${selectedUsers.length > 1 ? 's' : ''}`
      },
      error: `Error al ${status === 'active' ? 'activar' : 'desactivar'} usuarios`,
    })
    table.resetRowSelection()
  }

  const handleBulkInvite = () => {
    const selectedUsers = selectedRows.map((row) => row.original as User)
    toast.promise(sleep(1000), {
      loading: 'Invitando usuarios...',
      success: () => {
        table.resetRowSelection()
        return `Invitados ${selectedUsers.length} usuario${selectedUsers.length > 1 ? 's' : ''}`
      },
      error: 'Error al invitar usuarios',
    })
    table.resetRowSelection()
  }

  return (
    <>
      <BulkActionsToolbar table={table} entityName='usuario'>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant='outline'
              size='icon'
              onClick={handleBulkInvite}
              className='size-8'
              aria-label='Invitar usuarios seleccionados'
              title='Invitar usuarios seleccionados'
            >
              <Mail />
              <span className='sr-only'>Invitar usuarios seleccionados</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>Invitar usuarios seleccionados</p>
          </TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant='outline'
              size='icon'
              onClick={() => handleBulkStatusChange('active')}
              className='size-8'
              aria-label='Activar usuarios seleccionados'
              title='Activar usuarios seleccionados'
            >
              <UserCheck />
              <span className='sr-only'>Activar usuarios seleccionados</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>Activar usuarios seleccionados</p>
          </TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant='outline'
              size='icon'
              onClick={() => handleBulkStatusChange('inactive')}
              className='size-8'
              aria-label='Desactivar usuarios seleccionados'
              title='Desactivar usuarios seleccionados'
            >
              <UserX />
              <span className='sr-only'>Desactivar usuarios seleccionados</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>Desactivar usuarios seleccionados</p>
          </TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant='destructive'
              size='icon'
              onClick={() => setShowDeleteConfirm(true)}
              className='size-8'
              aria-label='Eliminar usuarios seleccionados'
              title='Eliminar usuarios seleccionados'
            >
              <Trash2 />
              <span className='sr-only'>Eliminar usuarios seleccionados</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>Eliminar usuarios seleccionados</p>
          </TooltipContent>
        </Tooltip>
      </BulkActionsToolbar>

      <UsersMultiDeleteDialog
        table={table}
        open={showDeleteConfirm}
        onOpenChange={setShowDeleteConfirm}
      />
    </>
  )
}
