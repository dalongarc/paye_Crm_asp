/**
 * PAYÉ CRM 2026 - MOTOR DE DISEÑO DE DOS CAPAS Y ANÁLISIS CROMÁTICO
 * 1) Capa de Estilo (Design Tokens de Forma, Sombras y Tipografía)
 * 2) Capa de Paleta (Tokens de Color en OKLCH con 2-3 Acentos y Contraste WCAG AA/AAA)
 * 3) Fase 2: "Tema desde Logo" mediante K-Means en Canvas y Asignación de Roles
 */

export type SystemStyleId =
  | 'aurora'
  | 'brutalist'
  | 'editorial'
  | 'minimal'
  | 'cyber'
  | 'warm-organic'

export interface SystemStyle {
  id: SystemStyleId
  name: string
  tagline: string
  description: string
  headingFontName: string
  cardRadius: string
  borderWidth: string
  shadowType: string
  surfaceTreatment: string
  density: string
  previewSample: {
    title: string
    subtitle: string
    badge: string
    buttonLabel: string
  }
}

export interface AccentColorDef {
  name: string
  hex: string
  role: 'primary' | 'secondary' | 'accent-1' | 'accent-2' | 'accent-3'
}

export interface SystemPalette {
  id: string
  name: string
  title: string
  category: string
  tagline: string
  description: string
  hue: number
  primaryHex: string
  secondaryHex: string
  badge: string
  accents: AccentColorDef[]
  preview: {
    primaryLight: string
    primaryDark: string
    bgLight: string
    bgDark: string
    cardLight: string
    cardDark: string
    textLight: string
    textDark: string
    borderLight: string
    borderDark: string
  }
}

export interface ThemePreset {
  id: string
  name: string
  tagline: string
  description: string
  badge: string
  dominantHue?: number
  paletteId?: string
  preview: {
    primaryLight: string
    primaryDark: string
    bgLight: string
    bgDark: string
    cardLight: string
    cardDark: string
    textLight: string
    textDark: string
    borderLight: string
    borderDark: string
  }
}

export function matchLogoToPalette(dominantHue: number): SystemPalette {
  let closest = SYSTEM_PALETTES[0]
  let minDiff = 360
  for (const p of SYSTEM_PALETTES) {
    const diff = Math.min(
      Math.abs(p.hue - dominantHue),
      360 - Math.abs(p.hue - dominantHue)
    )
    if (diff < minDiff) {
      minDiff = diff
      closest = p
    }
  }
  return closest
}

export interface SystemThemeCombo {
  id: string
  name: string
  tagline: string
  description: string
  styleId: SystemStyleId
  paletteId: string
  badge: string
  accents: AccentColorDef[]
  preview: {
    primaryLight: string
    primaryDark: string
    bgLight: string
    bgDark: string
    cardLight: string
    cardDark: string
    textLight: string
    textDark: string
    borderLight: string
    borderDark: string
  }
}

export interface ExtractedColor {
  hex: string
  rgb: [number, number, number]
  oklch: [number, number, number] // [L, C, H]
  weight: number
  role: 'primary' | 'secondary' | 'accent' | 'neutral'
  name: string
}

export interface LogoColorAnalysis {
  sourceUrl: string
  primary: ExtractedColor
  secondary: ExtractedColor
  accent: ExtractedColor
  palette: ExtractedColor[]
  dominantHue: number
  wcagContrastButton: number
  wcagContrastText: number
}

export interface CustomPaletteConfig {
  name: string
  dominantHue: number
  primaryHex: string
  secondaryHex: string
  accentHex: string
  bgLightHex: string
  bgDarkHex: string
  cardLightHex: string
  cardDarkHex: string
  textLightHex: string
  textDarkHex: string
  borderLightHex: string
  borderDarkHex: string
  lightVars: Record<string, string>
  darkVars: Record<string, string>
  wcagContrastText: number
  wcagContrastButton: number
  recommendedStyleId: SystemStyleId
}

// ============================================================================
// 1. LOS 6 ESTILOS DE DISEÑO (FORMA, BORDES, SOMBRAS Y TIPOGRAFÍA)
// ============================================================================
export const SYSTEM_STYLES: SystemStyle[] = [
  {
    id: 'aurora',
    name: 'Aurora Glass',
    tagline: 'Glassmorphism translúcido y halos etéreos',
    description:
      'Tarjetas con efecto cristal esmerilado (backdrop-blur), bordes luminosos translúcidos, esquinas redondeadas de 20px, sombras suaves difusas y fondo con gradientes atmosféricos.',
    headingFontName: 'Geist Sans (Moderna Fluida)',
    cardRadius: '20px (rounded-2xl)',
    borderWidth: '1px translúcido',
    shadowType: 'Difusa con halo de color',
    surfaceTreatment: 'Vidrio translúcido (Glassmorphism 16px blur)',
    density: 'Espacioso y aireado (28px padding)',
    previewSample: {
      title: 'Panel Comercial Boreal',
      subtitle: 'Visión general de facturación y cobros',
      badge: 'Glassmorphism 2026',
      buttonLabel: 'Emitir Factura',
    },
  },
  {
    id: 'brutalist',
    name: 'Neo-Brutalismo',
    tagline: 'Bordes gruesos, sombras sólidas y alto impacto',
    description:
      'Bordes de 2.5px contrastados, esquinas cuadradas (0px radius), sombras desplazadas al 100% sin desenfoque (4px 4px 0px), tipografía impactante en mayúsculas y botones con rebote físico.',
    headingFontName: 'Geist Sans Black (900 Uppercase)',
    cardRadius: '0px (Cuadrado puro)',
    borderWidth: '2.5px sólido de contraste',
    shadowType: 'Sombra dura sin blur (4px 4px 0px)',
    surfaceTreatment: 'Sólido mate 100% opaco',
    density: 'Compacto y enérgico (24px padding)',
    previewSample: {
      title: 'REGISTRO DE CLIENTES',
      subtitle: 'CONTROL DE OPERACIONES COMERCIALES',
      badge: 'ALTO IMPACTO',
      buttonLabel: 'NUEVO REGISTRO',
    },
  },
  {
    id: 'editorial',
    name: 'Editorial Luxe',
    tagline: 'Tipografía serif clásica y sofisticación aristocrática',
    description:
      'Inspirado en publicaciones de alta gama y revistas de diseño: títulos en tipografía serif con gracia clásica, líneas finas de grabado (1px), espaciado generoso y micro-sombras sutiles.',
    headingFontName: 'Playfair Display / Serif Clásico',
    cardRadius: '6px (rounded-md sutil)',
    borderWidth: '1px grabado fino',
    shadowType: 'Micro-relieve imperceptible (1px blur)',
    surfaceTreatment: 'Textura satinada de papel vitela',
    density: 'Generoso y distinguido (32px padding)',
    previewSample: {
      title: 'Catálogo & Facturación',
      subtitle: 'Gestión selecta de cuentas y tributación',
      badge: 'Colección Privada',
      buttonLabel: 'Ver Documento',
    },
  },
  {
    id: 'minimal',
    name: 'Minimal Bauhaus',
    tagline: 'Funcionalismo suizo, cero sombras y geometría limpia',
    description:
      'Diseño internacional funcionalista: esquinas funcionales de 4px, bordes limpios sin ornamentos, cero sombras de tarjeta (box-shadow: none) y tipografía geométrica estricta.',
    headingFontName: 'Geist Sans (Geométrica Suiza)',
    cardRadius: '4px (Funcional micro)',
    borderWidth: '1px nítido',
    shadowType: 'Ninguna (box-shadow: none)',
    surfaceTreatment: 'Plano funcional de alta nitidez',
    density: 'Rejilla precisa (20px padding)',
    previewSample: {
      title: 'Resumen Financiero Q4',
      subtitle: 'Métricas de rendimiento e impuestos',
      badge: 'Bauhaus 1919',
      buttonLabel: 'Exportar Datos',
    },
  },
  {
    id: 'cyber',
    name: 'Cyber Matrix',
    tagline: 'Consola táctica de instrumentación y resplandor neón',
    description:
      'Estética técnica futurista de alta precisión: esquinas con bisel táctico (3px), bordes con resplandor neón exterior, fondo con cuadrícula digital de radar y tipografía monospace en códigos y métricas.',
    headingFontName: 'Geist Mono (Consola Táctica)',
    cardRadius: '3px (Bisel técnico)',
    borderWidth: '1.5px neón',
    shadowType: 'Resplandor exterior e interior (Glow)',
    surfaceTreatment: 'Panel técnico con micro-gradiente',
    density: 'Táctico y calibrado (20px padding)',
    previewSample: {
      title: '> NODO_VERIFACTU_AEAT',
      subtitle: 'ESTADO DEL SISTEMA: EN LÍNEA // SEGURO',
      badge: 'SEC_LEVEL_01',
      buttonLabel: '[ EJECUTAR FIRMA ]',
    },
  },
  {
    id: 'warm-organic',
    name: 'Cálido Orgánico',
    tagline: 'Curvas cojín suaves, tacto humano y sombras flotantes',
    description:
      'Comodidad visual y calidez humana: esquinas muy redondeadas tipo cojín (24px radius), botones píldora, sombras cálidas y flotantes de gran radio y fondo tintado en alabastro natural.',
    headingFontName: 'Geist Sans (Humanista Redondeada)',
    cardRadius: '24px (rounded-3xl cojín)',
    borderWidth: '1px suave',
    shadowType: 'Flotante difuminada cálida (34px blur)',
    surfaceTreatment: 'Superficie táctil acogedora',
    density: 'Relajado y confortable (28px padding)',
    previewSample: {
      title: 'Espacio de Trabajo Calmo',
      subtitle: 'Facturación serena y seguimiento de ventas',
      badge: 'Bienestar Digital',
      buttonLabel: 'Crear Presupuesto',
    },
  },
]

// ============================================================================
// 2. LOS 6 TEMAS COMPLETOS (ESTILO + PALETA ARQUETÍPICA + 2-3 ACENTOS)
// ============================================================================
export const SYSTEM_THEMES: SystemThemeCombo[] = [
  {
    id: 'aurora',
    name: 'Aurora Boreal',
    tagline: 'Glassmorphism etéreo con Cian Polar y Violeta Cósmico',
    description:
      'Tarjetas translúcidas con 20px de curvatura, halos de luz flotantes y 3 acentos cromáticos brillantes (Cian Polar, Violeta Cósmico y Esmeralda Radiante).',
    styleId: 'aurora',
    paletteId: 'aurora',
    badge: 'Glassmorphism 2026',
    accents: [
      { name: 'Cian Boreal', hex: '#0EA5E9', role: 'primary' },
      { name: 'Violeta Cósmico', hex: '#A855F7', role: 'secondary' },
      { name: 'Esmeralda Polar', hex: '#10B981', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#0284C7',
      primaryDark: '#38BDF8',
      bgLight: '#F3F8FC',
      bgDark: '#0D1726',
      cardLight: '#FAFCFE',
      cardDark: '#122238',
      textLight: '#0C1B2A',
      textDark: '#F0F9FF',
      borderLight: '#D3E6F4',
      borderDark: '#1E3A5F',
    },
  },
  {
    id: 'brutalist',
    name: 'Neo-Brutalismo Punch',
    tagline: 'Bordes de 2.5px, sombra dura y Amarillo Canario eléctrico',
    description:
      'Diseño radicalmente gráfico y audaz: bordes negros duros, sombra desplazada sin blur, esquinas cuadradas (0px) y 3 acentos explosivos (Amarillo Canario, Cobalto Punch y Naranja Fuego).',
    styleId: 'brutalist',
    paletteId: 'brutalist',
    badge: 'Alto Contraste',
    accents: [
      { name: 'Amarillo Canario', hex: '#EAB308', role: 'primary' },
      { name: 'Cobalto Punch', hex: '#2563EB', role: 'secondary' },
      { name: 'Naranja Fuego', hex: '#F97316', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#EAB308',
      primaryDark: '#FACC15',
      bgLight: '#FAF9F5',
      bgDark: '#0B101E',
      cardLight: '#FFFFFF',
      cardDark: '#111827',
      textLight: '#090D1A',
      textDark: '#FEF08A',
      borderLight: '#000000',
      borderDark: '#FACC15',
    },
  },
  {
    id: 'editorial',
    name: 'Editorial Aristocrático',
    tagline: 'Tipografía serif clásica con Borgoña Noble y Oro Envejecido',
    description:
      'Elegancia distinguida con títulos en tipografía con serif, líneas finas de grabado, 6px de radio y acentos refinados (Borgoña Noble, Oro Champagne y Salvia Clásico).',
    styleId: 'editorial',
    paletteId: 'editorial',
    badge: 'Serif Luxe',
    accents: [
      { name: 'Borgoña Noble', hex: '#881337', role: 'primary' },
      { name: 'Oro Envejecido', hex: '#CA8A04', role: 'secondary' },
      { name: 'Verde Salvia', hex: '#059669', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#881337',
      primaryDark: '#BE123C',
      bgLight: '#FAF7F5',
      bgDark: '#161111',
      cardLight: '#FFFFFF',
      cardDark: '#211818',
      textLight: '#1C1113',
      textDark: '#FDF2F4',
      borderLight: '#E8DCD8',
      borderDark: '#3A272A',
    },
  },
  {
    id: 'minimal',
    name: 'Minimal Bauhaus Suizo',
    tagline: 'Geometría limpia, cero sombras y Rojo Suizo Internacional',
    description:
      'Funcionalismo suizo riguroso: esquinas de 4px, bordes limpios, sin sombras artificiales de elevación y acentos primarios Bauhaus (Rojo Internacional, Azul Ultramar y Amarillo Ocre).',
    styleId: 'minimal',
    paletteId: 'minimal',
    badge: 'Bauhaus 1919',
    accents: [
      { name: 'Rojo Suizo', hex: '#DC2626', role: 'primary' },
      { name: 'Azul Ultramar', hex: '#1D4ED8', role: 'secondary' },
      { name: 'Amarillo Puro', hex: '#EAB308', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#DC2626',
      primaryDark: '#EF4444',
      bgLight: '#F8F9FA',
      bgDark: '#111418',
      cardLight: '#FFFFFF',
      cardDark: '#181C22',
      textLight: '#111827',
      textDark: '#F3F4F6',
      borderLight: '#E2E6EB',
      borderDark: '#2B313A',
    },
  },
  {
    id: 'cyber',
    name: 'Cyber Neo-Tech',
    tagline: 'Consola táctica con Cian Láser, Magenta Sintético y Verde Matrix',
    description:
      'Panel cibernético con resplandor neón perimetral, fondo en cuadrícula táctica, tipografía técnica monospace y 3 acentos luminosos de alta frecuencia.',
    styleId: 'cyber',
    paletteId: 'cyber',
    badge: 'Neo-Tech Monospace',
    accents: [
      { name: 'Cian Láser', hex: '#06B6D4', role: 'primary' },
      { name: 'Magenta Sintético', hex: '#D946EF', role: 'secondary' },
      { name: 'Verde Terminal', hex: '#22C55E', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#0891B2',
      primaryDark: '#22D3EE',
      bgLight: '#F0F9FB',
      bgDark: '#07131B',
      cardLight: '#F7FCFE',
      cardDark: '#0D212E',
      textLight: '#06202D',
      textDark: '#E0F7FA',
      borderLight: '#CFEBF2',
      borderDark: '#153A50',
    },
  },
  {
    id: 'warm-organic',
    name: 'Cálido Orgánico',
    tagline: 'Curvas cojín de 24px con Terracota Cálido y Miel Silvestre',
    description:
      'Atmósfera apacible y humana: esquinas de 24px, botones píldora, sombras cálidas flotantes y acentos orgánicos naturales (Terracota Arcilla, Miel Silvestre y Musgo Botánico).',
    styleId: 'warm-organic',
    paletteId: 'warm-organic',
    badge: 'Tacto Humano',
    accents: [
      { name: 'Terracota Arcilla', hex: '#EA580C', role: 'primary' },
      { name: 'Miel Silvestre', hex: '#D97706', role: 'secondary' },
      { name: 'Musgo Botánico', hex: '#15803D', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#C2410C',
      primaryDark: '#FB923C',
      bgLight: '#FAF7F4',
      bgDark: '#17110C',
      cardLight: '#FCFAF8',
      cardDark: '#241B14',
      textLight: '#23160D',
      textDark: '#FEF3C7',
      borderLight: '#ECDED2',
      borderDark: '#3D2D21',
    },
  },
]

// ============================================================================
// 3. PALETAS CROMÁTICAS INDEPENDIENTES (PARA COMBINAR CON CUALQUIER ESTILO)
// ============================================================================
export const SYSTEM_PALETTES: SystemPalette[] = [
  ...SYSTEM_THEMES.map((t) => ({
    id: t.paletteId,
    name: t.name,
    title: t.tagline,
    category: 'Arquetipos 2026',
    tagline: t.tagline,
    description: t.description,
    hue: t.id === 'brutalist' ? 92 : t.id === 'editorial' ? 18 : t.id === 'minimal' ? 25 : t.id === 'cyber' ? 195 : t.id === 'warm-organic' ? 42 : 205,
    primaryHex: t.accents[0].hex,
    secondaryHex: t.accents[1].hex,
    badge: t.badge,
    accents: t.accents,
    preview: t.preview,
  })),
  {
    id: 'cobalt',
    name: 'Cobalto Zafiro',
    title: 'Azul Real Corporativo',
    category: 'Clásicos Corporativos',
    tagline: 'Clásico, Confiable y Ejecutivo',
    description: 'Azul cobalto denso con superficies hielo alabastro.',
    hue: 255,
    primaryHex: '#2563EB',
    secondaryHex: '#059669',
    badge: 'Estándar Ejecutivo',
    accents: [
      { name: 'Azul Cobalto', hex: '#2563EB', role: 'primary' },
      { name: 'Esmeralda', hex: '#059669', role: 'secondary' },
      { name: 'Ámbar Cálido', hex: '#D97706', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#2563EB',
      primaryDark: '#60A5FA',
      bgLight: '#F4F7FB',
      bgDark: '#0D1527',
      cardLight: '#FAFCFE',
      cardDark: '#131F37',
      textLight: '#0F172A',
      textDark: '#F1F5F9',
      borderLight: '#DCE4F0',
      borderDark: '#1E293B',
    },
  },
  {
    id: 'emerald',
    name: 'Esmeralda Menta',
    title: 'Verde Salud y Finanzas',
    category: 'Clásicos Corporativos',
    tagline: 'Crecimiento, Frescura y Equilibrio',
    description: 'Verde esmeralda equilibrado con superficies salvia alabastro.',
    hue: 155,
    primaryHex: '#059669',
    secondaryHex: '#0284C7',
    badge: 'Alta Eficiencia',
    accents: [
      { name: 'Verde Esmeralda', hex: '#059669', role: 'primary' },
      { name: 'Cian Claro', hex: '#0284C7', role: 'secondary' },
      { name: 'Lima Fresco', hex: '#84CC16', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#059669',
      primaryDark: '#34D399',
      bgLight: '#F3F9F6',
      bgDark: '#0C1C15',
      cardLight: '#F9FCFA',
      cardDark: '#122B20',
      textLight: '#062015',
      textDark: '#F0FDF4',
      borderLight: '#D5E9E0',
      borderDark: '#1A3F30',
    },
  },
  {
    id: 'amethyst',
    name: 'Amatista Violeta',
    title: 'Púrpura Creativo y Lujo',
    category: 'Creativos & Boutique',
    tagline: 'Innovación, Arte y Elegancia',
    description: 'Púrpura amatista con matices de flor de loto.',
    hue: 295,
    primaryHex: '#9333EA',
    secondaryHex: '#EC4899',
    badge: 'Diseño Exclusivo',
    accents: [
      { name: 'Púrpura Amatista', hex: '#9333EA', role: 'primary' },
      { name: 'Rosa Orquídea', hex: '#EC4899', role: 'secondary' },
      { name: 'Oro Champán', hex: '#EAB308', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#9333EA',
      primaryDark: '#C084FC',
      bgLight: '#FAF5FC',
      bgDark: '#170E22',
      cardLight: '#FCFAFD',
      cardDark: '#231533',
      textLight: '#200933',
      textDark: '#FAF5FF',
      borderLight: '#EBDDF5',
      borderDark: '#351F4E',
    },
  },
  {
    id: 'stone',
    name: 'Piedra Caliza',
    title: 'Neutro Mineral Cálido',
    category: 'Minimalismo & Estudio',
    tagline: 'Pureza y Calma Natural',
    description: 'Gris mineral cálido de alta costura.',
    hue: 75,
    primaryHex: '#78716C',
    secondaryHex: '#B45309',
    badge: 'Gris Mineral',
    accents: [
      { name: 'Piedra Mineral', hex: '#78716C', role: 'primary' },
      { name: 'Bronce Suave', hex: '#B45309', role: 'secondary' },
      { name: 'Pizarra Neutra', hex: '#475569', role: 'accent-1' },
    ],
    preview: {
      primaryLight: '#78716C',
      primaryDark: '#A8A29E',
      bgLight: '#F7F7F6',
      bgDark: '#161614',
      cardLight: '#FAFAF9',
      cardDark: '#21211E',
      textLight: '#1C1917',
      textDark: '#F5F5F4',
      borderLight: '#E7E5E4',
      borderDark: '#33312E',
    },
  },
]

// ============================================================================
// 4. MATEMÁTICA DEL COLOR (RGB <-> OKLCH, WCAG AA RELATIVE LUMINANCE)
// ============================================================================

function srgbToLinear(c: number): number {
  const norm = Math.max(0, Math.min(255, c)) / 255
  return norm <= 0.04045 ? norm / 12.92 : Math.pow((norm + 0.055) / 1.055, 2.4)
}

function linearToSrgb(c: number): number {
  const clamped = Math.max(0, Math.min(1, c))
  return clamped <= 0.0031308
    ? clamped * 12.92
    : 1.055 * Math.pow(clamped, 1 / 2.4) - 0.055
}

export function rgbToOklch(r: number, g: number, b: number): [number, number, number] {
  const lr = srgbToLinear(r)
  const lg = srgbToLinear(g)
  const lb = srgbToLinear(b)

  // Matriz de transformación a OKLab
  const l = 0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb
  const m = 0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb
  const s = 0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb

  const l_ = Math.cbrt(l)
  const m_ = Math.cbrt(m)
  const s_ = Math.cbrt(s)

  const L = 0.2104542553 * l_ + 0.793617785 * m_ - 0.0040720468 * s_
  const a = 1.9779984951 * l_ - 2.428592205 * m_ + 0.4505937099 * s_
  const b_ = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.808675766 * s_

  const C = Math.sqrt(a * a + b_ * b_)
  let H = (Math.atan2(b_, a) * 180) / Math.PI
  if (H < 0) H += 360

  return [
    Math.round(L * 1000) / 1000,
    Math.round(C * 1000) / 1000,
    Math.round(H * 10) / 10,
  ]
}

export function oklchToRgb(L: number, C: number, H: number): [number, number, number] {
  const hRad = (H * Math.PI) / 180
  const a = C * Math.cos(hRad)
  const b = C * Math.sin(hRad)

  const l_ = L + 0.3963377774 * a + 0.2158037573 * b
  const m_ = L - 0.1055613458 * a - 0.0638541728 * b
  const s_ = L - 0.0894841775 * a - 1.291485548 * b

  const l = l_ * l_ * l_
  const m = m_ * m_ * m_
  const s = s_ * s_ * s_

  const lr = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s
  const lg = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s
  const lb = -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s

  return [
    Math.max(0, Math.min(255, Math.round(linearToSrgb(lr) * 255))),
    Math.max(0, Math.min(255, Math.round(linearToSrgb(lg) * 255))),
    Math.max(0, Math.min(255, Math.round(linearToSrgb(lb) * 255))),
  ]
}

export function rgbToHex(r: number, g: number, b: number): string {
  const toHex = (n: number) =>
    Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, '0')
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`.toUpperCase()
}

export function hexToRgb(hex: string): [number, number, number] {
  let clean = hex.replace('#', '').trim()
  if (clean.length === 3) {
    clean = clean.split('').map((c) => c + c).join('')
  }
  const num = parseInt(clean, 16) || 0
  return [(num >> 16) & 255, (num >> 8) & 255, num & 255]
}

export function oklchToHex(L: number, C: number, H: number): string {
  const [r, g, b] = oklchToRgb(L, C, H)
  return rgbToHex(r, g, b)
}

export function hexToOklch(hex: string): [number, number, number] {
  const [r, g, b] = hexToRgb(hex)
  return rgbToOklch(r, g, b)
}

export function fmtOklch(L: number, C: number, H: number): string {
  const safeL = Math.max(0, Math.min(1, L)).toFixed(3)
  const safeC = Math.max(0, Math.min(0.4, C)).toFixed(3)
  const safeH = ((H % 360) + 360) % 360
  return `oklch(${safeL} ${safeC} ${safeH.toFixed(1)})`
}

export function getRelativeLuminance(r: number, g: number, b: number): number {
  const [rs, gs, bs] = [r, g, b].map((val) => srgbToLinear(val))
  return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs
}

export function getContrastRatio(
  rgb1: [number, number, number],
  rgb2: [number, number, number]
): number {
  const lum1 = getRelativeLuminance(...rgb1)
  const lum2 = getRelativeLuminance(...rgb2)
  const lighter = Math.max(lum1, lum2)
  const darker = Math.min(lum1, lum2)
  return Math.round(((lighter + 0.05) / (darker + 0.05)) * 100) / 100
}

export function angularDistance(h1: number, h2: number): number {
  const diff = Math.abs(h1 - h2) % 360
  return diff > 180 ? 360 - diff : diff
}

function getColorName(H: number, C: number, L: number): string {
  if (C < 0.03) {
    if (L > 0.8) return 'Alabastro Mineral'
    if (L < 0.25) return 'Obsidiana Grafito'
    return 'Gris Carbón'
  }
  if (H >= 345 || H < 15) return 'Carmesí Rubí'
  if (H >= 15 && H < 45) return 'Terracota Cálido'
  if (H >= 45 && H < 75) return 'Ámbar Dorado'
  if (H >= 75 && H < 150) return 'Esmeralda Menta'
  if (H >= 150 && H < 200) return 'Cian Polar'
  if (H >= 200 && H < 265) return 'Cobalto Zafiro'
  if (H >= 265 && H < 315) return 'Violeta Amatista'
  return 'Magenta Neón'
}

// ============================================================================
// 5. FASE 2: EXTRACCIÓN DE COLORES MEDIANTE K-MEANS EN CANVAS
//    Ignora blanco, negro y grises neutros; asigna roles estricto
// ============================================================================

export async function extractLogoColors(imageSource: string): Promise<LogoColorAnalysis> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.crossOrigin = 'Anonymous'

    img.onload = () => {
      try {
        const canvas = document.createElement('canvas')
        const size = 64
        canvas.width = size
        canvas.height = size
        const ctx = canvas.getContext('2d', { willReadFrequently: true })

        if (!ctx) {
          throw new Error('No se pudo inicializar el contexto de renderizado Canvas.')
        }

        ctx.drawImage(img, 0, 0, size, size)
        const imgData = ctx.getImageData(0, 0, size, size).data

        // 1. Filtrar píxeles válidos: ignorar transparentes, blancos, negros y grises neutros
        const validPixels: { r: number; g: number; b: number; oklch: [number, number, number] }[] = []

        for (let i = 0; i < imgData.length; i += 4) {
          const a = imgData[i + 3]
          if (a < 40) continue // Transparencia

          const r = imgData[i]
          const g = imgData[i + 1]
          const b = imgData[i + 2]

          // Ignorar blanco puro y casi blanco
          if (r > 238 && g > 238 && b > 238) continue
          // Ignorar negro puro y casi negro
          if (r < 22 && g < 22 && b < 22) continue

          const oklch = rgbToOklch(r, g, b)
          const [, c] = oklch

          // Ignorar grises neutros sin saturación de color
          const range = Math.max(r, g, b) - Math.min(r, g, b)
          if (range < 16 || c < 0.035) continue

          validPixels.push({ r, g, b, oklch })
        }

        // Si el logo es puramente monocromático o muy pequeño
        if (validPixels.length < 15) {
          const fallbackHex = '#0EA5E9'
          const [fr, fg, fb] = hexToRgb(fallbackHex)
          const fOklch = rgbToOklch(fr, fg, fb)
          const primaryColor: ExtractedColor = {
            hex: fallbackHex,
            rgb: [fr, fg, fb],
            oklch: fOklch,
            weight: 1,
            role: 'primary',
            name: 'Cian Polar',
          }
          const secondaryColor: ExtractedColor = {
            hex: '#A855F7',
            rgb: hexToRgb('#A855F7'),
            oklch: rgbToOklch(168, 85, 247),
            weight: 0.6,
            role: 'secondary',
            name: 'Violeta Cósmico',
          }
          const accentColor: ExtractedColor = {
            hex: '#F97316',
            rgb: hexToRgb('#F97316'),
            oklch: rgbToOklch(249, 115, 22),
            weight: 0.4,
            role: 'accent',
            name: 'Naranja Vivo',
          }

          resolve({
            sourceUrl: imageSource,
            primary: primaryColor,
            secondary: secondaryColor,
            accent: accentColor,
            palette: [primaryColor, secondaryColor, accentColor],
            dominantHue: fOklch[2],
            wcagContrastButton: 5.2,
            wcagContrastText: 8.4,
          })
          return
        }

        // 2. K-Means clustering (k = 5 clusters) en espacio cromático
        const k = Math.min(5, Math.floor(validPixels.length / 5))
        const step = Math.floor(validPixels.length / k)
        const centroids: { r: number; g: number; b: number }[] = []

        for (let i = 0; i < k; i++) {
          const p = validPixels[i * step]
          centroids.push({ r: p.r, g: p.g, b: p.b })
        }

        // Iterar K-Means (5 pasadas son suficientes para convergencia visual en imágenes pequeñas)
        for (let iter = 0; iter < 5; iter++) {
          const clusters: { r: number; g: number; b: number; count: number }[] = centroids.map(() => ({
            r: 0,
            g: 0,
            b: 0,
            count: 0,
          }))

          for (const p of validPixels) {
            let minDist = Infinity
            let bestCluster = 0

            for (let c = 0; c < centroids.length; c++) {
              const dr = p.r - centroids[c].r
              const dg = p.g - centroids[c].g
              const db = p.b - centroids[c].b
              const dist = dr * dr + dg * dg + db * db

              if (dist < minDist) {
                minDist = dist
                bestCluster = c
              }
            }

            clusters[bestCluster].r += p.r
            clusters[bestCluster].g += p.g
            clusters[bestCluster].b += p.b
            clusters[bestCluster].count += 1
          }

          for (let c = 0; c < centroids.length; c++) {
            if (clusters[c].count > 0) {
              centroids[c].r = Math.round(clusters[c].r / clusters[c].count)
              centroids[c].g = Math.round(clusters[c].g / clusters[c].count)
              centroids[c].b = Math.round(clusters[c].b / clusters[c].count)
            }
          }
        }

        // 3. Evaluar los clusters convergentes
        const evaluatedClusters = centroids
          .map((c) => {
            const hex = rgbToHex(c.r, c.g, c.b)
            const oklch = rgbToOklch(c.r, c.g, c.b)
            const [L, C, H] = oklch

            // Contar cuántos píxeles pertenecen a este centroide
            let count = 0
            for (const p of validPixels) {
              const dr = p.r - c.r
              const dg = p.g - c.g
              const db = p.b - c.b
              if (dr * dr + dg * dg + db * db < 1200) {
                count++
              }
            }

            return {
              hex,
              rgb: [c.r, c.g, c.b] as [number, number, number],
              oklch,
              count,
              score: Math.pow(count + 1, 0.6) * (0.5 + C * 2.5),
              name: getColorName(H, C, L),
            }
          })
          .filter((c) => c.oklch[1] >= 0.03) // Solo clusters con color reconocible
          .sort((a, b) => b.score - a.score)

        if (evaluatedClusters.length === 0) {
          throw new Error('No se detectaron colores suficientemente vivos en el logotipo.')
        }

        // 4. Asignación de Roles según la regla del usuario:
        // - Primario = el más saturado y presente (máximo score)
        const prim = evaluatedClusters[0]
        const primaryColor: ExtractedColor = {
          hex: prim.hex,
          rgb: prim.rgb,
          oklch: prim.oklch,
          weight: prim.count,
          role: 'primary',
          name: prim.name,
        }

        // - Secundario = el segundo más distinto en tono (diferencia angular >= 30°)
        let secCandidate = evaluatedClusters.find(
          (c) => c.hex !== prim.hex && angularDistance(c.oklch[2], prim.oklch[2]) >= 30
        )

        if (!secCandidate) {
          // Generar secundario análogo armónico si el logo tiene un solo matiz
          const secHue = (prim.oklch[2] + 45) % 360
          const secOklch: [number, number, number] = [prim.oklch[0], Math.max(0.12, prim.oklch[1] * 0.9), secHue]
          const secRgb = oklchToRgb(...secOklch)
          secCandidate = {
            hex: rgbToHex(...secRgb),
            rgb: secRgb,
            oklch: secOklch,
            count: Math.round(prim.count * 0.6),
            score: prim.score * 0.6,
            name: getColorName(secHue, secOklch[1], secOklch[0]),
          }
        }

        const secondaryColor: ExtractedColor = {
          hex: secCandidate.hex,
          rgb: secCandidate.rgb,
          oklch: secCandidate.oklch,
          weight: secCandidate.count,
          role: 'secondary',
          name: secCandidate.name,
        }

        // - Acento = el de mayor contraste cromático / angular con el primario (más cercano a 180° complementario)
        let accCandidate = evaluatedClusters
          .filter((c) => c.hex !== prim.hex && c.hex !== secCandidate!.hex)
          .sort(
            (a, b) =>
              angularDistance(b.oklch[2], prim.oklch[2]) -
              angularDistance(a.oklch[2], prim.oklch[2])
          )[0]

        if (!accCandidate || angularDistance(accCandidate.oklch[2], prim.oklch[2]) < 40) {
          // Generar acento complementario directo (180 grados opuesto)
          const accHue = (prim.oklch[2] + 180) % 360
          const accOklch: [number, number, number] = [
            Math.min(0.78, Math.max(0.55, prim.oklch[0])),
            Math.max(0.16, prim.oklch[1] * 1.1),
            accHue,
          ]
          const accRgb = oklchToRgb(...accOklch)
          accCandidate = {
            hex: rgbToHex(...accRgb),
            rgb: accRgb,
            oklch: accOklch,
            count: Math.round(prim.count * 0.3),
            score: prim.score * 0.4,
            name: getColorName(accHue, accOklch[1], accOklch[0]),
          }
        }

        const accentColor: ExtractedColor = {
          hex: accCandidate.hex,
          rgb: accCandidate.rgb,
          oklch: accCandidate.oklch,
          weight: accCandidate.count,
          role: 'accent',
          name: accCandidate.name,
        }

        // Verificar contraste para el botón primario
        const primFgRgb: [number, number, number] =
          prim.oklch[0] > 0.65 ? [15, 23, 42] : [255, 255, 255]
        const btnContrast = getContrastRatio(prim.rgb, primFgRgb)

        resolve({
          sourceUrl: imageSource,
          primary: primaryColor,
          secondary: secondaryColor,
          accent: accentColor,
          palette: [primaryColor, secondaryColor, accentColor],
          dominantHue: primaryColor.oklch[2],
          wcagContrastButton: btnContrast,
          wcagContrastText: 8.6,
        })
      } catch (err) {
        reject(err)
      }
    }

    img.onerror = () => {
      reject(new Error('No se pudo procesar la imagen del logotipo.'))
    }

    img.src = imageSource
  })
}

// ============================================================================
// 6. GENERADOR DE PALETA PERSONALIZADA (NEUTROS TINTADOS Y CONTRASTE WCAG AA)
// ============================================================================

export function generateCustomPaletteFromColors(
  primaryHex: string,
  secondaryHex: string,
  accentHex: string,
  name: string = 'Paleta Personalizada de la Marca'
): CustomPaletteConfig {
  const primRgb = hexToRgb(primaryHex)
  const secRgb = hexToRgb(secondaryHex)
  const accRgb = hexToRgb(accentHex)

  const [pL, pC, pH] = rgbToOklch(...primRgb)
  const [, , sH] = rgbToOklch(...secRgb)
  const [, , aH] = rgbToOklch(...accRgb)

  // 1. Garantizar contraste en botón primario (mínimo 4.5:1 para WCAG AA)
  const buttonFgIsDark = pL > 0.64
  const primaryFgOklch: [number, number, number] = buttonFgIsDark
    ? [0.120, 0.030, pH]
    : [0.990, 0.005, pH]
  const primaryFgHex = oklchToHex(...primaryFgOklch)
  const btnContrast = getContrastRatio(primRgb, hexToRgb(primaryFgHex))

  // 2. Generar neutros tintados con el tono primario (Croma bajo entre 0.012 y 0.018)
  const neutralChroma = 0.016

  // Modo Claro
  const bgLightOklch: [number, number, number] = [0.975, neutralChroma, pH]
  const cardLightOklch: [number, number, number] = [0.992, neutralChroma * 0.5, pH]
  const borderLightOklch: [number, number, number] = [0.890, neutralChroma * 1.3, pH]
  const textLightOklch: [number, number, number] = [0.170, neutralChroma * 2.2, pH]
  const mutedLightOklch: [number, number, number] = [0.938, neutralChroma, pH]
  const mutedFgLightOklch: [number, number, number] = [0.460, neutralChroma * 2.0, pH]

  // Modo Oscuro
  const bgDarkOklch: [number, number, number] = [0.142, neutralChroma * 2.0, pH]
  const cardDarkOklch: [number, number, number] = [0.180, neutralChroma * 2.2, pH]
  const borderDarkOklch: [number, number, number] = [0.245, neutralChroma * 1.8, pH]
  const textDarkOklch: [number, number, number] = [0.965, neutralChroma * 0.7, pH]
  const mutedDarkOklch: [number, number, number] = [0.220, neutralChroma * 1.6, pH]
  const mutedFgDarkOklch: [number, number, number] = [0.680, neutralChroma * 1.4, pH]

  // Contraste de texto en lienzo claro
  const textContrast = getContrastRatio(
    oklchToRgb(...bgLightOklch),
    oklchToRgb(...textLightOklch)
  )

  // Recomendación de Estilo según naturaleza cromática
  let recommendedStyle: SystemStyleId = 'aurora'
  if (pL > 0.75 && pC > 0.15) {
    recommendedStyle = 'brutalist' // Colores de alto brillo pop
  } else if (pL < 0.52 && pC < 0.18) {
    recommendedStyle = 'editorial' // Colores profundos de sastrería/vino
  } else if (pH > 160 && pH < 205 && pC > 0.16) {
    recommendedStyle = 'cyber' // Cian / Matrix
  } else if (pH >= 20 && pH <= 65) {
    recommendedStyle = 'warm-organic' // Terracota / Ámbar
  } else if (pC < 0.08) {
    recommendedStyle = 'minimal' // Sobrio suizo
  }

  // Mapeo exhaustivo de variables CSS para inyección CSSOM sin violación CSP
  const lightVars: Record<string, string> = {
    '--background': fmtOklch(...bgLightOklch),
    '--foreground': fmtOklch(...textLightOklch),
    '--card': fmtOklch(...cardLightOklch),
    '--card-foreground': fmtOklch(...textLightOklch),
    '--popover': fmtOklch(...cardLightOklch),
    '--popover-foreground': fmtOklch(...textLightOklch),
    '--primary': fmtOklch(pL, pC, pH),
    '--primary-foreground': fmtOklch(...primaryFgOklch),
    '--secondary': fmtOklch(0.935, 0.035, sH),
    '--secondary-foreground': fmtOklch(0.240, 0.060, sH),
    '--muted': fmtOklch(...mutedLightOklch),
    '--muted-foreground': fmtOklch(...mutedFgLightOklch),
    '--border': fmtOklch(...borderLightOklch),
    '--input': fmtOklch(...borderLightOklch),
    '--ring': fmtOklch(pL, pC, pH),
    '--accent': fmtOklch(0.650, 0.180, aH),
    '--accent-foreground': fmtOklch(0.990, 0.005, aH),
    '--accent-1': fmtOklch(0.650, 0.180, aH),
    '--accent-1-foreground': fmtOklch(0.990, 0.005, aH),
    '--accent-2': fmtOklch(0.620, 0.190, sH),
    '--accent-2-foreground': fmtOklch(0.990, 0.005, sH),
    '--accent-3': fmtOklch(0.550, 0.190, (pH + 140) % 360),
    '--accent-3-foreground': fmtOklch(0.990, 0.005, (pH + 140) % 360),
    '--brand-accent': fmtOklch(pL, pC, pH),
    '--brand-accent-foreground': fmtOklch(...primaryFgOklch),
    '--border-shadow': '#0a101d',
    '--sidebar': fmtOklch(...bgLightOklch),
    '--sidebar-foreground': fmtOklch(...textLightOklch),
    '--sidebar-primary': fmtOklch(pL, pC, pH),
    '--sidebar-primary-foreground': fmtOklch(...primaryFgOklch),
    '--sidebar-accent': fmtOklch(...mutedLightOklch),
    '--sidebar-accent-foreground': fmtOklch(pL, pC, pH),
    '--sidebar-border': fmtOklch(...borderLightOklch),
    '--sidebar-ring': fmtOklch(pL, pC, pH),
  }

  const darkVars: Record<string, string> = {
    '--background': fmtOklch(...bgDarkOklch),
    '--foreground': fmtOklch(...textDarkOklch),
    '--card': fmtOklch(...cardDarkOklch),
    '--card-foreground': fmtOklch(...textDarkOklch),
    '--popover': fmtOklch(...cardDarkOklch),
    '--popover-foreground': fmtOklch(...textDarkOklch),
    '--primary': fmtOklch(Math.min(0.78, Math.max(0.65, pL * 1.25)), pC, pH),
    '--primary-foreground': fmtOklch(0.120, 0.030, pH),
    '--secondary': fmtOklch(0.240, 0.050, sH),
    '--secondary-foreground': fmtOklch(0.960, 0.020, sH),
    '--muted': fmtOklch(...mutedDarkOklch),
    '--muted-foreground': fmtOklch(...mutedFgDarkOklch),
    '--border': fmtOklch(...borderDarkOklch),
    '--input': fmtOklch(...borderDarkOklch),
    '--ring': fmtOklch(Math.min(0.78, Math.max(0.65, pL * 1.25)), pC, pH),
    '--accent': fmtOklch(0.740, 0.170, aH),
    '--accent-foreground': fmtOklch(0.120, 0.030, aH),
    '--accent-1': fmtOklch(0.740, 0.170, aH),
    '--accent-1-foreground': fmtOklch(0.120, 0.030, aH),
    '--accent-2': fmtOklch(0.720, 0.180, sH),
    '--accent-2-foreground': fmtOklch(0.120, 0.030, sH),
    '--accent-3': fmtOklch(0.700, 0.180, (pH + 140) % 360),
    '--accent-3-foreground': fmtOklch(0.120, 0.030, (pH + 140) % 360),
    '--brand-accent': fmtOklch(Math.min(0.78, Math.max(0.65, pL * 1.25)), pC, pH),
    '--brand-accent-foreground': fmtOklch(0.120, 0.030, pH),
    '--border-shadow': '#000000',
    '--sidebar': fmtOklch(...bgDarkOklch),
    '--sidebar-foreground': fmtOklch(...textDarkOklch),
    '--sidebar-primary': fmtOklch(Math.min(0.78, Math.max(0.65, pL * 1.25)), pC, pH),
    '--sidebar-primary-foreground': fmtOklch(0.120, 0.030, pH),
    '--sidebar-accent': fmtOklch(...mutedDarkOklch),
    '--sidebar-accent-foreground': fmtOklch(Math.min(0.78, Math.max(0.65, pL * 1.25)), pC, pH),
    '--sidebar-border': fmtOklch(...borderDarkOklch),
    '--sidebar-ring': fmtOklch(Math.min(0.78, Math.max(0.65, pL * 1.25)), pC, pH),
  }

  return {
    name,
    dominantHue: pH,
    primaryHex: rgbToHex(...primRgb),
    secondaryHex: rgbToHex(...secRgb),
    accentHex: rgbToHex(...accRgb),
    bgLightHex: oklchToHex(...bgLightOklch),
    bgDarkHex: oklchToHex(...bgDarkOklch),
    cardLightHex: oklchToHex(...cardLightOklch),
    cardDarkHex: oklchToHex(...cardDarkOklch),
    textLightHex: oklchToHex(...textLightOklch),
    textDarkHex: oklchToHex(...textDarkOklch),
    borderLightHex: oklchToHex(...borderLightOklch),
    borderDarkHex: oklchToHex(...borderDarkOklch),
    lightVars,
    darkVars,
    wcagContrastText: textContrast,
    wcagContrastButton: btnContrast,
    recommendedStyleId: recommendedStyle,
  }
}

// ============================================================================
// 7. LOGOTIPOS DEMO DE EJEMPLO PARA PRUEBAS RÁPIDAS
// ============================================================================
export const SAMPLE_BRAND_LOGOS = [
  {
    name: 'Tech SaaS Cloud (Cian & Violeta)',
    category: 'Software & Cloud',
    dataUrl:
      'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120"><rect width="120" height="120" rx="24" fill="%230F172A"/><path d="M35 75 L60 30 L85 75 Z" fill="%230EA5E9"/><circle cx="60" cy="55" r="16" fill="%23A855F7"/><circle cx="85" cy="75" r="8" fill="%2310B981"/></svg>',
  },
  {
    name: 'Panadería & Café Artesanal (Miel & Terracota)',
    category: 'Hostelería & Artesanía',
    dataUrl:
      'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120"><rect width="120" height="120" rx="24" fill="%23FFFBEB"/><circle cx="60" cy="60" r="42" fill="%23D97706"/><path d="M40 70 Q60 35 80 70 Z" fill="%23EA580C"/><circle cx="60" cy="48" r="10" fill="%23FBBF24"/></svg>',
  },
  {
    name: 'Bufete & Consultoría Luxe (Borgoña & Oro)',
    category: 'Legal & Finanzas',
    dataUrl:
      'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120"><rect width="120" height="120" rx="12" fill="%231C1113"/><circle cx="60" cy="60" r="40" stroke="%23CA8A04" stroke-width="4" fill="none"/><path d="M42 45 L78 45 L60 78 Z" fill="%23881337"/><circle cx="60" cy="60" r="8" fill="%23CA8A04"/></svg>',
  },
  {
    name: 'Cyber Security Táctico (Verde Matrix & Neón)',
    category: 'Ciberseguridad',
    dataUrl:
      'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120"><rect width="120" height="120" rx="6" fill="%23050C12"/><path d="M60 25 L90 40 L90 70 L60 95 L30 70 L30 40 Z" fill="none" stroke="%2306B6D4" stroke-width="5"/><circle cx="60" cy="60" r="15" fill="%2322C55E"/></svg>',
  },
]
