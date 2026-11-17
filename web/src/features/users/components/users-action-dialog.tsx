'use client'

import { useEffect } from 'react'
import { z } from 'zod'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { useCreateUserMutation, useUpdateUserMutation } from '@/hooks/use-users'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { PasswordInput } from '@/components/password-input'
import { SelectDropdown } from '@/components/select-dropdown'
import { roles } from '../data/data'
import { type User } from '../data/schema'

const formSchema = z
  .object({
    firstName: z.string().min(1, 'El nombre es obligatorio.'),
    lastName: z.string().min(1, 'Los apellidos son obligatorios.'),
    username: z.string().min(1, 'El nombre de usuario es obligatorio.'),
    phoneNumber: z.string(),
    email: z.email({
      error: (iss) => (iss.input === '' ? 'El correo electrónico es obligatorio.' : undefined),
    }),
    password: z.string().transform((pwd) => pwd.trim()),
    role: z.string().min(1, 'El rol es obligatorio.'),
    confirmPassword: z.string().transform((pwd) => pwd.trim()),
    isEdit: z.boolean(),
  })
  .refine(
    (data) => {
      if (data.isEdit && !data.password) return true
      return data.password.length > 0
    },
    {
      message: 'La contraseña es obligatoria.',
      path: ['password'],
    }
  )
  .refine(
    ({ isEdit, password }) => {
      if (isEdit && !password) return true
      return password.length >= 4
    },
    {
      message: 'La contraseña debe tener al menos 4 caracteres.',
      path: ['password'],
    }
  )
  .refine(
    ({ isEdit, password, confirmPassword }) => {
      if (isEdit && !password) return true
      return password === confirmPassword
    },
    {
      message: 'Las contraseñas no coinciden.',
      path: ['confirmPassword'],
    }
  )
type UserForm = z.infer<typeof formSchema>

type UserActionDialogProps = {
  currentRow?: User
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function UsersActionDialog({
  currentRow,
  open,
  onOpenChange,
}: UserActionDialogProps) {
  const isEdit = !!currentRow
  const createMutation = useCreateUserMutation()
  const updateMutation = useUpdateUserMutation()
  const isPending = createMutation.isPending || updateMutation.isPending

  const form = useForm<UserForm>({
    resolver: zodResolver(formSchema),
    defaultValues: isEdit
      ? {
          ...currentRow,
          password: '',
          confirmPassword: '',
          isEdit,
        }
      : {
          firstName: '',
          lastName: '',
          username: '',
          email: '',
          role: 'admin',
          phoneNumber: '',
          password: '',
          confirmPassword: '',
          isEdit,
        },
  })

  useEffect(() => {
    if (open) {
      if (currentRow) {
        form.reset({
          firstName: currentRow.firstName,
          lastName: currentRow.lastName,
          username: currentRow.username,
          email: currentRow.email,
          role: 'admin',
          phoneNumber: currentRow.phoneNumber || '',
          password: '',
          confirmPassword: '',
          isEdit: true,
        })
      } else {
        form.reset({
          firstName: '',
          lastName: '',
          username: '',
          email: '',
          role: 'admin',
          phoneNumber: '',
          password: '',
          confirmPassword: '',
          isEdit: false,
        })
      }
    }
  }, [open, currentRow, form])

  const onSubmit = async (values: UserForm) => {
    try {
      if (isEdit && currentRow) {
        await updateMutation.mutateAsync({
          id: currentRow.id,
          data: {
            firstName: values.firstName,
            lastName: values.lastName,
            username: values.username,
            email: values.email,
            phoneNumber: values.phoneNumber,
            password: values.password ? values.password : undefined,
            role: 'admin',
          },
        })
        toast.success(`Usuario ${values.username} actualizado correctamente.`)
      } else {
        await createMutation.mutateAsync({
          firstName: values.firstName,
          lastName: values.lastName,
          username: values.username,
          email: values.email,
          phoneNumber: values.phoneNumber,
          password: values.password,
          role: 'admin',
        })
        toast.success(`Usuario ${values.username} creado correctamente.`)
      }
      form.reset()
      onOpenChange(false)
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : 'Error al guardar el usuario.'
      toast.error(errorMsg)
    }
  }

  const isPasswordTouched = !!form.formState.dirtyFields.password

  return (
    <Dialog
      open={open}
      onOpenChange={(state) => {
        if (!state) {
          form.reset()
        }
        onOpenChange(state)
      }}
    >
      <DialogContent
        className='sm:max-w-lg'
        onFocusOutside={(e) => e.preventDefault()}
      >
        <DialogHeader className='text-start'>
          <DialogTitle>{isEdit ? 'Editar Usuario' : 'Añadir Nuevo Usuario'}</DialogTitle>
          <DialogDescription>
            {isEdit
              ? 'Modifica los datos del usuario. '
              : 'Introduce los datos del nuevo usuario. '}
            Haz clic en guardar cambios al terminar.
          </DialogDescription>
        </DialogHeader>
        <div className='h-105 w-[calc(100%+0.75rem)] overflow-y-auto py-1 pe-3'>
          <Form {...form}>
            <form
              id='user-form'
              onSubmit={form.handleSubmit(onSubmit)}
              className='space-y-4 px-0.5'
            >
              <FormField
                control={form.control}
                name='firstName'
                render={({ field }) => (
                  <FormItem className='grid grid-cols-6 items-center space-y-0 gap-x-4 gap-y-1'>
                    <FormLabel className='col-span-2 text-end'>
                      Nombre
                    </FormLabel>
                    <FormControl>
                      <Input
                        placeholder='Dario'
                        className='col-span-4'
                        autoComplete='off'
                        {...field}
                      />
                    </FormControl>
                    <FormMessage className='col-span-4 col-start-3' />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name='lastName'
                render={({ field }) => (
                  <FormItem className='grid grid-cols-6 items-center space-y-0 gap-x-4 gap-y-1'>
                    <FormLabel className='col-span-2 text-end'>
                      Apellidos
                    </FormLabel>
                    <FormControl>
                      <Input
                        placeholder='García'
                        className='col-span-4'
                        autoComplete='off'
                        {...field}
                      />
                    </FormControl>
                    <FormMessage className='col-span-4 col-start-3' />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name='username'
                render={({ field }) => (
                  <FormItem className='grid grid-cols-6 items-center space-y-0 gap-x-4 gap-y-1'>
                    <FormLabel className='col-span-2 text-end'>
                      Nombre de usuario
                    </FormLabel>
                    <FormControl>
                      <Input
                        placeholder='admin'
                        className='col-span-4'
                        {...field}
                      />
                    </FormControl>
                    <FormMessage className='col-span-4 col-start-3' />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name='email'
                render={({ field }) => (
                  <FormItem className='grid grid-cols-6 items-center space-y-0 gap-x-4 gap-y-1'>
                    <FormLabel className='col-span-2 text-end'>Correo electrónico</FormLabel>
                    <FormControl>
                      <Input
                        placeholder='propietario@comercio.es'
                        className='col-span-4'
                        {...field}
                      />
                    </FormControl>
                    <FormMessage className='col-span-4 col-start-3' />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name='phoneNumber'
                render={({ field }) => (
                  <FormItem className='grid grid-cols-6 items-center space-y-0 gap-x-4 gap-y-1'>
                    <FormLabel className='col-span-2 text-end'>
                      Teléfono
                    </FormLabel>
                    <FormControl>
                      <Input
                        placeholder='+34 910 245 800'
                        className='col-span-4'
                        {...field}
                      />
                    </FormControl>
                    <FormMessage className='col-span-4 col-start-3' />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name='role'
                render={({ field }) => (
                  <FormItem className='grid grid-cols-6 items-center space-y-0 gap-x-4 gap-y-1'>
                    <FormLabel className='col-span-2 text-end'>Rol</FormLabel>
                    <SelectDropdown
                      defaultValue={field.value}
                      onValueChange={field.onChange}
                      placeholder='Seleccionar rol'
                      className='col-span-4'
                      items={roles.map(({ label, value }) => ({
                        label,
                        value,
                      }))}
                    />
                    <FormMessage className='col-span-4 col-start-3' />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name='password'
                render={({ field }) => (
                  <FormItem className='grid grid-cols-6 items-center space-y-0 gap-x-4 gap-y-1'>
                    <FormLabel className='col-span-2 text-end'>
                      Contraseña
                    </FormLabel>
                    <FormControl>
                      <PasswordInput
                        placeholder='p. ej., C0ntr@señaSegura123'
                        className='col-span-4'
                        {...field}
                      />
                    </FormControl>
                    <FormMessage className='col-span-4 col-start-3' />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name='confirmPassword'
                render={({ field }) => (
                  <FormItem className='grid grid-cols-6 items-center space-y-0 gap-x-4 gap-y-1'>
                    <FormLabel className='col-span-2 text-end'>
                      Confirmar contraseña
                    </FormLabel>
                    <FormControl>
                      <PasswordInput
                        disabled={!isPasswordTouched}
                        placeholder='Repite la contraseña'
                        className='col-span-4'
                        {...field}
                      />
                    </FormControl>
                    <FormMessage className='col-span-4 col-start-3' />
                  </FormItem>
                )}
              />
            </form>
          </Form>
        </div>
        <DialogFooter>
          <Button type='submit' form='user-form' disabled={isPending}>
            {isPending ? 'Guardando...' : 'Guardar cambios'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
