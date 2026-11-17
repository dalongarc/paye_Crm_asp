import { MockNavigationApi } from './navigation-api'
import { MockAuthApi } from './auth-api'
import { MockDashboardApi } from './dashboard-api'
import { MockClientesApi } from './clientes-api'
import { MockUsersApi } from './users-api'
import { MockDocumentacionApi } from './documentacion-api'
import type {
  INavigationApi,
  IAuthApi,
  IDashboardApi,
  IClientesApi,
  IUsersApi,
  IDocumentacionApi,
} from '../interfaces'

export const api: {
  navigation: INavigationApi
  auth: IAuthApi
  dashboard: IDashboardApi
  clientes: IClientesApi
  users: IUsersApi
  documentacion: IDocumentacionApi
} = {
  navigation: new MockNavigationApi(),
  auth: new MockAuthApi(),
  dashboard: new MockDashboardApi(),
  clientes: new MockClientesApi(),
  users: new MockUsersApi(),
  documentacion: new MockDocumentacionApi(),
}

