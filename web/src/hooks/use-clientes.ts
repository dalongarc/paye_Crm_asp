import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  api,
  type ClienteDto,
  type ClienteFilterQuery,
  type ClientesPagedResult,
  type CreateClienteRequest,
  type UpdateClienteRequest,
} from '@/api'

export function useClientesQuery(query?: ClienteFilterQuery) {
  return useQuery<ClientesPagedResult>({
    queryKey: ['clientes', query],
    queryFn: () => api.clientes.getClientes(query),
    staleTime: 1000 * 30, // 30 segundos
  })
}

export function useClienteQuery(id: string | null) {
  return useQuery<ClienteDto>({
    queryKey: ['cliente', id],
    queryFn: () => api.clientes.getClienteById(id!),
    enabled: !!id,
  })
}

export function useCreateClienteMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreateClienteRequest) => api.clientes.createCliente(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['clientes'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
  })
}

export function useUpdateClienteMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateClienteRequest }) =>
      api.clientes.updateCliente(id, data),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ['clientes'] })
      queryClient.invalidateQueries({ queryKey: ['cliente', updated.id] })
    },
  })
}

export function useDeleteClienteMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => api.clientes.deleteCliente(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['clientes'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
  })
}
