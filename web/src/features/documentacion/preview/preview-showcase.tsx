import React, { useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { FileCheck2, FileText, ArrowLeft, Layers } from 'lucide-react'
import type {
  DocumentacionCompanySettingsDto,
  DocumentoBloqueDto,
  DocumentoLineaInputDto,
} from '@/api/types'
import {
  DEFAULT_LEGAL_NOTICE_TEXT,
  DEFAULT_LEGAL_NOTICE_TITLE,
  DOCUMENTACION_ASSETS,
  createInitialBlocksForType,
} from '../config/default-templates'
import {
  DocumentoA4Renderer,
  type DocumentoA4Data,
} from '../components/documento-a4-renderer'

export type PreviewShowcaseMode =
  | 'presupuesto-corto'
  | 'presupuesto-largo'
  | 'contrato-corto'
  | 'contrato-largo'

const DEFAULT_COMPANY: DocumentacionCompanySettingsDto = {
  id: 'company-preview',
  nombreEmpresa: 'Payé Digital',
  cifNif: 'B15987456',
  direccion: 'Calle Real 42, 15003 A Coruña',
  telefono: '+34 881 000 123',
  email: 'hola@payedigital.com',
  web: 'www.payedigital.com',
  iban: 'ES91 2100 0418 4502 0005 1332',
  logoUrl: DOCUMENTACION_ASSETS.logoHorizontalDark,
  textoLegalTitulo: DEFAULT_LEGAL_NOTICE_TITLE,
  textoLegalContenido: DEFAULT_LEGAL_NOTICE_TEXT,
  rowVersion: 1,
}

const LONG_PRESUPUESTO_LINES: DocumentoLineaInputDto[] = [
  {
    descripcion: 'Consultoría estratégica digital y arquitectura de información e-commerce',
    cantidad: 1,
    precioUnitario: 320,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Diseño UX/UI a medida (prototipado Figma desktop y mobile-first)',
    cantidad: 1,
    precioUnitario: 480,
    descuentoPorcentaje: 10,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Maquetación y desarrollo de portada corporativa de alta conversión',
    cantidad: 1,
    precioUnitario: 290,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Configuración de motor Shopify / WooCommerce y catálogo inicial (50 SKU)',
    cantidad: 1,
    precioUnitario: 420,
    descuentoPorcentaje: 5,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Integración de pasarela TPV Virtual Redsys, Stripe y Bizum',
    cantidad: 1,
    precioUnitario: 210,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Módulo de automatización de gastos de envío e impresión instantánea',
    cantidad: 1,
    precioUnitario: 150,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Optimización SEO Técnico On-Page (Schema.org, Sitemap, Core Web Vitals)',
    cantidad: 1,
    precioUnitario: 260,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Redacción editorial de fichas de producto y guía de estilo (IVA reducido)',
    cantidad: 4,
    precioUnitario: 65,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 10,
  },
  {
    descripcion: 'Manual didáctico impreso de administración de la tienda (IVA superreducido)',
    cantidad: 2,
    precioUnitario: 45,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 4,
  },
  {
    descripcion: 'Sesión de formación reglada online para el equipo del cliente (Exento IVA)',
    cantidad: 6,
    precioUnitario: 55,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 0,
  },
  {
    descripcion: 'Adaptación legal RGPD / LSSI-CE (textos legales, banner de cookies CMP)',
    cantidad: 1,
    precioUnitario: 180,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Configuración de analítica avanzada GA4 y píxeles de conversión',
    cantidad: 1,
    precioUnitario: 165,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Sistema de copias de seguridad diarias automatizadas en nube europea',
    cantidad: 12,
    precioUnitario: 18,
    descuentoPorcentaje: 10,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Firewall de aplicación web (WAF), protección anti-DDoS y monitorización 24/7',
    cantidad: 12,
    precioUnitario: 22,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Bolsa anual de soporte evolutivo y pequeños cambios (10 horas incluidas)',
    cantidad: 10,
    precioUnitario: 48,
    descuentoPorcentaje: 15,
    ivaPorcentaje: 21,
  },
  {
    descripcion: 'Auditoría trimestral de rendimiento, conversión y embudo de ventas',
    cantidad: 4,
    precioUnitario: 95,
    descuentoPorcentaje: 0,
    ivaPorcentaje: 21,
  },
]

function buildLongContratoBlocks(): DocumentoBloqueDto[] {
  const base = createInitialBlocksForType(1)
  const extraClauses: DocumentoBloqueDto[] = [
    {
      id: 'cnt-extra-sla',
      templateBlockId: null,
      orden: 8,
      numeroPastilla: '7.',
      titulo: 'NIVELES DE SERVICIO (SLA) Y TIEMPOS DE RESPUESTA',
      contenidoHtml: `<p>El Prestador garantiza una disponibilidad mensual de la infraestructura superior al <strong>99,8 %</strong> en horario comercial y nocturno. Ante cualquier incidencia técnica comunicada por el Cliente, se aplicarán los siguientes tiempos máximos de respuesta y resolución:</p><ul><li><strong>Incidencia Crítica (caída total o fallo en pasarela de pagos):</strong> tiempo de respuesta inferior a 2 horas laborables y resolución prioritaria en menos de 6 horas.</li><li><strong>Incidencia Moderada (errores visuales o de catálogo no bloqueantes):</strong> respuesta en menos de 8 horas laborables y resolución en 24-48 horas.</li><li><strong>Consultas y soporte evolutivo:</strong> planificación semanal acordada con el responsable designado por el Cliente.</li></ul>`,
      visible: true,
      esPersonalizado: true,
      tipoBloque: 'pill-section',
    },
    {
      id: 'cnt-extra-subcontratacion',
      templateBlockId: null,
      orden: 9,
      numeroPastilla: '8.',
      titulo: 'INFRAESTRUCTURA CLOUD Y COPIAS DE SEGURIDAD',
      contenidoHtml: `<p>Los servidores de producción contarán con almacenamiento NVMe redundante en centros de datos ubicados dentro de la Unión Europea, cumpliendo con los estándares ISO 27001 y ENS. Se realizarán copias de seguridad incrementales cada 24 horas con retención de 30 días naturales y verificación automática de integridad.</p>`,
      visible: true,
      esPersonalizado: true,
      tipoBloque: 'pill-section',
    },
    {
      id: 'cnt-extra-pricing',
      templateBlockId: null,
      orden: 10,
      numeroPastilla: '9.',
      titulo: 'ANEXO ECONÓMICO Y DESGLOSE DE PARTIDAS',
      contenidoHtml: `<p>Se adjunta el desglose pormenorizado de las 16 partidas técnicas contratadas, incluyendo los distintos tipos impositivos aplicables según la naturaleza de cada servicio:</p>`,
      visible: true,
      esPersonalizado: true,
      tipoBloque: 'pricing-table',
    },
  ]

  const withoutSig = base.filter((b) => b.tipoBloque !== 'signatures')
  const sigBlock = base.find((b) => b.tipoBloque === 'signatures')!
  return [
    ...withoutSig,
    ...extraClauses,
    {
      ...sigBlock,
      orden: 11,
      numeroPastilla: '10.',
    },
  ]
}

function buildLongPresupuestoBlocks(): DocumentoBloqueDto[] {
  const base = createInitialBlocksForType(2).map((b: DocumentoBloqueDto) =>
    b.tipoBloque === 'pricing-table' ? { ...b, visible: true } : b
  )
  const extraBlock: DocumentoBloqueDto = {
    id: 'pres-extra-garantia',
    templateBlockId: null,
    orden: 8,
    numeroPastilla: '7.',
    titulo: 'GARANTÍA TÉCNICA, ENTREGABLES Y CRONOGRAMA',
    contenidoHtml: `<p>El proyecto se ejecutará en un plazo estimado de <strong>5 semanas</strong> divididas en cuatro hitos verificables:</p><ul><li><strong>Semana 1 — Arquitectura y Diseño UX/UI:</strong> entrega de wireframes y validación visual de la identidad en Figma.</li><li><strong>Semanas 2 y 3 — Desarrollo e Integración Shopify:</strong> configuración del tema a medida, pasarelas de pago, servicio de impresión automática y carga de 50 artículos.</li><li><strong>Semana 4 — Pruebas QA, SEO y Rendimiento:</strong> auditoría en dispositivos reales iOS/Android, pruebas de compra y optimización de velocidad.</li><li><strong>Semana 5 — Formación y Lanzamiento:</strong> sesión práctica con el equipo de Alpe y puesta en producción sin corte de servicio.</li></ul><p>Todos los desarrollos cuentan con una <strong>garantía técnica integral de 6 meses</strong> frente a cualquier defecto de programación imputable al desarrollo original.</p>`,
    visible: true,
    esPersonalizado: true,
    tipoBloque: 'pill-section',
  }
  return [...base, extraBlock]
}

export interface PreviewShowcaseProps {
  initialMode?: PreviewShowcaseMode
  cleanMode?: boolean
}

export const PreviewShowcase: React.FC<PreviewShowcaseProps> = ({
  initialMode = 'presupuesto-corto',
  cleanMode = false,
}) => {
  const [mode, setMode] = useState<PreviewShowcaseMode>(initialMode)

  const documentData: DocumentoA4Data = useMemo(() => {
    switch (mode) {
      case 'presupuesto-corto': {
        // Réplica 1:1 exacta de las 4 páginas de Alpe_Web.pdf (Total: 2.600 + IVA)
        const blocks = createInitialBlocksForType(2).map((b: DocumentoBloqueDto) =>
          b.tipoBloque === 'pricing-table' ? { ...b, visible: false } : b
        )
        return {
          tipo: 2,
          numeroDocumento: 'PRES-2026-001',
          titulo: 'Web para aumentar venta online',
          subtitulo: 'e-Commerce',
          categoria: 'e-Commerce',
          clienteNombre: 'Alpe',
          clienteCifNif: 'B15000111',
          clienteDireccion: 'A Coruña',
          fechaEmision: '2026-04-16',
          fechaValidez: '2026-05-16',
          bloques: blocks,
          lineas: [
            {
              descripcion: 'Creación y configuración de tienda online Shopify + Subida de 50 artículos + SEO',
              cantidad: 1,
              precioUnitario: 1850,
              descuentoPorcentaje: 0,
              ivaPorcentaje: 21,
            },
            {
              descripcion: 'Servicio web de impresión instantánea y automática de pedido y etiqueta',
              cantidad: 1,
              precioUnitario: 750,
              descuentoPorcentaje: 0,
              ivaPorcentaje: 21,
            },
          ],
          mostrarCabeceraEmpresa: false,
          mostrarCodigoEnPortada: false,
          mostrarCenefaPortada: false,
          mostrarDatosFiscalesPie: false,
        }
      }

      case 'presupuesto-largo': {
        return {
          tipo: 2,
          numeroDocumento: 'PRES-2026-002',
          titulo: 'Plataforma E-Commerce Omnicanal y Plan de Crecimiento',
          subtitulo: 'Diseño Web',
          categoria: 'Diseño Web',
          clienteNombre: 'Alpe',
          clienteCifNif: 'B15000111',
          clienteDireccion: 'Polígono Industrial Pocomaco, A Coruña',
          fechaEmision: '2026-04-16',
          fechaValidez: '2026-05-16',
          bloques: buildLongPresupuestoBlocks(),
          lineas: LONG_PRESUPUESTO_LINES,
          mostrarCabeceraEmpresa: false,
          mostrarCodigoEnPortada: false,
          mostrarCenefaPortada: false,
          mostrarDatosFiscalesPie: false,
        }
      }

      case 'contrato-corto': {
        return {
          tipo: 1,
          numeroDocumento: 'CONT-2026-001',
          titulo: 'Desarrollo de Tienda Online y Mantenimiento Anual',
          subtitulo: 'Servicios Digitales',
          categoria: 'Servicios Digitales',
          clienteNombre: 'Alpe',
          clienteCifNif: 'B15000111',
          clienteDireccion: 'Calle Real 15, 15003 A Coruña',
          clienteEmail: 'direccion@alpe.es',
          clienteTelefono: '+34 981 000 111',
          fechaEmision: '2026-04-16',
          objetoContrato:
            'Diseño, desarrollo e implantación de tienda online y servicio de impresión automática.',
          duracionContrato: '12 meses (prorrogable por anualidades)',
          precioTextoContrato: '1.900,00 € + IVA (2.299,00 € IVA incluido)',
          formaPagoContrato: '50 % a la firma y 50 % a la publicación de la web',
          lugarFirma: 'A Coruña',
          bloques: createInitialBlocksForType(1),
          lineas: [
            {
              descripcion: 'Desarrollo de tienda online y servicio de impresión automática',
              cantidad: 1,
              precioUnitario: 1900,
              descuentoPorcentaje: 0,
              ivaPorcentaje: 21,
            },
          ],
          mostrarCabeceraEmpresa: false,
          mostrarCodigoEnPortada: false,
          mostrarCenefaPortada: false,
          mostrarDatosFiscalesPie: false,
        }
      }

      case 'contrato-largo': {
        return {
          tipo: 1,
          numeroDocumento: 'CONT-2026-002',
          titulo: 'Contrato Marco de Desarrollo E-Commerce, SLA y Protección de Datos',
          subtitulo: 'Ingeniería & Diseño Web',
          categoria: 'Ingeniería & Diseño Web',
          clienteNombre: 'Alpe',
          clienteCifNif: 'B15000111',
          clienteDireccion: 'Polígono Industrial Pocomaco, Parcela D-12, 15190 A Coruña',
          clienteEmail: 'direccion@alpe.es',
          clienteTelefono: '+34 981 000 111',
          fechaEmision: '2026-04-16',
          objetoContrato:
            'Desarrollo integral de plataforma e-commerce, alojamiento gestionado, SLA 99,8% y soporte.',
          duracionContrato: '24 meses desde el acta de puesta en producción',
          precioTextoContrato: 'Según anexo económico desglosado (16 partidas)',
          formaPagoContrato: '40 % al inicio, 40 % en entrega beta y 20 % en lanzamiento',
          lugarFirma: 'A Coruña',
          bloques: buildLongContratoBlocks(),
          lineas: LONG_PRESUPUESTO_LINES,
          mostrarCabeceraEmpresa: false,
          mostrarCodigoEnPortada: false,
          mostrarCenefaPortada: false,
          mostrarDatosFiscalesPie: false,
        }
      }
    }
  }, [mode])

  if (cleanMode) {
    return (
      <div className='fixed inset-0 z-50 bg-zinc-900 overflow-auto py-4'>
        <DocumentoA4Renderer document={documentData} company={DEFAULT_COMPANY} />
      </div>
    )
  }

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border/80 bg-card px-4 py-3 shadow-2xs print:hidden'>
        <div className='flex flex-wrap items-center gap-2.5'>
          <Button variant='ghost' size='sm' asChild className='h-8 gap-1.5 text-xs'>
            <a href='/documentacion'>
              <ArrowLeft className='h-3.5 w-3.5' />
              <span>Ir a Documentación</span>
            </a>
          </Button>
          <span className='h-4 w-px bg-border' />
          <Layers className='h-4 w-4 text-amber-500' />
          <span className='text-sm font-semibold'>
            Verificación Visual A4 (/documentacion/preview)
          </span>
          <Badge variant='outline' className='font-mono text-xs'>
            {documentData.numeroDocumento}
          </Badge>
        </div>

        <div className='flex flex-wrap items-center gap-1.5'>
          {(
            [
              {
                id: 'presupuesto-corto',
                label: '1. Presupuesto Corto (Réplica 1:1 Alpe_Web.pdf)',
                icon: FileText,
              },
              {
                id: 'presupuesto-largo',
                label: '2. Presupuesto Largo (16 líneas + Multi-IVA)',
                icon: FileText,
              },
              {
                id: 'contrato-corto',
                label: '3. Contrato Corto',
                icon: FileCheck2,
              },
              {
                id: 'contrato-largo',
                label: '4. Contrato Largo (10 cláusulas + 16 líneas)',
                icon: FileCheck2,
              },
            ] as const
          ).map((item) => {
            const Icon = item.icon
            const active = mode === item.id
            return (
              <Button
                key={item.id}
                type='button'
                variant={active ? 'default' : 'outline'}
                size='sm'
                className='h-8 text-xs gap-1.5'
                onClick={() => setMode(item.id)}
                data-testid={`btn-preview-${item.id}`}
              >
                <Icon className='h-3.5 w-3.5' />
                <span>{item.label}</span>
              </Button>
            )
          })}
        </div>
      </div>

      <div className='rounded-2xl border border-border/80 bg-zinc-900 overflow-auto'>
        <DocumentoA4Renderer document={documentData} company={DEFAULT_COMPANY} />
      </div>
    </div>
  )
}
