import { RealNavigationApi } from './navigation-api'
import { RealAuthApi } from './auth-api'
import { RealDashboardApi } from './dashboard-api'
import { RealClientesApi } from './clientes-api'
import { RealUsersApi } from './users-api'
import { RealDocumentacionApi } from './documentacion-api'
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
  navigation: new RealNavigationApi(),
  auth: new RealAuthApi(),
  dashboard: new RealDashboardApi(),
  clientes: new RealClientesApi(),
  users: new RealUsersApi(),
  documentacion: new RealDocumentacionApi(),
}

