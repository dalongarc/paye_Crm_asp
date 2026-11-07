import { MockNavigationApi } from './navigation-api'
import { MockAuthApi } from './auth-api'
import { MockDashboardApi } from './dashboard-api'
import { MockClientesApi } from './clientes-api'
import type {
  INavigationApi,
  IAuthApi,
  IDashboardApi,
  IClientesApi,
} from '../interfaces'

export const api: {
  navigation: INavigationApi
  auth: IAuthApi
  dashboard: IDashboardApi
  clientes: IClientesApi
} = {
  navigation: new MockNavigationApi(),
  auth: new MockAuthApi(),
  dashboard: new MockDashboardApi(),
  clientes: new MockClientesApi(),
}

