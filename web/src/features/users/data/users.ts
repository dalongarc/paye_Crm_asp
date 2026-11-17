import { type User } from './schema'

export const users: User[] = [
  {
    id: 'usr-admin-01',
    firstName: 'Administrador',
    lastName: 'Propietario',
    username: 'admin',
    email: 'propietario@payedigital.local',
    phoneNumber: '+34 910 245 800',
    status: 'active',
    role: 'admin',
    createdAt: new Date('2026-01-01T00:00:00Z'),
    updatedAt: new Date('2026-11-10T12:00:00Z'),
  },
]
