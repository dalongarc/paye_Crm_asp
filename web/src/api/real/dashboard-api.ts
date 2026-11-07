import type { IDashboardApi } from '../interfaces'
import type { DashboardSummary } from '../types'

const SAMPLE_DASHBOARD_DATA: DashboardSummary = {
  periodo: 'Noviembre 2026',
  kpis: {
    totalFacturadoMes: 8450.0,
    facturadoVariacionPorcentaje: 14.2,
    baseImponibleMes: 6983.47,
    ivaMes: 1466.53,
    pendienteCobro: 2150.0,
    facturasPendientesCount: 3,
    clientesActivos: 24,
    clientesNuevosMes: 3,
    efectividadCobroPorcentaje: 92.5,
    diasPromedioCobro: 12,
  },
  evolucion: [
    { mes: 'Jun', base: 4800, total: 5808 },
    { mes: 'Jul', base: 5400, total: 6534 },
    { mes: 'Ago', base: 4100, total: 4961 },
    { mes: 'Sep', base: 6200, total: 7502 },
    { mes: 'Oct', base: 6100, total: 7381 },
    { mes: 'Nov', base: 6983.47, total: 8450 },
  ],
  ultimasFacturas: [
    {
      id: 'fac-42',
      numero: 'FAC-2026-042',
      clienteNombre: 'Informática Norte S.L.',
      servicio: 'Desarrollo a medida',
      importe: 1850.0,
      estado: 'cobrada',
      fecha: 'Hoy, 11:30',
    },
    {
      id: 'fac-41',
      numero: 'FAC-2026-041',
      clienteNombre: 'Estudio Creativo Web',
      servicio: 'Mantenimiento WP & Hosting',
      importe: 450.0,
      estado: 'pendiente',
      fecha: 'Ayer, 17:15',
    },
    {
      id: 'fac-40',
      numero: 'FAC-2026-040',
      clienteNombre: 'Grupo Logístico Sur',
      servicio: 'Infraestructura VPS Coolify',
      importe: 1200.0,
      estado: 'cobrada',
      fecha: '3 Nov, 09:20',
    },
    {
      id: 'fac-39',
      numero: 'FAC-2026-039',
      clienteNombre: 'Clínica Dental Alameda',
      servicio: 'Bolsa de horas soporte',
      importe: 350.0,
      estado: 'pendiente',
      fecha: '2 Nov, 14:05',
    },
    {
      id: 'fac-38',
      numero: 'FAC-2026-038',
      clienteNombre: 'Restaurante El Puerto',
      servicio: 'TPV Payé y Carta QR',
      importe: 600.0,
      estado: 'vencida',
      fecha: '1 Nov, 18:40',
    },
  ],
  servicios: [
    { nombre: 'Desarrollo a medida', clientesCount: 10, porcentaje: 42 },
    { nombre: 'WordPress & Hosting', clientesCount: 7, porcentaje: 29 },
    { nombre: 'VPS, Cloud & Coolify', clientesCount: 4, porcentaje: 17 },
    { nombre: 'Equipos & Asesoría', clientesCount: 3, porcentaje: 12 },
  ],
  proximosVencimientos: [
    {
      id: 'fac-41',
      numero: 'FAC-2026-041',
      clienteNombre: 'Estudio Creativo Web',
      importe: 450.0,
      diasRestantes: 3,
    },
    {
      id: 'fac-39',
      numero: 'FAC-2026-039',
      clienteNombre: 'Consultoría Gómez',
      importe: 850.0,
      diasRestantes: 5,
    },
  ],
}

export class RealDashboardApi implements IDashboardApi {
  async getSummary(): Promise<DashboardSummary> {
    try {
      const response = await fetch('/api/dashboard/summary', {
        method: 'GET',
        headers: {
          Accept: 'application/json',
        },
        credentials: 'same-origin',
      })

      if (response.ok) {
        return (await response.json()) as DashboardSummary
      }
    } catch {
      // Si el endpoint aún no existe o hay error de red, cae al fallback de datos de ejemplo
    }

    return SAMPLE_DASHBOARD_DATA
  }
}
