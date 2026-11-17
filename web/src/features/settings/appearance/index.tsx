import { useState, useEffect, useRef } from 'react'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Switch } from '@/components/ui/switch'
import { Input } from '@/components/ui/input'
import {
  FileSpreadsheet,
  Check,
  Sun,
  Moon,
  Laptop,
  QrCode,
  Building,
  Receipt,
  Sparkles,
  Palette,
  Upload,
  CheckCircle2,
  RotateCcw,
  Layers,
  ArrowRight,
  ImageIcon,
  Wand2,
  Sliders,
  Terminal,
  Shapes,
  ShieldCheck,
} from 'lucide-react'
import { toast } from 'sonner'
import { useTheme } from '@/context/theme-provider'
import {
  useBillingStore,
  billingActions,
  type InvoiceTemplateConfig,
} from '@/lib/billing-store'
import {
  extractLogoColors,
  generateCustomPaletteFromColors,
  SAMPLE_BRAND_LOGOS,
  type SystemStyleId,
  type SystemStyle,
  type SystemPalette,
  type SystemThemeCombo,
  type LogoColorAnalysis,
  type CustomPaletteConfig,
} from '@/lib/color-analyzer'

// ============================================================================
// COMPONENTE: MINIATURA DE PLANTILLAS DE FACTURA (MODERN / CLASSIC / COMPACT)
// ============================================================================
interface ThumbnailProps {
  style: InvoiceTemplateConfig['style']
  companyName: string
}

function InvoiceTemplateThumbnail({ style, companyName }: ThumbnailProps) {
  if (style === 'modern') {
    return (
      <div className='relative w-full h-44 rounded-md border border-border/70 bg-card p-2.5 flex flex-col justify-between overflow-hidden shadow-2xs select-none'>
        <div className='flex items-start justify-between gap-1'>
          <div className='space-y-0.5'>
            <div className='flex items-center gap-1'>
              <div className='h-2.5 w-2.5 rounded bg-primary/80' />
              <span className='text-[10px] font-bold tracking-tight text-foreground truncate max-w-[80px]'>
                {companyName || 'Payé Base'}
              </span>
            </div>
            <div className='h-1 w-12 bg-muted-foreground/20 rounded' />
          </div>
          <div className='text-right'>
            <span className='text-[8px] font-mono px-1 py-0.5 rounded bg-muted text-muted-foreground font-semibold'>
              FAC-2026-001
            </span>
            <div className='text-[7px] text-muted-foreground mt-0.5'>01/11/2026</div>
          </div>
        </div>

        <div className='space-y-1 my-auto'>
          <div className='flex justify-between border-b border-border/40 pb-0.5 text-[7px] font-semibold text-muted-foreground uppercase tracking-wider'>
            <span>Concepto</span>
            <span>Total</span>
          </div>
          <div className='flex justify-between text-[7.5px] text-foreground/80 py-0.5 border-b border-dashed border-border/40'>
            <span className='truncate max-w-[100px]'>Desarrollo & Consultoría</span>
            <span className='font-mono'>850,00 €</span>
          </div>
          <div className='flex justify-between text-[7.5px] text-foreground/80 py-0.5'>
            <span className='truncate max-w-[100px]'>Mantenimiento Cloud</span>
            <span className='font-mono'>150,00 €</span>
          </div>
        </div>

        <div className='flex items-end justify-between pt-1 border-t border-border/50'>
          <div className='flex items-center gap-1'>
            <QrCode className='h-5 w-5 text-primary shrink-0' />
            <div className='space-y-0.5'>
              <div className='text-[6.5px] font-mono font-medium text-primary'>
                VERI*FACTU
              </div>
              <div className='h-0.5 w-8 bg-primary/30 rounded' />
            </div>
          </div>
          <div className='rounded bg-foreground text-background px-2 py-1 text-right min-w-[70px]'>
            <div className='text-[6px] uppercase tracking-wider opacity-80'>Total Factura</div>
            <div className='text-[10px] font-extrabold font-mono leading-none mt-0.5'>
              1.210,00 €
            </div>
          </div>
        </div>
      </div>
    )
  }

  if (style === 'classic') {
    return (
      <div className='relative w-full h-44 rounded-md border border-border/70 bg-card p-2.5 flex flex-col justify-between overflow-hidden shadow-2xs select-none'>
        <div className='border-b border-border/80 pb-1.5 flex items-start justify-between'>
          <div className='space-y-0.5'>
            <div className='flex items-center gap-1'>
              <Building className='h-3 w-3 text-primary' />
              <span className='text-[9.5px] font-bold text-foreground truncate max-w-[80px]'>
                {companyName || 'Payé Base'}
              </span>
            </div>
            <div className='text-[7px] text-muted-foreground'>NIF: B-12345678</div>
          </div>
          <div className='border border-border/80 rounded px-1.5 py-0.5 text-center bg-muted/30'>
            <div className='text-[6px] font-bold uppercase text-muted-foreground'>FACTURA OFICIAL</div>
            <div className='text-[8px] font-mono font-bold text-foreground'>2026/A-001</div>
          </div>
        </div>

        <div className='grid grid-cols-2 gap-1 my-1'>
          <div className='rounded border border-border/60 bg-muted/15 p-1'>
            <div className='text-[6px] font-bold uppercase text-muted-foreground'>Emisor</div>
            <div className='text-[7px] text-foreground truncate font-medium'>{companyName || 'Payé Base'}</div>
            <div className='text-[6px] text-muted-foreground truncate'>Madrid, España</div>
          </div>
          <div className='rounded border border-border/60 bg-muted/15 p-1'>
            <div className='text-[6px] font-bold uppercase text-muted-foreground'>Cliente Destinatario</div>
            <div className='text-[7px] text-foreground truncate font-medium'>Comercial Ibérica SL</div>
            <div className='text-[6px] text-muted-foreground truncate'>NIF: B-87654321</div>
          </div>
        </div>

        <div className='border-t border-border/80 pt-1 flex items-center justify-between'>
          <div className='space-y-0.5'>
            <div className='text-[6.5px] font-mono text-muted-foreground'>Base: 1.000,00 €</div>
            <div className='text-[6.5px] font-mono text-muted-foreground'>IVA (21%): 210,00 €</div>
          </div>
          <div className='text-right'>
            <div className='text-[6px] uppercase text-muted-foreground font-bold'>Total a Pagar</div>
            <div className='text-[10px] font-mono font-bold text-foreground'>1.210,00 €</div>
          </div>
        </div>
      </div>
    )
  }

  // Compact Style
  return (
    <div className='relative w-full h-44 rounded-md border border-border/70 bg-card p-2 flex flex-col justify-between overflow-hidden shadow-2xs select-none font-mono'>
      <div className='text-center border-b border-dashed border-border/80 pb-1 space-y-0.5'>
        <div className='text-[9px] font-extrabold uppercase tracking-tight text-foreground truncate'>
          {companyName || 'Payé Base'}
        </div>
        <div className='text-[6.5px] text-muted-foreground'>COMPROBANTE COMERCIAL SIMPLIFICADO</div>
        <div className='text-[6.5px] text-muted-foreground'>TIK-2026-9042 · 01/11/2026 14:32</div>
      </div>

      <div className='space-y-0.5 my-auto text-[7px]'>
        <div className='flex justify-between text-foreground/90'>
          <span>1x SERVICIO GESTIÓN</span>
          <span>45,00 €</span>
        </div>
        <div className='flex justify-between text-foreground/90'>
          <span>1x LICENCIA CLOUD MES</span>
          <span>25,00 €</span>
        </div>
        <div className='flex justify-between text-muted-foreground pt-0.5 border-t border-dashed border-border/60'>
          <span>IVA INCLUIDO (21%)</span>
          <span>12,15 €</span>
        </div>
      </div>

      <div className='border-t border-dashed border-border/80 pt-1 flex items-center justify-between'>
        <div className='flex items-center gap-1'>
          <QrCode className='h-4 w-4 text-primary shrink-0' />
          <span className='text-[6px] text-primary font-bold'>AEAT QR</span>
        </div>
        <div className='text-right'>
          <span className='text-[6px] text-muted-foreground block uppercase'>Total Ticket</span>
          <span className='text-[10px] font-bold text-foreground leading-none'>70,00 €</span>
        </div>
      </div>
    </div>
  )
}

// ============================================================================
// COMPONENTE: PREVISUALIZACIÓN DE ESTILO REAL PARA LOS 6 TEMAS COMPLETOS
// ============================================================================
interface RealStyleThumbnailProps {
  combo: SystemThemeCombo
  isDark: boolean
}

function RealStyleThumbnail({ combo, isDark: _isDark }: RealStyleThumbnailProps) {
  const { id } = combo

  // 1. Aurora Glass: Glassmorphism 16px blur, curvas 20px, sombras suaves difusas, gradiente radial
  if (id === 'aurora') {
    return (
      <div className='relative w-full h-44 rounded-[20px] p-3 flex flex-col justify-between overflow-hidden border border-cyan-500/25 dark:border-cyan-400/20 shadow-md bg-gradient-to-br from-cyan-500/10 via-purple-500/10 to-emerald-500/10 backdrop-blur-md select-none transition-all'>
        <div className='flex items-center justify-between'>
          <div className='flex items-center gap-1.5'>
            <div className='h-2.5 w-2.5 rounded-full bg-cyan-400 shadow-[0_0_8px_rgba(6,182,212,0.8)]' />
            <span className='text-[11px] font-bold tracking-tight text-foreground'>
              Panel Aurora
            </span>
          </div>
          <span className='text-[8px] font-medium px-2 py-0.5 rounded-full bg-cyan-500/15 text-cyan-600 dark:text-cyan-300 border border-cyan-500/20'>
            Glassmorphism 16px
          </span>
        </div>

        <div className='p-2 rounded-[14px] bg-background/50 border border-white/20 dark:border-white/10 shadow-xs backdrop-blur-sm space-y-1'>
          <div className='flex justify-between items-center text-[8px] text-muted-foreground'>
            <span>Facturación del mes</span>
            <span className='text-emerald-500 font-semibold'>+18.4%</span>
          </div>
          <div className='text-xs font-bold text-foreground'>4.820,00 €</div>
        </div>

        <div className='flex items-center justify-between pt-1'>
          <div className='flex items-center gap-1'>
            <span className='text-[7.5px] text-muted-foreground'>Acentos:</span>
            <div className='flex items-center gap-1'>
              <span className='h-2 w-2 rounded-full bg-[#06B6D4]' title='Cian Boreal' />
              <span className='h-2 w-2 rounded-full bg-[#8B5CF6]' title='Violeta Aurora' />
              <span className='h-2 w-2 rounded-full bg-[#10B981]' title='Esmeralda Flúor' />
            </div>
          </div>
          <div className='h-5 px-2 rounded-full bg-cyan-500 text-white text-[8px] font-semibold flex items-center justify-center shadow-xs'>
            Curva 20px
          </div>
        </div>
      </div>
    )
  }

  // 2. Neo-Brutalismo: Bordes 2.5px duros, sombra desplazada sin blur (4px 4px 0px), 0px radius, mayúsculas impact
  if (id === 'brutalist') {
    return (
      <div className='relative w-full h-44 rounded-none p-3 flex flex-col justify-between overflow-hidden border-[2.5px] border-black dark:border-white shadow-[4px_4px_0px_0px_#0a101d] dark:shadow-[4px_4px_0px_0px_#ffffff] bg-[#FEF9C3] dark:bg-[#1C1917] select-none transition-all'>
        <div className='flex items-center justify-between'>
          <span className='text-[10.5px] font-black uppercase tracking-wider text-black dark:text-white'>
            REGISTRO TÉCNICO
          </span>
          <span className='text-[8px] font-black uppercase px-1.5 py-0.5 bg-black text-white dark:bg-white dark:text-black'>
            0PX RADIUS
          </span>
        </div>

        <div className='p-2 rounded-none bg-white dark:bg-zinc-900 border-[2px] border-black dark:border-white shadow-[2px_2px_0px_0px_#000] dark:shadow-[2px_2px_0px_0px_#fff] space-y-1'>
          <div className='flex justify-between items-center text-[8px] font-bold text-black dark:text-white uppercase'>
            <span>TOTAL VENTAS</span>
            <span className='text-amber-600 dark:text-amber-400'>ACTIVO</span>
          </div>
          <div className='text-xs font-black text-black dark:text-white'>8.140,00 €</div>
        </div>

        <div className='flex items-center justify-between pt-1'>
          <div className='flex items-center gap-1'>
            <span className='text-[7.5px] font-bold text-black dark:text-white'>ACENTOS:</span>
            <div className='flex items-center gap-1'>
              <span className='h-2.5 w-2.5 rounded-none bg-[#FBBF24] border border-black' title='Amarillo Canario' />
              <span className='h-2.5 w-2.5 rounded-none bg-[#1D4ED8] border border-black' title='Azul Cobalto' />
              <span className='h-2.5 w-2.5 rounded-none bg-[#EA580C] border border-black' title='Naranja Fuego' />
            </div>
          </div>
          <div className='h-5 px-2 rounded-none bg-black text-white dark:bg-white dark:text-black text-[8px] font-black uppercase flex items-center justify-center border border-black'>
            CLIC FÍSICO
          </div>
        </div>
      </div>
    )
  }

  // 3. Editorial Luxe: Tipografía con serif en títulos, líneas finas 1px, 6px radius, micro-sombras sutiles
  if (id === 'editorial') {
    return (
      <div className='relative w-full h-44 rounded-[6px] p-3 flex flex-col justify-between overflow-hidden border border-amber-900/25 dark:border-amber-500/20 shadow-xs bg-[#FAF5EE] dark:bg-[#1A1215] select-none transition-all'>
        <div className='flex items-center justify-between border-b border-amber-900/15 dark:border-amber-500/15 pb-1'>
          <span className='text-[12px] font-serif italic font-bold tracking-normal text-amber-950 dark:text-amber-100'>
            Colección & Finanzas
          </span>
          <span className='text-[7.5px] font-serif uppercase tracking-widest text-amber-800 dark:text-amber-300'>
            Edición Privada
          </span>
        </div>

        <div className='p-2 rounded-[4px] bg-white/70 dark:bg-zinc-900/70 border border-amber-900/20 dark:border-amber-500/20 space-y-1 shadow-2xs'>
          <div className='flex justify-between items-center text-[7.5px] font-serif text-muted-foreground uppercase tracking-wider'>
            <span>Balance de Cuentas</span>
            <span className='text-amber-700 dark:text-amber-400 italic'>Verificado</span>
          </div>
          <div className='text-xs font-serif font-bold text-foreground'>12.450,00 €</div>
        </div>

        <div className='flex items-center justify-between pt-1'>
          <div className='flex items-center gap-1'>
            <span className='text-[7.5px] font-serif text-muted-foreground'>Acentos:</span>
            <div className='flex items-center gap-1'>
              <span className='h-2 w-2 rounded-full bg-[#881337]' title='Borgoña Profundo' />
              <span className='h-2 w-2 rounded-full bg-[#CA8A04]' title='Oro Champagne' />
              <span className='h-2 w-2 rounded-full bg-[#059669]' title='Salvia Satinado' />
            </div>
          </div>
          <div className='h-5 px-2 rounded-[4px] bg-rose-950 text-amber-200 text-[8px] font-serif font-medium flex items-center justify-center border border-amber-600/30'>
            Grabado 1px
          </div>
        </div>
      </div>
    )
  }

  // 4. Minimal Bauhaus: Funcionalismo suizo, 4px radius, 1px borde, cero sombras, rejilla precisa
  if (id === 'minimal') {
    return (
      <div className='relative w-full h-44 rounded-[4px] p-3 flex flex-col justify-between overflow-hidden border border-zinc-300 dark:border-zinc-700 shadow-none bg-[#F4F4F5] dark:bg-[#18181B] select-none transition-all'>
        <div className='flex items-center justify-between border-b border-zinc-200 dark:border-zinc-800 pb-1'>
          <span className='text-[10px] font-mono font-bold tracking-tight text-foreground'>
            SISTEMA BÁSICO
          </span>
          <span className='text-[7.5px] font-mono uppercase px-1 py-0.2 rounded-[2px] bg-zinc-200 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300'>
            0 Sombras
          </span>
        </div>

        <div className='p-2 rounded-[2px] bg-white dark:bg-zinc-900 border border-zinc-300 dark:border-zinc-700 space-y-1'>
          <div className='flex justify-between items-center text-[7.5px] font-mono text-muted-foreground'>
            <span>REGISTRO DIARIO</span>
            <span className='text-red-600 font-bold'>ESTRICTO</span>
          </div>
          <div className='text-xs font-mono font-bold text-foreground'>3.290,00 €</div>
        </div>

        <div className='flex items-center justify-between pt-1'>
          <div className='flex items-center gap-1'>
            <span className='text-[7.5px] font-mono text-muted-foreground'>PIGMENTOS:</span>
            <div className='flex items-center gap-1'>
              <span className='h-2 w-2 rounded-[1px] bg-[#DC2626]' title='Rojo Suizo' />
              <span className='h-2 w-2 rounded-[1px] bg-[#2563EB]' title='Azul Ultramar' />
              <span className='h-2 w-2 rounded-[1px] bg-[#D97706]' title='Amarillo Ocre' />
            </div>
          </div>
          <div className='h-5 px-2 rounded-[2px] bg-red-600 text-white text-[8px] font-mono font-bold flex items-center justify-center'>
            4px Radius
          </div>
        </div>
      </div>
    )
  }

  // 5. Cyber Matrix: Consola táctica, bisel 3px, cuadrícula digital, resplandor neón (glow), monospace
  if (id === 'cyber') {
    return (
      <div className='relative w-full h-44 rounded-none p-3 flex flex-col justify-between overflow-hidden border border-cyan-500/50 shadow-[0_0_15px_rgba(6,182,212,0.25)] bg-[#050C12] select-none transition-all'>
        <div className='flex items-center justify-between border-b border-cyan-500/30 pb-1'>
          <span className='text-[10px] font-mono font-bold text-cyan-400 flex items-center gap-1'>
            <Terminal className='h-3 w-3 text-cyan-400' />
            <span>// CONSOLA_TÁCTICA</span>
          </span>
          <span className='text-[7.5px] font-mono uppercase px-1 py-0.2 bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'>
            SYS_ONLINE
          </span>
        </div>

        <div className='p-2 bg-[#081520] border border-cyan-500/40 space-y-1 shadow-[inset_0_0_10px_rgba(6,182,212,0.15)]'>
          <div className='flex justify-between items-center text-[7.5px] font-mono text-cyan-300/80'>
            <span>TELEMETRÍA CRIPTOGRÁFICA</span>
            <span className='text-pink-400 font-bold'>ENLACE OK</span>
          </div>
          <div className='text-xs font-mono font-bold text-cyan-300'>9.870,00 €</div>
        </div>

        <div className='flex items-center justify-between pt-1'>
          <div className='flex items-center gap-1'>
            <span className='text-[7.5px] font-mono text-cyan-500/80'>NEÓN:</span>
            <div className='flex items-center gap-1'>
              <span className='h-2 w-2 rounded-full bg-[#06B6D4] shadow-[0_0_6px_#06B6D4]' title='Cian Láser' />
              <span className='h-2 w-2 rounded-full bg-[#EC4899] shadow-[0_0_6px_#EC4899]' title='Magenta Neón' />
              <span className='h-2 w-2 rounded-full bg-[#22C55E] shadow-[0_0_6px_#22C55E]' title='Verde Terminal' />
            </div>
          </div>
          <div className='h-5 px-2 bg-cyan-500 text-black text-[8px] font-mono font-extrabold flex items-center justify-center shadow-[0_0_8px_#06B6D4]'>
            GLOW ACTIVO
          </div>
        </div>
      </div>
    )
  }

  // 6. Cálido Orgánico: Curvas cojín 24px, botones píldora, sombras cálidas flotantes (34px blur), terracota/miel/musgo
  return (
    <div className='relative w-full h-44 rounded-[24px] p-3.5 flex flex-col justify-between overflow-hidden border border-amber-600/25 shadow-[0_12px_28px_rgba(194,65,12,0.12)] bg-[#FDF8F3] dark:bg-[#1A1412] select-none transition-all'>
      <div className='flex items-center justify-between'>
        <div className='flex items-center gap-1.5'>
          <div className='h-2.5 w-2.5 rounded-full bg-orange-600' />
          <span className='text-[11px] font-bold tracking-tight text-foreground'>
            Obrador & Artesanía
          </span>
        </div>
        <span className='text-[8px] font-medium px-2 py-0.5 rounded-full bg-orange-500/15 text-orange-700 dark:text-orange-300'>
          Cojín 24px
        </span>
      </div>

      <div className='p-2.5 rounded-[18px] bg-white/80 dark:bg-zinc-900/80 border border-orange-500/20 shadow-xs space-y-1'>
        <div className='flex justify-between items-center text-[8px] text-muted-foreground'>
          <span>Producción artesanal</span>
          <span className='text-amber-600 font-semibold'>Calidad A+</span>
        </div>
        <div className='text-xs font-bold text-foreground'>5.320,00 €</div>
      </div>

      <div className='flex items-center justify-between pt-1'>
        <div className='flex items-center gap-1'>
          <span className='text-[7.5px] text-muted-foreground'>Acentos:</span>
          <div className='flex items-center gap-1'>
            <span className='h-2 w-2 rounded-full bg-[#C2410C]' title='Terracota Caliza' />
            <span className='h-2 w-2 rounded-full bg-[#D97706]' title='Miel Silvestre' />
            <span className='h-2 w-2 rounded-full bg-[#15803D]' title='Musgo Verde' />
          </div>
        </div>
        <div className='h-5 px-2.5 rounded-full bg-orange-600 text-white text-[8px] font-semibold flex items-center justify-center shadow-xs'>
          Flotante 34px
        </div>
      </div>
    </div>
  )
}

// ============================================================================
// COMPONENTE PRINCIPAL: SETTINGS APPEARANCE
// ============================================================================
export function SettingsAppearance() {
  const { template, company } = useBillingStore()
  const {
    theme,
    setTheme,
    resolvedTheme,
    style,
    setStyle,
    activeStyleDef,
    systemStyles,
    palette,
    setPalette,
    activePaletteDef,
    systemPalettes,
    systemThemes,
    applyTheme,
    customPalette,
    setCustomPalette,
    resetTheme,
  } = useTheme()

  // Estado para el logo en análisis
  const [currentLogoUrl, setCurrentLogoUrl] = useState<string>(() => {
    if (company.logoWebpDataUrl) return company.logoWebpDataUrl
    return SAMPLE_BRAND_LOGOS[0].dataUrl
  })

  const [isAnalyzing, setIsAnalyzing] = useState(false)
  const [logoAnalysisResult, setLogoAnalysisResult] = useState<LogoColorAnalysis | null>(null)

  // Colores extraídos y editables interactivamente para la Fase 2
  const [editableColors, setEditableColors] = useState<{
    primary: string
    secondary: string
    accent: string
  }>({
    primary: '#0EA5E9',
    secondary: '#A855F7',
    accent: '#10B981',
  })

  // Estilo seleccionado para aplicar la paleta del logo
  const [targetStyleForLogo, setTargetStyleForLogo] = useState<SystemStyleId>('aurora')

  // Paleta personalizada calculada dinámicamente en tiempo real
  const [previewCustomConfig, setPreviewCustomConfig] = useState<CustomPaletteConfig | null>(null)

  const fileInputRef = useRef<HTMLInputElement | null>(null)

  // Ejecutar K-Means sobre el logo actual
  const runColorAnalysis = async (imgSource: string) => {
    setIsAnalyzing(true)
    try {
      const analysis = await extractLogoColors(imgSource)
      setLogoAnalysisResult(analysis)

      // Actualizar colores editables
      const newColors = {
        primary: analysis.primary.hex,
        secondary: analysis.secondary.hex,
        accent: analysis.accent.hex,
      }
      setEditableColors(newColors)

      // Calcular paleta de neutros tintados
      const config = generateCustomPaletteFromColors(
        newColors.primary,
        newColors.secondary,
        newColors.accent,
        company.nombreComercial ? `Marca ${company.nombreComercial}` : 'Paleta de tu Marca'
      )
      setPreviewCustomConfig(config)
      setTargetStyleForLogo(config.recommendedStyleId)

      toast.success(
        `Pigmentos detectados con éxito (Tono dominante ${Math.round(analysis.dominantHue)}°). Se generaron neutros tintados con contraste WCAG AA.`
      )
    } catch (err) {
      toast.error('No se pudo analizar el logotipo seleccionado.')
      console.error(err)
    } finally {
      setIsAnalyzing(false)
    }
  }

  // Recalcular neutros tintados cuando el usuario ajusta un color manualmente
  const handleColorChange = (role: 'primary' | 'secondary' | 'accent', hexValue: string) => {
    const updated = { ...editableColors, [role]: hexValue }
    setEditableColors(updated)
    const config = generateCustomPaletteFromColors(
      updated.primary,
      updated.secondary,
      updated.accent,
      company.nombreComercial ? `Marca ${company.nombreComercial}` : 'Paleta Personalizada'
    )
    setPreviewCustomConfig(config)
  }

  // Al montar, analizar automáticamente el logo inicial si aún no se ha analizado
  useEffect(() => {
    if (!logoAnalysisResult) {
      const initialLogo = company.logoWebpDataUrl || currentLogoUrl
      runColorAnalysis(initialLogo)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Manejar subida de archivo para análisis en vivo
  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    const reader = new FileReader()
    reader.onload = (event) => {
      const dataUrl = event.target?.result as string
      if (dataUrl) {
        setCurrentLogoUrl(dataUrl)
        runColorAnalysis(dataUrl)
      }
    }
    reader.readAsDataURL(file)
  }

  // Guardar y activar la paleta personalizada desde logo
  const handleApplyCustomLogoTheme = () => {
    if (!previewCustomConfig) return
    setCustomPalette(previewCustomConfig)
    setStyle(targetStyleForLogo)
    setPalette('custom')
    toast.success(
      `¡Paleta de tu logotipo activada con éxito sobre el estilo "${
        systemStyles.find((s) => s.id === targetStyleForLogo)?.name
      }"!`
    )
  }

  const invoiceStyles: {
    id: InvoiceTemplateConfig['style']
    title: string
    desc: string
    badge: string
    icon: typeof Building
  }[] = [
    {
      id: 'modern',
      title: 'Moderna / Minimalista',
      desc: 'Encabezado asimétrico limpio, caja de totales contrastada y QR VERI*FACTU destacado.',
      badge: 'Startups & Servicios',
      icon: Sparkles,
    },
    {
      id: 'classic',
      title: 'Clásica / Corporativa',
      desc: 'Estructura cuadriculada con doble recuadro emisor/receptor y membrete formal.',
      badge: 'Tradicional Gestoría',
      icon: Building,
    },
    {
      id: 'compact',
      title: 'Compacta / Ticket A5',
      desc: 'Aprovechamiento denso del papel con separadores de puntos y QR centrado.',
      badge: 'Comercio / TPV',
      icon: Receipt,
    },
  ]

  return (
    <div className='space-y-8'>
      {/* ------------------------------------------------------------- */}
      {/* CABECERA: MODO DE ILUMINACIÓN Y ESTADO DE DOS CAPAS */}
      {/* ------------------------------------------------------------- */}
      <Card className='border border-border/80 shadow-xs overflow-hidden'>
        <CardHeader className='pb-4 border-b border-border/60 bg-muted/15'>
          <div className='flex flex-col lg:flex-row lg:items-center justify-between gap-4'>
            <div>
              <div className='flex items-center gap-2'>
                <div className='p-1.5 rounded-lg bg-primary/10 text-primary'>
                  <Shapes className='h-4 w-4' />
                </div>
                <CardTitle className='text-base font-semibold'>
                  Sistema de Temas de Dos Capas (Estilo + Paleta)
                </CardTitle>
                <Badge
                  variant='outline'
                  className='border-primary/30 bg-primary/5 text-primary text-[10px] uppercase tracking-wider font-semibold'
                >
                  OKLCH Perceptual · WCAG AA
                </Badge>
              </div>
              <CardDescription className='text-xs mt-1 leading-relaxed'>
                Desacoplamiento total: la capa de <strong>Estilo</strong> gobierna la forma física (radios, bordes, sombras, tipografía y fondos), mientras que la capa de <strong>Paleta</strong> define la armonía cromática y los neutros tintados.
              </CardDescription>
            </div>

            {/* Selector de Modo (Claro / Oscuro / Sistema) y Reset */}
            <div className='flex flex-wrap items-center gap-2'>
              <div className='flex items-center gap-1 p-1 rounded-lg border bg-card shrink-0'>
                <Button
                  type='button'
                  variant={theme === 'light' ? 'default' : 'ghost'}
                  size='sm'
                  className='h-8 text-xs gap-1.5 px-3 font-medium'
                  onClick={() => {
                    setTheme('light')
                    toast.success('Modo Claro activado (Superficies de alabastro tintadas).')
                  }}
                >
                  <Sun className='h-3.5 w-3.5' />
                  <span>Modo Claro</span>
                </Button>
                <Button
                  type='button'
                  variant={theme === 'dark' ? 'default' : 'ghost'}
                  size='sm'
                  className='h-8 text-xs gap-1.5 px-3 font-medium'
                  onClick={() => {
                    setTheme('dark')
                    toast.success('Modo Oscuro activado (Superficies obsidiana cromáticas).')
                  }}
                >
                  <Moon className='h-3.5 w-3.5' />
                  <span>Modo Oscuro</span>
                </Button>
                <Button
                  type='button'
                  variant={theme === 'system' ? 'default' : 'ghost'}
                  size='sm'
                  className='h-8 text-xs gap-1.5 px-2.5 font-medium'
                  onClick={() => {
                    setTheme('system')
                    toast.success('Modo adaptativo del Sistema activado.')
                  }}
                >
                  <Laptop className='h-3.5 w-3.5' />
                  <span>Sistema</span>
                </Button>
              </div>

              {(style !== 'aurora' || palette !== 'aurora') && (
                <Button
                  type='button'
                  variant='outline'
                  size='sm'
                  className='h-8 text-xs gap-1 text-muted-foreground hover:text-foreground'
                  onClick={() => {
                    resetTheme()
                    toast.info('Se restableció el tema base Aurora Glass.')
                  }}
                >
                  <RotateCcw className='h-3 w-3' />
                  <span>Restablecer</span>
                </Button>
              )}
            </div>
          </div>
        </CardHeader>

        {/* Resumen de Capas Activas */}
        <div className='px-5 py-3 bg-primary/5 border-b border-border/50 flex flex-wrap items-center justify-between gap-3 text-xs'>
          <div className='flex items-center gap-3'>
            <div className='flex items-center gap-1.5'>
              <span className='text-muted-foreground'>1) Estilo Activo:</span>
              <strong className='text-primary font-bold'>{activeStyleDef.name}</strong>
              <span className='text-muted-foreground/70'>({activeStyleDef.cardRadius})</span>
            </div>
            <span className='text-border'>•</span>
            <div className='flex items-center gap-1.5'>
              <span className='text-muted-foreground'>2) Paleta Activa:</span>
              <div
                className='h-3.5 w-3.5 rounded-full shadow-2xs border border-black/10'
                style={{ backgroundColor: activePaletteDef.primaryHex }}
              />
              <strong className='text-foreground font-bold'>{activePaletteDef.name}</strong>
            </div>
          </div>
          <span className='text-muted-foreground font-mono text-[11px]'>
            Espacio Perceptual OKLCH · {resolvedTheme === 'dark' ? 'Obsidiana Oscura' : 'Alabastro Claro'}
          </span>
        </div>
      </Card>

      {/* ------------------------------------------------------------- */}
      {/* SECCIÓN 1: LOS 6 TEMAS COMPLETOS (ESTRUCTURA RADICAL + 2-3 ACENTOS) */}
      {/* ------------------------------------------------------------- */}
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <div className='flex flex-col sm:flex-row sm:items-center justify-between gap-2'>
            <div>
              <CardTitle className='text-base font-semibold flex items-center gap-2'>
                <Sparkles className='h-4 w-4 text-primary' />
                <span>Los 6 Temas Arquetípicos Completos</span>
              </CardTitle>
              <CardDescription className='text-xs mt-0.5'>
                Temas radicalmente distintos entre sí: cambian radios de esquinas, bordes, sombras, tipografía de títulos, tratamiento de superficie y contienen de 2 a 3 colores de acento.
              </CardDescription>
            </div>
            <Badge variant='outline' className='text-[10px] self-start sm:self-auto font-mono'>
              Estructura Real · 2-3 Acentos
            </Badge>
          </div>
        </CardHeader>

        <CardContent className='space-y-4'>
          <div className='grid sm:grid-cols-2 lg:grid-cols-3 gap-4'>
            {systemThemes.map((combo) => {
              const isActive = style === combo.styleId && palette === combo.paletteId
              return (
                <div
                  key={combo.id}
                  onClick={() => {
                    applyTheme(combo.id)
                    toast.success(`Tema completo "${combo.name}" activado en toda la plataforma.`)
                  }}
                  className={`group cursor-pointer rounded-xl border p-3 transition-all space-y-3 relative ${
                    isActive
                      ? 'border-primary bg-primary/5 ring-2 ring-primary/40 shadow-sm'
                      : 'border-border/80 hover:border-primary/50 hover:bg-muted/20 hover:shadow-xs'
                  }`}
                >
                  {/* Vista Previa del Estilo Real (Formas, Sombras y Tipografía reales) */}
                  <div className='relative'>
                    <RealStyleThumbnail combo={combo} isDark={resolvedTheme === 'dark'} />
                    {isActive && (
                      <div className='absolute top-2 right-2 z-10'>
                        <Badge className='h-5 px-2 text-[10px] gap-1 shadow-xs bg-primary text-primary-foreground'>
                          <Check className='h-3 w-3 stroke-[3]' /> Activo
                        </Badge>
                      </div>
                    )}
                  </div>

                  {/* Ficha Descriptiva del Tema */}
                  <div className='space-y-1 pt-1'>
                    <div className='flex items-center justify-between gap-1'>
                      <span className='text-xs font-bold text-foreground group-hover:text-primary transition-colors'>
                        {combo.name}
                      </span>
                      <span className='text-[9px] text-muted-foreground font-medium px-1.5 py-0.5 rounded bg-muted/60'>
                        {combo.badge}
                      </span>
                    </div>
                    <p className='text-[10.5px] text-primary/90 font-medium truncate'>
                      {combo.tagline}
                    </p>
                    <p className='text-[10.5px] text-muted-foreground leading-relaxed line-clamp-2'>
                      {combo.description}
                    </p>
                  </div>

                  {/* Fila de los 2-3 Colores de Acento Reales */}
                  <div className='p-2 rounded-lg bg-muted/30 border border-border/50 space-y-1.5'>
                    <span className='text-[9.5px] font-semibold text-muted-foreground block'>
                      Colores de Acento del Tema:
                    </span>
                    <div className='flex items-center gap-1.5 flex-wrap'>
                      {combo.accents.map((acc, idx) => (
                        <div
                          key={idx}
                          className='flex items-center gap-1 text-[9px] px-1.5 py-0.5 rounded bg-background/80 border'
                          title={acc.name}
                        >
                          <div
                            className='h-2.5 w-2.5 rounded-full border border-black/10 shrink-0'
                            style={{ backgroundColor: acc.hex }}
                          />
                          <span className='text-foreground/90 font-medium truncate max-w-[80px]'>
                            {acc.name.split(' ')[0]}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* Botón de Aplicar */}
                  <Button
                    type='button'
                    size='sm'
                    variant={isActive ? 'default' : 'outline'}
                    className='w-full h-7 text-xs gap-1'
                    onClick={(e) => {
                      e.stopPropagation()
                      applyTheme(combo.id)
                      toast.success(`Tema completo "${combo.name}" activado en toda la plataforma.`)
                    }}
                  >
                    {isActive ? (
                      <>
                        <CheckCircle2 className='h-3.5 w-3.5' />
                        <span>Tema Aplicado</span>
                      </>
                    ) : (
                      <>
                        <ArrowRight className='h-3.5 w-3.5' />
                        <span>Aplicar este tema</span>
                      </>
                    )}
                  </Button>
                </div>
              )
            })}
          </div>
        </CardContent>
      </Card>

      {/* ------------------------------------------------------------- */}
      {/* SECCIÓN 2: ARQUITECTURA DE DOS CAPAS INDEPENDIENTES */}
      {/* ------------------------------------------------------------- */}
      <div className='grid lg:grid-cols-2 gap-6'>
        {/* Capa 1: Selector de Estilo (Estructura, Bordes, Sombras) */}
        <Card className='border border-border/80 shadow-xs'>
          <CardHeader className='pb-3'>
            <CardTitle className='text-sm font-semibold flex items-center gap-2'>
              <Shapes className='h-4 w-4 text-primary' />
              <span>Capa 1: Estilo de Diseño (Forma & Estructura)</span>
            </CardTitle>
            <CardDescription className='text-xs'>
              Define el lenguaje físico: radios de tarjeta, grosor de borde, sombras, tipografía y fondos. No altera los colores.
            </CardDescription>
          </CardHeader>
          <CardContent className='space-y-2.5'>
            {systemStyles.map((s: SystemStyle) => {
              const isSelected = style === s.id
              return (
                <div
                  key={s.id}
                  onClick={() => {
                    setStyle(s.id)
                    toast.success(`Estilo de forma "${s.name}" activado.`)
                  }}
                  className={`p-3 rounded-lg border cursor-pointer transition-all flex items-start justify-between gap-3 ${
                    isSelected
                      ? 'border-primary bg-primary/5 ring-1 ring-primary'
                      : 'border-border/70 hover:border-primary/50 hover:bg-muted/20'
                  }`}
                >
                  <div className='space-y-1 min-w-0'>
                    <div className='flex items-center gap-2'>
                      <span className='text-xs font-bold text-foreground'>{s.name}</span>
                      <Badge variant='outline' className='text-[9px] h-4.5 px-1 font-mono'>
                        {s.cardRadius}
                      </Badge>
                      {isSelected && (
                        <Badge className='text-[9px] h-4.5 px-1.5 bg-primary text-primary-foreground'>
                          Activo
                        </Badge>
                      )}
                    </div>
                    <p className='text-[10.5px] text-muted-foreground leading-tight'>
                      {s.surfaceTreatment} · Borde {s.borderWidth} · {s.shadowType}
                    </p>
                    <p className='text-[9.5px] text-primary/80 font-mono'>
                      Títulos: {s.headingFontName}
                    </p>
                  </div>
                  <Button
                    type='button'
                    size='sm'
                    variant={isSelected ? 'default' : 'ghost'}
                    className='h-7 text-xs shrink-0'
                    onClick={(e) => {
                      e.stopPropagation()
                      setStyle(s.id)
                      toast.success(`Estilo de forma "${s.name}" activado.`)
                    }}
                  >
                    {isSelected ? 'Elegido' : 'Elegir'}
                  </Button>
                </div>
              )
            })}
          </CardContent>
        </Card>

        {/* Capa 2: Selector de Paleta (Color OKLCH) */}
        <Card className='border border-border/80 shadow-xs'>
          <CardHeader className='pb-3'>
            <CardTitle className='text-sm font-semibold flex items-center gap-2'>
              <Palette className='h-4 w-4 text-primary' />
              <span>Capa 2: Paleta de Color (OKLCH con 2-3 Acentos)</span>
            </CardTitle>
            <CardDescription className='text-xs'>
              Define el matiz cromático de fondos, tarjetas y botones. Puede combinarse libremente con cualquier estilo.
            </CardDescription>
          </CardHeader>
          <CardContent className='space-y-2.5 max-h-[520px] overflow-y-auto pr-1'>
            {/* Si existe paleta personalizada desde logo, mostrarla primero */}
            {customPalette && (
              <div
                onClick={() => {
                  setPalette('custom')
                  toast.success('Paleta personalizada de tu logotipo activada.')
                }}
                className={`p-3 rounded-lg border cursor-pointer transition-all flex items-center justify-between gap-3 ${
                  palette === 'custom'
                    ? 'border-primary bg-primary/10 ring-1 ring-primary'
                    : 'border-primary/40 bg-primary/5 hover:border-primary'
                }`}
              >
                <div className='flex items-center gap-2.5 min-w-0'>
                  <div
                    className='h-6 w-6 rounded-full shadow-2xs border border-black/10 shrink-0'
                    style={{ backgroundColor: customPalette.primaryHex }}
                  />
                  <div className='space-y-0.5 truncate'>
                    <div className='flex items-center gap-1.5'>
                      <span className='text-xs font-bold text-foreground truncate'>
                        {customPalette.name}
                      </span>
                      <Badge className='text-[8.5px] h-4 px-1 bg-primary text-primary-foreground'>
                        Logo Custom
                      </Badge>
                    </div>
                    <span className='text-[10px] text-muted-foreground block truncate'>
                      Tono {Math.round(customPalette.dominantHue)}° · Contraste WCAG AA verificado
                    </span>
                  </div>
                </div>
                <Button
                  type='button'
                  size='sm'
                  variant={palette === 'custom' ? 'default' : 'outline'}
                  className='h-7 text-xs shrink-0'
                >
                  {palette === 'custom' ? 'Activa' : 'Aplicar'}
                </Button>
              </div>
            )}

            {/* Paletas del sistema */}
            {systemPalettes.map((p: SystemPalette) => {
              const isSelected = palette === p.id
              return (
                <div
                  key={p.id}
                  onClick={() => {
                    setPalette(p.id)
                    toast.success(`Paleta cromática "${p.name}" activada.`)
                  }}
                  className={`p-2.5 rounded-lg border cursor-pointer transition-all flex items-center justify-between gap-3 ${
                    isSelected
                      ? 'border-primary bg-primary/5 ring-1 ring-primary'
                      : 'border-border/70 hover:border-primary/50 hover:bg-muted/20'
                  }`}
                >
                  <div className='flex items-center gap-2.5 min-w-0'>
                    <div
                      className='h-5 w-5 rounded-full shadow-2xs border border-black/10 shrink-0'
                      style={{ backgroundColor: p.primaryHex }}
                    />
                    <div className='space-y-0.5 truncate'>
                      <div className='flex items-center gap-1.5'>
                        <span className='text-xs font-bold text-foreground truncate'>{p.name}</span>
                        <span className='text-[9px] text-muted-foreground px-1 py-0.2 rounded bg-muted/70'>
                          {p.badge}
                        </span>
                      </div>
                      <span className='text-[10px] text-muted-foreground block truncate'>
                        {p.tagline}
                      </span>
                    </div>
                  </div>

                  <div className='flex items-center gap-1 shrink-0'>
                    {p.accents?.slice(0, 3).map((a, i) => (
                      <span
                        key={i}
                        className='h-2.5 w-2.5 rounded-full border border-black/10'
                        style={{ backgroundColor: a.hex }}
                        title={a.name}
                      />
                    ))}
                  </div>
                </div>
              )
            })}
          </CardContent>
        </Card>
      </div>

      {/* ------------------------------------------------------------- */}
      {/* SECCIÓN 3: FASE 2 - TEMA DESDE LOGOTIPO DE EMPRESA */}
      {/* ------------------------------------------------------------- */}
      <Card className='border border-border/80 shadow-xs overflow-hidden'>
        <CardHeader className='pb-3 border-b border-border/60 bg-muted/15'>
          <div className='flex flex-col sm:flex-row sm:items-center justify-between gap-2'>
            <div>
              <CardTitle className='text-base font-semibold flex items-center gap-2'>
                <Wand2 className='h-4 w-4 text-primary' />
                <span>Fase 2: "Tema desde Logotipo" (K-Means en Canvas & Neutros Tintados)</span>
              </CardTitle>
              <CardDescription className='text-xs mt-0.5'>
                1. Sube tu logo. 2. Extrae colores dominantes ignorando blancos y negros. 3. Asigna roles (Primario, Secundario, Acento). 4. Genera neutros tintados con contraste WCAG AA. 5. Elige el estilo de forma y guárdalo.
              </CardDescription>
            </div>
          </div>
        </CardHeader>

        <CardContent className='p-5 space-y-6'>
          {/* Sub-bloque A: Carga de Logo & Muestra Demo */}
          <div className='grid lg:grid-cols-12 gap-5 items-start p-4 rounded-xl border bg-muted/20'>
            {/* Visor de Logotipo Activo */}
            <div className='lg:col-span-5 space-y-3'>
              <div className='flex items-center justify-between'>
                <span className='text-xs font-semibold text-foreground flex items-center gap-1.5'>
                  <ImageIcon className='h-3.5 w-3.5 text-primary' />
                  <span>Logotipo en Análisis</span>
                </span>
                {company.logoWebpDataUrl && currentLogoUrl === company.logoWebpDataUrl && (
                  <Badge variant='secondary' className='text-[10px] h-4.5 px-1.5'>
                    Logo de tu Empresa
                  </Badge>
                )}
              </div>

              <div className='h-28 w-full rounded-lg border border-border/80 bg-background/80 p-3 flex items-center justify-center relative overflow-hidden shadow-2xs group'>
                {currentLogoUrl ? (
                  <img
                    src={currentLogoUrl}
                    alt='Logotipo analizado'
                    className='max-h-full max-w-full object-contain transition-transform group-hover:scale-105 duration-200'
                  />
                ) : (
                  <div className='text-xs text-muted-foreground flex items-center gap-2'>
                    <Building className='h-4 w-4' />
                    <span>Sin logotipo seleccionado</span>
                  </div>
                )}
              </div>

              {/* Botón de subida de imagen */}
              <div className='flex items-center gap-2'>
                <input
                  ref={fileInputRef}
                  type='file'
                  accept='image/png,image/jpeg,image/svg+xml,image/webp'
                  className='hidden'
                  onChange={handleFileUpload}
                />
                <Button
                  type='button'
                  size='sm'
                  variant='outline'
                  className='w-full h-8 text-xs gap-1.5'
                  disabled={isAnalyzing}
                  onClick={() => fileInputRef.current?.click()}
                >
                  <Upload className='h-3.5 w-3.5' />
                  <span>Subir Logotipo (PNG, SVG, JPG)</span>
                </Button>
              </div>
            </div>

            {/* Selector de Marcas Demo para Pruebas Rápidas */}
            <div className='lg:col-span-7 space-y-2.5'>
              <span className='text-xs font-semibold text-foreground block'>
                O prueba con identidades corporativas de muestra:
              </span>
              <div className='grid sm:grid-cols-2 gap-2'>
                {SAMPLE_BRAND_LOGOS.map((demo, idx) => {
                  const isCurrent = currentLogoUrl === demo.dataUrl
                  return (
                    <button
                      key={idx}
                      type='button'
                      onClick={() => {
                        setCurrentLogoUrl(demo.dataUrl)
                        runColorAnalysis(demo.dataUrl)
                      }}
                      className={`flex items-center gap-2.5 p-2 rounded-lg border text-left transition-all ${
                        isCurrent
                          ? 'border-primary bg-primary/10 ring-1 ring-primary'
                          : 'border-border/70 hover:border-primary/50 hover:bg-card'
                      }`}
                    >
                      <div className='h-8 w-8 rounded-md border bg-background/90 p-1 flex items-center justify-center shrink-0'>
                        <img
                          src={demo.dataUrl}
                          alt={demo.name}
                          className='max-h-full max-w-full object-contain'
                        />
                      </div>
                      <div className='truncate'>
                        <div className='text-[11px] font-semibold text-foreground truncate'>
                          {demo.name.split('(')[0]}
                        </div>
                        <div className='text-[9.5px] text-muted-foreground truncate'>
                          {demo.category}
                        </div>
                      </div>
                    </button>
                  )
                })}
              </div>
            </div>
          </div>

          {/* Sub-bloque B: Extracción K-Means, Ajuste Manual y Previsualización */}
          {previewCustomConfig && (
            <div className='space-y-5 p-4 rounded-xl border border-primary/30 bg-primary/5'>
              <div className='flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-border/50 pb-3'>
                <div>
                  <h4 className='text-xs font-bold text-foreground flex items-center gap-2'>
                    <Sliders className='h-4 w-4 text-primary' />
                    <span>Colores Extraídos y Asignación de Roles (Ajuste Manual)</span>
                  </h4>
                  <p className='text-[11px] text-muted-foreground mt-0.5'>
                    Se ignoraron blancos, negros y grises neutros. Puedes afinar cualquier tono con los selectores de color:
                  </p>
                </div>
                <div className='flex items-center gap-2'>
                  <Badge variant='outline' className='text-[10px] bg-background gap-1'>
                    <ShieldCheck className='h-3 w-3 text-emerald-500' />
                    <span>Texto: {previewCustomConfig.wcagContrastText.toFixed(1)}:1 (AA)</span>
                  </Badge>
                  <Badge variant='outline' className='text-[10px] bg-background gap-1'>
                    <ShieldCheck className='h-3 w-3 text-emerald-500' />
                    <span>Botón: {previewCustomConfig.wcagContrastButton.toFixed(1)}:1 (AA)</span>
                  </Badge>
                </div>
              </div>

              {/* Fila de 3 Colores de Rol con Color-Pickers Interactivos */}
              <div className='grid sm:grid-cols-3 gap-3.5'>
                {/* 1. Primario */}
                <div className='p-3 rounded-lg border bg-card shadow-2xs space-y-2'>
                  <div className='flex items-center justify-between'>
                    <span className='text-xs font-bold text-foreground'>1. Primario</span>
                    <span className='text-[9px] text-muted-foreground'>Más saturado y presente</span>
                  </div>
                  <div className='flex items-center gap-2'>
                    <input
                      type='color'
                      value={editableColors.primary}
                      onChange={(e) => handleColorChange('primary', e.target.value)}
                      className='h-8 w-10 cursor-pointer rounded border border-border bg-transparent p-0.5'
                      title='Ajustar color primario'
                    />
                    <Input
                      type='text'
                      value={editableColors.primary}
                      onChange={(e) => handleColorChange('primary', e.target.value)}
                      className='h-8 font-mono text-xs'
                    />
                  </div>
                  <span className='text-[10px] text-muted-foreground block leading-tight'>
                    Gobierna los botones de acción, sellos y tintado de fondos neutros.
                  </span>
                </div>

                {/* 2. Secundario */}
                <div className='p-3 rounded-lg border bg-card shadow-2xs space-y-2'>
                  <div className='flex items-center justify-between'>
                    <span className='text-xs font-bold text-foreground'>2. Secundario</span>
                    <span className='text-[9px] text-muted-foreground'>Distinto en tono (≥30°)</span>
                  </div>
                  <div className='flex items-center gap-2'>
                    <input
                      type='color'
                      value={editableColors.secondary}
                      onChange={(e) => handleColorChange('secondary', e.target.value)}
                      className='h-8 w-10 cursor-pointer rounded border border-border bg-transparent p-0.5'
                      title='Ajustar color secundario'
                    />
                    <Input
                      type='text'
                      value={editableColors.secondary}
                      onChange={(e) => handleColorChange('secondary', e.target.value)}
                      className='h-8 font-mono text-xs'
                    />
                  </div>
                  <span className='text-[10px] text-muted-foreground block leading-tight'>
                    Aplica a tarjetas auxiliares, filtros de tabla y cabeceras de sección.
                  </span>
                </div>

                {/* 3. Acento */}
                <div className='p-3 rounded-lg border bg-card shadow-2xs space-y-2'>
                  <div className='flex items-center justify-between'>
                    <span className='text-xs font-bold text-foreground'>3. Acento</span>
                    <span className='text-[9px] text-muted-foreground'>Mayor contraste</span>
                  </div>
                  <div className='flex items-center gap-2'>
                    <input
                      type='color'
                      value={editableColors.accent}
                      onChange={(e) => handleColorChange('accent', e.target.value)}
                      className='h-8 w-10 cursor-pointer rounded border border-border bg-transparent p-0.5'
                      title='Ajustar color de acento'
                    />
                    <Input
                      type='text'
                      value={editableColors.accent}
                      onChange={(e) => handleColorChange('accent', e.target.value)}
                      className='h-8 font-mono text-xs'
                    />
                  </div>
                  <span className='text-[10px] text-muted-foreground block leading-tight'>
                    Aplica a insignias de estado, alertas de cobro e indicadores de éxito.
                  </span>
                </div>
              </div>

              {/* Muestra de Neutros Tintados (Alabastro Claro & Obsidiana Oscura) */}
              <div className='p-3 rounded-lg border bg-card/60 space-y-2'>
                <span className='text-[11px] font-semibold text-muted-foreground block'>
                  Neutros Tintados con el Tono Primario (Sin blanco ni negro puro):
                </span>
                <div className='grid grid-cols-2 sm:grid-cols-4 gap-2 text-[10px] font-mono'>
                  <div
                    className='p-2 rounded border flex flex-col justify-between h-14'
                    style={{
                      backgroundColor: previewCustomConfig.bgLightHex,
                      color: previewCustomConfig.textLightHex,
                    }}
                  >
                    <span>Lienzo Claro</span>
                    <span className='font-bold'>{previewCustomConfig.bgLightHex}</span>
                  </div>
                  <div
                    className='p-2 rounded border flex flex-col justify-between h-14'
                    style={{
                      backgroundColor: previewCustomConfig.cardLightHex,
                      color: previewCustomConfig.textLightHex,
                    }}
                  >
                    <span>Tarjeta Clara</span>
                    <span className='font-bold'>{previewCustomConfig.cardLightHex}</span>
                  </div>
                  <div
                    className='p-2 rounded border flex flex-col justify-between h-14'
                    style={{
                      backgroundColor: previewCustomConfig.bgDarkHex,
                      color: previewCustomConfig.textDarkHex,
                    }}
                  >
                    <span>Lienzo Oscuro</span>
                    <span className='font-bold'>{previewCustomConfig.bgDarkHex}</span>
                  </div>
                  <div
                    className='p-2 rounded border flex flex-col justify-between h-14'
                    style={{
                      backgroundColor: previewCustomConfig.cardDarkHex,
                      color: previewCustomConfig.textDarkHex,
                    }}
                  >
                    <span>Tarjeta Obsidiana</span>
                    <span className='font-bold'>{previewCustomConfig.cardDarkHex}</span>
                  </div>
                </div>
              </div>

              {/* Selector del Estilo Destino & Botón de Guardado */}
              <div className='flex flex-col sm:flex-row sm:items-center justify-between gap-4 pt-2 border-t border-border/50'>
                <div className='flex items-center gap-2'>
                  <span className='text-xs font-semibold text-foreground shrink-0'>
                    Aplicar sobre el Estilo:
                  </span>
                  <select
                    value={targetStyleForLogo}
                    onChange={(e) => setTargetStyleForLogo(e.target.value as SystemStyleId)}
                    className='h-8 text-xs rounded-md border border-border bg-background px-2.5 font-medium'
                  >
                    {systemStyles.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name} ({s.cardRadius}){' '}
                        {s.id === previewCustomConfig.recommendedStyleId ? '★ Sugerido' : ''}
                      </option>
                    ))}
                  </select>
                </div>

                <Button
                  type='button'
                  size='sm'
                  className='h-8 text-xs gap-1.5 font-semibold shadow-xs'
                  onClick={handleApplyCustomLogoTheme}
                >
                  <Wand2 className='h-3.5 w-3.5' />
                  <span>Guardar y Aplicar Paleta de tu Logo</span>
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* ------------------------------------------------------------- */}
      {/* SECCIÓN 4: BANCO DE PRUEBAS DE INTERFAZ EN VIVO (SANDBOX) */}
      {/* ------------------------------------------------------------- */}
      <div className='p-4 rounded-xl border border-primary/20 bg-primary/5 space-y-3 shadow-2xs'>
        <div className='flex flex-col sm:flex-row sm:items-center justify-between gap-2'>
          <div className='flex items-center gap-2'>
            <Layers className='h-4 w-4 text-primary' />
            <span className='text-xs font-semibold text-foreground'>
              Previsualización en Vivo de Controles de la Plataforma
            </span>
          </div>
          <span className='text-[11px] text-muted-foreground font-mono'>
            Estilo: <strong>{activeStyleDef.name}</strong> · Paleta:{' '}
            <strong>{activePaletteDef.name}</strong> ({resolvedTheme === 'dark' ? 'Oscuro' : 'Claro'})
          </span>
        </div>

        <div className='grid sm:grid-cols-3 gap-3 pt-1'>
          <div className='p-3 rounded-lg border bg-card shadow-2xs space-y-2'>
            <span className='text-[11px] font-medium text-muted-foreground block'>
              Botones con los Acentos del Tema
            </span>
            <div className='flex flex-wrap gap-2'>
              <Button size='sm' className='h-8 text-xs font-medium'>
                Primario
              </Button>
              <Button size='sm' variant='outline' className='h-8 text-xs font-medium'>
                Secundario
              </Button>
            </div>
          </div>

          <div className='p-3 rounded-lg border bg-card shadow-2xs space-y-2'>
            <span className='text-[11px] font-medium text-muted-foreground block'>
              Campos de Entrada y Foco
            </span>
            <Input
              placeholder='Escribe para probar el foco...'
              className='h-8 text-xs'
              defaultValue='Demo de contraste 2026'
            />
          </div>

          <div className='p-3 rounded-lg border bg-card shadow-2xs space-y-2'>
            <span className='text-[11px] font-medium text-muted-foreground block'>
              Badges y Sellos Veri*Factu
            </span>
            <div className='flex items-center gap-1.5 flex-wrap pt-0.5'>
              <Badge className='bg-primary text-primary-foreground text-[10px] h-5'>
                Factura Emitida
              </Badge>
              <Badge variant='outline' className='text-[10px] h-5 border-primary/40'>
                AEAT Sello OK
              </Badge>
            </div>
          </div>
        </div>
      </div>

      {/* ------------------------------------------------------------- */}
      {/* SECCIÓN 5: DISEÑO DE PLANTILLAS DE FACTURA (MODERN / CLASSIC / COMPACT) */}
      {/* ------------------------------------------------------------- */}
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <CardTitle className='text-base font-semibold flex items-center gap-2'>
            <FileSpreadsheet className='h-4 w-4 text-primary' />
            <span>Diseño de Plantillas de Factura y Documentos</span>
          </CardTitle>
          <CardDescription className='text-xs'>
            Previsualiza en pequeño el estilo visual de tus facturas y selecciona tu plantilla favorita. Se aplicará a la visualización y exportación en PDF.
          </CardDescription>
        </CardHeader>

        <CardContent className='space-y-6'>
          {/* Rejilla de Miniaturas */}
          <div className='grid md:grid-cols-3 gap-4'>
            {invoiceStyles.map((s) => {
              const active = template.style === s.id
              return (
                <div
                  key={s.id}
                  onClick={() => {
                    billingActions.updateTemplate({ style: s.id })
                    toast.success(`Plantilla "${s.title}" activada para emisión y PDF.`)
                  }}
                  className={`group cursor-pointer rounded-xl border p-3 transition-all space-y-3 relative ${
                    active
                      ? 'border-primary bg-primary/5 ring-2 ring-primary/40 shadow-sm'
                      : 'border-border/80 hover:border-primary/50 hover:bg-muted/20 hover:shadow-xs'
                  }`}
                >
                  <div className='relative'>
                    <InvoiceTemplateThumbnail
                      style={s.id}
                      companyName={company.nombreComercial}
                    />
                    {active && (
                      <div className='absolute top-2 right-2 z-10'>
                        <Badge className='h-5 px-2 text-[10px] gap-1 shadow-xs bg-primary text-primary-foreground'>
                          <Check className='h-3 w-3 stroke-[3]' /> Seleccionada
                        </Badge>
                      </div>
                    )}
                  </div>

                  <div className='space-y-1 pt-1'>
                    <div className='flex items-center justify-between gap-1'>
                      <span className='text-xs font-semibold text-foreground group-hover:text-primary transition-colors'>
                        {s.title}
                      </span>
                      <span className='text-[10px] text-muted-foreground font-medium px-1.5 py-0.5 rounded bg-muted/50'>
                        {s.badge}
                      </span>
                    </div>
                    <p className='text-[11px] text-muted-foreground leading-relaxed'>
                      {s.desc}
                    </p>
                  </div>
                </div>
              )
            })}
          </div>

          {/* Interruptores de Elementos Visibles en la Factura */}
          <div className='grid sm:grid-cols-2 gap-3 pt-2 border-t border-border/60'>
            <div className='flex items-center justify-between p-3 rounded-lg border bg-muted/15'>
              <div>
                <p className='text-xs font-medium text-foreground'>
                  Mostrar Logotipo WebP en Cabecera
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Incrusta el logo comprimido de {company.nombreComercial || 'tu empresa'}
                </p>
              </div>
              <Switch
                checked={template.showLogo}
                onCheckedChange={(checked) =>
                  billingActions.updateTemplate({ showLogo: checked })
                }
              />
            </div>

            <div className='flex items-center justify-between p-3 rounded-lg border bg-muted/15'>
              <div>
                <p className='text-xs font-medium text-foreground'>
                  Sello QR Oficial VERI*FACTU
                </p>
                <p className='text-[11px] text-muted-foreground'>
                  Código bidimensional según Orden HFP/1307/2024 de la AEAT
                </p>
              </div>
              <Switch
                checked={template.showQrVerifactu}
                onCheckedChange={(checked) =>
                  billingActions.updateTemplate({ showQrVerifactu: checked })
                }
              />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
