import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type ApiUser, type LoginResult } from '@/api'

export function useCurrentUserQuery() {
  return useQuery({
    queryKey: ['auth', 'me'],
    queryFn: () => api.auth.getCurrentUser(),
    retry: false,
    staleTime: 1000 * 60 * 5, // 5 min
  })
}

export function usePublicAuthConfigQuery() {
  return useQuery({
    queryKey: ['auth', 'public-config'],
    queryFn: () => api.auth.getPublicConfig(),
    staleTime: 1000 * 60 * 60, // 1 hour
  })
}

export function useLoginMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ email, password }: { email: string; password: string }) =>
      api.auth.login(email, password),
    onSuccess: (result: LoginResult) => {
      if (result.status === 'success') {
        queryClient.setQueryData(['auth', 'me'], result.user)
        queryClient.invalidateQueries({ queryKey: ['navigation'] })
      }
    },
  })
}

export function useVerify2faMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ email, code }: { email: string; code: string }) =>
      api.auth.verify2fa(email, code),
    onSuccess: (user: ApiUser) => {
      queryClient.setQueryData(['auth', 'me'], user)
      queryClient.invalidateQueries({ queryKey: ['navigation'] })
    },
  })
}

export function useDemoSandboxLoginMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.auth.loginDemoSandbox(),
    onSuccess: (user: ApiUser) => {
      queryClient.setQueryData(['auth', 'me'], user)
      queryClient.invalidateQueries({ queryKey: ['navigation'] })
    },
  })
}

export function useForgotPasswordMutation() {
  return useMutation({
    mutationFn: ({ email }: { email: string }) =>
      api.auth.forgotPassword(email),
  })
}

export function useLogoutMutation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.auth.logout(),
    onSuccess: () => {
      queryClient.setQueryData(['auth', 'me'], null)
      queryClient.invalidateQueries()
    },
  })
}
