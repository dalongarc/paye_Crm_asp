import type {
  DocumentoBloqueDto,
  DocumentoLineaInputDto,
  TipoDocumentoComercial,
} from '@/api/types'
import { DEFAULT_LINE_VAT_RATE } from '../utils/totals-calculator'

/**
 * Rutas locales a los recursos gráficos PNG/SVG en /public/images/documentacion/
 */
export const DOCUMENTACION_ASSETS = {
  logoHorizontalDark: '/images/documentacion/logo-paye-horizontal.png',
  logoStackedDark: '/images/documentacion/logo-paye-dark.png',
  logoBadgeDark: '/images/documentacion/logo-paye-badge.png',
  cenefaHorizontal: '/images/documentacion/cenefa-horizontal.png',
  cenefaVertical: '/images/documentacion/cenefa-vertical.png',
  sparkleRed: '/images/documentacion/sparkle-red.svg',
} as const

export const DEFAULT_LEGAL_NOTICE_TITLE =
  'Propiedad Intelectual y Confidencialidad'

export const DEFAULT_LEGAL_NOTICE_TEXT =
  'El contenido de esta propuesta es propiedad exclusiva de Payé Digital © 2026. Queda prohibida la reproducción parcial o total, así como su distribución a terceros ajenos a la administración del Concello receptor sin autorización expresa. La información técnica y metodológica aquí descrita se presenta bajo un marco de confidencialidad estrictamente comercial.'

/**
 * Bloques predefinidos para PRESUPUESTOS (réplica fiel del PDF adjunto Alpe_Web.pdf)
 */
export const PRESUPUESTO_TEMPLATE_BLOCKS: readonly DocumentoBloqueDto[] = [
  {
    id: 'pres-intro',
    templateBlockId: 'pres-intro',
    tipoBloque: 'pill-section',
    numeroPastilla: '1.',
    titulo: 'INTRODUCCIÓN',
    contenidoHtml:
      '<p>La propuesta apoya la fuerza y el empuje que están cogiendo las redes sociales Instagram y Facebook, para crear una nueva línea de negocio: Ventas Online. A partir de aquí se creará una página web usando la plataforma Shopify además de incluir un servicio de impresión instantánea para los pedidos. Reduciendo el tiempo necesario para la creación de cada pedido.</p>',
    visible: true,
    orden: 1,
    esPersonalizado: false,
  },
  {
    id: 'pres-funcionamiento',
    templateBlockId: 'pres-funcionamiento',
    tipoBloque: 'pill-section',
    numeroPastilla: '2.',
    titulo: 'FUNCIONAMIENTO',
    contenidoHtml:
      '<p>La web será visible a través de Google pero su verdadero potencial es conectarla con las redes existentes para maximizar visibilidad, de esta forma cuando Paula publique una oferta o quiera dar a conocer un producto, en las redes sociales aparecerá un link directo a la web para comprarlo, una vez el cliente haya realizado el pago el servicio imprimirá el pedido junto con la etiqueta de envío.</p>',
    visible: true,
    orden: 2,
    esPersonalizado: false,
  },
  {
    id: 'pres-resumen-costes',
    templateBlockId: 'pres-resumen-costes',
    tipoBloque: 'highlight-cost-box',
    numeroPastilla: null,
    titulo: 'Resumen de costes.',
    contenidoHtml: `<ul>
      <li><strong>Web - Shopify</strong>
        <ul>
          <li>Creación y configuración inicial de Shopify</li>
          <li>Subida de 50 artículos</li>
          <li>Creación de imágenes publicitarias para la web</li>
          <li>Implementación de SEO en base al conocimiento recogido de las redes sociales.</li>
        </ul>
      </li>
      <li><strong>Impresión automática</strong>
        <ul>
          <li>Servicio web que imprime el pedido y la etiqueta de forma instantánea y automática</li>
        </ul>
      </li>
    </ul>`,
    visible: true,
    orden: 3,
    esPersonalizado: false,
  },
  {
    id: 'pres-beneficios',
    templateBlockId: 'pres-beneficios',
    tipoBloque: 'pill-section',
    numeroPastilla: '3.',
    titulo: 'Beneficios y oportunidades',
    contenidoHtml: `<p>Una tienda online hace que tus clientes desde la comodidad de su casa, sin tener que coger el coche, preguntar por WhatsApp o llamar, puedan adquirir productos que o bien ven en redes sociales o bien necesitan.</p>
    <p>Te permite ayudarte de las redes sociales, tu público entra a la web por ejemplo “al promocionar patatas a 8€”, pero también ven un saco de pienso para su gato o una camita para su perro. Al tener en un mismo lugar los productos más interesantes, los usuarios entran por un único producto y acaban comprando 2, 3 o incluso más.</p>
    <p>También te abre las puertas a hacer envíos nacionales de forma más estable, sencilla y dedicándole menos tiempo que recogiendo pedidos por Instagram o por WhatsApp.</p>
    <p>El alcance será tanto nacional como local, ya que la web tendrá disponible recogida en tienda o envío a través de Packlink.</p>`,
    visible: true,
    orden: 4,
    esPersonalizado: false,
  },
  {
    id: 'pres-consejos',
    templateBlockId: 'pres-consejos',
    tipoBloque: 'pill-section',
    numeroPastilla: '4.',
    titulo: 'Consejos',
    contenidoHtml: `<p>Lo ideal cuando la web esté funcionando con un volumen aceptable, sería dejar la tienda de “El Burgo” como almacén para los pedidos online.</p>
    <p>Esto evita que un cliente físico compre algo que otro pidió online y tengas que hacer una devolución que siempre es más tedioso.</p>
    <p>Hay que analizar y comentar la política de devoluciones, es algo que pasa mucho en Amazon y en grandes plataformas, el usuario hoy compra algo que o bien no le gusta, o se olvidó que lo compró o lo devuelve con pillería, hay que pensar cómo vas a gestionar esto ya que es complejo.</p>
    <p>Contratar Packlink Pro para envíos de hasta 10kg, esto agiliza muchísimo la propia gestión del pedido, ya que saldría junto con el albarán del pedido en la impresión.</p>
    <p>Tener cajas, burbujas, cartón, o algo preparado en algún rincón para empaquetar con agilidad los pedidos, cuanto más optimizados estén los procesos de empaque, mucho más rentable y menos caos causará hacer los envíos.</p>`,
    visible: true,
    orden: 5,
    esPersonalizado: false,
  },
  {
    id: 'pres-costes-timeline',
    templateBlockId: 'pres-costes-timeline',
    tipoBloque: 'timeline-steps',
    numeroPastilla: '5.',
    titulo: 'Costes',
    contenidoHtml: `<p class="doc-timeline-lead">En este proyecto hay varios costes, unos únicos y otros mensuales</p>
    <div class="doc-timeline-item">
      <div class="doc-timeline-badge">A</div>
      <div class="doc-timeline-body">
        <h4>Costes únicos</h4>
        <ul>
          <li>Creación y configuración de la web con Shopify</li>
          <li>Desarrollo del servicio para imprimir automáticamente pedido y etiqueta de envío</li>
          <li>Subida de 50 productos a la web</li>
          <li>Implementación de SEO</li>
          <li>Creación de imágenes para promocionar al inicio de la web
            <ul>
              <li><strong>Coste total: 2.600€ + IVA</strong></li>
            </ul>
          </li>
        </ul>
      </div>
    </div>
    <div class="doc-timeline-item">
      <div class="doc-timeline-badge">B</div>
      <div class="doc-timeline-body">
        <h4>Costes mensuales</h4>
        <ul>
          <li>El alojamiento, hosting, básicamente donde se almacena la web, y sus productos
            <ul>
              <li>Coste si se paga anual 24€ si se paga mensual 32€</li>
            </ul>
          </li>
          <li>Tarifa de pago por venta, una comisión que se lleva Shopify al efectuar los pagos dentro de la web.
            <ul>
              <li>2,1% + 0.3cent.
                <ul>
                  <li>Ejemplo 1: Si te hacen 20 pedidos con un total de 2.000€ Shopify se quedaría 48€.</li>
                </ul>
              </li>
            </ul>
          </li>
        </ul>
      </div>
    </div>
    <div class="doc-timeline-item">
      <div class="doc-timeline-badge">C</div>
      <div class="doc-timeline-body">
        <h4>Costes esporádicos</h4>
        <ul>
          <li>Cada temporada crear imágenes y subir productos nuevos o descatalogar productos ya subidos, aquí se incluye todo el mantenimiento de una web, solo que en vez de cobrar mes a mes, se cobra en función de lo realizado, cálculo que cada temporada serán 100€ más o menos.</li>
        </ul>
      </div>
    </div>`,
    visible: true,
    orden: 6,
    esPersonalizado: false,
  },
  {
    id: 'pres-pricing-table',
    templateBlockId: 'pres-pricing-table',
    tipoBloque: 'pricing-table',
    numeroPastilla: '6.',
    titulo: 'Desglose Económico e Impuestos',
    contenidoHtml:
      '<p>A continuación se detalla la valoración económica desglosada por conceptos, base imponible y tipos de IVA aplicables:</p>',
    visible: true,
    orden: 7,
    esPersonalizado: false,
  },
]

export const PRESUPUESTO_DEFAULT_LINES: readonly DocumentoLineaInputDto[] = [
  {
    descripcion: 'Creación y configuración inicial de tienda online Shopify + Diseño e-Commerce',
    cantidad: 1,
    precioUnitario: 1400,
    descuentoPorcentaje: 0,
    ivaPorcentaje: DEFAULT_LINE_VAT_RATE,
  },
  {
    descripcion: 'Desarrollo de servicio de impresión automática de pedidos y etiquetas de envío',
    cantidad: 1,
    precioUnitario: 750,
    descuentoPorcentaje: 0,
    ivaPorcentaje: DEFAULT_LINE_VAT_RATE,
  },
  {
    descripcion: 'Subida inicial de 50 artículos, creatividades publicitarias y configuración SEO',
    cantidad: 1,
    precioUnitario: 450,
    descuentoPorcentaje: 0,
    ivaPorcentaje: DEFAULT_LINE_VAT_RATE,
  },
]

/**
 * Bloques y cláusulas estándar predefinidas para CONTRATOS
 */
export const CONTRATO_TEMPLATE_BLOCKS: readonly DocumentoBloqueDto[] = [
  {
    id: 'cont-partes',
    templateBlockId: 'cont-partes',
    tipoBloque: 'contract-parties',
    numeroPastilla: null,
    titulo: 'REUNIDOS / PARTES CONTRATANTES',
    contenidoHtml:
      '<p>Ambas partes se reconocen mutua y recíprocamente la capacidad legal suficiente para suscribir el presente <strong>Contrato Mercantil de Prestación de Servicios Digitales</strong>, y a tal efecto exponen las siguientes estipulaciones:</p>',
    visible: true,
    orden: 1,
    esPersonalizado: false,
  },
  {
    id: 'cont-objeto',
    templateBlockId: 'cont-objeto',
    tipoBloque: 'pill-section',
    numeroPastilla: '1.',
    titulo: 'OBJETO DEL CONTRATO',
    contenidoHtml:
      '<p>El presente contrato tiene por objeto regular la prestación de servicios profesionales de desarrollo tecnológico, diseño digital y consultoría por parte del <strong>PRESTADOR</strong> a favor del <strong>CLIENTE</strong>, conforme al alcance técnico, entregables y especificaciones detalladas en este documento y su propuesta técnica asociada.</p>',
    visible: true,
    orden: 2,
    esPersonalizado: false,
  },
  {
    id: 'cont-duracion',
    templateBlockId: 'cont-duracion',
    tipoBloque: 'pill-section',
    numeroPastilla: '2.',
    titulo: 'DURACIÓN Y PLAZOS DE EJECUCIÓN',
    contenidoHtml:
      '<p>El presente contrato entrará en vigor en la fecha de su firma y tendrá la duración estipulada en las condiciones particulares para la ejecución y puesta en producción del proyecto. El cómputo de los plazos quedará supeditado a la entrega en tiempo y forma por parte del CLIENTE de los materiales, accesos y validaciones requeridas.</p>',
    visible: true,
    orden: 3,
    esPersonalizado: false,
  },
  {
    id: 'cont-precio-pago',
    templateBlockId: 'cont-precio-pago',
    tipoBloque: 'pill-section',
    numeroPastilla: '3.',
    titulo: 'PRECIO Y FORMA DE PAGO',
    contenidoHtml: `<p>Como contraprestación por los servicios objeto de este contrato, el CLIENTE abonará al PRESTADOR el importe económico acordado más los impuestos legalmente aplicables (IVA).</p>
    <ul>
      <li><strong>50% en concepto de anticipo</strong> a la firma del presente contrato e inicio de los trabajos.</li>
      <li><strong>50% restante</strong> a la finalización, validación técnica y entrega del proyecto antes de su pase a producción.</li>
    </ul>
    <p>Los pagos se efectuarán mediante transferencia bancaria a la cuenta IBAN indicada por el PRESTADOR en el encabezado o pie de este documento.</p>`,
    visible: true,
    orden: 4,
    esPersonalizado: false,
  },
  {
    id: 'cont-obligaciones',
    templateBlockId: 'cont-obligaciones',
    tipoBloque: 'timeline-steps',
    numeroPastilla: '4.',
    titulo: 'FASES Y OBLIGACIONES DE LAS PARTES',
    contenidoHtml: `<p class="doc-timeline-lead">El desarrollo y cumplimiento del contrato se articula en tres compromisos fundamentales:</p>
    <div class="doc-timeline-item">
      <div class="doc-timeline-badge">A</div>
      <div class="doc-timeline-body">
        <h4>Ejecución Técnica y Calidad</h4>
        <ul>
          <li>El PRESTADOR ejecutará los trabajos con arreglo a las mejores prácticas del sector tecnológico, aportando los medios técnicos y humanos necesarios.</li>
        </ul>
      </div>
    </div>
    <div class="doc-timeline-item">
      <div class="doc-timeline-badge">B</div>
      <div class="doc-timeline-body">
        <h4>Colaboración y Entrega de Materiales</h4>
        <ul>
          <li>El CLIENTE facilitará los textos, logotipos, catálogo de productos, credenciales necesarias y designará un interlocutor válido para las revisiones.</li>
        </ul>
      </div>
    </div>
    <div class="doc-timeline-item">
      <div class="doc-timeline-badge">C</div>
      <div class="doc-timeline-body">
        <h4>Garantía y Soporte Post-Lanzamiento</h4>
        <ul>
          <li>Una vez entregado el proyecto, se incluye un período de garantía técnica de 30 días naturales para la subsanación de cualquier incidencia imputable al desarrollo inicial.</li>
        </ul>
      </div>
    </div>`,
    visible: true,
    orden: 5,
    esPersonalizado: false,
  },
  {
    id: 'cont-propiedad-intelectual',
    templateBlockId: 'cont-propiedad-intelectual',
    tipoBloque: 'pill-section',
    numeroPastilla: '5.',
    titulo: 'PROPIEDAD INTELECTUAL Y CONFIDENCIALIDAD',
    contenidoHtml: `<p>Una vez satisfecho el pago íntegro del precio pactado, el CLIENTE adquirirá los derechos de explotación sobre los desarrollos y diseños realizados específicamente a medida para su proyecto. El PRESTADOR conservará la titularidad sobre sus librerías previas, metodologías y herramientas de uso general.</p>
    <p>Ambas partes se comprometen a mantener la más estricta confidencialidad respecto a toda la información comercial, técnica, estratégica o de negocio a la que tengan acceso con motivo del presente contrato.</p>`,
    visible: true,
    orden: 6,
    esPersonalizado: false,
  },
  {
    id: 'cont-rgpd',
    templateBlockId: 'cont-rgpd',
    tipoBloque: 'pill-section',
    numeroPastilla: '6.',
    titulo: 'PROTECCIÓN DE DATOS Y JURISDICCIÓN',
    contenidoHtml: `<p>En cumplimiento del Reglamento (UE) 2016/679 (RGPD) y la LOPDGDD 3/2018, los datos personales de los firmantes serán tratados con la única finalidad de gestionar la relación contractual y facturación derivada del presente acuerdo.</p>
    <p>Para la resolución de cualquier controversia que pudiera derivarse de la interpretación o cumplimiento de este contrato, las partes se someten expresamente a los Juzgados y Tribunales del domicilio del PRESTADOR, con renuncia a cualquier otro fuero que pudiera corresponderles.</p>`,
    visible: true,
    orden: 7,
    esPersonalizado: false,
  },
  {
    id: 'cont-firmas',
    templateBlockId: 'cont-firmas',
    tipoBloque: 'signatures',
    numeroPastilla: null,
    titulo: 'FIRMA Y CONFORMIDAD DE LAS PARTES',
    contenidoHtml:
      '<p>Y en prueba de conformidad con cuanto antecede, ambas partes firman el presente contrato por duplicado y a un solo efecto en el lugar y fecha indicados.</p>',
    visible: true,
    orden: 8,
    esPersonalizado: false,
  },
]

export function createInitialBlocksForType(tipo: TipoDocumentoComercial): DocumentoBloqueDto[] {
  const source = tipo === 1 ? CONTRATO_TEMPLATE_BLOCKS : PRESUPUESTO_TEMPLATE_BLOCKS
  return source.map((b, idx) => ({
    ...b,
    orden: idx + 1,
  }))
}

export function getOriginalTemplateBlock(
  tipo: TipoDocumentoComercial,
  templateBlockId?: string | null
): DocumentoBloqueDto | undefined {
  if (!templateBlockId) return undefined
  const source = tipo === 1 ? CONTRATO_TEMPLATE_BLOCKS : PRESUPUESTO_TEMPLATE_BLOCKS
  return source.find((b) => b.templateBlockId === templateBlockId)
}
