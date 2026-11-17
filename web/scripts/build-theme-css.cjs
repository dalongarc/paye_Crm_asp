const fs = require('fs');
const path = require('path');

// ============================================================================
// 1. DEFINICIÓN DE LOS 6 ESTILOS DE DISEÑO (ESTRUCTURA, FORMA Y TIPOGRAFÍA)
// ============================================================================
const styles = [
  {
    id: 'aurora',
    name: 'Aurora Glass',
    tagline: 'Glassmorphism translúcido, halos difusos y curvas orgánicas',
    radiusCard: '1.25rem', // 20px
    radiusBtn: '9999px',   // Pill
    radiusInput: '0.75rem',// 12px
    radiusBadge: '9999px',
    borderWidthCard: '1px',
    borderWidthBtn: '1px',
    borderWidthInput: '1px',
    borderStyle: 'solid',
    shadowCardLight: '0 8px 32px 0 rgba(31, 38, 135, 0.08), 0 2px 8px 0 rgba(0, 0, 0, 0.04)',
    shadowCardDark: '0 8px 32px 0 rgba(0, 0, 0, 0.35), 0 0 16px -2px oklch(from var(--primary) l c h / 0.15)',
    shadowCardHoverLight: '0 16px 48px -4px rgba(31, 38, 135, 0.14), 0 4px 16px 0 rgba(0, 0, 0, 0.06)',
    shadowCardHoverDark: '0 16px 48px -4px rgba(0, 0, 0, 0.50), 0 0 24px 0px oklch(from var(--primary) l c h / 0.25)',
    shadowBtn: '0 4px 14px -2px oklch(from var(--primary) l c h / 0.25)',
    cardBackdropBlur: 'blur(16px)',
    cardSurfaceLight: 'linear-gradient(135deg, oklch(from var(--card) l c h / 0.88) 0%, oklch(from var(--card) l c h / 0.70) 100%)',
    cardSurfaceDark: 'linear-gradient(135deg, oklch(from var(--card) l c h / 0.85) 0%, oklch(from var(--card) l c h / 0.65) 100%)',
    fontHeading: "'Geist Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
    fontHeadingWeight: '700',
    fontHeadingTracking: '-0.025em',
    fontHeadingTransform: 'none',
    fontBtn: "'Geist Sans', sans-serif",
    letterSpacingBtn: '-0.01em',
    textTransformBtn: 'none',
    letterSpacingBadge: '0.01em',
    textTransformBadge: 'none',
    fontWeightBadge: '500',
    densityCardPadding: '1.75rem',
    densityGap: '1.5rem',
    appBackgroundLight: 'radial-gradient(ellipse at 10% 10%, oklch(from var(--accent-1) l c h / 0.08) 0%, transparent 50%), radial-gradient(ellipse at 90% 90%, oklch(from var(--accent-2) l c h / 0.08) 0%, transparent 50%), var(--background)',
    appBackgroundDark: 'radial-gradient(ellipse at 15% 15%, oklch(from var(--accent-1) l c h / 0.12) 0%, transparent 60%), radial-gradient(ellipse at 85% 85%, oklch(from var(--accent-2) l c h / 0.12) 0%, transparent 60%), var(--background)',
    effectsHoverTransform: 'translateY(-2px)',
    glowAccent: 'oklch(from var(--primary) l c h / 0.30)',
  },
  {
    id: 'brutalist',
    name: 'Neo-Brutalismo',
    tagline: 'Bordes gruesos, sombras duras desplazadas y tipografía impact',
    radiusCard: '0px',
    radiusBtn: '0px',
    radiusInput: '0px',
    radiusBadge: '0px',
    borderWidthCard: '2.5px',
    borderWidthBtn: '2px',
    borderWidthInput: '2px',
    borderStyle: 'solid',
    shadowCardLight: '4px 4px 0px 0px var(--border-shadow, #0f172a)',
    shadowCardDark: '4px 4px 0px 0px var(--border-shadow, #000000)',
    shadowCardHoverLight: '6px 6px 0px 0px var(--border-shadow, #0f172a)',
    shadowCardHoverDark: '6px 6px 0px 0px var(--border-shadow, #000000)',
    shadowBtn: '3px 3px 0px 0px var(--border-shadow, #0f172a)',
    cardBackdropBlur: 'none',
    cardSurfaceLight: 'var(--card)',
    cardSurfaceDark: 'var(--card)',
    fontHeading: "'Geist Sans', -apple-system, BlinkMacSystemFont, Impact, sans-serif",
    fontHeadingWeight: '900',
    fontHeadingTracking: '-0.035em',
    fontHeadingTransform: 'uppercase',
    fontBtn: "'Geist Sans', sans-serif",
    letterSpacingBtn: '0.04em',
    textTransformBtn: 'uppercase',
    letterSpacingBadge: '0.08em',
    textTransformBadge: 'uppercase',
    fontWeightBadge: '800',
    densityCardPadding: '1.5rem',
    densityGap: '1.25rem',
    appBackgroundLight: 'radial-gradient(circle, oklch(from var(--border) l c h / 0.35) 1.5px, transparent 1.5px) 0 0 / 22px 22px, var(--background)',
    appBackgroundDark: 'radial-gradient(circle, oklch(from var(--border) l c h / 0.40) 1.5px, transparent 1.5px) 0 0 / 22px 22px, var(--background)',
    effectsHoverTransform: 'translate(-2px, -2px)',
    glowAccent: 'none',
  },
  {
    id: 'editorial',
    name: 'Editorial Luxe',
    tagline: 'Elegancia clásica, tipografía serif en títulos y líneas finas de grabado',
    radiusCard: '0.375rem', // 6px
    radiusBtn: '0.25rem',   // 4px
    radiusInput: '0.25rem',
    radiusBadge: '0.25rem',
    borderWidthCard: '1px',
    borderWidthBtn: '1px',
    borderWidthInput: '1px',
    borderStyle: 'solid',
    shadowCardLight: '0 1px 3px 0 rgba(0, 0, 0, 0.04), 0 8px 24px -10px rgba(0, 0, 0, 0.03)',
    shadowCardDark: '0 1px 3px 0 rgba(0, 0, 0, 0.30), 0 8px 24px -10px rgba(0, 0, 0, 0.20)',
    shadowCardHoverLight: '0 2px 8px 0 rgba(0, 0, 0, 0.06), 0 14px 30px -10px rgba(0, 0, 0, 0.06)',
    shadowCardHoverDark: '0 2px 8px 0 rgba(0, 0, 0, 0.40), 0 14px 30px -10px rgba(0, 0, 0, 0.28)',
    shadowBtn: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
    cardBackdropBlur: 'none',
    cardSurfaceLight: 'var(--card)',
    cardSurfaceDark: 'var(--card)',
    fontHeading: "'Playfair Display', Georgia, Cambria, 'Times New Roman', serif",
    fontHeadingWeight: '600',
    fontHeadingTracking: '0.015em',
    fontHeadingTransform: 'none',
    fontBtn: "'Geist Sans', sans-serif",
    letterSpacingBtn: '0.04em',
    textTransformBtn: 'none',
    letterSpacingBadge: '0.08em',
    textTransformBadge: 'uppercase',
    fontWeightBadge: '600',
    densityCardPadding: '2rem',
    densityGap: '1.75rem',
    appBackgroundLight: 'var(--background)',
    appBackgroundDark: 'var(--background)',
    effectsHoverTransform: 'none',
    glowAccent: 'none',
  },
  {
    id: 'minimal',
    name: 'Minimal Bauhaus',
    tagline: 'Funcionalismo geométrico suizo, cero sombras superfluas y rejilla limpia',
    radiusCard: '0.25rem', // 4px
    radiusBtn: '0.25rem',
    radiusInput: '0.25rem',
    radiusBadge: '0.125rem',
    borderWidthCard: '1px',
    borderWidthBtn: '1px',
    borderWidthInput: '1px',
    borderStyle: 'solid',
    shadowCardLight: 'none',
    shadowCardDark: 'none',
    shadowCardHoverLight: 'none',
    shadowCardHoverDark: 'none',
    shadowBtn: 'none',
    cardBackdropBlur: 'none',
    cardSurfaceLight: 'var(--card)',
    cardSurfaceDark: 'var(--card)',
    fontHeading: "'Geist Sans', -apple-system, BlinkMacSystemFont, 'Helvetica Neue', Arial, sans-serif",
    fontHeadingWeight: '600',
    fontHeadingTracking: '-0.03em',
    fontHeadingTransform: 'none',
    fontBtn: "'Geist Sans', sans-serif",
    letterSpacingBtn: '-0.01em',
    textTransformBtn: 'none',
    letterSpacingBadge: '0.02em',
    textTransformBadge: 'uppercase',
    fontWeightBadge: '600',
    densityCardPadding: '1.25rem',
    densityGap: '1rem',
    appBackgroundLight: 'var(--background)',
    appBackgroundDark: 'var(--background)',
    effectsHoverTransform: 'none',
    glowAccent: 'none',
  },
  {
    id: 'cyber',
    name: 'Cyber Matrix',
    tagline: 'Consola táctica de instrumentación, cuadrícula digital y resplandor neón',
    radiusCard: '0.1875rem', // 3px
    radiusBtn: '0.1875rem',
    radiusInput: '0.1875rem',
    radiusBadge: '0.125rem',
    borderWidthCard: '1.5px',
    borderWidthBtn: '1.5px',
    borderWidthInput: '1.5px',
    borderStyle: 'solid',
    shadowCardLight: '0 0 16px -3px oklch(from var(--primary) l c h / 0.22), inset 0 0 10px -6px oklch(from var(--primary) l c h / 0.15)',
    shadowCardDark: '0 0 20px -2px oklch(from var(--primary) l c h / 0.35), inset 0 0 14px -6px oklch(from var(--primary) l c h / 0.25)',
    shadowCardHoverLight: '0 0 26px -1px oklch(from var(--primary) l c h / 0.35), inset 0 0 14px -4px oklch(from var(--primary) l c h / 0.25)',
    shadowCardHoverDark: '0 0 32px 0px oklch(from var(--primary) l c h / 0.50), inset 0 0 18px -4px oklch(from var(--primary) l c h / 0.35)',
    shadowBtn: '0 0 14px -2px oklch(from var(--primary) l c h / 0.40)',
    cardBackdropBlur: 'none',
    cardSurfaceLight: 'linear-gradient(180deg, oklch(from var(--card) l c h / 0.98) 0%, oklch(from var(--card) calc(l * 0.92) c h / 0.98) 100%)',
    cardSurfaceDark: 'linear-gradient(180deg, oklch(from var(--card) l c h / 0.95) 0%, oklch(from var(--card) calc(l * 0.85) c h / 0.95) 100%)',
    fontHeading: "'Geist Mono', ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace",
    fontHeadingWeight: '700',
    fontHeadingTracking: '0.04em',
    fontHeadingTransform: 'none',
    fontBtn: "'Geist Mono', monospace",
    letterSpacingBtn: '0.03em',
    textTransformBtn: 'uppercase',
    letterSpacingBadge: '0.08em',
    textTransformBadge: 'uppercase',
    fontWeightBadge: '700',
    densityCardPadding: '1.25rem',
    densityGap: '1.25rem',
    appBackgroundLight: 'linear-gradient(90deg, oklch(from var(--border) l c h / 0.25) 1px, transparent 1px) 0 0 / 28px 28px, linear-gradient(180deg, oklch(from var(--border) l c h / 0.25) 1px, transparent 1px) 0 0 / 28px 28px, var(--background)',
    appBackgroundDark: 'linear-gradient(90deg, oklch(from var(--border) l c h / 0.25) 1px, transparent 1px) 0 0 / 28px 28px, linear-gradient(180deg, oklch(from var(--border) l c h / 0.25) 1px, transparent 1px) 0 0 / 28px 28px, var(--background)',
    effectsHoverTransform: 'scale(1.004)',
    glowAccent: 'oklch(from var(--primary) l c h / 0.40)',
  },
  {
    id: 'warm-organic',
    name: 'Cálido Orgánico',
    tagline: 'Curvas cojín súper redondeadas, tacto humano reconfortante y sombras flotantes',
    radiusCard: '1.5rem', // 24px
    radiusBtn: '9999px',  // Pill
    radiusInput: '1rem',  // 16px
    radiusBadge: '9999px',
    borderWidthCard: '1px',
    borderWidthBtn: '1px',
    borderWidthInput: '1px',
    borderStyle: 'solid',
    shadowCardLight: '0 14px 34px -10px oklch(from var(--primary) l c h / 0.10), 0 4px 14px -4px oklch(from var(--border) l c h / 0.15)',
    shadowCardDark: '0 14px 34px -10px rgba(0, 0, 0, 0.40), 0 4px 14px -4px oklch(from var(--primary) l c h / 0.15)',
    shadowCardHoverLight: '0 22px 48px -12px oklch(from var(--primary) l c h / 0.16), 0 6px 18px -4px oklch(from var(--border) l c h / 0.20)',
    shadowCardHoverDark: '0 22px 48px -12px rgba(0, 0, 0, 0.55), 0 6px 18px -4px oklch(from var(--primary) l c h / 0.22)',
    shadowBtn: '0 4px 14px -2px oklch(from var(--primary) l c h / 0.22)',
    cardBackdropBlur: 'none',
    cardSurfaceLight: 'var(--card)',
    cardSurfaceDark: 'var(--card)',
    fontHeading: "'Geist Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
    fontHeadingWeight: '600',
    fontHeadingTracking: '-0.01em',
    fontHeadingTransform: 'none',
    fontBtn: "'Geist Sans', sans-serif",
    letterSpacingBtn: '0',
    textTransformBtn: 'none',
    letterSpacingBadge: '0.01em',
    textTransformBadge: 'none',
    fontWeightBadge: '500',
    densityCardPadding: '1.75rem',
    densityGap: '1.5rem',
    appBackgroundLight: 'radial-gradient(ellipse at 50% 0%, oklch(from var(--accent-1) l c h / 0.08) 0%, transparent 60%), var(--background)',
    appBackgroundDark: 'radial-gradient(ellipse at 50% 0%, oklch(from var(--accent-1) l c h / 0.12) 0%, transparent 60%), var(--background)',
    effectsHoverTransform: 'translateY(-2px)',
    glowAccent: 'none',
  },
];

// ============================================================================
// 2. DEFINICIÓN DE LAS PALETAS CROMÁTICAS EN OKLCH (2-3 ACENTOS, WCAG AA)
// ============================================================================
const archetypalPalettes = [
  {
    id: 'aurora',
    name: 'Aurora Boreal',
    title: 'Cian Polar, Violeta Cósmico y Esmeralda Radiante',
    hue: 205,
    primaryLight: 'oklch(0.550 0.180 205)',
    primaryDark: 'oklch(0.720 0.170 205)',
    primaryFgLight: 'oklch(0.990 0.005 205)',
    primaryFgDark: 'oklch(0.120 0.030 205)',
    secondaryLight: 'oklch(0.930 0.030 295)',
    secondaryDark: 'oklch(0.240 0.050 295)',
    secondaryFgLight: 'oklch(0.250 0.080 295)',
    secondaryFgDark: 'oklch(0.950 0.020 295)',
    accent1Light: 'oklch(0.600 0.170 155)', // Esmeralda Polar
    accent1Dark: 'oklch(0.740 0.160 155)',
    accent2Light: 'oklch(0.620 0.210 340)', // Rosa Aurora
    accent2Dark: 'oklch(0.750 0.190 340)',
    accent3Light: 'oklch(0.520 0.190 260)', // Zafiro Abisal
    accent3Dark: 'oklch(0.700 0.180 260)',
    bgLight: 'oklch(0.975 0.014 205)',
    bgDark: 'oklch(0.142 0.032 205)',
    cardLight: 'oklch(0.992 0.008 205)',
    cardDark: 'oklch(0.178 0.035 205)',
    textLight: 'oklch(0.170 0.035 205)',
    textDark: 'oklch(0.965 0.012 205)',
    mutedLight: 'oklch(0.938 0.016 205)',
    mutedDark: 'oklch(0.225 0.025 205)',
    mutedFgLight: 'oklch(0.460 0.032 205)',
    mutedFgDark: 'oklch(0.680 0.025 205)',
    borderLight: 'oklch(0.890 0.022 205)',
    borderDark: 'oklch(0.245 0.030 205)',
    borderShadow: '#091522',
  },
  {
    id: 'brutalist',
    name: 'Neo-Brutalismo Alto Impacto',
    title: 'Amarillo Canario, Cobalto Punch y Naranja Fuego',
    hue: 92,
    primaryLight: 'oklch(0.840 0.190 92)', // Amarillo Canario intenso
    primaryDark: 'oklch(0.850 0.190 92)',
    primaryFgLight: 'oklch(0.100 0.020 92)', // Texto negro para WCAG AAA
    primaryFgDark: 'oklch(0.100 0.020 92)',
    secondaryLight: 'oklch(0.520 0.240 255)', // Cobalto Punch
    secondaryDark: 'oklch(0.680 0.220 255)',
    secondaryFgLight: 'oklch(0.990 0.005 255)',
    secondaryFgDark: 'oklch(0.100 0.020 255)',
    accent1Light: 'oklch(0.660 0.220 45)', // Naranja Fuego
    accent1Dark: 'oklch(0.720 0.200 45)',
    accent2Light: 'oklch(0.800 0.220 135)', // Verde Neón Lima
    accent2Dark: 'oklch(0.820 0.200 135)',
    accent3Light: 'oklch(0.650 0.240 330)', // Fucsia Chicle
    accent3Dark: 'oklch(0.720 0.220 330)',
    bgLight: 'oklch(0.968 0.016 92)',
    bgDark: 'oklch(0.130 0.020 250)',
    cardLight: 'oklch(0.994 0.006 92)',
    cardDark: 'oklch(0.170 0.025 250)',
    textLight: 'oklch(0.100 0.020 250)',
    textDark: 'oklch(0.970 0.010 92)',
    mutedLight: 'oklch(0.930 0.020 92)',
    mutedDark: 'oklch(0.210 0.025 250)',
    mutedFgLight: 'oklch(0.380 0.030 250)',
    mutedFgDark: 'oklch(0.720 0.020 92)',
    borderLight: 'oklch(0.120 0.020 250)', // Borde negro duro en modo claro
    borderDark: 'oklch(0.840 0.190 92)',  // Borde amarillo ácido en modo oscuro
    borderShadow: '#000000',
  },
  {
    id: 'editorial',
    name: 'Editorial Aristocrático',
    title: 'Borgoña Noble, Oro Envejecido y Salvia Clásico',
    hue: 18,
    primaryLight: 'oklch(0.480 0.180 18)', // Borgoña Noble
    primaryDark: 'oklch(0.660 0.160 18)',
    primaryFgLight: 'oklch(0.990 0.005 18)',
    primaryFgDark: 'oklch(0.120 0.030 18)',
    secondaryLight: 'oklch(0.935 0.030 82)', // Marfil dorado
    secondaryDark: 'oklch(0.240 0.040 82)',
    secondaryFgLight: 'oklch(0.250 0.060 82)',
    secondaryFgDark: 'oklch(0.950 0.020 82)',
    accent1Light: 'oklch(0.720 0.140 82)', // Oro Envejecido
    accent1Dark: 'oklch(0.780 0.130 82)',
    accent2Light: 'oklch(0.580 0.110 145)', // Verde Salvia Clásico
    accent2Dark: 'oklch(0.700 0.100 145)',
    accent3Light: 'oklch(0.460 0.120 240)', // Pizarra Azulado
    accent3Dark: 'oklch(0.650 0.110 240)',
    bgLight: 'oklch(0.976 0.012 55)',
    bgDark: 'oklch(0.145 0.025 55)',
    cardLight: 'oklch(0.992 0.006 55)',
    cardDark: 'oklch(0.180 0.030 55)',
    textLight: 'oklch(0.165 0.028 55)',
    textDark: 'oklch(0.965 0.010 55)',
    mutedLight: 'oklch(0.940 0.015 55)',
    mutedDark: 'oklch(0.220 0.025 55)',
    mutedFgLight: 'oklch(0.450 0.025 55)',
    mutedFgDark: 'oklch(0.680 0.020 55)',
    borderLight: 'oklch(0.895 0.018 55)',
    borderDark: 'oklch(0.250 0.025 55)',
    borderShadow: '#1a100d',
  },
  {
    id: 'minimal',
    name: 'Minimal Suizo Bauhaus',
    title: 'Rojo Internacional, Azul Ultramar y Carbón Grafito',
    hue: 25,
    primaryLight: 'oklch(0.560 0.230 25)', // Rojo Suizo Internacional
    primaryDark: 'oklch(0.680 0.220 25)',
    primaryFgLight: 'oklch(0.990 0.005 25)',
    primaryFgDark: 'oklch(0.100 0.020 25)',
    secondaryLight: 'oklch(0.935 0.015 250)',
    secondaryDark: 'oklch(0.230 0.030 250)',
    secondaryFgLight: 'oklch(0.200 0.030 250)',
    secondaryFgDark: 'oklch(0.960 0.010 250)',
    accent1Light: 'oklch(0.480 0.220 260)', // Azul Ultramar
    accent1Dark: 'oklch(0.660 0.200 260)',
    accent2Light: 'oklch(0.800 0.170 85)', // Amarillo Bauhaus
    accent2Dark: 'oklch(0.840 0.160 85)',
    accent3Light: 'oklch(0.380 0.040 250)', // Carbón Grafito
    accent3Dark: 'oklch(0.650 0.030 250)',
    bgLight: 'oklch(0.978 0.005 250)',
    bgDark: 'oklch(0.138 0.015 250)',
    cardLight: 'oklch(0.994 0.003 250)',
    cardDark: 'oklch(0.175 0.020 250)',
    textLight: 'oklch(0.150 0.020 250)',
    textDark: 'oklch(0.965 0.008 250)',
    mutedLight: 'oklch(0.940 0.010 250)',
    mutedDark: 'oklch(0.215 0.020 250)',
    mutedFgLight: 'oklch(0.440 0.020 250)',
    mutedFgDark: 'oklch(0.690 0.015 250)',
    borderLight: 'oklch(0.885 0.012 250)',
    borderDark: 'oklch(0.240 0.020 250)',
    borderShadow: '#0c0f14',
  },
  {
    id: 'cyber',
    name: 'Cyber Neo-Tech',
    tagline: 'Cian Láser, Magenta Sintético y Verde Terminal',
    hue: 195,
    primaryLight: 'oklch(0.620 0.200 195)', // Cian Láser
    primaryDark: 'oklch(0.780 0.210 195)',
    primaryFgLight: 'oklch(0.990 0.005 195)',
    primaryFgDark: 'oklch(0.100 0.030 195)',
    secondaryLight: 'oklch(0.925 0.040 328)',
    secondaryDark: 'oklch(0.240 0.060 328)',
    secondaryFgLight: 'oklch(0.250 0.080 328)',
    secondaryFgDark: 'oklch(0.960 0.020 328)',
    accent1Light: 'oklch(0.640 0.250 328)', // Magenta Sintético
    accent1Dark: 'oklch(0.720 0.240 328)',
    accent2Light: 'oklch(0.680 0.220 142)', // Verde Terminal
    accent2Dark: 'oklch(0.800 0.230 142)',
    accent3Light: 'oklch(0.740 0.190 70)', // Ámbar Alerta
    accent3Dark: 'oklch(0.800 0.180 70)',
    bgLight: 'oklch(0.970 0.016 195)',
    bgDark: 'oklch(0.125 0.035 195)',
    cardLight: 'oklch(0.990 0.008 195)',
    cardDark: 'oklch(0.165 0.040 195)',
    textLight: 'oklch(0.150 0.035 195)',
    textDark: 'oklch(0.965 0.015 195)',
    mutedLight: 'oklch(0.935 0.020 195)',
    mutedDark: 'oklch(0.210 0.035 195)',
    mutedFgLight: 'oklch(0.440 0.035 195)',
    mutedFgDark: 'oklch(0.690 0.025 195)',
    borderLight: 'oklch(0.885 0.025 195)',
    borderDark: 'oklch(0.240 0.040 195)',
    borderShadow: '#05131d',
  },
  {
    id: 'warm-organic',
    name: 'Cálido Orgánico',
    title: 'Terracota Arcilla, Miel Silvestre y Musgo Botánico',
    hue: 42,
    primaryLight: 'oklch(0.560 0.160 42)', // Terracota Arcilla
    primaryDark: 'oklch(0.720 0.150 42)',
    primaryFgLight: 'oklch(0.990 0.005 42)',
    primaryFgDark: 'oklch(0.120 0.030 42)',
    secondaryLight: 'oklch(0.935 0.030 78)', // Miel suave
    secondaryDark: 'oklch(0.240 0.045 78)',
    secondaryFgLight: 'oklch(0.250 0.060 78)',
    secondaryFgDark: 'oklch(0.960 0.020 78)',
    accent1Light: 'oklch(0.720 0.140 78)', // Miel Silvestre
    accent1Dark: 'oklch(0.780 0.130 78)',
    accent2Light: 'oklch(0.580 0.130 130)', // Musgo Botánico
    accent2Dark: 'oklch(0.720 0.120 130)',
    accent3Light: 'oklch(0.720 0.120 55)', // Melocotón Tostado
    accent3Dark: 'oklch(0.780 0.110 55)',
    bgLight: 'oklch(0.974 0.016 42)',
    bgDark: 'oklch(0.146 0.030 42)',
    cardLight: 'oklch(0.992 0.008 42)',
    cardDark: 'oklch(0.182 0.035 42)',
    textLight: 'oklch(0.175 0.032 42)',
    textDark: 'oklch(0.965 0.012 42)',
    mutedLight: 'oklch(0.938 0.018 42)',
    mutedDark: 'oklch(0.220 0.028 42)',
    mutedFgLight: 'oklch(0.450 0.030 42)',
    mutedFgDark: 'oklch(0.680 0.022 42)',
    borderLight: 'oklch(0.890 0.022 42)',
    borderDark: 'oklch(0.245 0.030 42)',
    borderShadow: '#1c110b',
  },
];

// Paletas clásicas de compatibilidad (12 paletas)
const legacyPalettes = [
  { id: 'cobalt', name: 'Cobalto Zafiro', hue: 255, cm: 1.0 },
  { id: 'emerald', name: 'Esmeralda Menta', hue: 155, cm: 1.0 },
  { id: 'amethyst', name: 'Amatista Violeta', hue: 295, cm: 1.0 },
  { id: 'amber', name: 'Ámbar Dorado', hue: 65, cm: 1.0 },
  { id: 'crimson', name: 'Carmesí Rubí', hue: 22, cm: 1.0 },
  { id: 'glacier', name: 'Glaciar Cian', hue: 195, cm: 0.95 },
  { id: 'terracotta', name: 'Terracota Cálido', hue: 40, cm: 1.0 },
  { id: 'fuchsia', name: 'Fucsia Neón', hue: 335, cm: 1.0 },
  { id: 'forest', name: 'Bosque Oliva', hue: 130, cm: 0.95 },
  { id: 'indigo', name: 'Índigo Galáctico', hue: 275, cm: 1.0 },
  { id: 'stone', name: 'Piedra Cálida', hue: 75, cm: 0.65 },
  { id: 'graphite', name: 'Grafito Titanio', hue: 240, cm: 0.55 },
];

function fmt(n) {
  return Number(n.toFixed(3)).toString();
}

let css = `/* ==========================================================================
   PAYÉ CRM 2026 - SISTEMA DE DISEÑO DE DOS CAPAS: ESTILO + PALETA
   100% Estático · Sin Blanco Puro (#FFFFFF) · Sin Negro Puro (#000000)
   Espacio Perceptual Uniforme OKLCH con Contraste WCAG AA/AAA
   100% Conforme con Content-Security-Policy (style-src 'self')
   ========================================================================== */

/* --------------------------------------------------------------------------
   REGLAS GLOBALES Y CONEXIÓN DE TOKENS DE FORMA A COMPONENTES
   -------------------------------------------------------------------------- */
:root {
  /* Tokens por defecto (Aurora) */
  --radius-card: 1.25rem;
  --radius-btn: 9999px;
  --radius-input: 0.75rem;
  --radius-badge: 9999px;
  --border-width-card: 1px;
  --border-width-btn: 1px;
  --border-width-input: 1px;
  --border-style: solid;
  --shadow-card: 0 8px 32px 0 rgba(31, 38, 135, 0.08);
  --shadow-card-hover: 0 16px 48px -4px rgba(31, 38, 135, 0.14);
  --shadow-btn: 0 4px 14px -2px oklch(from var(--primary) l c h / 0.25);
  --card-backdrop-blur: blur(16px);
  --card-surface: linear-gradient(135deg, oklch(from var(--card) l c h / 0.88) 0%, oklch(from var(--card) l c h / 0.70) 100%);
  --font-heading: 'Geist Sans', -apple-system, BlinkMacSystemFont, sans-serif;
  --font-heading-weight: 700;
  --font-heading-tracking: -0.025em;
  --font-heading-transform: none;
  --font-btn: 'Geist Sans', sans-serif;
  --letter-spacing-btn: -0.01em;
  --text-transform-btn: none;
  --letter-spacing-badge: 0.01em;
  --text-transform-badge: none;
  --font-weight-badge: 500;
  --density-card-padding: 1.75rem;
  --density-gap: 1.5rem;
  --app-background: var(--background);
  --effects-hover-transform: translateY(-2px);
  --glow-accent: none;

  /* Aliases para compatibilidad con Tailwind y Shadcn */
  --radius: var(--radius-btn);
  --radius-sm: var(--radius-badge);
  --radius-md: var(--radius-btn);
  --radius-lg: var(--radius-card);
  --radius-xl: var(--radius-card);
  --radius-2xl: calc(var(--radius-card) * 1.25);
}

@theme inline {
  --font-geist: 'Geist Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  --font-sans: 'Geist Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  --font-mono: 'Geist Mono', monospace;
  --font-inter: 'Inter', -apple-system, sans-serif;
  --font-manrope: 'Manrope', -apple-system, sans-serif;

  --radius-sm: var(--radius-badge);
  --radius-md: var(--radius-btn);
  --radius-lg: var(--radius-card);
  --radius-xl: var(--radius-card);
  --radius-2xl: calc(var(--radius-card) * 1.25);

  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-popover: var(--popover);
  --color-popover-foreground: var(--popover-foreground);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-secondary: var(--secondary);
  --color-secondary-foreground: var(--secondary-foreground);
  --color-muted: var(--muted);
  --color-muted-foreground: var(--muted-foreground);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
  --color-accent-1: var(--accent-1);
  --color-accent-1-foreground: var(--accent-1-foreground);
  --color-accent-2: var(--accent-2);
  --color-accent-2-foreground: var(--accent-2-foreground);
  --color-accent-3: var(--accent-3);
  --color-accent-3-foreground: var(--accent-3-foreground);
  --color-destructive: var(--destructive);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
  --color-brand-accent: var(--brand-accent);
  --color-brand-accent-foreground: var(--brand-accent-foreground);
  --color-chart-1: var(--chart-1);
  --color-chart-2: var(--chart-2);
  --color-chart-3: var(--chart-3);
  --color-chart-4: var(--chart-4);
  --color-chart-5: var(--chart-5);
  --color-sidebar: var(--sidebar);
  --color-sidebar-foreground: var(--sidebar-foreground);
  --color-sidebar-primary: var(--sidebar-primary);
  --color-sidebar-primary-foreground: var(--sidebar-primary-foreground);
  --color-sidebar-accent: var(--sidebar-accent);
  --color-sidebar-accent-foreground: var(--sidebar-accent-foreground);
  --color-sidebar-border: var(--sidebar-border);
  --color-sidebar-ring: var(--sidebar-ring);
}

/* Conexión directa a componentes UI */
body {
  background: var(--app-background, var(--background));
  background-attachment: fixed;
  transition: background 0.3s ease;
}

[data-slot="card"], .theme-card {
  border-radius: var(--radius-card) !important;
  border-width: var(--border-width-card) !important;
  border-style: var(--border-style, solid) !important;
  box-shadow: var(--shadow-card) !important;
  backdrop-filter: var(--card-backdrop-blur, none);
  -webkit-backdrop-filter: var(--card-backdrop-blur, none);
  background: var(--card-surface, var(--card));
  transition: transform 0.2s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.2s cubic-bezier(0.16, 1, 0.3, 1), border-color 0.2s ease, background 0.3s ease;
}

[data-slot="card"]:hover, .theme-card:hover {
  box-shadow: var(--shadow-card-hover, var(--shadow-card)) !important;
  transform: var(--effects-hover-transform, none);
}

[data-slot="button"], .theme-btn {
  border-radius: var(--radius-btn) !important;
  border-width: var(--border-width-btn, 1px) !important;
  box-shadow: var(--shadow-btn, none) !important;
  font-family: var(--font-btn, inherit);
  letter-spacing: var(--letter-spacing-btn, normal);
  text-transform: var(--text-transform-btn, none);
  transition: transform 0.15s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.15s ease, background-color 0.15s ease, border-color 0.15s ease;
}

[data-slot="button"]:active, .theme-btn:active {
  transform: scale(0.98);
}

/* Efecto click físico distintivo para Neo-Brutalismo */
:root[data-style="brutalist"] [data-slot="button"]:active,
:root[data-style="brutalist"] .theme-btn:active {
  transform: translate(2px, 2px) !important;
  box-shadow: 1px 1px 0px 0px var(--border-shadow, #000) !important;
}

[data-slot="input"], input:not([type="checkbox"]):not([type="radio"]), select, textarea {
  border-radius: var(--radius-input) !important;
  border-width: var(--border-width-input, 1px) !important;
}

[data-slot="badge"], .theme-badge {
  border-radius: var(--radius-badge) !important;
  letter-spacing: var(--letter-spacing-badge, normal);
  text-transform: var(--text-transform-badge, none);
  font-weight: var(--font-weight-badge, 500);
}

h1, h2, h3, [data-slot="card-title"], .font-heading {
  font-family: var(--font-heading) !important;
  letter-spacing: var(--font-heading-tracking, normal) !important;
  text-transform: var(--font-heading-transform, none) !important;
  font-weight: var(--font-heading-weight, 700) !important;
}

/* ==========================================================================
   CAPA 1: LOS 6 ESTILOS DE DISEÑO (DESIGN TOKENS DE FORMA)
   ========================================================================== */
`;

for (const s of styles) {
  css += `
/* Estilo: ${s.name} */
:root[data-style="${s.id}"]:not(.dark) {
  --radius-card: ${s.radiusCard};
  --radius-btn: ${s.radiusBtn};
  --radius-input: ${s.radiusInput};
  --radius-badge: ${s.radiusBadge};
  --border-width-card: ${s.borderWidthCard};
  --border-width-btn: ${s.borderWidthBtn};
  --border-width-input: ${s.borderWidthInput};
  --border-style: ${s.borderStyle};
  --shadow-card: ${s.shadowCardLight};
  --shadow-card-hover: ${s.shadowCardHoverLight};
  --shadow-btn: ${s.shadowBtn};
  --card-backdrop-blur: ${s.cardBackdropBlur};
  --card-surface: ${s.cardSurfaceLight};
  --font-heading: ${s.fontHeading};
  --font-heading-weight: ${s.fontHeadingWeight};
  --font-heading-tracking: ${s.fontHeadingTracking};
  --font-heading-transform: ${s.fontHeadingTransform};
  --font-btn: ${s.fontBtn};
  --letter-spacing-btn: ${s.letterSpacingBtn};
  --text-transform-btn: ${s.textTransformBtn};
  --letter-spacing-badge: ${s.letterSpacingBadge};
  --text-transform-badge: ${s.textTransformBadge};
  --font-weight-badge: ${s.fontWeightBadge};
  --density-card-padding: ${s.densityCardPadding};
  --density-gap: ${s.densityGap};
  --app-background: ${s.appBackgroundLight};
  --effects-hover-transform: ${s.effectsHoverTransform};
  --glow-accent: ${s.glowAccent};

  --radius: var(--radius-btn);
  --radius-sm: var(--radius-badge);
  --radius-md: var(--radius-btn);
  --radius-lg: var(--radius-card);
  --radius-xl: var(--radius-card);
  --radius-2xl: calc(var(--radius-card) * 1.25);
}

:root.dark[data-style="${s.id}"], .dark[data-style="${s.id}"] {
  --radius-card: ${s.radiusCard};
  --radius-btn: ${s.radiusBtn};
  --radius-input: ${s.radiusInput};
  --radius-badge: ${s.radiusBadge};
  --border-width-card: ${s.borderWidthCard};
  --border-width-btn: ${s.borderWidthBtn};
  --border-width-input: ${s.borderWidthInput};
  --border-style: ${s.borderStyle};
  --shadow-card: ${s.shadowCardDark};
  --shadow-card-hover: ${s.shadowCardHoverDark};
  --shadow-btn: ${s.shadowBtn};
  --card-backdrop-blur: ${s.cardBackdropBlur};
  --card-surface: ${s.cardSurfaceDark};
  --font-heading: ${s.fontHeading};
  --font-heading-weight: ${s.fontHeadingWeight};
  --font-heading-tracking: ${s.fontHeadingTracking};
  --font-heading-transform: ${s.fontHeadingTransform};
  --font-btn: ${s.fontBtn};
  --letter-spacing-btn: ${s.letterSpacingBtn};
  --text-transform-btn: ${s.textTransformBtn};
  --letter-spacing-badge: ${s.letterSpacingBadge};
  --text-transform-badge: ${s.textTransformBadge};
  --font-weight-badge: ${s.fontWeightBadge};
  --density-card-padding: ${s.densityCardPadding};
  --density-gap: ${s.densityGap};
  --app-background: ${s.appBackgroundDark};
  --effects-hover-transform: ${s.effectsHoverTransform};
  --glow-accent: ${s.glowAccent};

  --radius: var(--radius-btn);
  --radius-sm: var(--radius-badge);
  --radius-md: var(--radius-btn);
  --radius-lg: var(--radius-card);
  --radius-xl: var(--radius-card);
  --radius-2xl: calc(var(--radius-card) * 1.25);
}
`;
}

css += `
/* ==========================================================================
   CAPA 2: LAS 6 PALETAS CROMÁTICAS ARQUETÍPICAS (OKLCH + 3 ACENTOS)
   ========================================================================== */
`;

for (const p of archetypalPalettes) {
  css += `
/* PALETA: ${p.name} (${p.id}) */
:root[data-palette="${p.id}"]:not(.dark) {
  --background: ${p.bgLight};
  --foreground: ${p.textLight};
  --card: ${p.cardLight};
  --card-foreground: ${p.textLight};
  --popover: ${p.cardLight};
  --popover-foreground: ${p.textLight};
  --primary: ${p.primaryLight};
  --primary-foreground: ${p.primaryFgLight};
  --secondary: ${p.secondaryLight};
  --secondary-foreground: ${p.secondaryFgLight};
  --muted: ${p.mutedLight};
  --muted-foreground: ${p.mutedFgLight};
  --border: ${p.borderLight};
  --input: ${p.borderLight};
  --ring: ${p.primaryLight};

  /* 3 Acentos Cromáticos Reales */
  --accent: ${p.accent1Light};
  --accent-foreground: oklch(0.990 0.005 ${p.hue});
  --accent-1: ${p.accent1Light};
  --accent-1-foreground: oklch(0.990 0.005 ${p.hue});
  --accent-2: ${p.accent2Light};
  --accent-2-foreground: oklch(0.990 0.005 ${p.hue});
  --accent-3: ${p.accent3Light};
  --accent-3-foreground: oklch(0.990 0.005 ${p.hue});

  --brand-accent: ${p.primaryLight};
  --brand-accent-foreground: ${p.primaryFgLight};
  --border-shadow: ${p.borderShadow};

  /* Semántica */
  --success: oklch(0.620 0.180 145);
  --success-foreground: oklch(0.990 0.005 145);
  --destructive: oklch(0.580 0.220 25);
  --destructive-foreground: oklch(0.990 0.005 25);
  --warning: oklch(0.760 0.170 75);
  --warning-foreground: oklch(0.180 0.035 75);
  --info: oklch(0.600 0.180 230);
  --info-foreground: oklch(0.990 0.005 230);

  /* Gráficas */
  --chart-1: ${p.primaryLight};
  --chart-2: ${p.accent1Light};
  --chart-3: ${p.accent2Light};
  --chart-4: ${p.accent3Light};
  --chart-5: ${p.secondaryLight};

  /* Barra lateral */
  --sidebar: ${p.bgLight};
  --sidebar-foreground: ${p.textLight};
  --sidebar-primary: ${p.primaryLight};
  --sidebar-primary-foreground: ${p.primaryFgLight};
  --sidebar-accent: ${p.mutedLight};
  --sidebar-accent-foreground: ${p.primaryLight};
  --sidebar-border: ${p.borderLight};
  --sidebar-ring: ${p.primaryLight};
}

:root.dark[data-palette="${p.id}"], .dark[data-palette="${p.id}"] {
  --background: ${p.bgDark};
  --foreground: ${p.textDark};
  --card: ${p.cardDark};
  --card-foreground: ${p.textDark};
  --popover: ${p.cardDark};
  --popover-foreground: ${p.textDark};
  --primary: ${p.primaryDark};
  --primary-foreground: ${p.primaryFgDark};
  --secondary: ${p.secondaryDark};
  --secondary-foreground: ${p.secondaryFgDark};
  --muted: ${p.mutedDark};
  --muted-foreground: ${p.mutedFgDark};
  --border: ${p.borderDark};
  --input: ${p.borderDark};
  --ring: ${p.primaryDark};

  /* 3 Acentos Cromáticos Reales */
  --accent: ${p.accent1Dark};
  --accent-foreground: oklch(0.120 0.030 ${p.hue});
  --accent-1: ${p.accent1Dark};
  --accent-1-foreground: oklch(0.120 0.030 ${p.hue});
  --accent-2: ${p.accent2Dark};
  --accent-2-foreground: oklch(0.120 0.030 ${p.hue});
  --accent-3: ${p.accent3Dark};
  --accent-3-foreground: oklch(0.120 0.030 ${p.hue});

  --brand-accent: ${p.primaryDark};
  --brand-accent-foreground: ${p.primaryFgDark};
  --border-shadow: ${p.borderShadow};

  /* Semántica */
  --success: oklch(0.700 0.170 145);
  --success-foreground: oklch(0.120 0.030 145);
  --destructive: oklch(0.650 0.220 25);
  --destructive-foreground: oklch(0.990 0.005 25);
  --warning: oklch(0.800 0.170 75);
  --warning-foreground: oklch(0.150 0.030 75);
  --info: oklch(0.700 0.170 230);
  --info-foreground: oklch(0.120 0.030 230);

  /* Gráficas */
  --chart-1: ${p.primaryDark};
  --chart-2: ${p.accent1Dark};
  --chart-3: ${p.accent2Dark};
  --chart-4: ${p.accent3Dark};
  --chart-5: ${p.secondaryDark};

  /* Barra lateral */
  --sidebar: ${p.bgDark};
  --sidebar-foreground: ${p.textDark};
  --sidebar-primary: ${p.primaryDark};
  --sidebar-primary-foreground: ${p.primaryFgDark};
  --sidebar-accent: ${p.mutedDark};
  --sidebar-accent-foreground: ${p.primaryDark};
  --sidebar-border: ${p.borderDark};
  --sidebar-ring: ${p.primaryDark};
}
`;
}

css += `
/* ==========================================================================
   PALETAS CLÁSICAS DE COMPATIBILIDAD (12 PALETAS CROMÁTICAS ESTÁTICAS)
   ========================================================================== */
`;

for (const p of legacyPalettes) {
  const h = p.hue;
  const cm = p.cm || 1.0;
  const hAcc1 = (h + 35) % 360;
  const hAcc2 = (h + 180) % 360;
  const hAcc3 = (h + 290) % 360;

  css += `
:root[data-palette="${p.id}"]:not(.dark) {
  --background: oklch(0.974 ${fmt(0.016 * cm)} ${h});
  --foreground: oklch(0.180 ${fmt(0.035 * cm)} ${h});
  --card: oklch(0.990 ${fmt(0.008 * cm)} ${h});
  --card-foreground: oklch(0.180 ${fmt(0.035 * cm)} ${h});
  --popover: oklch(0.990 ${fmt(0.008 * cm)} ${h});
  --popover-foreground: oklch(0.180 ${fmt(0.035 * cm)} ${h});
  --primary: oklch(0.530 ${fmt(0.200 * cm)} ${h});
  --primary-foreground: oklch(0.988 0.005 ${h});
  --secondary: oklch(0.940 ${fmt(0.025 * cm)} ${h});
  --secondary-foreground: oklch(0.200 ${fmt(0.040 * cm)} ${h});
  --muted: oklch(0.942 ${fmt(0.018 * cm)} ${h});
  --muted-foreground: oklch(0.480 ${fmt(0.035 * cm)} ${h});
  --border: oklch(0.895 ${fmt(0.022 * cm)} ${h});
  --input: oklch(0.895 ${fmt(0.022 * cm)} ${h});
  --ring: oklch(0.530 ${fmt(0.200 * cm)} ${h});
  --accent: oklch(0.930 ${fmt(0.035 * cm)} ${h});
  --accent-foreground: oklch(0.180 ${fmt(0.045 * cm)} ${h});
  --accent-1: oklch(0.580 ${fmt(0.180 * cm)} ${hAcc1});
  --accent-1-foreground: oklch(0.988 0.005 ${hAcc1});
  --accent-2: oklch(0.620 ${fmt(0.200 * cm)} ${hAcc2});
  --accent-2-foreground: oklch(0.988 0.005 ${hAcc2});
  --accent-3: oklch(0.560 ${fmt(0.190 * cm)} ${hAcc3});
  --accent-3-foreground: oklch(0.988 0.005 ${hAcc3});
  --destructive: oklch(0.580 0.220 25);
  --destructive-foreground: oklch(0.988 0.005 25);
  --brand-accent: oklch(0.530 ${fmt(0.200 * cm)} ${h});
  --brand-accent-foreground: oklch(0.988 0.005 ${h});
  --border-shadow: #0f172a;
  --chart-1: oklch(0.530 ${fmt(0.200 * cm)} ${h});
  --chart-2: oklch(0.650 0.180 ${hAcc1});
  --chart-3: oklch(0.600 0.190 ${hAcc2});
  --chart-4: oklch(0.720 0.140 ${hAcc3});
  --chart-5: oklch(0.800 0.090 ${h});
  --sidebar: oklch(0.962 ${fmt(0.022 * cm)} ${h});
  --sidebar-foreground: oklch(0.180 ${fmt(0.035 * cm)} ${h});
  --sidebar-primary: oklch(0.530 ${fmt(0.200 * cm)} ${h});
  --sidebar-primary-foreground: oklch(0.988 0.005 ${h});
  --sidebar-accent: oklch(0.925 ${fmt(0.042 * cm)} ${h});
  --sidebar-accent-foreground: oklch(0.240 ${fmt(0.130 * cm)} ${h});
  --sidebar-border: oklch(0.895 ${fmt(0.022 * cm)} ${h});
  --sidebar-ring: oklch(0.530 ${fmt(0.200 * cm)} ${h});
}

:root.dark[data-palette="${p.id}"], .dark[data-palette="${p.id}"] {
  --background: oklch(0.148 ${fmt(0.032 * cm)} ${h});
  --foreground: oklch(0.960 ${fmt(0.012 * cm)} ${h});
  --card: oklch(0.182 ${fmt(0.035 * cm)} ${h});
  --card-foreground: oklch(0.960 ${fmt(0.012 * cm)} ${h});
  --popover: oklch(0.182 ${fmt(0.035 * cm)} ${h});
  --popover-foreground: oklch(0.960 ${fmt(0.012 * cm)} ${h});
  --primary: oklch(0.720 ${fmt(0.190 * cm)} ${h});
  --primary-foreground: oklch(0.120 ${fmt(0.030 * cm)} ${h});
  --secondary: oklch(0.240 ${fmt(0.040 * cm)} ${h});
  --secondary-foreground: oklch(0.950 ${fmt(0.015 * cm)} ${h});
  --muted: oklch(0.220 ${fmt(0.030 * cm)} ${h});
  --muted-foreground: oklch(0.680 ${fmt(0.030 * cm)} ${h});
  --border: oklch(0.250 ${fmt(0.035 * cm)} ${h});
  --input: oklch(0.250 ${fmt(0.035 * cm)} ${h});
  --ring: oklch(0.720 ${fmt(0.190 * cm)} ${h});
  --accent: oklch(0.240 ${fmt(0.040 * cm)} ${h});
  --accent-foreground: oklch(0.960 ${fmt(0.012 * cm)} ${h});
  --accent-1: oklch(0.720 ${fmt(0.180 * cm)} ${hAcc1});
  --accent-1-foreground: oklch(0.120 0.030 ${hAcc1});
  --accent-2: oklch(0.740 ${fmt(0.200 * cm)} ${hAcc2});
  --accent-2-foreground: oklch(0.120 0.030 ${hAcc2});
  --accent-3: oklch(0.700 ${fmt(0.190 * cm)} ${hAcc3});
  --accent-3-foreground: oklch(0.120 0.030 ${hAcc3});
  --destructive: oklch(0.620 0.220 25);
  --destructive-foreground: oklch(0.988 0.005 25);
  --brand-accent: oklch(0.720 ${fmt(0.190 * cm)} ${h});
  --brand-accent-foreground: oklch(0.120 ${fmt(0.030 * cm)} ${h});
  --border-shadow: #050b14;
  --chart-1: oklch(0.720 ${fmt(0.190 * cm)} ${h});
  --chart-2: oklch(0.720 0.180 ${hAcc1});
  --chart-3: oklch(0.740 0.190 ${hAcc2});
  --chart-4: oklch(0.780 0.140 ${hAcc3});
  --chart-5: oklch(0.650 0.120 ${h});
  --sidebar: oklch(0.138 ${fmt(0.032 * cm)} ${h});
  --sidebar-foreground: oklch(0.960 ${fmt(0.012 * cm)} ${h});
  --sidebar-primary: oklch(0.720 ${fmt(0.190 * cm)} ${h});
  --sidebar-primary-foreground: oklch(0.120 ${fmt(0.030 * cm)} ${h});
  --sidebar-accent: oklch(0.220 ${fmt(0.035 * cm)} ${h});
  --sidebar-accent-foreground: oklch(0.850 ${fmt(0.160 * cm)} ${h});
  --sidebar-border: oklch(0.240 ${fmt(0.030 * cm)} ${h});
  --sidebar-ring: oklch(0.720 ${fmt(0.190 * cm)} ${h});
}
`;
}

css += `
/* ==========================================================================
   PALETA PERSONALIZADA (TEMA DESDE LOGO)
   Lee las propiedades CSSOM aplicadas dinámicamente sobre :root
   ========================================================================== */
:root[data-palette="custom"] {
  /* Si el usuario selecciona 'custom', las variables primarias, secundarias,
     neutros y acentos se inyectan en document.documentElement.style vía JS */
}
`;

const outputPath = path.resolve(__dirname, '../src/styles/theme.css');
fs.writeFileSync(outputPath, css, 'utf-8');
console.log(`Generated theme.css successfully at ${outputPath} (${css.length} bytes, ${styles.length} styles, ${archetypalPalettes.length + legacyPalettes.length} palettes).`);
