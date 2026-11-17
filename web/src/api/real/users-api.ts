import type { IUsersApi } from '../interfaces'
import type {
  UserItemDto,
  CreateUserApiRequest,
  UpdateUserApiRequest,
} from '../types'
import { apiFetch } from '../client'

export class RealUsersApi implements IUsersApi {
  async getUsers(): Promise<UserItemDto[]> {
    return apiFetch<UserItemDto[]>('/api/users')
  }

  async createUser(data: CreateUserApiRequest): Promise<UserItemDto> {
    return apiFetch<UserItemDto>('/api/users', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    })
  }

  async updateUser(id: string, data: UpdateUserApiRequest): Promise<UserItemDto> {
    return apiFetch<UserItemDto>(`/api/users/${encodeURIComponent(id)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    })
  }

  async deleteUser(id: string): Promise<void> {
    return apiFetch<void>(`/api/users/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    })
  }
}
