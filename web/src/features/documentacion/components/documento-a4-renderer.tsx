import React, { useMemo } from 'react'
import type {
  CalculatedDocumentoTotals,
  DocumentacionCompanySettingsDto,
  DocumentoBloqueDto,
  DocumentoLineaInputDto,
  TipoDocumentoComercial,
} from '@/api/types'
import { DOCUMENTACION_ASSETS } from '../config/default-templates'
import { sanitizeDocumentoHtml } from '../utils/html-sanitizer'
import { calculateDocumentoTotals } from '../utils/totals-calculator'

export interface DocumentoA4LayoutOptions {
  /** Mostrar barra superior con datos de empresa y referencia en páginas interiores (por defecto: false, igual al PDF original) */
  mostrarCabeceraEmpresa?: boolean
  /** Mostrar código correlativo (PRES-2026-001) en la parte superior de la portada negra (por defecto: false, igual al PDF original) */
  mostrarCodigoEnPortada?: boolean
  /** Mostrar cenefa étnica horizontal en la portada negra (por defecto: false) */
  mostrarCenefaPortada?: boolean
  /** Mostrar dirección e IBAN debajo del texto legal en el pie de página (por defecto: false) */
  mostrarDatosFiscalesPie?: boolean
  /** Mantener cada bloque completo en una misma página siempre que quepa en una hoja A4 (por defecto: true) */
  evitarPartirBloques?: boolean
}

export interface DocumentoA4Data extends DocumentoA4LayoutOptions {
  tipo: TipoDocumentoComercial
  numeroDocumento: string
  titulo: string
  subtitulo?: string
  categoria?: string
  clienteNombre: string
  clienteCifNif: string
  clienteDireccion: string
  clienteEmail?: string
  clienteTelefono?: string
  fechaEmision: string
  fechaValidez?: string | null
  objetoContrato?: string | null
  duracionContrato?: string | null
  precioTextoContrato?: string | null
  formaPagoContrato?: string | null
  lugarFirma?: string | null
  bloques: DocumentoBloqueDto[]
  lineas: DocumentoLineaInputDto[]
}

export interface DocumentoA4RendererProps {
  document: DocumentoA4Data
  company: DocumentacionCompanySettingsDto
  calculatedTotals?: CalculatedDocumentoTotals
  interactive?: boolean
  selectedBlockId?: string | null
  onSelectBlock?: (blockId: string) => void
}

interface RenderableBlockSlice {
  sliceKey: string
  sourceBlockId: string
  block: DocumentoBloqueDto
  lineSlice?: CalculatedDocumentoTotals['lineas']
  showPricingSummary?: boolean
  isContinuation?: boolean
}

const currencyFormatter = new Intl.NumberFormat('es-ES', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

export function formatCurrencyEs(amount: number): string {
  return `${currencyFormatter.format(amount)} €`
}

export function formatDateEs(isoDate?: string | null): string {
  if (!isoDate) return '—'
  const parts = isoDate.slice(0, 10).split('-')
  if (parts.length !== 3) return isoDate
  return `${parts[2]}/${parts[1]}/${parts[0]}`
}

interface HtmlRenderUnit {
  listWrapperTag?: 'ul' | 'ol'
  listGroupId?: number
  parentItemGroupId?: number
  olStartIndex?: number
  subListTag?: 'ul' | 'ol'
  subOlStartIndex?: number
  leadHtml?: string
  itemHtml: string
  heightMm: number
}

const LINE_HEIGHT_MM = 5.85
const PILL_HEADING_HEIGHT_MM = 13.5
const BLOCK_GAP_MM = 4.5

function estimateTextHeightMm(text: string, charsPerLine: number, extraMarginMm = 1.2): number {
  const clean = text.replace(/\s+/g, ' ').trim()
  if (!clean) return 0
  const lines = Math.max(1, Math.ceil(clean.length / charsPerLine))
  return lines * LINE_HEIGHT_MM + extraMarginMm
}

function pruneEmptyDomNodes(root: HTMLElement): void {
  const candidates = Array.from(
    root.querySelectorAll('li, p, ul, ol, span, strong, b, em, i, div')
  ).reverse()

  for (const el of candidates) {
    if (el.querySelector('img, table, hr')) continue
    const txt = (el.textContent ?? '').replace(/\u00a0/g, ' ').trim()
    if (txt.length === 0) {
      el.remove()
    }
  }
}

function decomposeHtmlIntoUnits(rawHtml?: string | null): HtmlRenderUnit[] {
  const safeHtml = sanitizeDocumentoHtml(rawHtml)
  if (!safeHtml.trim()) return []

  if (typeof document === 'undefined') {
    const plain = safeHtml.replace(/<[^>]+>/g, ' ').trim()
    return [
      {
        itemHtml: safeHtml,
        heightMm: estimateTextHeightMm(plain, 74, 2),
      },
    ]
  }

  const container = document.createElement('div')
  container.innerHTML = safeHtml
  pruneEmptyDomNodes(container)

  const units: HtmlRenderUnit[] = []
  let listGroupCounter = 0
  let parentItemCounter = 0

  const topNodes = Array.from(container.childNodes)
  for (const node of topNodes) {
    if (node.nodeType === Node.TEXT_NODE) {
      const txt = (node.textContent ?? '').trim()
      if (txt.length > 0) {
        const p = document.createElement('p')
        p.textContent = txt
        units.push({
          itemHtml: p.outerHTML,
          heightMm: estimateTextHeightMm(txt, 75, 1.6),
        })
      }
      continue
    }

    if (node.nodeType !== Node.ELEMENT_NODE) continue
    const el = node as HTMLElement
    const tag = el.tagName.toLowerCase()

    if (tag === 'ul' || tag === 'ol') {
      listGroupCounter++
      const currentListGroupId = listGroupCounter
      const listTag = tag as 'ul' | 'ol'
      const liChildren = Array.from(el.children).filter(
        (c) => c.tagName.toLowerCase() === 'li'
      ) as HTMLElement[]

      liChildren.forEach((li, liIdx) => {
        const nestedList = Array.from(li.children).find((c) => {
          const t = c.tagName.toLowerCase()
          return t === 'ul' || t === 'ol'
        }) as HTMLElement | undefined

        const totalLiHeight = estimateTextHeightMm(li.textContent ?? '', 70, 1.5)

        // Si un <li> contiene una sub-lista larga (ej. Fase B con múltiples módulos),
        // descomponer cada sub-punto para poder paginar con precisión milimétrica sin dejar huecos ni viñetas vacías
        if (nestedList && totalLiHeight > 28) {
          parentItemCounter++
          const currentParentId = parentItemCounter
          const subTag = nestedList.tagName.toLowerCase() as 'ul' | 'ol'
          const subLis = Array.from(nestedList.children).filter(
            (c) => c.tagName.toLowerCase() === 'li'
          ) as HTMLElement[]

          const leadClone = li.cloneNode(true) as HTMLElement
          for (const child of Array.from(leadClone.children)) {
            const ct = child.tagName.toLowerCase()
            if (ct === 'ul' || ct === 'ol') child.remove()
          }
          const leadHtml = leadClone.innerHTML.trim()
          const leadText = (leadClone.textContent ?? '').trim()

          if (subLis.length > 0) {
            subLis.forEach((subLi, subIdx) => {
              const subText = (subLi.textContent ?? '').trim()
              const subHeight =
                estimateTextHeightMm(subText, 66, 1.1) +
                (subIdx === 0 ? estimateTextHeightMm(leadText, 71, 1.2) : 0)

              units.push({
                listWrapperTag: listTag,
                listGroupId: currentListGroupId,
                parentItemGroupId: currentParentId,
                olStartIndex: liIdx + 1,
                subListTag: subTag,
                subOlStartIndex: subIdx + 1,
                leadHtml: subIdx === 0 ? leadHtml : undefined,
                itemHtml: subLi.outerHTML,
                heightMm: subHeight,
              })
            })
            return
          }
        }

        units.push({
          listWrapperTag: listTag,
          listGroupId: currentListGroupId,
          olStartIndex: liIdx + 1,
          itemHtml: li.outerHTML,
          heightMm: totalLiHeight,
        })
      })
      continue
    }

    units.push({
      itemHtml: el.outerHTML,
      heightMm: estimateTextHeightMm(el.textContent ?? '', 75, 1.8),
    })
  }

  return units
}

function serializeUnitsToValidHtml(units: HtmlRenderUnit[]): string {
  if (units.length === 0) return ''

  let html = ''
  let i = 0

  while (i < units.length) {
    const u = units[i]
    if (!u.listWrapperTag) {
      html += u.itemHtml
      i++
      continue
    }

    // Agrupar unidades consecutivas de la misma lista principal (<ul> o <ol>)
    const listTag = u.listWrapperTag
    const groupId = u.listGroupId
    const startAttr =
      listTag === 'ol' && u.olStartIndex && u.olStartIndex > 1
        ? ` start="${u.olStartIndex}"`
        : ''

    html += `<${listTag}${startAttr}>`

    while (
      i < units.length &&
      units[i].listWrapperTag === listTag &&
      units[i].listGroupId === groupId
    ) {
      const itemUnit = units[i]
      if (itemUnit.parentItemGroupId !== undefined && itemUnit.subListTag) {
        const parentId = itemUnit.parentItemGroupId
        const subTag = itemUnit.subListTag
        const firstSubStart = itemUnit.subOlStartIndex ?? 1
        const subStartAttr =
          subTag === 'ol' && firstSubStart > 1 ? ` start="${firstSubStart}"` : ''
        const hasLead = Boolean(itemUnit.leadHtml && itemUnit.leadHtml.trim().length > 0)
        const liClassAttr = hasLead ? '' : ' class="doc-li-continuation"'

        html += `<li${liClassAttr}>`
        if (hasLead) {
          html += itemUnit.leadHtml
        }
        html += `<${subTag}${subStartAttr}>`

        while (
          i < units.length &&
          units[i].listWrapperTag === listTag &&
          units[i].listGroupId === groupId &&
          units[i].parentItemGroupId === parentId
        ) {
          html += units[i].itemHtml
          i++
        }

        html += `</${subTag}></li>`
      } else {
        html += itemUnit.itemHtml
        i++
      }
    }

    html += `</${listTag}>`
  }

  return html
}

function estimateAtomicBlockHeightMm(
  block: DocumentoBloqueDto,
  lineCount: number,
  showPricingSummary: boolean
): number {
  switch (block.tipoBloque) {
    case 'highlight-cost-box': {
      const plain = (block.contenidoHtml || '').replace(/<[^>]+>/g, ' ')
      return 26 + estimateTextHeightMm(plain, 62, 4)
    }
    case 'timeline-steps': {
      const plain = (block.contenidoHtml || '').replace(/<[^>]+>/g, ' ')
      return PILL_HEADING_HEIGHT_MM + 42 + estimateTextHeightMm(plain, 64, 6)
    }
    case 'pricing-table': {
      const headerMm = PILL_HEADING_HEIGHT_MM + 12
      const rowsMm = lineCount * 9.5
      const summaryMm = showPricingSummary ? 42 : 0
      return headerMm + rowsMm + summaryMm
    }
    case 'contract-parties':
      return 66
    case 'signatures':
      return 62
    default: {
      const units = decomposeHtmlIntoUnits(block.contenidoHtml)
      const bodyMm = units.reduce((acc, u) => acc + u.heightMm, 0)
      return PILL_HEADING_HEIGHT_MM + bodyMm
    }
  }
}

/**
 * Pagina los bloques directamente en hojas interiores A4 usando medidas reales en milímetros (mm):
 * - Rellena cada página A4 con precisión hasta el margen inferior útil (~226mm) sin dejar grandes huecos en blanco.
 * - Si evitarPartirBloques está activo (por defecto true), mantiene cada bloque completo en una misma página
 *   pasándolo íntegro a la página siguiente si no cabe en el hueco restante, salvo que un único bloque supere por sí solo una hoja A4.
 * - Solo divide un bloque con "(Cont.)" cuando realmente salta a la página siguiente (jamás en la misma hoja).
 * - Nunca corta listas dejando viñetas vacías ("•" u "o") ni pierde la viñeta del primer punto de la página siguiente.
 */
export function paginateBlocksToA4Pages(
  visibleBlocks: DocumentoBloqueDto[],
  totals: CalculatedDocumentoTotals,
  hasPageHeader: boolean,
  hasFooterLegal: boolean,
  evitarPartirBloques = true
): RenderableBlockSlice[][] {
  if (visibleBlocks.length === 0) return [[]]

  const MAX_PAGE_MM = hasPageHeader ? 214 : 226
  const SINGLE_BLOCK_FIT_TOLERANCE_MM = evitarPartirBloques ? 22 : 0
  const pages: RenderableBlockSlice[][] = []
  let currentPage: RenderableBlockSlice[] = []
  let currentUsedMm = 0

  const pushCurrentPage = () => {
    if (currentPage.length > 0) {
      pages.push(currentPage)
      currentPage = []
      currentUsedMm = 0
    }
  }

  for (const block of visibleBlocks) {
    // 1. Tabla económica de líneas e impuestos
    if (block.tipoBloque === 'pricing-table') {
      const allLines = totals.lineas
      let lineCursor = 0
      let partIdx = 0

      if (allLines.length === 0) {
        const emptyTableMm = estimateAtomicBlockHeightMm(block, 1, true)
        const gapMm = currentPage.length > 0 ? BLOCK_GAP_MM : 0
        if (currentPage.length > 0 && currentUsedMm + gapMm + emptyTableMm > MAX_PAGE_MM) {
          pushCurrentPage()
        }
        currentPage.push({
          sliceKey: block.id,
          sourceBlockId: block.id,
          block,
          lineSlice: [],
          showPricingSummary: true,
          isContinuation: false,
        })
        currentUsedMm += (currentPage.length > 1 ? BLOCK_GAP_MM : 0) + emptyTableMm
        continue
      }

      const fullTableTotalMm = PILL_HEADING_HEIGHT_MM + 12 + allLines.length * 9.5 + 42
      if (
        evitarPartirBloques &&
        currentPage.length > 0 &&
        currentUsedMm + BLOCK_GAP_MM + fullTableTotalMm > MAX_PAGE_MM
      ) {
        pushCurrentPage()
      }

      while (lineCursor < allLines.length) {
        const gapMm = currentPage.length > 0 ? BLOCK_GAP_MM : 0
        const availMm = MAX_PAGE_MM - currentUsedMm - gapMm
        const minTableSliceMm = PILL_HEADING_HEIGHT_MM + 12 + 2 * 9.5

        if (currentPage.length > 0 && availMm < minTableSliceMm) {
          pushCurrentPage()
          continue
        }

        const effectiveAvailMm =
          MAX_PAGE_MM - currentUsedMm - (currentPage.length > 0 ? BLOCK_GAP_MM : 0)
        const remainingLines = allLines.length - lineCursor
        const fullRemainingMm =
          PILL_HEADING_HEIGHT_MM + 12 + remainingLines * 9.5 + 42

        if (
          fullRemainingMm <=
          effectiveAvailMm + (currentPage.length === 0 ? SINGLE_BLOCK_FIT_TOLERANCE_MM : 0)
        ) {
          const chunk = allLines.slice(lineCursor)
          const isCont = partIdx > 0
          currentPage.push({
            sliceKey: isCont ? `${block.id}-part-${partIdx}` : block.id,
            sourceBlockId: block.id,
            block: isCont
              ? { ...block, titulo: `${block.titulo} (Cont.)`, contenidoHtml: '' }
              : block,
            lineSlice: chunk,
            showPricingSummary: true,
            isContinuation: isCont,
          })
          currentUsedMm +=
            (currentPage.length > 1 ? BLOCK_GAP_MM : 0) + Math.min(fullRemainingMm, MAX_PAGE_MM)
          break
        }

        // Empaquetar tantas filas como quepan en la hoja actual y continuar el resto en la hoja siguiente
        const maxRowsFit = Math.max(
          1,
          Math.floor((effectiveAvailMm - PILL_HEADING_HEIGHT_MM - 12) / 9.5)
        )
        const rowsToTake = Math.min(remainingLines, maxRowsFit)
        const isLastChunk = lineCursor + rowsToTake >= allLines.length
        const summaryFitsToo =
          isLastChunk &&
          PILL_HEADING_HEIGHT_MM + 12 + rowsToTake * 9.5 + 42 <= effectiveAvailMm

        const actualRowsToTake =
          isLastChunk && !summaryFitsToo && rowsToTake > 1
            ? rowsToTake - 1
            : rowsToTake

        const chunk = allLines.slice(lineCursor, lineCursor + actualRowsToTake)
        const willFinish =
          lineCursor + actualRowsToTake >= allLines.length && summaryFitsToo
        const isCont = partIdx > 0

        currentPage.push({
          sliceKey: isCont ? `${block.id}-part-${partIdx}` : block.id,
          sourceBlockId: block.id,
          block: isCont
            ? { ...block, titulo: `${block.titulo} (Cont.)`, contenidoHtml: '' }
            : block,
          lineSlice: chunk,
          showPricingSummary: willFinish,
          isContinuation: isCont,
        })

        lineCursor += actualRowsToTake
        partIdx++
        pushCurrentPage()
      }
      continue
    }

    // 2. Bloques de texto y listas con pastilla ámbar ('pill-section')
    if (block.tipoBloque === 'pill-section') {
      const units = decomposeHtmlIntoUnits(block.contenidoHtml)
      if (units.length === 0) {
        continue
      }

      const totalUnitsMm = units.reduce((acc, u) => acc + u.heightMm, 0)
      const totalBlockMm = PILL_HEADING_HEIGHT_MM + totalUnitsMm
      const cleanBaseTitle = block.titulo.replace(/\s*\(Cont\.\)\s*$/i, '').trim()

      // Si evitarPartirBloques está activo y el bloque no cabe entero en el hueco restante de la hoja actual,
      // cerramos la hoja actual para que el bloque comience íntegro en una página nueva.
      if (
        evitarPartirBloques &&
        currentPage.length > 0 &&
        currentUsedMm + BLOCK_GAP_MM + totalBlockMm > MAX_PAGE_MM
      ) {
        pushCurrentPage()
      }

      // Si estamos al inicio de una hoja y el bloque completo cabe en una sola página (con margen de tolerancia),
      // lo mantenemos íntegro en esta misma página sin partirlo con "(Cont.)"
      if (
        evitarPartirBloques &&
        currentPage.length === 0 &&
        totalBlockMm <= MAX_PAGE_MM + SINGLE_BLOCK_FIT_TOLERANCE_MM
      ) {
        const fullHtml = serializeUnitsToValidHtml(units)
        currentPage.push({
          sliceKey: block.id,
          sourceBlockId: block.id,
          block: {
            ...block,
            titulo: cleanBaseTitle,
            contenidoHtml: fullHtml,
          },
          isContinuation: false,
        })
        currentUsedMm += Math.min(totalBlockMm, MAX_PAGE_MM)
        continue
      }

      let unitCursor = 0
      let partIdx = 0

      while (unitCursor < units.length) {
        const gapMm = currentPage.length > 0 ? BLOCK_GAP_MM : 0
        const availMm = MAX_PAGE_MM - currentUsedMm - gapMm
        const firstUnitMm = units[unitCursor].heightMm

        // Si en el hueco final de la hoja actual no cabe ni el título + el primer párrafo/punto,
        // pasamos limpiamente a la hoja siguiente antes de empezar el bloque
        if (
          currentPage.length > 0 &&
          availMm < PILL_HEADING_HEIGHT_MM + firstUnitMm + 2
        ) {
          pushCurrentPage()
          continue
        }

        const effectiveGapMm = currentPage.length > 0 ? BLOCK_GAP_MM : 0
        const maxBodyMm =
          MAX_PAGE_MM - currentUsedMm - effectiveGapMm - PILL_HEADING_HEIGHT_MM

        const fittedUnits: HtmlRenderUnit[] = []
        let accumulatedBodyMm = 0

        while (unitCursor < units.length) {
          const nextUnit = units[unitCursor]
          if (
            fittedUnits.length > 0 &&
            accumulatedBodyMm + nextUnit.heightMm > maxBodyMm
          ) {
            break
          }
          fittedUnits.push(nextUnit)
          accumulatedBodyMm += nextUnit.heightMm
          unitCursor++
        }

        const sliceHtml = serializeUnitsToValidHtml(fittedUnits)
        const isCont = partIdx > 0

        currentPage.push({
          sliceKey: isCont ? `${block.id}-part-${partIdx}` : block.id,
          sourceBlockId: block.id,
          block: {
            ...block,
            titulo: isCont ? `${cleanBaseTitle} (Cont.)` : cleanBaseTitle,
            contenidoHtml: sliceHtml,
          },
          isContinuation: isCont,
        })

        currentUsedMm +=
          effectiveGapMm + PILL_HEADING_HEIGHT_MM + accumulatedBodyMm
        partIdx++

        // Si aún quedan puntos/párrafos de este bloque, cerramos la hoja actual para que "(Cont.)"
        // empiece SIEMPRE en la página siguiente y nunca en la misma hoja
        if (unitCursor < units.length) {
          pushCurrentPage()
        }
      }
      continue
    }

    // 3. Resto de bloques atómicos (highlight-cost-box, timeline-steps, contract-parties, signatures)
    const atomicMm = estimateAtomicBlockHeightMm(block, 0, false)
    const gapMm = currentPage.length > 0 ? BLOCK_GAP_MM : 0
    if (currentPage.length > 0 && currentUsedMm + gapMm + atomicMm > MAX_PAGE_MM) {
      pushCurrentPage()
    }

    currentPage.push({
      sliceKey: block.id,
      sourceBlockId: block.id,
      block,
      isContinuation: false,
    })
    currentUsedMm += (currentPage.length > 1 ? BLOCK_GAP_MM : 0) + atomicMm
  }

  pushCurrentPage()

  // Reservar espacio en la última página para el bloque legal de Propiedad Intelectual
  if (hasFooterLegal && pages.length > 0) {
    const lastPage = pages[pages.length - 1]
    const lastPageHeightMm = lastPage.reduce((acc, slice, idx) => {
      const h = slice.lineSlice
        ? PILL_HEADING_HEIGHT_MM +
          12 +
          slice.lineSlice.length * 9.5 +
          (slice.showPricingSummary ? 42 : 0)
        : slice.block.tipoBloque === 'pill-section'
        ? PILL_HEADING_HEIGHT_MM +
          decomposeHtmlIntoUnits(slice.block.contenidoHtml).reduce(
            (a, u) => a + u.heightMm,
            0
          )
        : estimateAtomicBlockHeightMm(slice.block, 0, false)
      return acc + (idx > 0 ? BLOCK_GAP_MM : 0) + h
    }, 0)

    if (lastPage.length > 1 && lastPageHeightMm > MAX_PAGE_MM - 28) {
      const moved = lastPage.pop()!
      pages.push([moved])
    }
  }

  return pages.length > 0 ? pages : [[]]
}

export const DocumentoA4Renderer: React.FC<DocumentoA4RendererProps> = ({
  document,
  company,
  calculatedTotals,
  interactive = false,
  selectedBlockId,
  onSelectBlock,
}) => {
  const totals = useMemo(
    () => calculatedTotals ?? calculateDocumentoTotals(document.lineas),
    [calculatedTotals, document.lineas]
  )

  const visibleBlocks = useMemo(
    () =>
      [...document.bloques]
        .filter((b) => b.visible)
        .sort((a, b) => a.orden - b.orden),
    [document.bloques]
  )

  const interiorPages = useMemo(
    () =>
      paginateBlocksToA4Pages(
        visibleBlocks,
        totals,
        Boolean(document.mostrarCabeceraEmpresa),
        Boolean(company.textoLegalTitulo || company.textoLegalContenido),
        document.evitarPartirBloques ?? true
      ),
    [
      visibleBlocks,
      totals,
      document.mostrarCabeceraEmpresa,
      company.textoLegalTitulo,
      company.textoLegalContenido,
      document.evitarPartirBloques,
    ]
  )

  const effectiveLogo =
    company.logoUrl &&
    company.logoUrl.trim().length > 0 &&
    company.logoUrl !== '/images/documentacion/logo-paye-dark.png'
      ? company.logoUrl
      : DOCUMENTACION_ASSETS.logoHorizontalDark

  const isContrato = document.tipo === 1
  const coverKindLabel = isContrato ? 'CONTRATO' : 'PROPUESTA'

  return (
    <div className='doc-a4-root doc-a4-preview-stack'>
      {/* ====================================================================
          HOJA 1: PORTADA A4 (FONDO NEGRO #000000 — IDÉNTICA A PÁGINA 1 DEL PDF)
          ==================================================================== */}
      <section
        id='doc-a4-page-1'
        data-page-number='1'
        className='doc-a4-sheet doc-a4-cover'
        aria-label='Portada A4'
      >
        <div className='doc-cover-top-spacer'>
          {document.mostrarCenefaPortada ? (
            <img
              src={DOCUMENTACION_ASSETS.cenefaHorizontal}
              alt='Cenefa corporativa Payé Digital'
              className='doc-cover-cenefa-top'
            />
          ) : document.mostrarCodigoEnPortada ? (
            <span className='doc-cover-doc-number'>{document.numeroDocumento}</span>
          ) : null}
        </div>

        <div className='doc-cover-center'>
          <h1 className='doc-cover-kind'>{coverKindLabel}</h1>
          <h2 className='doc-cover-client'>{document.clienteNombre || 'Cliente'}</h2>
          <p className='doc-cover-title'>
            {document.titulo ||
              (isContrato
                ? 'Prestación de servicios digitales'
                : 'Web para aumentar venta online')}
          </p>
          {(document.categoria || document.subtitulo) && (
            <p className='doc-cover-category'>{document.categoria || document.subtitulo}</p>
          )}
        </div>

        <div className='doc-cover-footer'>
          <img
            src={effectiveLogo}
            alt={company.nombreEmpresa || 'Payé Digital'}
            className='doc-cover-logo'
          />
        </div>
      </section>

      {/* ====================================================================
          HOJAS INTERIORES A4 (NUMERADAS 1..N COMO EN EL PDF ADJUNTO)
          ==================================================================== */}
      {interiorPages.map((pageSlices, pageIndex) => {
        const interiorPageNumber = pageIndex + 1
        const totalSheetNumber = pageIndex + 2
        const isLastInteriorPage = pageIndex === interiorPages.length - 1

        return (
          <section
            key={`interior-page-${interiorPageNumber}`}
            id={`doc-a4-page-${totalSheetNumber}`}
            data-page-number={String(totalSheetNumber)}
            className='doc-a4-sheet doc-a4-interior'
            aria-label={`Página ${interiorPageNumber}`}
          >
            {/* Cabecera opcional (desactivada por defecto para igualar el PDF original) */}
            {document.mostrarCabeceraEmpresa && (
              <header className='doc-page-header'>
                <div className='doc-page-header-issuer'>
                  <span>{company.nombreEmpresa || 'Payé Digital'}</span>
                  {company.cifNif && <span>· CIF/NIF: {company.cifNif}</span>}
                  {company.telefono && <span>· Tel: {company.telefono}</span>}
                  {company.email && <span>· {company.email}</span>}
                  {company.web && <span>· {company.web}</span>}
                </div>
                <div className='doc-page-header-meta'>
                  <span>{document.numeroDocumento}</span>
                  <span>· {formatDateEs(document.fechaEmision)}</span>
                  {!isContrato && document.fechaValidez && (
                    <span>· Validez: {formatDateEs(document.fechaValidez)}</span>
                  )}
                </div>
              </header>
            )}

            {/* Contenido de bloques de la página */}
            <div className='doc-page-content'>
              {pageSlices.map((slice) => {
                const isSelected = interactive && selectedBlockId === slice.sourceBlockId
                const interactiveClasses = interactive
                  ? `cursor-pointer rounded-xl transition-all ${
                      isSelected
                        ? 'ring-2 ring-amber-500 ring-offset-4'
                        : 'hover:ring-1 hover:ring-amber-400/70 hover:ring-offset-2'
                    }`
                  : ''

                return (
                  <div
                    key={slice.sliceKey}
                    className={`doc-block-wrapper ${interactiveClasses}`}
                    onClick={
                      interactive && onSelectBlock
                        ? (e) => {
                            e.stopPropagation()
                            onSelectBlock(slice.sourceBlockId)
                          }
                        : undefined
                    }
                  >
                    {renderSliceContent(slice, document, company, totals)}
                  </div>
                )
              })}
            </div>

            {/* Pie de página A4: sello del logo en cada página + bloque legal en la última + número de página */}
            <footer className='doc-a4-footer'>
              <div className='doc-footer-logo-pill'>
                <img
                  src={effectiveLogo}
                  alt={company.nombreEmpresa || 'Payé Digital'}
                  className='doc-footer-logo-img'
                />
              </div>

              {isLastInteriorPage &&
                (company.textoLegalTitulo || company.textoLegalContenido) && (
                  <div className='doc-footer-legal'>
                    {company.textoLegalTitulo && (
                      <h5 className='doc-footer-legal-title'>{company.textoLegalTitulo}</h5>
                    )}
                    {company.textoLegalContenido && (
                      <p className='doc-footer-legal-text'>{company.textoLegalContenido}</p>
                    )}
                  </div>
                )}

              {document.mostrarDatosFiscalesPie && (company.direccion || company.iban) && (
                <div className='doc-footer-company-contact'>
                  {company.direccion && <span>{company.direccion}</span>}
                  {company.iban && <span>· IBAN: {company.iban}</span>}
                </div>
              )}

              <div className='doc-footer-page-number'>{interiorPageNumber}</div>
            </footer>
          </section>
        )
      })}
    </div>
  )
}

function renderSliceContent(
  slice: RenderableBlockSlice,
  document: DocumentoA4Data,
  company: DocumentacionCompanySettingsDto,
  totals: CalculatedDocumentoTotals
): React.ReactNode {
  const { block, lineSlice, showPricingSummary } = slice
  const headingLabel = [block.numeroPastilla, block.titulo]
    .filter((s) => s && s.trim().length > 0)
    .join(' ')
  const safeHtml = sanitizeDocumentoHtml(block.contenidoHtml)

  switch (block.tipoBloque) {
    case 'highlight-cost-box':
      return (
        <div className='doc-highlight-cost-box'>
          <img
            src={DOCUMENTACION_ASSETS.sparkleRed}
            alt=''
            aria-hidden='true'
            className='doc-highlight-sparkle'
          />
          {block.titulo && <h3 className='doc-highlight-cost-title'>{block.titulo}</h3>}
          <div
            className='doc-section-body'
            dangerouslySetInnerHTML={{ __html: safeHtml }}
          />
          {totals.baseImponible > 0 && (
            <div className='doc-highlight-cost-total-line'>
              <span>
                • Total:{' '}
                {Number.isInteger(totals.baseImponible)
                  ? totals.baseImponible.toLocaleString('es-ES')
                  : currencyFormatter.format(totals.baseImponible)}{' '}
                + IVA
              </span>
            </div>
          )}
        </div>
      )

    case 'timeline-steps':
      return (
        <div>
          {headingLabel && <h3 className='doc-pill-heading'>{headingLabel}</h3>}
          <div
            className='doc-section-body doc-timeline-container'
            dangerouslySetInnerHTML={{ __html: safeHtml }}
          />
        </div>
      )

    case 'pricing-table': {
      const rows = lineSlice ?? totals.lineas
      const allLines = totals.lineas
      const hasAnyDiscount =
        allLines.some((l) => l.descuentoPorcentaje > 0) || totals.totalDescuento > 0
      const showLineTotalCol = hasAnyDiscount

      return (
        <div className='doc-pricing-table-wrap'>
          {headingLabel && <h3 className='doc-pill-heading'>{headingLabel}</h3>}
          {safeHtml && (
            <div
              className='doc-section-body'
              dangerouslySetInnerHTML={{ __html: safeHtml }}
            />
          )}

          <div className='doc-pricing-table-frame'>
            <table className='doc-pricing-table'>
              <thead>
                <tr>
                  <th className='doc-col-desc'>Concepto / Descripción</th>
                  <th className='num doc-col-price'>Precio Ud.</th>
                  {hasAnyDiscount && <th className='num doc-col-narrow'>Dto.</th>}
                  {showLineTotalCol && (
                    <th className='num doc-col-total'>Importe</th>
                  )}
                </tr>
              </thead>
              <tbody>
                {rows.map((line) => (
                  <tr key={`line-row-${line.orden}`}>
                    <td className='doc-col-desc'>{line.descripcion || '—'}</td>
                    <td className='num doc-col-price'>
                      {formatCurrencyEs(line.precioUnitario)}
                    </td>
                    {hasAnyDiscount && (
                      <td className='num doc-col-narrow'>
                        {line.descuentoPorcentaje > 0
                          ? `${line.descuentoPorcentaje}%`
                          : '—'}
                      </td>
                    )}
                    {showLineTotalCol && (
                      <td className='num doc-col-total'>
                        {formatCurrencyEs(line.baseImponible)}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {showPricingSummary && (
            <div className='doc-pricing-summary-grid'>
              <div className='doc-pricing-summary-card'>
                {hasAnyDiscount && (
                  <>
                    <div className='doc-pricing-summary-row'>
                      <span>Subtotal bruto</span>
                      <strong>{formatCurrencyEs(totals.subtotalBruto)}</strong>
                    </div>
                    <div className='doc-pricing-summary-row'>
                      <span>Descuento aplicado</span>
                      <strong>-{formatCurrencyEs(totals.totalDescuento)}</strong>
                    </div>
                  </>
                )}

                <div className='doc-pricing-summary-row'>
                  <span>Base Imponible Total</span>
                  <strong>{formatCurrencyEs(totals.baseImponible)}</strong>
                </div>

                {totals.desgloseIva.map((vat) => (
                  <div
                    key={`vat-breakdown-${vat.ivaPorcentaje}`}
                    className='doc-pricing-summary-row vat-row'
                  >
                    <span>
                      {vat.etiqueta} (s/ {formatCurrencyEs(vat.baseImponible)})
                    </span>
                    <span>{formatCurrencyEs(vat.cuotaIva)}</span>
                  </div>
                ))}

                <div className='doc-pricing-summary-total'>
                  <span>TOTAL (IVA incl.)</span>
                  <span>{formatCurrencyEs(totals.totalGeneral)}</span>
                </div>
              </div>
            </div>
          )}
        </div>
      )
    }

    case 'contract-parties':
      return (
        <div>
          {headingLabel && <h3 className='doc-pill-heading'>{headingLabel}</h3>}
          <div className='doc-parties-grid'>
            <div className='doc-party-card'>
              <span className='doc-party-role'>El Prestador</span>
              <div>
                <strong>{company.nombreEmpresa || 'Payé Digital'}</strong>
              </div>
              {company.cifNif && <div>CIF/NIF: {company.cifNif}</div>}
              {company.direccion && <div>Domicilio: {company.direccion}</div>}
              {company.email && <div>Email: {company.email}</div>}
              {company.telefono && <div>Tel: {company.telefono}</div>}
            </div>
            <div className='doc-party-card'>
              <span className='doc-party-role'>El Cliente</span>
              <div>
                <strong>{document.clienteNombre || 'Cliente'}</strong>
              </div>
              {document.clienteCifNif && <div>CIF/NIF: {document.clienteCifNif}</div>}
              {document.clienteDireccion && <div>Domicilio: {document.clienteDireccion}</div>}
              {document.clienteEmail && <div>Email: {document.clienteEmail}</div>}
              {document.clienteTelefono && <div>Tel: {document.clienteTelefono}</div>}
            </div>
          </div>

          {(document.objetoContrato ||
            document.duracionContrato ||
            document.precioTextoContrato ||
            document.formaPagoContrato) && (
            <div className='doc-contract-summary-strip'>
              {document.objetoContrato && (
                <div>
                  <strong>Objeto:</strong> {document.objetoContrato}
                </div>
              )}
              {document.duracionContrato && (
                <div>
                  <strong>Duración:</strong> {document.duracionContrato}
                </div>
              )}
              {document.precioTextoContrato && (
                <div>
                  <strong>Precio pactado:</strong> {document.precioTextoContrato}
                </div>
              )}
              {document.formaPagoContrato && (
                <div>
                  <strong>Forma de pago:</strong> {document.formaPagoContrato}
                </div>
              )}
            </div>
          )}

          {safeHtml && (
            <div
              className='doc-section-body doc-mt-sm'
              dangerouslySetInnerHTML={{ __html: safeHtml }}
            />
          )}
        </div>
      )

    case 'signatures':
      return (
        <div>
          {headingLabel && <h3 className='doc-pill-heading'>{headingLabel}</h3>}
          {safeHtml && (
            <div
              className='doc-section-body'
              dangerouslySetInnerHTML={{ __html: safeHtml }}
            />
          )}
          <p className='doc-section-body doc-signature-place-date'>
            En {document.lugarFirma || 'A Coruña'}, a {formatDateEs(document.fechaEmision)}.
          </p>
          <div className='doc-signatures-grid'>
            <div className='doc-signature-box'>
              <span className='doc-party-role'>Por el Prestador</span>
              <div className='doc-signature-line'>
                Fdo.: {company.nombreEmpresa || 'Payé Digital'}
                {company.cifNif ? ` (${company.cifNif})` : ''}
              </div>
            </div>
            <div className='doc-signature-box'>
              <span className='doc-party-role'>Por el Cliente</span>
              <div className='doc-signature-line'>
                Fdo.: {document.clienteNombre || 'Cliente'}
                {document.clienteCifNif ? ` (${document.clienteCifNif})` : ''}
              </div>
            </div>
          </div>
        </div>
      )

    default:
      return (
        <div>
          {headingLabel && <h3 className='doc-pill-heading'>{headingLabel}</h3>}
          <div
            className='doc-section-body'
            dangerouslySetInnerHTML={{ __html: safeHtml }}
          />
        </div>
      )
  }
}
