import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  api,
  type UserItemDto,
  type CreateUserApiRequest,
  type UpdateUserApiRequest,
} from '@/api'
import type { User } from '@/features/users/data/schema'

export function useUsersQuery() {
  return useQuery<User[]>({
    queryKey: ['users'],
    queryFn: async () => {
      const items = await api.users.getUsers()
      return items.map((u: UserItemDto): User => ({
        id: u.id,
        firstName: u.firstName,
        lastName: u.lastName,
        username: u.username,
        email: u.email,
        phoneNumber: u.phoneNumber,
        status: u.status,
        role: 'admin',
        createdAt: new Date(u.createdAt),
        updatedAt: new Date(u.updatedAt),
      }))
    },
    staleTime: 1000 * 30,
  })
}

export function useCreateUserMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreateUserApiRequest) => api.users.createUser(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
    },
  })
}

export function useUpdateUserMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateUserApiRequest }) =>
      api.users.updateUser(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
    },
  })
}

export function useDeleteUserMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => api.users.deleteUser(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] })
    },
  })
}
