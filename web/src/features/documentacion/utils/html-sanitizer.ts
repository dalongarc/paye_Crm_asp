import DOMPurify from 'dompurify'

const ALLOWED_TAGS = [
  'p',
  'br',
  'strong',
  'b',
  'em',
  'i',
  'u',
  's',
  'ul',
  'ol',
  'li',
  'h1',
  'h2',
  'h3',
  'h4',
  'blockquote',
  'span',
  'div',
  'table',
  'thead',
  'tbody',
  'tfoot',
  'tr',
  'th',
  'td',
  'hr',
  'sub',
  'sup',
  'img',
]

const ALLOWED_ATTR = ['class', 'colspan', 'rowspan', 'src', 'alt']

let hookRegistered = false

function ensureDomPurifyHooks(): void {
  if (hookRegistered || typeof window === 'undefined') return
  hookRegistered = true

  DOMPurify.addHook('afterSanitizeAttributes', (node) => {
    if (node.tagName === 'IMG') {
      const src = node.getAttribute('src')?.trim() ?? ''
      const isLocal = /^\/images\/[a-zA-Z0-9_\-/]+\.(png|jpg|jpeg|webp|svg)$/i.test(src)
      const isDataUri = /^data:image\/(png|jpeg|jpg|webp);base64,[A-Za-z0-9+/=\s]+$/i.test(src)
      if ((!isLocal && !isDataUri) || src.includes('..') || src.includes('//')) {
        node.parentNode?.removeChild(node)
      }
    }
  })
}

/**
 * Sanitiza HTML en el cliente usando DOMPurify con la misma lista blanca estricta del servidor.
 */
export function sanitizeDocumentoHtml(dirtyHtml?: string | null): string {
  if (!dirtyHtml) return ''
  ensureDomPurifyHooks()

  // Reparar posibles entidades numéricas o con nombre doblemente escapadas (&amp;#243; -> ó)
  const normalizedEntities = decodeCorruptedHtmlEntities(dirtyHtml).replace(
    /&amp;(aacute|eacute|iacute|oacute|uacute|ntilde|uuml|Aacute|Eacute|Iacute|Oacute|Uacute|Ntilde|Uuml|ordm|ordf|iquest|iexcl);/g,
    '&$1;'
  )

  const sanitized = DOMPurify.sanitize(normalizedEntities, {
    ALLOWED_TAGS,
    ALLOWED_ATTR,
    ALLOW_DATA_ATTR: false,
    FORBID_TAGS: ['script', 'iframe', 'object', 'embed', 'style', 'link', 'meta', 'form', 'svg'],
    FORBID_ATTR: ['style', 'onerror', 'onclick', 'onload', 'onmouseover'],
  })

  // Eliminar viñetas vacías (<li> vacíos o solo con <br>/espacios) y listas/párrafos vacíos
  return sanitized
    .replace(/<li[^>]*>(?:\s|&nbsp;|<br\s*\/?>)*<\/li>/gi, '')
    .replace(/<(ul|ol)[^>]*>(?:\s|&nbsp;)*<\/\1>/gi, '')
    .replace(/<li[^>]*>(?:\s|&nbsp;|<br\s*\/?>)*<\/li>/gi, '')
    .replace(/<p[^>]*>(?:\s|&nbsp;|<br\s*\/?>)*<\/p>/gi, '')
}

/**
 * Limpia el HTML pegado desde Microsoft Word / Google Docs / páginas web externas:
 * elimina comentarios condicionales de Office, etiquetas <o:p>, clases Mso*, estilos inline
 * y devuelve únicamente el marcado semántico limpio validado por DOMPurify.
 */
export function cleanPastedWordHtml(rawClipboardHtml: string, fallbackPlainText: string): string {
  if (!rawClipboardHtml || !rawClipboardHtml.trim()) {
    return fallbackPlainText
      .split(/\r?\n\r?\n/)
      .map((para) => `<p>${escapeHtml(para.trim()).replace(/\r?\n/g, '<br />')}</p>`)
      .join('')
  }

  let cleaned = rawClipboardHtml
    // Eliminar comentarios condicionales de Word <!--[if ...]>...<![endif]-->
    .replace(/<!--[\s\S]*?-->/g, '')
    // Eliminar bloques <style>, <xml>, <meta>, <link>, <title> de cabeceras de Word
    .replace(/<(style|xml|meta|link|title|script)[^>]*>[\s\S]*?<\/\1>/gi, '')
    // Eliminar etiquetas con namespace de Office (<o:p>, <w:...>, <v:...>)
    .replace(/<\/?[a-z]+:[^>]*>/gi, '')
    // Eliminar clases Mso* y atributos style/lang/align de Word
    .replace(/\s*class=("|')?Mso[a-zA-Z0-9_\-\s]*\1/gi, '')
    .replace(/\s*style=("[^"]*"|'[^']*'|[^\s>]+)/gi, '')
    .replace(/\s*lang=("[^"]*"|'[^']*'|[^\s>]+)/gi, '')
    // Convertir <b> a <strong> y <i> a <em>
    .replace(/<b(\s|>)/gi, '<strong$1')
    .replace(/<\/b>/gi, '</strong>')
    .replace(/<i(\s|>)/gi, '<em$1')
    .replace(/<\/i>/gi, '</em>')
    // Eliminar spans vacíos residuales de Word
    .replace(/<span>([\s\S]*?)<\/span>/gi, '$1')

  return sanitizeDocumentoHtml(cleaned)
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

/**
 * Decodifica entidades HTML numéricas o con nombre doblemente escapadas (ej. Recepci&amp;#243;n -> Recepción)
 * conservando intactas las etiquetas HTML válidas.
 */
export function decodeCorruptedHtmlEntities(html: string): string {
  if (!html) return ''
  let result = html
  for (let pass = 0; pass < 3; pass++) {
    const prev = result
    result = result
      .replace(/&amp;#(\d+);/g, '&#$1;')
      .replace(/&amp;#x([0-9a-fA-F]+);/g, '&#x$1;')
      .replace(/&#(\d+);/g, (_, dec: string) => {
        const code = Number.parseInt(dec, 10)
        return Number.isFinite(code) && code > 31 ? String.fromCodePoint(code) : _
      })
      .replace(/&#x([0-9a-fA-F]+);/g, (_, hex: string) => {
        const code = Number.parseInt(hex, 16)
        return Number.isFinite(code) && code > 31 ? String.fromCodePoint(code) : _
      })
    if (result === prev) break
  }
  return result
}

let regenerationVariationCounter = 0

interface ParsedOriginSection {
  heading: string
  paragraphs: string[]
  bullets: string[]
}

function stripEmailsAndTechnicalTags(text: string): string {
  return decodeCorruptedHtmlEntities(text)
    .replace(/\b[\w.\-+]+@[\w.\-]+\.[a-zA-Z]{2,}\b\s*(?:[—–\-:·]\s*)?/g, '')
    .replace(/\((?:DSP\/ASP|CMR|2ºC\s*-\s*5ºC|JSON|SQL|API|HTTP[^)]*)\)/gi, '')
    .replace(/\s+/g, ' ')
    .trim()
}

function parseOriginSourceSections(rawSource?: string | null): {
  projectTitle: string
  overviewParagraphs: string[]
  sections: ParsedOriginSection[]
} {
  if (!rawSource || !rawSource.trim()) {
    return {
      projectTitle: 'el proyecto',
      overviewParagraphs: [],
      sections: [],
    }
  }

  const lines = decodeCorruptedHtmlEntities(rawSource)
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(
      (l) =>
        l.length > 0 &&
        !l.startsWith('```') &&
        !l.startsWith('---') &&
        !l.startsWith('===') &&
        !l.startsWith('|')
    )

  const h1 =
    lines.find((l) => l.startsWith('# '))?.replace(/^#+\s*/, '') ??
    lines.find((l) => l.startsWith('## '))?.replace(/^#+\s*/, '') ??
    'el proyecto'

  const projectTitle = h1
    .replace(/^manual\s+de\s+documentaci[oó]n\s+integral\s*[—–\-:]\s*/i, '')
    .trim()

  const overviewParagraphs: string[] = []
  const sections: ParsedOriginSection[] = []
  let current: ParsedOriginSection | null = null

  for (const line of lines) {
    if (line.startsWith('# ')) continue
    if (line.startsWith('## ') || line.startsWith('### ')) {
      const heading = line
        .replace(/^#+\s*/, '')
        .replace(/^\d+(?:\.\d+)*[.)\s\-—–:]+\s*/, '')
        .replace(/\*\*/g, '')
        .trim()

      if (/cuentas de correo|buzones|direcciones de correo|arquitectura|base de datos|despliegue/i.test(heading)) {
        current = null
        continue
      }

      current = { heading, paragraphs: [], bullets: [] }
      sections.push(current)
      continue
    }

    const clean = stripEmailsAndTechnicalTags(
      line.replace(/\*\*([^*]+)\*\*/g, '$1').replace(/`([^`]+)`/g, '$1')
    )
    if (!clean) continue

    const isBullet = /^(-|\*|•|\d+\.)\s+/.test(line)
    if (isBullet) {
      const bulletBody = clean.replace(/^(-|\*|•|\d+\.)\s+/, '').trim()
      if (bulletBody.length > 10 && bulletBody.length < 240) {
        if (!current) {
          current = { heading: 'Funcionamiento general', paragraphs: [], bullets: [] }
          sections.push(current)
        }
        current.bullets.push(bulletBody.endsWith('.') ? bulletBody : `${bulletBody}.`)
      }
    } else if (clean.length > 25) {
      const sentence = clean.endsWith('.') ? clean : `${clean}.`
      if (!current) {
        overviewParagraphs.push(sentence)
      } else {
        current.paragraphs.push(sentence)
      }
    }
  }

  return { projectTitle, overviewParagraphs, sections }
}

function formatItemWithBoldLead(item: string): string {
  const clean = stripEmailsAndTechnicalTags(item)
  const colonIdx = clean.indexOf(':')
  if (colonIdx > 2 && colonIdx < 55) {
    const lead = escapeHtml(clean.slice(0, colonIdx).trim())
    const rest = escapeHtml(clean.slice(colonIdx + 1).trim())
    return `<strong>${lead}:</strong> ${rest}`
  }
  return escapeHtml(clean)
}

/**
 * Garantiza que la regeneración o simplificación de un bloque limpie entidades corruptas,
 * elimine buzones de correo internos, extrapole el funcionamiento básico del proyecto desde el origen de datos
 * y ofrezca una redacción nueva en cada pulsación.
 */
export function refineRegeneratedBlockContent(params: {
  bloqueTitulo: string
  bloqueContenidoActual: string
  returnedHtml: string
  modo: 'regenerar' | 'simplificar'
  instruccionesExtra?: string | null
  textoOrigen?: string | null
}): string {
  const cleanedReturned = sanitizeDocumentoHtml(
    stripEmailsAndTechnicalTags(decodeCorruptedHtmlEntities(params.returnedHtml))
  )
  const cleanedCurrent = sanitizeDocumentoHtml(
    stripEmailsAndTechnicalTags(decodeCorruptedHtmlEntities(params.bloqueContenidoActual))
  )

  const extra = (params.instruccionesExtra ?? '').toLowerCase()
  const titleLower = params.bloqueTitulo.toLowerCase()
  const isSimplify =
    params.modo === 'simplificar' ||
    extra.includes('sencillo') ||
    extra.includes('breve') ||
    extra.includes('resum') ||
    extra.includes('corto')

  const wantsParagraphs =
    extra.includes('párrafo') || extra.includes('parrafo') || extra.includes('texto corrido')

  const plainReturned = cleanedReturned.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim()
  const plainCurrent = cleanedCurrent.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim()
  const needsFreshExtrapolation =
    !plainReturned ||
    plainReturned === plainCurrent ||
    params.returnedHtml.includes('&amp;#') ||
    /@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}/.test(params.returnedHtml) ||
    extra.length > 0

  if (!needsFreshExtrapolation) {
    return sanitizeDocumentoHtml(decodeCorruptedHtmlEntities(params.returnedHtml))
  }

  regenerationVariationCounter = (regenerationVariationCounter + 1) % 3
  const variation = regenerationVariationCounter
  const parsed = parseOriginSourceSections(params.textoOrigen || plainCurrent)
  const meaningfulSections = parsed.sections.filter(
    (s) => s.paragraphs.length > 0 || s.bullets.length > 0
  )

  const highLevelPoints: string[] = []
  if (meaningfulSections.length >= 2) {
    for (const sec of meaningfulSections.slice(0, 6)) {
      const detail =
        variation === 1 && sec.bullets.length > 1
          ? sec.bullets[1]
          : sec.paragraphs[0] ?? sec.bullets[0] ?? ''
      if (!detail) continue
      if (
        sec.heading.toLowerCase() !== 'funcionamiento general' &&
        !detail.toLowerCase().startsWith(sec.heading.toLowerCase())
      ) {
        highLevelPoints.push(`${sec.heading}: ${detail}`)
      } else {
        highLevelPoints.push(detail)
      }
    }
  }

  if (highLevelPoints.length < 3) {
    const allBullets = parsed.sections.flatMap((s) => s.bullets)
    const offset = variation === 1 && allBullets.length > 4 ? 2 : 0
    for (const b of allBullets.slice(offset, offset + 6)) {
      if (highLevelPoints.length >= 6) break
      if (!highLevelPoints.includes(b)) highLevelPoints.push(b)
    }
  }

  if (highLevelPoints.length === 0 && plainCurrent.length > 15) {
    const sentences = plainCurrent
      .split(/[.;]+/)
      .map((s) => s.trim())
      .filter((s) => s.length > 10)
    for (const s of sentences.slice(0, 5)) {
      highLevelPoints.push(s.endsWith('.') ? s : `${s}.`)
    }
  }

  if (
    titleLower.includes('funciona') ||
    titleLower.includes('incluye') ||
    titleLower.includes('alcance') ||
    extra.includes('funcionamiento') ||
    extra.includes('extrapola')
  ) {
    const selectedPoints = isSimplify
      ? highLevelPoints.slice(0, 4)
      : highLevelPoints.slice(0, 6)

    if (selectedPoints.length > 0) {
      if (wantsParagraphs || variation === 2) {
        const intro =
          parsed.overviewParagraphs[0] ??
          `El funcionamiento básico de ${parsed.projectTitle} centraliza la operativa diaria en un entorno claro, ordenado y fácil de usar para todo el equipo.`
        const summary = `En la práctica, el sistema coordina de forma conectada las áreas clave del proyecto: ${selectedPoints
          .slice(0, 4)
          .map((p) => p.replace(/\.$/, ''))
          .join('; ')}.`
        return sanitizeDocumentoHtml(`<p>${escapeHtml(intro)}</p><p>${escapeHtml(summary)}</p>`)
      }
      return sanitizeDocumentoHtml(
        `<ul>${selectedPoints.map((p) => `<li>${formatItemWithBoldLead(p)}</li>`).join('')}</ul>`
      )
    }
  }

  if (
    titleLower.includes('introducci') ||
    titleLower.includes('objeto') ||
    titleLower.includes('resumen')
  ) {
    const sectionList = meaningfulSections
      .map((s) => s.heading)
      .filter((h) => h.toLowerCase() !== 'funcionamiento general')
      .slice(0, 4)
    const scopeSentence =
      sectionList.length > 0
        ? `Integra en una única herramienta los apartados de ${sectionList.join(', ')}, facilitando el trabajo diario sin complicaciones técnicas.`
        : 'Permite gestionar toda la actividad diaria con rapidez, orden y control desde cualquier dispositivo.'
    const baseIntro =
      parsed.overviewParagraphs[variation % Math.max(1, parsed.overviewParagraphs.length)] ??
      `Ponemos en marcha ${parsed.projectTitle} para agilizar la gestión diaria y ofrecer una experiencia moderna y clara.`

    return sanitizeDocumentoHtml(
      isSimplify
        ? `<p>${escapeHtml(baseIntro)} ${escapeHtml(scopeSentence)}</p>`
        : `<p>${escapeHtml(baseIntro)}</p><p>${escapeHtml(scopeSentence)}</p>`
    )
  }

  if (highLevelPoints.length > 0) {
    if (isSimplify || wantsParagraphs) {
      return sanitizeDocumentoHtml(
        `<p>${escapeHtml(highLevelPoints.slice(0, 3).join(' '))}</p>`
      )
    }
    return sanitizeDocumentoHtml(
      `<ul>${highLevelPoints
        .slice(0, 5)
        .map((p) => `<li>${formatItemWithBoldLead(p)}</li>`)
        .join('')}</ul>`
    )
  }

  return cleanedReturned || cleanedCurrent
}
