import { createContext, useContext, useEffect, useState, useMemo } from 'react'
import { getCookie, setCookie, removeCookie } from '@/lib/cookies'
import {
  SYSTEM_STYLES,
  SYSTEM_PALETTES,
  SYSTEM_THEMES,
  type SystemStyleId,
  type SystemStyle,
  type SystemPalette,
  type SystemThemeCombo,
  type CustomPaletteConfig,
  type ThemePreset,
  type LogoColorAnalysis,
  matchLogoToPalette,
} from '@/lib/color-analyzer'

type Theme = 'dark' | 'light' | 'system'
type ResolvedTheme = Exclude<Theme, 'system'>

const DEFAULT_THEME = 'system'
const THEME_COOKIE_NAME = 'vite-ui-theme'
const THEME_COOKIE_MAX_AGE = 60 * 60 * 24 * 365 // 1 year
const STORAGE_STYLE_KEY = 'paye_active_style'
const STORAGE_PALETTE_KEY = 'paye_active_palette'
const STORAGE_CUSTOM_PALETTE_KEY = 'paye_custom_palette'
const STORAGE_ANALYSIS_KEY = 'paye_logo_analysis_data'
const DEFAULT_STYLE_ID: SystemStyleId = 'aurora'
const DEFAULT_PALETTE_ID = 'aurora'

type ThemeProviderProps = {
  children: React.ReactNode
  defaultTheme?: Theme
  storageKey?: string
}

export type ThemeProviderState = {
  defaultTheme: Theme
  resolvedTheme: ResolvedTheme
  theme: Theme
  setTheme: (theme: Theme) => void
  resetTheme: () => void

  // Capa 1: Estilo (Forma, Sombras y Tipografía)
  style: SystemStyleId
  setStyle: (styleId: SystemStyleId) => void
  activeStyleDef: SystemStyle
  systemStyles: SystemStyle[]

  // Capa 2: Paleta (Color en OKLCH)
  palette: string
  setPalette: (paletteId: string) => void
  activePaletteDef: SystemPalette
  systemPalettes: SystemPalette[]

  // Temas Completos (Combos Estilo + Paleta)
  systemThemes: SystemThemeCombo[]
  applyTheme: (themeId: string) => void

  // Fase 2: Paleta Personalizada desde Logotipo
  customPalette: CustomPaletteConfig | null
  setCustomPalette: (custom: CustomPaletteConfig | null) => void

  // Retrocompatibilidad con componentes existentes
  activePreset: ThemePreset | null
  setActivePreset: (preset: ThemePreset | null) => void
  availablePresets: ThemePreset[]
  setAvailablePresets: (presets: ThemePreset[]) => void
  logoAnalysis: LogoColorAnalysis | null
  setLogoAnalysis: (analysis: LogoColorAnalysis | null) => void
}

const defaultStyleDef =
  SYSTEM_STYLES.find((s) => s.id === DEFAULT_STYLE_ID) || SYSTEM_STYLES[0]
const defaultPaletteDef =
  SYSTEM_PALETTES.find((p) => p.id === DEFAULT_PALETTE_ID) || SYSTEM_PALETTES[0]

const initialState: ThemeProviderState = {
  defaultTheme: DEFAULT_THEME,
  resolvedTheme: 'light',
  theme: DEFAULT_THEME,
  setTheme: () => null,
  resetTheme: () => null,

  style: DEFAULT_STYLE_ID,
  setStyle: () => null,
  activeStyleDef: defaultStyleDef,
  systemStyles: SYSTEM_STYLES,

  palette: DEFAULT_PALETTE_ID,
  setPalette: () => null,
  activePaletteDef: defaultPaletteDef,
  systemPalettes: SYSTEM_PALETTES,

  systemThemes: SYSTEM_THEMES,
  applyTheme: () => null,

  customPalette: null,
  setCustomPalette: () => null,

  activePreset: null,
  setActivePreset: () => null,
  availablePresets: [],
  setAvailablePresets: () => null,
  logoAnalysis: null,
  setLogoAnalysis: () => null,
}

const ThemeContext = createContext<ThemeProviderState>(initialState)

export function ThemeProvider({
  children,
  defaultTheme = DEFAULT_THEME,
  storageKey = THEME_COOKIE_NAME,
  ...props
}: ThemeProviderProps) {
  const [theme, _setTheme] = useState<Theme>(
    () => (getCookie(storageKey) as Theme) || defaultTheme
  )

  // Capa 1: Estilo (Forma, Sombras y Tipografía)
  const [style, _setStyle] = useState<SystemStyleId>(() => {
    if (typeof window === 'undefined') return DEFAULT_STYLE_ID
    try {
      const stored = localStorage.getItem(STORAGE_STYLE_KEY) as SystemStyleId
      if (stored && SYSTEM_STYLES.some((s) => s.id === stored)) {
        return stored
      }
    } catch {
      // Ignorar fallos de acceso a localStorage
    }
    return DEFAULT_STYLE_ID
  })

  // Capa 2: Paleta (Color)
  const [palette, _setPalette] = useState<string>(() => {
    if (typeof window === 'undefined') return DEFAULT_PALETTE_ID
    try {
      const stored = localStorage.getItem(STORAGE_PALETTE_KEY)
      if (stored === 'custom') return 'custom'
      if (stored && SYSTEM_PALETTES.some((p) => p.id === stored)) {
        return stored
      }
    } catch {
      // Ignorar fallos de acceso a localStorage
    }
    return DEFAULT_PALETTE_ID
  })

  // Paleta personalizada desde Logo
  const [customPalette, _setCustomPalette] = useState<CustomPaletteConfig | null>(() => {
    if (typeof window === 'undefined') return null
    try {
      const stored = localStorage.getItem(STORAGE_CUSTOM_PALETTE_KEY)
      return stored ? (JSON.parse(stored) as CustomPaletteConfig) : null
    } catch {
      return null
    }
  })

  // Análisis de Logotipo
  const [logoAnalysis, _setLogoAnalysis] = useState<LogoColorAnalysis | null>(() => {
    if (typeof window === 'undefined') return null
    try {
      const stored = localStorage.getItem(STORAGE_ANALYSIS_KEY)
      return stored ? (JSON.parse(stored) as LogoColorAnalysis) : null
    } catch {
      return null
    }
  })

  const [availablePresets, setAvailablePresets] = useState<ThemePreset[]>([])

  // Definición activa de Estilo
  const activeStyleDef = useMemo(() => {
    return SYSTEM_STYLES.find((s) => s.id === style) || defaultStyleDef
  }, [style])

  // Definición activa de Paleta
  const activePaletteDef = useMemo((): SystemPalette => {
    if (palette === 'custom' && customPalette) {
      return {
        id: 'custom',
        name: customPalette.name || 'Paleta desde Logotipo',
        title: 'Generada con OKLCH tintado',
        category: 'Personalizada',
        tagline: 'Paleta extraída de tu identidad corporativa',
        description: `Extraída con K-Means perceptual (Tono dominante ${Math.round(customPalette.dominantHue)}°). Fondos alabastro y obsidiana con contraste WCAG AA verificado.`,
        hue: customPalette.dominantHue,
        primaryHex: customPalette.primaryHex,
        secondaryHex: customPalette.secondaryHex,
        badge: 'Logo Custom',
        accents: [
          { name: 'Primario Logo', hex: customPalette.primaryHex, role: 'primary' },
          { name: 'Secundario Armónico', hex: customPalette.secondaryHex, role: 'secondary' },
          { name: 'Acento Alto Contraste', hex: customPalette.accentHex, role: 'accent-1' },
        ],
        preview: {
          primaryLight: customPalette.primaryHex,
          primaryDark: customPalette.primaryHex,
          bgLight: customPalette.bgLightHex,
          bgDark: customPalette.bgDarkHex,
          cardLight: customPalette.cardLightHex,
          cardDark: customPalette.cardDarkHex,
          textLight: customPalette.textLightHex,
          textDark: customPalette.textDarkHex,
          borderLight: customPalette.borderLightHex,
          borderDark: customPalette.borderDarkHex,
        },
      }
    }
    return SYSTEM_PALETTES.find((p) => p.id === palette) || defaultPaletteDef
  }, [palette, customPalette])

  // Preset activo sintético para retrocompatibilidad
  const activePreset = useMemo((): ThemePreset => {
    return {
      id: activePaletteDef.id,
      name: activePaletteDef.name,
      tagline: activePaletteDef.tagline,
      description: activePaletteDef.description,
      badge: activePaletteDef.badge,
      dominantHue: activePaletteDef.hue,
      paletteId: activePaletteDef.id,
      preview: activePaletteDef.preview,
    }
  }, [activePaletteDef])

  // Memoize resolved theme (dark / light)
  const resolvedTheme = useMemo((): ResolvedTheme => {
    if (theme === 'system') {
      if (typeof window === 'undefined') return 'light'
      return window.matchMedia('(prefers-color-scheme: dark)').matches
        ? 'dark'
        : 'light'
    }
    return theme as ResolvedTheme
  }, [theme])

  // Sincronizar clases 'dark' y 'light' en documentElement
  useEffect(() => {
    if (typeof document === 'undefined') return
    const root = document.documentElement
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')

    const applyThemeClass = (currentResolvedTheme: ResolvedTheme) => {
      root.classList.remove('light', 'dark')
      root.classList.add(currentResolvedTheme)
    }

    const handleChange = () => {
      if (theme === 'system') {
        const systemTheme = mediaQuery.matches ? 'dark' : 'light'
        applyThemeClass(systemTheme)
      }
    }

    applyThemeClass(resolvedTheme)
    mediaQuery.addEventListener('change', handleChange)
    return () => mediaQuery.removeEventListener('change', handleChange)
  }, [theme, resolvedTheme])

  // Sincronizar atributos data-style y data-palette en documentElement (100% CSP-Compliant)
  useEffect(() => {
    if (typeof document === 'undefined') return
    const root = document.documentElement
    root.setAttribute('data-style', style)
    root.setAttribute('data-palette', palette)

    // Si la paleta es 'custom' y hay variables personalizadas, inyectarlas mediante CSSOM directo
    if (palette === 'custom' && customPalette) {
      const vars = resolvedTheme === 'dark' ? customPalette.darkVars : customPalette.lightVars
      Object.entries(vars).forEach(([key, val]) => {
        root.style.setProperty(key, val)
      })
    } else {
      // Limpiar propiedades CSSOM personalizadas si no es 'custom'
      if (customPalette) {
        Object.keys(customPalette.lightVars).forEach((key) => {
          root.style.removeProperty(key)
        })
      }
    }

    // Limpiar cualquier estilo inline dinámico legacy para evitar violaciones de CSP
    const oldStyleEl = document.getElementById('paye-dynamic-theme-style')
    if (oldStyleEl) {
      oldStyleEl.remove()
    }
  }, [style, palette, customPalette, resolvedTheme])

  const setTheme = (newTheme: Theme) => {
    setCookie(storageKey, newTheme, THEME_COOKIE_MAX_AGE)
    _setTheme(newTheme)
  }

  const setStyle = (styleId: SystemStyleId) => {
    const valid = SYSTEM_STYLES.some((s) => s.id === styleId)
    const target = valid ? styleId : DEFAULT_STYLE_ID
    _setStyle(target)
    try {
      localStorage.setItem(STORAGE_STYLE_KEY, target)
    } catch {
      // Ignorar fallos de cuota
    }
    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('data-style', target)
    }
  }

  const setPalette = (paletteId: string) => {
    const isCustom = paletteId === 'custom' && customPalette !== null
    const valid = isCustom || SYSTEM_PALETTES.some((p) => p.id === paletteId)
    const targetId = valid ? paletteId : DEFAULT_PALETTE_ID
    _setPalette(targetId)
    try {
      localStorage.setItem(STORAGE_PALETTE_KEY, targetId)
    } catch {
      // Ignorar fallos de cuota
    }
    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('data-palette', targetId)
    }
  }

  const applyTheme = (themeId: string) => {
    const combo = SYSTEM_THEMES.find((t) => t.id === themeId)
    if (combo) {
      setStyle(combo.styleId)
      setPalette(combo.paletteId)
    } else {
      const sMatch = SYSTEM_STYLES.find((s) => s.id === themeId)
      if (sMatch) setStyle(sMatch.id)
      const pMatch = SYSTEM_PALETTES.find((p) => p.id === themeId)
      if (pMatch) setPalette(pMatch.id)
    }
  }

  const setCustomPalette = (custom: CustomPaletteConfig | null) => {
    _setCustomPalette(custom)
    try {
      if (custom) {
        localStorage.setItem(STORAGE_CUSTOM_PALETTE_KEY, JSON.stringify(custom))
      } else {
        localStorage.removeItem(STORAGE_CUSTOM_PALETTE_KEY)
      }
    } catch {
      // Ignorar fallos de cuota
    }
  }

  const resetTheme = () => {
    removeCookie(storageKey)
    _setTheme(DEFAULT_THEME)
    setStyle(DEFAULT_STYLE_ID)
    setPalette(DEFAULT_PALETTE_ID)
    setCustomPalette(null)
    try {
      localStorage.removeItem(STORAGE_STYLE_KEY)
      localStorage.removeItem(STORAGE_PALETTE_KEY)
      localStorage.removeItem(STORAGE_CUSTOM_PALETTE_KEY)
      localStorage.removeItem(STORAGE_ANALYSIS_KEY)
    } catch {
      // Ignorar fallos
    }
  }

  const setActivePreset = (preset: ThemePreset | null) => {
    if (!preset) {
      setPalette(DEFAULT_PALETTE_ID)
      return
    }
    const targetId = preset.paletteId || preset.id
    const match = SYSTEM_PALETTES.find((p) => p.id === targetId)
    if (match) {
      setPalette(match.id)
    } else if (preset.dominantHue !== undefined) {
      const matched = matchLogoToPalette(preset.dominantHue)
      setPalette(matched.id)
    } else {
      setPalette(DEFAULT_PALETTE_ID)
    }
  }

  const setLogoAnalysis = (analysis: LogoColorAnalysis | null) => {
    _setLogoAnalysis(analysis)
    try {
      if (analysis) {
        localStorage.setItem(STORAGE_ANALYSIS_KEY, JSON.stringify(analysis))
      } else {
        localStorage.removeItem(STORAGE_ANALYSIS_KEY)
      }
    } catch {
      // Ignorar fallos de cuota localStorage
    }
  }

  const contextValue: ThemeProviderState = {
    defaultTheme,
    resolvedTheme,
    resetTheme,
    theme,
    setTheme,

    style,
    setStyle,
    activeStyleDef,
    systemStyles: SYSTEM_STYLES,

    palette,
    setPalette,
    activePaletteDef,
    systemPalettes: SYSTEM_PALETTES,

    systemThemes: SYSTEM_THEMES,
    applyTheme,

    customPalette,
    setCustomPalette,

    activePreset,
    setActivePreset,
    availablePresets,
    setAvailablePresets,
    logoAnalysis,
    setLogoAnalysis,
  }

  return (
    <ThemeContext value={contextValue} {...props}>
      {children}
    </ThemeContext>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export const useTheme = () => {
  const context = useContext(ThemeContext)

  if (!context) throw new Error('useTheme must be used within a ThemeProvider')

  return context
}
