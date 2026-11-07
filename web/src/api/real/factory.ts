import { RealNavigationApi } from './navigation-api'
import { RealAuthApi } from './auth-api'
import { RealDashboardApi } from './dashboard-api'
import { RealClientesApi } from './clientes-api'
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
  navigation: new RealNavigationApi(),
  auth: new RealAuthApi(),
  dashboard: new RealDashboardApi(),
  clientes: new RealClientesApi(),
}

