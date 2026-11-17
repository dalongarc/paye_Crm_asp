import type { IDashboardApi } from '../interfaces'
import type { DashboardSummary, DashboardFacturaReciente } from '../types'

interface StoredInvoice {
  id: string
  numero: string
  cliente: string
  fecha: string
  total: number
  baseImponible: number
  iva: number
  estado: 'emitida' | 'cobrada' | 'borrador' | 'rectificativa'
  lineas?: { concepto: string }[]
}

interface StoredBillingState {
  facturas?: StoredInvoice[]
}

const STORAGE_KEY = 'paye_pos_billing_store_v1'

const EMPTY_DASHBOARD_DATA: DashboardSummary = {
  periodo: 'Mes actual',
  kpis: {
    totalFacturadoMes: 0,
    facturadoVariacionPorcentaje: 0,
    baseImponibleMes: 0,
    ivaMes: 0,
    pendienteCobro: 0,
    facturasPendientesCount: 0,
    clientesActivos: 0,
    clientesNuevosMes: 0,
    efectividadCobroPorcentaje: 100,
    diasPromedioCobro: 0,
  },
  evolucion: [
    { mes: 'Ene', base: 0, total: 0 },
    { mes: 'Feb', base: 0, total: 0 },
    { mes: 'Mar', base: 0, total: 0 },
    { mes: 'Abr', base: 0, total: 0 },
    { mes: 'May', base: 0, total: 0 },
    { mes: 'Jun', base: 0, total: 0 },
  ],
  ultimasFacturas: [],
  servicios: [],
  proximosVencimientos: [],
}

function computeSummaryFromStore(): DashboardSummary {
  if (typeof window === 'undefined') return EMPTY_DASHBOARD_DATA

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return EMPTY_DASHBOARD_DATA

    const parsed = JSON.parse(raw) as StoredBillingState
    const facturas = Array.isArray(parsed.facturas) ? parsed.facturas : []

    if (facturas.length === 0) {
      return EMPTY_DASHBOARD_DATA
    }

    const validInvoices = facturas.filter((f) => f.estado !== 'borrador')
    const totalFacturado = validInvoices.reduce((acc, f) => acc + f.total, 0)
    const baseTotal = validInvoices.reduce((acc, f) => acc + f.baseImponible, 0)
    const ivaTotal = validInvoices.reduce((acc, f) => acc + f.iva, 0)

    const pendingInvoices = validInvoices.filter((f) => f.estado === 'emitida')
    const pendienteCobro = pendingInvoices.reduce((acc, f) => acc + f.total, 0)

    const clientSet = new Set(validInvoices.map((f) => f.cliente).filter(Boolean))

    const ultimasFacturas: DashboardFacturaReciente[] = validInvoices
      .slice(-5)
      .reverse()
      .map((f) => ({
        id: f.id,
        numero: f.numero,
        clienteNombre: f.cliente,
        servicio: f.lineas?.[0]?.concepto ?? 'Factura comercial',
        importe: f.total,
        estado: (f.estado === 'cobrada' ? 'cobrada' : 'pendiente') as
          | 'cobrada'
          | 'pendiente'
          | 'vencida',
        fecha: f.fecha,
      }))

    return {
      periodo: 'Ejercicio actual',
      kpis: {
        totalFacturadoMes: Math.round(totalFacturado * 100) / 100,
        facturadoVariacionPorcentaje: 0,
        baseImponibleMes: Math.round(baseTotal * 100) / 100,
        ivaMes: Math.round(ivaTotal * 100) / 100,
        pendienteCobro: Math.round(pendienteCobro * 100) / 100,
        facturasPendientesCount: pendingInvoices.length,
        clientesActivos: clientSet.size,
        clientesNuevosMes: clientSet.size,
        efectividadCobroPorcentaje:
          validInvoices.length > 0
            ? Math.round(
                ((validInvoices.length - pendingInvoices.length) /
                  validInvoices.length) *
                  100
              )
            : 100,
        diasPromedioCobro: 0,
      },
      evolucion: [
        { mes: 'Mes actual', base: baseTotal, total: totalFacturado },
      ],
      ultimasFacturas,
      servicios: [],
      proximosVencimientos: [],
    }
  } catch {
    return EMPTY_DASHBOARD_DATA
  }
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
      // Si el endpoint aún no existe o hay error de red, calculamos con datos reales locales
    }

    return computeSummaryFromStore()
  }
}
