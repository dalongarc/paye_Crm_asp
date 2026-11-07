import { useState, useEffect } from 'react'
import type { Variants, Transition } from 'motion/react'

/**
 * Curvas de aceleración y constantes de tiempo inspiradas en Linear / Vercel
 * Duración contenida (150-250ms) y curvas suaves sin rebotes excesivos.
 */
export const easeLinearSmooth = [0.16, 1, 0.3, 1] as const
export const easeOutCubic = [0.22, 1, 0.36, 1] as const

export const DURATION_FAST = 0.15
export const DURATION_NORMAL = 0.2
export const DURATION_SLOW = 0.25

export const defaultTransition: Transition = {
  duration: DURATION_NORMAL,
  ease: easeLinearSmooth,
}

/**
 * Detecta si el usuario tiene activado 'prefers-reduced-motion' en el sistema
 */
export const prefersReducedMotion = (): boolean => {
  if (typeof window === 'undefined') return false
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

/**
 * Hook reactivo para prefers-reduced-motion
 */
export function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false)

  useEffect(() => {
    if (typeof window === 'undefined') return
    const mq = window.matchMedia('(prefers-reduced-motion: reduce)')
    setReduced(mq.matches)

    const onChange = (e: MediaQueryListEvent) => setReduced(e.matches)
    mq.addEventListener('change', onChange)
    return () => mq.removeEventListener('change', onChange)
  }, [])

  return reduced
}

/**
 * Variantes de movimiento reutilizables
 */

// 1. Aparición con fundido básico
export const fadeInVariants: Variants = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: { duration: DURATION_NORMAL, ease: easeLinearSmooth },
  },
}

// 2. Aparición con elevación suave (Fade In Up)
export const fadeInUpVariants: Variants = {
  hidden: { opacity: 0, y: 8 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: DURATION_NORMAL, ease: easeLinearSmooth },
  },
}

// 3. Contenedor de listas con entrada escalonada (Staggered List)
export const staggerContainerVariants: Variants = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: {
      staggerChildren: 0.05,
      delayChildren: 0.02,
    },
  },
}

// 4. Elemento individual dentro de lista escalonada
export const staggerItemVariants: Variants = {
  hidden: { opacity: 0, y: 6 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: DURATION_NORMAL, ease: easeLinearSmooth },
  },
}

// 5. Transición entre páginas
export const pageTransitionVariants: Variants = {
  initial: { opacity: 0, y: 4 },
  animate: {
    opacity: 1,
    y: 0,
    transition: { duration: DURATION_NORMAL, ease: easeLinearSmooth },
  },
  exit: {
    opacity: 0,
    y: -4,
    transition: { duration: DURATION_FAST, ease: easeLinearSmooth },
  },
}

// 6. Apertura de diálogo modal
export const dialogScaleVariants: Variants = {
  hidden: { opacity: 0, scale: 0.97, y: 4 },
  visible: {
    opacity: 1,
    scale: 1,
    y: 0,
    transition: { duration: DURATION_NORMAL, ease: easeLinearSmooth },
  },
  exit: {
    opacity: 0,
    scale: 0.97,
    y: 4,
    transition: { duration: DURATION_FAST, ease: easeLinearSmooth },
  },
}

// 7. Apertura de menús desplegables / popovers
export const dropdownMenuVariants: Variants = {
  hidden: { opacity: 0, scale: 0.96, y: -4 },
  visible: {
    opacity: 1,
    scale: 1,
    y: 0,
    transition: { duration: DURATION_FAST, ease: easeLinearSmooth },
  },
  exit: {
    opacity: 0,
    scale: 0.96,
    y: -4,
    transition: { duration: 0.12, ease: easeLinearSmooth },
  },
}

/**
 * Variantes simplificadas para cuando el usuario prefiere movimiento reducido
 */
export const reducedMotionVariants: Variants = {
  hidden: { opacity: 0 },
  visible: { opacity: 1, transition: { duration: 0.05 } },
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: { duration: 0.05 } },
  exit: { opacity: 0, transition: { duration: 0.05 } },
}
