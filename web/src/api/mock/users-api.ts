import type { IUsersApi } from '../interfaces'
import type {
  UserItemDto,
  CreateUserApiRequest,
  UpdateUserApiRequest,
} from '../types'

let mockUsers: UserItemDto[] = [
  {
    id: 'user-001',
    firstName: 'Admin',
    lastName: 'Payé',
    username: 'propietario',
    email: 'propietario@payedigital.local',
    phoneNumber: '+34 600 000 000',
    status: 'active',
    role: 'admin',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
]

export class MockUsersApi implements IUsersApi {
  async getUsers(): Promise<UserItemDto[]> {
    return [...mockUsers]
  }

  async createUser(data: CreateUserApiRequest): Promise<UserItemDto> {
    const newUser: UserItemDto = {
      id: `user-${Date.now()}`,
      firstName: data.firstName,
      lastName: data.lastName,
      username: data.username,
      email: data.email,
      phoneNumber: data.phoneNumber ?? '',
      status: 'active',
      role: 'admin',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }
    mockUsers.push(newUser)
    return newUser
  }

  async updateUser(id: string, data: UpdateUserApiRequest): Promise<UserItemDto> {
    const idx = mockUsers.findIndex((u) => u.id === id)
    if (idx === -1) {
      throw new Error('Usuario no encontrado')
    }
    const updated: UserItemDto = {
      ...mockUsers[idx],
      firstName: data.firstName,
      lastName: data.lastName,
      username: data.username,
      email: data.email,
      phoneNumber: data.phoneNumber ?? mockUsers[idx].phoneNumber,
      updatedAt: new Date().toISOString(),
    }
    mockUsers[idx] = updated
    return updated
  }

  async deleteUser(id: string): Promise<void> {
    mockUsers = mockUsers.filter((u) => u.id !== id)
  }
}
