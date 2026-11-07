import { useState } from 'react'
import { motion } from 'motion/react'
import {
  Sparkles,
  Layers,
  Type,
  CheckCircle2,
  RefreshCw,
  Search as SearchIcon,
  ChevronRight,
  ChevronDown,
  Send,
  Trash2,
  ShieldCheck,
  Palette,
  ExternalLink,
} from 'lucide-react'
import { toast } from 'sonner'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { Checkbox } from '@/components/ui/checkbox'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  fadeInUpVariants,
  staggerContainerVariants,
  staggerItemVariants,
  usePrefersReducedMotion,
} from '@/lib/motion'

export function DesignShowcase() {
  const [animKey, setAnimKey] = useState(0)
  const [showSkeleton, setShowSkeleton] = useState(false)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [switchState, setSwitchState] = useState(true)
  const [checkState, setCheckState] = useState(true)
  const prefersReduced = usePrefersReducedMotion()

  const replayAnimations = () => {
    setAnimKey((prev) => prev + 1)
    toast.info('Animaciones reiniciadas', {
      description: 'Variantes de /lib/motion.ts ejecutadas nuevamente (150-250ms).',
    })
  }

  return (
    <>
      <Header>
        <div className='flex items-center gap-2 me-auto min-w-0'>
          <div className='hidden sm:flex items-center gap-1.5 text-xs text-muted-foreground'>
            <Palette className='size-3.5 text-foreground' />
            <span>Fundamentos de Diseño</span>
            <ChevronRight className='size-3' />
            <span className='font-medium text-foreground'>Sistema & Movimiento</span>
          </div>
          <Badge
            variant='outline'
            className='hidden md:inline-flex h-5 text-[10px] uppercase font-mono tracking-wider border-border'
          >
            Paso 2.5 • Dev Only
          </Badge>
        </div>
        <div className='ms-auto flex items-center space-x-3'>
          <Search />
          <ThemeSwitch />
          <ProfileDropdown />
        </div>
      </Header>

      <Main fixed={false}>
        <div className='space-y-10 pb-16' key={animKey}>
          {/* ===== 1. HERO & CONTROLES RÁPIDOS ===== */}
          <motion.div
            variants={fadeInUpVariants}
            initial='hidden'
            animate='visible'
            className='flex flex-col md:flex-row md:items-end justify-between gap-4 border-b border-border pb-6'
          >
            <div className='space-y-1.5'>
              <div className='flex items-center gap-2'>
                <h1 className='text-2xl md:text-3xl font-semibold tracking-tight'>
                  Fundamentos de Diseño & Movimiento
                </h1>
                <Badge className='bg-primary text-primary-foreground font-mono text-xs'>
                  Linear / Vercel
                </Badge>
              </div>
              <p className='text-sm text-muted-foreground max-w-2xl'>
                Tokens centralizados en Tailwind v4, paleta casi monocroma con un único acento,
                radios precisos de 6px (<code className='font-mono text-xs'>--radius: 0.375rem</code>),
                fuentes locales Geist Sans & Mono (cero CDN, CSP estricta) y variantes fluidas de 150-250ms.
              </p>
            </div>

            <div className='flex items-center gap-2 flex-wrap'>
              <Button
                variant='outline'
                size='sm'
                onClick={replayAnimations}
                className='h-8 gap-1.5'
              >
                <RefreshCw className='size-3.5' />
                <span>Replay Motion</span>
              </Button>

              <Button
                variant={showSkeleton ? 'default' : 'outline'}
                size='sm'
                onClick={() => setShowSkeleton(!showSkeleton)}
                className='h-8 gap-1.5'
              >
                <Layers className='size-3.5' />
                <span>{showSkeleton ? 'Ver Datos Reales' : 'Simular Skeletons'}</span>
              </Button>

              <Button
                variant='default'
                size='sm'
                onClick={() =>
                  toast.success('Micro-interacción confirmada', {
                    description: 'Notificación discreta con curva de entrada suave y sin bloqueo de interfaz.',
                  })
                }
                className='h-8 gap-1.5'
              >
                <Sparkles className='size-3.5' />
                <span>Toast Discreto</span>
              </Button>
            </div>
          </motion.div>

          {/* ===== 2. STATUS BAR (REDUCED MOTION & TOKENS INFO) ===== */}
          <div className='grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3'>
            <Card className='p-3.5 flex items-center justify-between shadow-none border-border'>
              <div className='space-y-0.5'>
                <div className='text-xs text-muted-foreground font-medium'>Tipografía Activa</div>
                <div className='text-sm font-semibold flex items-center gap-1.5'>
                  <Type className='size-3.5 text-muted-foreground' />
                  <span>Geist Sans + Mono</span>
                </div>
              </div>
              <Badge variant='secondary' className='text-[10px] font-mono'>Local / Cero CDN</Badge>
            </Card>

            <Card className='p-3.5 flex items-center justify-between shadow-none border-border'>
              <div className='space-y-0.5'>
                <div className='text-xs text-muted-foreground font-medium'>Radio de Bordes</div>
                <div className='text-sm font-semibold font-mono'>0.375rem (6px)</div>
              </div>
              <Badge variant='outline' className='text-[10px] font-mono'>Minimalista</Badge>
            </Card>

            <Card className='p-3.5 flex items-center justify-between shadow-none border-border'>
              <div className='space-y-0.5'>
                <div className='text-xs text-muted-foreground font-medium'>Duración de Animación</div>
                <div className='text-sm font-semibold font-mono'>150ms – 220ms</div>
              </div>
              <Badge variant='outline' className='text-[10px] font-mono'>cubic-bezier</Badge>
            </Card>

            <Card className='p-3.5 flex items-center justify-between shadow-none border-border'>
              <div className='space-y-0.5'>
                <div className='text-xs text-muted-foreground font-medium'>Movimiento Reducido</div>
                <div className='text-sm font-semibold flex items-center gap-1.5'>
                  <CheckCircle2 className='size-3.5 text-muted-foreground' />
                  <span>{prefersReduced ? 'Activado (SO)' : 'Respetado (Hook)'}</span>
                </div>
              </div>
              <Badge variant='secondary' className='text-[10px] font-mono'>A11y Compliant</Badge>
            </Card>
          </div>

          {/* ===== 3. TOKENS DE COLOR Y SUPERFICIES ===== */}
          <section className='space-y-4'>
            <div className='flex items-center justify-between'>
              <div>
                <h2 className='text-lg font-semibold tracking-tight'>1. Tokens de Superficie y Color</h2>
                <p className='text-xs text-muted-foreground'>
                  Paleta monocroma de alto contraste con un acento violeta sutil (Linear style) y bordes nítidos de 1px.
                </p>
              </div>
            </div>

            <div className='grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-8 gap-3'>
              {/* Background */}
              <div className='p-3 rounded-md border border-border bg-card space-y-2'>
                <div className='h-10 w-full rounded border border-border bg-background flex items-center justify-center text-[10px] font-mono text-foreground'>
                  bg
                </div>
                <div className='text-[11px] font-medium'>Background</div>
                <div className='text-[10px] text-muted-foreground font-mono'>--background</div>
              </div>

              {/* Card */}
              <div className='p-3 rounded-md border border-border bg-card space-y-2'>
                <div className='h-10 w-full rounded border border-border bg-card flex items-center justify-center text-[10px] font-mono text-card-foreground'>
                  card
                </div>
                <div className='text-[11px] font-medium'>Card Surface</div>
                <div className='text-[10px] text-muted-foreground font-mono'>--card</div>
              </div>

              {/* Muted */}
              <div className='p-3 rounded-md border border-border bg-card space-y-2'>
                <div className='h-10 w-full rounded border border-border bg-muted flex items-center justify-center text-[10px] font-mono text-muted-foreground'>
                  muted
                </div>
                <div className='text-[11px] font-medium'>Muted / Subdued</div>
                <div className='text-[10px] text-muted-foreground font-mono'>--muted</div>
              </div>

              {/* Border */}
              <div className='p-3 rounded-md border border-border bg-card space-y-2'>
                <div className='h-10 w-full rounded border-2 border-border bg-background flex items-center justify-center text-[10px] font-mono text-muted-foreground'>
                  border 1px
                </div>
                <div className='text-[11px] font-medium'>Border Outline</div>
                <div className='text-[10px] text-muted-foreground font-mono'>--border</div>
              </div>

              {/* Primary */}
              <div className='p-3 rounded-md border border-border bg-card space-y-2'>
                <div className='h-10 w-full rounded bg-primary flex items-center justify-center text-[10px] font-mono text-primary-foreground font-semibold'>
                  primary
                </div>
                <div className='text-[11px] font-medium'>Primary Action</div>
                <div className='text-[10px] text-muted-foreground font-mono'>--primary</div>
              </div>

              {/* Secondary */}
              <div className='p-3 rounded-md border border-border bg-card space-y-2'>
                <div className='h-10 w-full rounded border border-border bg-secondary flex items-center justify-center text-[10px] font-mono text-secondary-foreground'>
                  secondary
                </div>
                <div className='text-[11px] font-medium'>Secondary</div>
                <div className='text-[10px] text-muted-foreground font-mono'>--secondary</div>
              </div>

              {/* Brand Accent */}
              <div className='p-3 rounded-md border border-border bg-card space-y-2'>
                <div className='h-10 w-full rounded bg-[var(--brand-accent)] flex items-center justify-center text-[10px] font-mono text-[var(--brand-accent-foreground)] font-semibold'>
                  accent
                </div>
                <div className='text-[11px] font-medium'>Brand Accent</div>
                <div className='text-[10px] text-muted-foreground font-mono'>--brand-accent</div>
              </div>

              {/* Destructive */}
              <div className='p-3 rounded-md border border-border bg-card space-y-2'>
                <div className='h-10 w-full rounded bg-destructive flex items-center justify-center text-[10px] font-mono text-white font-semibold'>
                  error
                </div>
                <div className='text-[11px] font-medium'>Destructive</div>
                <div className='text-[10px] text-muted-foreground font-mono'>--destructive</div>
              </div>
            </div>
          </section>

          {/* ===== 4. TIPOGRAFÍA GEIST SANS & MONO ===== */}
          <section className='space-y-4'>
            <div>
              <h2 className='text-lg font-semibold tracking-tight'>2. Jerarquía Tipográfica (Geist Sans & Geist Mono)</h2>
              <p className='text-xs text-muted-foreground'>
                Empaquetado en local mediante <code className='font-mono'>@fontsource/geist-sans</code> y <code className='font-mono'>@fontsource/geist-mono</code>. Cero peticiones de red externas.
              </p>
            </div>

            <div className='grid grid-cols-1 md:grid-cols-2 gap-4'>
              <Card className='p-5 shadow-none border-border space-y-4'>
                <div className='flex items-center justify-between border-b border-border pb-2.5'>
                  <span className='text-xs font-mono font-medium text-muted-foreground'>Geist Sans • Proporciones & Jerarquía</span>
                  <Badge variant='outline' className='text-[10px] font-mono'>font-sans</Badge>
                </div>

                <div className='space-y-3'>
                  <div>
                    <span className='text-[10px] font-mono text-muted-foreground block mb-0.5'>Heading 1 (30px / 700)</span>
                    <h1 className='text-3xl font-bold tracking-tight'>Gestión Operativa Payé</h1>
                  </div>
                  <div>
                    <span className='text-[10px] font-mono text-muted-foreground block mb-0.5'>Heading 2 (24px / 600)</span>
                    <h2 className='text-2xl font-semibold tracking-tight'>Facturación & Clientes</h2>
                  </div>
                  <div>
                    <span className='text-[10px] font-mono text-muted-foreground block mb-0.5'>Heading 3 (18px / 500)</span>
                    <h3 className='text-lg font-medium tracking-tight'>Resumen del Período Mensual</h3>
                  </div>
                  <div>
                    <span className='text-[10px] font-mono text-muted-foreground block mb-0.5'>Body Regular (14px / 400)</span>
                    <p className='text-sm text-foreground leading-relaxed'>
                      El sistema aplica seguridad por defecto con autorización basada en roles (RBAC) y sesiones HttpOnly blindadas.
                    </p>
                  </div>
                  <div>
                    <span className='text-[10px] font-mono text-muted-foreground block mb-0.5'>Caption & Muted (12px / 400)</span>
                    <p className='text-xs text-muted-foreground'>
                      Última sincronización verificada hace 3 minutos por el worker de auditoría.
                    </p>
                  </div>
                </div>
              </Card>

              <Card className='p-5 shadow-none border-border space-y-4'>
                <div className='flex items-center justify-between border-b border-border pb-2.5'>
                  <span className='text-xs font-mono font-medium text-muted-foreground'>Geist Mono • Importes, Identificadores & Código</span>
                  <Badge variant='outline' className='text-[10px] font-mono'>font-mono</Badge>
                </div>

                <div className='space-y-3'>
                  <div>
                    <span className='text-[10px] font-mono text-muted-foreground block mb-0.5'>Valores Monetarios Tabulares</span>
                    <div className='text-2xl font-bold font-mono tracking-tight text-foreground'>
                      € 128.450,85 <span className='text-xs font-normal text-muted-foreground'>EUR</span>
                    </div>
                  </div>

                  <div>
                    <span className='text-[10px] font-mono text-muted-foreground block mb-0.5'>Identificadores de Factura & UUID</span>
                    <div className='flex gap-2 items-center'>
                      <code className='px-2 py-1 rounded bg-muted font-mono text-xs text-foreground border border-border'>
                        FAC-2026-00892
                      </code>
                      <code className='px-2 py-1 rounded bg-muted font-mono text-xs text-muted-foreground border border-border'>
                        usr_9f82d1ca38e4
                      </code>
                    </div>
                  </div>

                  <div>
                    <span className='text-[10px] font-mono text-muted-foreground block mb-0.5'>Bloque de Configuración / Snippet</span>
                    <pre className='p-3 rounded bg-muted/60 font-mono text-[11px] text-foreground border border-border overflow-x-auto'>
                      {`// Tokens de movimiento Linear
const motionConfig = {
  duration: 0.2, // 200ms
  ease: [0.16, 1, 0.3, 1],
  reducedMotion: "user"
};`}
                    </pre>
                  </div>
                </div>
              </Card>
            </div>
          </section>

          {/* ===== 5. BOTONES Y MICRO-INTERACCIONES ===== */}
          <section className='space-y-4'>
            <div>
              <h2 className='text-lg font-semibold tracking-tight'>3. Botones con Micro-interacciones (<code className='font-mono text-xs'>active:scale-[0.98]</code>)</h2>
              <p className='text-xs text-muted-foreground'>
                Feedback háptico-visual inmediato al hacer clic, con foco accesible y estilos sobrios. Haz clic para probar la pulsación.
              </p>
            </div>

            <Card className='p-5 shadow-none border-border'>
              <div className='flex flex-wrap items-center gap-3'>
                <Button variant='default' onClick={() => toast('Botón Primario pulsado')}>
                  Primario (Default)
                </Button>

                <Button variant='secondary' onClick={() => toast('Botón Secundario pulsado')}>
                  Secundario
                </Button>

                <Button variant='outline' onClick={() => toast('Botón Outline pulsado')}>
                  Borde Fino (Outline)
                </Button>

                <Button variant='ghost' onClick={() => toast('Botón Ghost pulsado')}>
                  Fantasma (Ghost)
                </Button>

                <Button variant='destructive' onClick={() => toast.error('Acción destructiva')}>
                  <Trash2 className='size-3.5' />
                  Eliminar
                </Button>

                <Button variant='default' className='bg-[var(--brand-accent)] text-white hover:bg-[var(--brand-accent)]/90'>
                  <Sparkles className='size-3.5' />
                  Acento Marca
                </Button>

                <Button variant='outline' size='sm' className='gap-1.5'>
                  <Send className='size-3' />
                  Pequeño (sm)
                </Button>

                <Button variant='outline' disabled>
                  Desactivado
                </Button>
              </div>
            </Card>
          </section>

          {/* ===== 6. MOVIMIENTO: STAGGERED CARDS & DIALOGS ===== */}
          <section className='space-y-4'>
            <div className='flex items-center justify-between'>
              <div>
                <h2 className='text-lg font-semibold tracking-tight'>4. Movimiento Reutilizable (<code className='font-mono text-xs'>/lib/motion.ts</code>)</h2>
                <p className='text-xs text-muted-foreground'>
                  Aparición escalonada de tarjetas (stagger de 50ms) y diálogo modal con ampliación suave (<code className='font-mono text-xs'>dialogScaleVariants</code>).
                </p>
              </div>

              <div className='flex items-center gap-2'>
                {/* Botón para abrir modal con animación suave */}
                <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
                  <DialogTrigger asChild>
                    <Button variant='outline' size='sm' className='gap-1.5'>
                      <ExternalLink className='size-3.5' />
                      Probar Modal Animado
                    </Button>
                  </DialogTrigger>
                  <DialogContent className='sm:max-w-md'>
                    <DialogHeader>
                      <DialogTitle className='flex items-center gap-2'>
                        <ShieldCheck className='size-4 text-foreground' />
                        <span>Transición Modal Fluida</span>
                      </DialogTitle>
                      <DialogDescription className='text-xs text-muted-foreground'>
                        Este modal se abre con <code className='font-mono'>scale: 0.97 → 1.0</code> y fundido de opacidad en 200ms.
                      </DialogDescription>
                    </DialogHeader>

                    <div className='py-3 space-y-3 text-sm'>
                      <div className='p-3 rounded border border-border bg-muted/40 text-xs font-mono'>
                        ease: [0.16, 1, 0.3, 1] • duration: 200ms
                      </div>
                      <p className='text-xs text-muted-foreground leading-relaxed'>
                        La experiencia de usuario se mantiene reactiva y sin distracciones cinemáticas innecesarias.
                      </p>
                    </div>

                    <DialogFooter className='gap-2 sm:gap-0'>
                      <Button variant='outline' size='sm' onClick={() => setDialogOpen(false)}>
                        Cerrar
                      </Button>
                      <Button
                        variant='default'
                        size='sm'
                        onClick={() => {
                          setDialogOpen(false)
                          toast.success('Cambios guardados con éxito')
                        }}
                      >
                        Aceptar
                      </Button>
                    </DialogFooter>
                  </DialogContent>
                </Dialog>

                {/* Dropdown Menu para probar apertura animada */}
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant='outline' size='sm' className='gap-1.5'>
                      <ChevronDown className='size-3.5' />
                      Probar Menú Desplegable
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align='end' className='w-52'>
                    <DropdownMenuLabel className='text-xs font-mono'>Micro-animación</DropdownMenuLabel>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem onClick={() => toast.info('Acción 1 ejecutada')}>
                      Aparición con escala
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => toast.info('Acción 2 ejecutada')}>
                      Transición suave 150ms
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem className='text-destructive' onClick={() => toast.error('Eliminación cancelada')}>
                      Cancelar registro
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            </div>

            {/* Grid animado con stagger */}
            {showSkeleton ? (
              <div className='grid grid-cols-1 md:grid-cols-3 gap-4'>
                {[1, 2, 3].map((i) => (
                  <Card key={i} className='p-5 shadow-none border-border space-y-3'>
                    <div className='flex justify-between items-center'>
                      <Skeleton className='h-4 w-28' />
                      <Skeleton className='h-4 w-12' />
                    </div>
                    <Skeleton className='h-8 w-20' />
                    <Skeleton className='h-3 w-48' />
                  </Card>
                ))}
              </div>
            ) : (
              <motion.div
                variants={staggerContainerVariants}
                initial='hidden'
                animate='visible'
                className='grid grid-cols-1 md:grid-cols-3 gap-4'
              >
                <motion.div variants={staggerItemVariants}>
                  <Card className='p-5 shadow-none border-border hover:border-foreground/20 transition-colors'>
                    <div className='flex items-center justify-between text-xs text-muted-foreground mb-2'>
                      <span>Tarjeta Escalonada 01</span>
                      <Badge variant='outline' className='font-mono text-[10px]'>+0ms</Badge>
                    </div>
                    <div className='text-xl font-semibold mb-1'>Rendimiento Inmediato</div>
                    <p className='text-xs text-muted-foreground leading-relaxed'>
                      Animaciones aceleradas por GPU sin recreación de DOM ni inyección de hojas de estilo.
                    </p>
                  </Card>
                </motion.div>

                <motion.div variants={staggerItemVariants}>
                  <Card className='p-5 shadow-none border-border hover:border-foreground/20 transition-colors'>
                    <div className='flex items-center justify-between text-xs text-muted-foreground mb-2'>
                      <span>Tarjeta Escalonada 02</span>
                      <Badge variant='outline' className='font-mono text-[10px]'>+50ms</Badge>
                    </div>
                    <div className='text-xl font-semibold mb-1'>Seguridad CSP Estricta</div>
                    <p className='text-xs text-muted-foreground leading-relaxed'>
                      Sin <code className='font-mono text-[10px]'>unsafe-inline</code> ni librerías CSS-in-JS. Estilos estáticos y variables nativas.
                    </p>
                  </Card>
                </motion.div>

                <motion.div variants={staggerItemVariants}>
                  <Card className='p-5 shadow-none border-border hover:border-foreground/20 transition-colors'>
                    <div className='flex items-center justify-between text-xs text-muted-foreground mb-2'>
                      <span>Tarjeta Escalonada 03</span>
                      <Badge variant='outline' className='font-mono text-[10px]'>+100ms</Badge>
                    </div>
                    <div className='text-xl font-semibold mb-1'>Accesibilidad A11y</div>
                    <p className='text-xs text-muted-foreground leading-relaxed'>
                      Desactivación instantánea al detectar la preferencia de reducción de movimiento del usuario.
                    </p>
                  </Card>
                </motion.div>
              </motion.div>
            )}
          </section>

          {/* ===== 7. CONTROLES DE FORMULARIO ===== */}
          <section className='space-y-4'>
            <div>
              <h2 className='text-lg font-semibold tracking-tight'>5. Controles de Formulario & Inputs</h2>
              <p className='text-xs text-muted-foreground'>
                Inputs limpios con anillo de foco sutil, selectores nativos y switches minimalistas.
              </p>
            </div>

            <Card className='p-5 shadow-none border-border'>
              <div className='grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5'>
                <div className='space-y-1.5'>
                  <Label htmlFor='demo-input' className='text-xs font-medium'>
                    Campo de Texto Estándar
                  </Label>
                  <Input id='demo-input' placeholder='ej. Supermercado El Trébol' className='h-9' />
                  <p className='text-[10px] text-muted-foreground'>Borde 1px con anillo de foco discreto</p>
                </div>

                <div className='space-y-1.5'>
                  <Label htmlFor='demo-icon' className='text-xs font-medium'>
                    Input con Icono Integrado
                  </Label>
                  <div className='relative'>
                    <SearchIcon className='absolute left-2.5 top-2.5 size-4 text-muted-foreground' />
                    <Input id='demo-icon' placeholder='Buscar factura o cliente...' className='ps-8 h-9' />
                  </div>
                  <p className='text-[10px] text-muted-foreground'>Integración nativa con Lucide Icons</p>
                </div>

                <div className='space-y-3 pt-2'>
                  <div className='flex items-center justify-between'>
                    <Label htmlFor='demo-switch' className='text-xs font-medium cursor-pointer'>
                      Notificaciones en tiempo real
                    </Label>
                    <Switch
                      id='demo-switch'
                      checked={switchState}
                      onCheckedChange={setSwitchState}
                    />
                  </div>

                  <div className='flex items-center gap-2'>
                    <Checkbox
                      id='demo-check'
                      checked={checkState}
                      onCheckedChange={(c) => setCheckState(Boolean(c))}
                    />
                    <Label htmlFor='demo-check' className='text-xs font-normal cursor-pointer'>
                      Exigir segundo factor de autenticación (2FA)
                    </Label>
                  </div>
                </div>
              </div>
            </Card>
          </section>

          {/* ===== 8. TABLA MINIMALISTA ===== */}
          <section className='space-y-4'>
            <div className='flex items-center justify-between'>
              <div>
                <h2 className='text-lg font-semibold tracking-tight'>6. Tabla de Registros Minimalista</h2>
                <p className='text-xs text-muted-foreground'>
                  Filas con bordes sutiles, tipografía mono en importes numéricos y badges de estado píldora.
                </p>
              </div>
              <Badge variant='outline' className='font-mono text-xs'>
                3 Registros
              </Badge>
            </div>

            <Card className='shadow-none border-border overflow-hidden'>
              <Table>
                <TableHeader>
                  <TableRow className='bg-muted/40 hover:bg-muted/40'>
                    <TableHead className='w-[140px] text-xs font-medium'>Identificador</TableHead>
                    <TableHead className='text-xs font-medium'>Cliente / Entidad</TableHead>
                    <TableHead className='text-xs font-medium'>Estado</TableHead>
                    <TableHead className='text-xs font-medium text-right'>Importe</TableHead>
                    <TableHead className='w-[80px] text-xs font-medium text-center'>Acción</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {showSkeleton ? (
                    [1, 2, 3].map((i) => (
                      <TableRow key={i}>
                        <TableCell><Skeleton className='h-4 w-20' /></TableCell>
                        <TableCell><Skeleton className='h-4 w-32' /></TableCell>
                        <TableCell><Skeleton className='h-4 w-16' /></TableCell>
                        <TableCell><Skeleton className='h-4 w-16 ms-auto' /></TableCell>
                        <TableCell><Skeleton className='h-6 w-12 mx-auto' /></TableCell>
                      </TableRow>
                    ))
                  ) : (
                    <>
                      <TableRow className='hover:bg-muted/20 transition-colors'>
                        <TableCell className='font-mono text-xs font-semibold text-foreground'>
                          FAC-2026-0041
                        </TableCell>
                        <TableCell className='text-xs font-medium text-foreground'>
                          Almacén Central Corrientes
                        </TableCell>
                        <TableCell>
                          <Badge variant='outline' className='bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20 text-[10px] font-mono'>
                            Pagada
                          </Badge>
                        </TableCell>
                        <TableCell className='text-right font-mono text-xs font-semibold text-foreground'>
                          € 1.420,00
                        </TableCell>
                        <TableCell className='text-center'>
                          <Button variant='ghost' size='sm' className='h-7 text-xs'>
                            Ver
                          </Button>
                        </TableCell>
                      </TableRow>

                      <TableRow className='hover:bg-muted/20 transition-colors'>
                        <TableCell className='font-mono text-xs font-semibold text-foreground'>
                          FAC-2026-0042
                        </TableCell>
                        <TableCell className='text-xs font-medium text-foreground'>
                          Distribuidora San Martín S.R.L.
                        </TableCell>
                        <TableCell>
                          <Badge variant='outline' className='bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20 text-[10px] font-mono'>
                            Pendiente
                          </Badge>
                        </TableCell>
                        <TableCell className='text-right font-mono text-xs font-semibold text-foreground'>
                          € 3.890,50
                        </TableCell>
                        <TableCell className='text-center'>
                          <Button variant='ghost' size='sm' className='h-7 text-xs'>
                            Ver
                          </Button>
                        </TableCell>
                      </TableRow>

                      <TableRow className='hover:bg-muted/20 transition-colors'>
                        <TableCell className='font-mono text-xs font-semibold text-foreground'>
                          FAC-2026-0043
                        </TableCell>
                        <TableCell className='text-xs font-medium text-foreground'>
                          Supermercados del Litoral
                        </TableCell>
                        <TableCell>
                          <Badge variant='outline' className='bg-muted text-muted-foreground border-border text-[10px] font-mono'>
                            Borrador
                          </Badge>
                        </TableCell>
                        <TableCell className='text-right font-mono text-xs font-semibold text-foreground'>
                          € 650,00
                        </TableCell>
                        <TableCell className='text-center'>
                          <Button variant='ghost' size='sm' className='h-7 text-xs'>
                            Ver
                          </Button>
                        </TableCell>
                      </TableRow>
                    </>
                  )}
                </TableBody>
              </Table>
            </Card>
          </section>

          {/* ===== 9. FOOTER INFORMATIVO CSP & NORMAS ===== */}
          <div className='p-4 rounded-md border border-border bg-muted/30 text-xs text-muted-foreground flex flex-col sm:flex-row items-center justify-between gap-3'>
            <div className='flex items-center gap-2'>
              <ShieldCheck className='size-4 text-foreground shrink-0' />
              <span>
                <strong>Cumplimiento de Seguridad:</strong> CSP estricta respetada sin inyección de etiquetas <code className='font-mono'>&lt;style&gt;</code> en tiempo de ejecución.
              </span>
            </div>
            <div className='font-mono text-[10px]'>
              Payé Base • Shadcn Admin 2026
            </div>
          </div>
        </div>
      </Main>
    </>
  )
}
