import React, { useRef, useState } from 'react'
import {
  Sparkles,
  Upload,
  FileText,
  Trash2,
  ShieldAlert,
  CheckCircle2,
  RotateCcw,
  Layers,
  Plus,
  Replace,
  AlertTriangle,
  Info,
} from 'lucide-react'
import DOMPurify from 'dompurify'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { toast } from 'sonner'
import { api } from '@/api'
import type {
  DocumentoAiBloqueGeneradoDto,
  DocumentoAiBorradorResponseDto,
  DocumentoAiLineaSugeridaDto,
  DocumentoSecretFindingDto,
  TipoDocumentoComercial,
} from '@/api/types'

const MAX_SOURCE_CHARS = 60000

export interface OrigenDatosTabProps {
  tipo: TipoDocumentoComercial
  documentoId?: string
  textoOrigen: string
  nombreArchivoOrigen: string
  tratoCliente: string
  instruccionesExtra: string
  canUndoAiApply: boolean
  onChangeTextoOrigen: (text: string, fileName?: string) => void
  onChangeTratoCliente: (trato: string) => void
  onChangeInstruccionesExtra: (extra: string) => void
  onClearOrigenDatos: () => void
  onApplyDraft: (
    draft: DocumentoAiBorradorResponseDto,
    mode: 'replace' | 'append'
  ) => void
  onUndoLastAiApply: () => void
}

export const OrigenDatosTab: React.FC<OrigenDatosTabProps> = ({
  tipo,
  documentoId,
  textoOrigen,
  nombreArchivoOrigen,
  tratoCliente,
  instruccionesExtra,
  canUndoAiApply,
  onChangeTextoOrigen,
  onChangeTratoCliente,
  onChangeInstruccionesExtra,
  onClearOrigenDatos,
  onApplyDraft,
  onUndoLastAiApply,
}) => {
  const isContrato = tipo === 1
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  const [isExtractingFile, setIsExtractingFile] = useState(false)
  const [isGenerating, setIsGenerating] = useState(false)
  const [selectedModel, setSelectedModel] = useState<string>('gemini-3.8-flash')
  const [previewDraft, setPreviewDraft] =
    useState<DocumentoAiBorradorResponseDto | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  // Estado del diálogo de advertencia de posibles datos sensibles
  const [secretDialogOpen, setSecretDialogOpen] = useState(false)
  const [secretFindings, setSecretFindings] = useState<DocumentoSecretFindingDto[]>([])
  const [redactedCandidate, setRedactedCandidate] = useState<string>('')

  const charCount = textoOrigen.length
  const isOverLimit = charCount > MAX_SOURCE_CHARS

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    setErrorMessage(null)
    setIsExtractingFile(true)
    try {
      const result = await api.documentacion.extractFile(file)
      onChangeTextoOrigen(result.extractedText, result.fileName)
      if (result.exceededMaxCharacters) {
        toast.warning(
          `El archivo supera los ${result.maxAllowedCharacters.toLocaleString('es-ES')} caracteres. Recorta el texto antes de generar.`
        )
      } else {
        toast.success(
          `Archivo "${result.fileName}" cargado (${result.characterCount.toLocaleString('es-ES')} caracteres).`
        )
      }
    } catch (err: unknown) {
      const msg =
        err instanceof Error
          ? err.message
          : 'No se pudo extraer el texto del archivo seleccionado.'
      setErrorMessage(msg)
      toast.error(msg)
    } finally {
      setIsExtractingFile(false)
      if (fileInputRef.current) {
        fileInputRef.current.value = ''
      }
    }
  }

  const executeGeneration = async (
    sourceToUse: string,
    eliminarSecretosDetectados: boolean
  ) => {
    setErrorMessage(null)
    setIsGenerating(true)
    try {
      const response = await api.documentacion.generarBorrador({
        tipo,
        documentoId: documentoId ?? null,
        textoOrigen: sourceToUse,
        tratoCliente,
        instruccionesExtra: instruccionesExtra.trim() || null,
        modeloIa: selectedModel,
        eliminarSecretosDetectados,
      })
      setPreviewDraft(response)
      toast.success(
        'Borrador generado en vista previa. Revísalo antes de aplicarlo al documento.'
      )
    } catch (err: unknown) {
      const msg =
        err instanceof Error
          ? err.message
          : 'No se pudo generar el borrador con IA.'
      setErrorMessage(msg)
      toast.error(msg)
    } finally {
      setIsGenerating(false)
    }
  }

  const handleClickGenerarBorrador = async () => {
    const trimmed = textoOrigen.trim()
    if (!trimmed) {
      toast.error(
        'Pega la documentación del proyecto o sube un archivo (.md, .txt, .pdf) primero.'
      )
      return
    }
    if (isOverLimit) {
      toast.error(
        `El texto supera el límite de ${MAX_SOURCE_CHARS.toLocaleString('es-ES')} caracteres. Redúcelo antes de continuar.`
      )
      return
    }

    setErrorMessage(null)
    setIsGenerating(true)
    try {
      const scan = await api.documentacion.scanSecrets(trimmed)
      if (scan.hasSecrets && scan.findings.length > 0) {
        setSecretFindings(scan.findings)
        setRedactedCandidate(scan.redactedText)
        setSecretDialogOpen(true)
        setIsGenerating(false)
        return
      }
      await executeGeneration(trimmed, false)
    } catch (err: unknown) {
      setIsGenerating(false)
      const msg =
        err instanceof Error
          ? err.message
          : 'Error al analizar o enviar el texto de origen.'
      setErrorMessage(msg)
      toast.error(msg)
    }
  }

  const handleConfirmSanitizeAndContinue = async () => {
    setSecretDialogOpen(false)
    if (redactedCandidate) {
      onChangeTextoOrigen(redactedCandidate, nombreArchivoOrigen)
      await executeGeneration(redactedCandidate, true)
    }
  }

  const renderSafeHtml = (html: string) => ({
    __html: DOMPurify.sanitize(html, {
      ALLOWED_TAGS: [
        'p',
        'br',
        'strong',
        'b',
        'em',
        'i',
        'u',
        'ul',
        'ol',
        'li',
        'h3',
        'h4',
        'span',
      ],
      ALLOWED_ATTR: ['class'],
    }),
  })

  return (
    <div className='space-y-4' data-testid='origen-datos-tab'>
      {/* AVISO VISIBLE DE ENVÍO A SERVICIO EXTERNO DE IA */}
      <div
        role='note'
        data-testid='ai-external-service-notice'
        className='flex items-start gap-2.5 rounded-xl border border-amber-500/40 bg-amber-500/10 px-3.5 py-2.5 text-xs text-amber-950 dark:text-amber-200'
      >
        <Info className='h-4 w-4 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5' />
        <div className='space-y-0.5'>
          <p className='font-semibold'>
            Aviso de privacidad: procesamiento con servicio externo de IA (Google Gemini)
          </p>
          <p className='text-[11px] opacity-90'>
            Al pulsar «Generar borrador», el texto de origen se analiza en el servidor
            para detectar posibles claves o contraseñas y se envía como datos a Google
            Gemini para redactar la propuesta en lenguaje claro. No incluyas secretos ni
            credenciales reales.
          </p>
        </div>
      </div>

      {/* BARRA DE DESHACER SI SE APLICÓ UN BORRADOR RECIENTEMENTE */}
      {canUndoAiApply && (
        <div className='flex items-center justify-between gap-2 rounded-xl border border-emerald-500/40 bg-emerald-500/10 px-3.5 py-2 text-xs'>
          <div className='flex items-center gap-2'>
            <CheckCircle2 className='h-4 w-4 text-emerald-600 shrink-0' />
            <span>
              Has aplicado bloques generados por IA al documento. Puedes deshacer el
              cambio si quieres volver al estado anterior.
            </span>
          </div>
          <Button
            type='button'
            variant='outline'
            size='sm'
            className='h-7 text-xs gap-1 shrink-0'
            onClick={onUndoLastAiApply}
            data-testid='btn-undo-ai-apply'
          >
            <RotateCcw className='h-3 w-3' />
            <span>Deshacer</span>
          </Button>
        </div>
      )}

      {/* SUBIDA DE ARCHIVO (.MD, .TXT, .PDF) O PEGADO DE TEXTO */}
      <div className='rounded-xl border border-border/80 bg-muted/15 p-3.5 space-y-3'>
        <div className='flex flex-wrap items-center justify-between gap-2'>
          <div>
            <Label className='text-xs font-semibold'>
              1. Documentación de origen del proyecto
            </Label>
            <p className='text-[11px] text-muted-foreground'>
              Pega el README, manual o alcance, o sube un archivo <code>.md</code>,{' '}
              <code>.txt</code> o <code>.pdf</code>. No se leen URLs externas.
            </p>
          </div>

          <div className='flex items-center gap-1.5'>
            <input
              ref={fileInputRef}
              type='file'
              accept='.md,.txt,.pdf,text/markdown,text/plain,application/pdf'
              className='hidden'
              onChange={handleFileChange}
              data-testid='input-upload-origen-file'
            />
            <Button
              type='button'
              variant='outline'
              size='sm'
              className='h-7 px-2.5 text-xs gap-1.5'
              disabled={isExtractingFile || isGenerating}
              onClick={() => fileInputRef.current?.click()}
              data-testid='btn-upload-origen-file'
            >
              <Upload className='h-3.5 w-3.5' />
              <span>
                {isExtractingFile ? 'Extrayendo...' : 'Subir .md / .txt / .pdf'}
              </span>
            </Button>

            {(textoOrigen || nombreArchivoOrigen) && (
              <Button
                type='button'
                variant='ghost'
                size='sm'
                className='h-7 px-2 text-xs text-destructive hover:text-destructive gap-1'
                disabled={isGenerating}
                onClick={() => {
                  onClearOrigenDatos()
                  setPreviewDraft(null)
                }}
                title='Borrar texto de origen guardado en el documento'
                data-testid='btn-clear-origen-datos'
              >
                <Trash2 className='h-3.5 w-3.5' />
                <span>Borrar origen</span>
              </Button>
            )}
          </div>
        </div>

        {nombreArchivoOrigen && (
          <div className='flex items-center gap-2 rounded-lg border border-border/70 bg-background px-2.5 py-1.5 text-xs'>
            <FileText className='h-3.5 w-3.5 text-amber-500 shrink-0' />
            <span className='font-medium truncate'>
              Archivo cargado: {nombreArchivoOrigen}
            </span>
          </div>
        )}

        <div className='space-y-1.5'>
          <textarea
            value={textoOrigen}
            onChange={(e) => onChangeTextoOrigen(e.target.value, nombreArchivoOrigen)}
            rows={8}
            placeholder='Pega aquí el README, manual funcional, notas de reunión o descripción del proyecto...'
            className='w-full rounded-lg border border-input bg-background p-2.5 text-xs leading-relaxed font-mono focus:outline-none focus:ring-2 focus:ring-ring'
            data-testid='textarea-origen-datos'
          />

          <div className='flex flex-wrap items-center justify-between gap-2 text-[11px]'>
            <span className='text-muted-foreground'>
              El texto se guarda junto al documento cuando pulsas «Guardar documento».
            </span>
            <span
              className={`font-mono font-semibold ${
                isOverLimit ? 'text-destructive' : 'text-muted-foreground'
              }`}
              data-testid='char-counter-origen'
            >
              {charCount.toLocaleString('es-ES')} /{' '}
              {MAX_SOURCE_CHARS.toLocaleString('es-ES')} caracteres
            </span>
          </div>

          {isOverLimit && (
            <div
              role='alert'
              data-testid='alert-origen-over-limit'
              className='flex items-center gap-2 rounded-lg border border-destructive/50 bg-destructive/10 px-3 py-2 text-xs text-destructive'
            >
              <AlertTriangle className='h-4 w-4 shrink-0' />
              <span>
                Has superado el límite máximo de{' '}
                {MAX_SOURCE_CHARS.toLocaleString('es-ES')} caracteres. Recorta el
                contenido para poder generar el borrador.
              </span>
            </div>
          )}
        </div>
      </div>

      {/* OPCIONES DE REDACCIÓN, MODELO DE IA E INSTRUCCIONES EXTRA */}
      <div className='rounded-xl border border-border/80 bg-muted/15 p-3.5 space-y-3'>
        <Label className='text-xs font-semibold'>
          2. Modelo de IA (Gemini), tono e instrucciones adicionales
        </Label>

        <div className='grid grid-cols-1 sm:grid-cols-2 gap-2.5'>
          <div className='space-y-1'>
            <Label className='text-[11px]'>Modelo de IA a utilizar</Label>
            <select
              aria-label='Modelo de IA a utilizar'
              value={selectedModel}
              onChange={(e) => setSelectedModel(e.target.value)}
              className='w-full h-8 rounded-md border border-input bg-background px-2.5 text-xs font-mono'
              data-testid='select-modelo-ia-origen'
            >
              <option value='gemini-3.8-flash'>
                gemini-3.8-flash (Actual · Recomendado · Rápido)
              </option>
              <option value='gemini-3.8-pro'>
                gemini-3.8-pro (Actual · Máxima calidad)
              </option>
              <option value='gemini-2.5-flash'>
                gemini-2.5-flash (Estable · Equilibrado)
              </option>
              <option value='gemini-2.5-pro'>
                gemini-2.5-pro (Estable · Redacción profunda)
              </option>
              <option value='gemini-2.5-flash-lite'>
                gemini-2.5-flash-lite (Ultra rápido)
              </option>
              <option value='gemini-2.0-flash'>
                gemini-2.0-flash (Versión 2.0)
              </option>
            </select>
          </div>

          <div className='space-y-1'>
            <Label className='text-[11px]'>Trato al cliente</Label>
            <select
              aria-label='Trato al cliente'
              value={tratoCliente}
              onChange={(e) => onChangeTratoCliente(e.target.value)}
              className='w-full h-8 rounded-md border border-input bg-background px-2.5 text-xs'
              data-testid='select-trato-cliente'
            >
              <option value='Tuteo cercano y profesional (tú)'>
                Tuteo cercano y profesional (por defecto)
              </option>
              <option value='Trato formal y respetuoso (usted)'>
                Trato de usted (formal)
              </option>
              <option value='Plural cercano (vosotros / vuestro equipo)'>
                Plural cercano (vosotros / vuestra empresa)
              </option>
            </select>
          </div>

          <div className='space-y-1 sm:col-span-2'>
            <Label className='text-[11px]'>Modo de plantilla activa</Label>
            <div className='flex h-8 items-center rounded-md border border-border/70 bg-background px-2.5 text-xs font-medium'>
              {isContrato
                ? 'Prompt B: Contrato (Conserva cláusulas legales intactas)'
                : 'Prompt A: Presupuesto (Orientado a beneficios y fases)'}
            </div>
          </div>

          <div className='space-y-1 sm:col-span-2'>
            <Label className='text-[11px]'>
              Instrucciones extra para la IA (ej. «tono más cercano», «más corto»,
              «destaca el ahorro de tiempo»)
            </Label>
            <Input
              value={instruccionesExtra}
              onChange={(e) => onChangeInstruccionesExtra(e.target.value)}
              placeholder='Opcional: indica matices de estilo o puntos que quieras destacar...'
              className='h-8 text-xs'
              data-testid='input-instrucciones-extra'
            />
          </div>
        </div>

        <div className='flex items-center justify-end pt-1'>
          <Button
            type='button'
            size='sm'
            className='h-8 text-xs gap-1.5'
            disabled={isGenerating || isExtractingFile || isOverLimit || !textoOrigen.trim()}
            onClick={handleClickGenerarBorrador}
            data-testid='btn-generar-borrador-ia'
          >
            <Sparkles className='h-3.5 w-3.5' />
            <span>
              {isGenerating
                ? 'Generando borrador con Gemini...'
                : isContrato
                  ? 'Generar borrador de Contrato'
                  : 'Generar borrador de Presupuesto'}
            </span>
          </Button>
        </div>
      </div>

      {/* MENSAJE DE ERROR CLARO (CUOTA 429, TIMEOUT, VALIDACIÓN) */}
      {errorMessage && (
        <div
          role='alert'
          data-testid='alert-ai-error'
          className='flex items-start gap-2.5 rounded-xl border border-destructive/50 bg-destructive/10 px-3.5 py-2.5 text-xs text-destructive'
        >
          <AlertTriangle className='h-4 w-4 shrink-0 mt-0.5' />
          <div className='space-y-0.5'>
            <p className='font-semibold'>No se pudo completar la generación</p>
            <p>{errorMessage}</p>
          </div>
        </div>
      )}

      {/* VISTA PREVIA DE BLOQUES GENERADOS ANTES DE APLICAR AL DOCUMENTO */}
      {previewDraft && (
        <div
          data-testid='ai-draft-preview-panel'
          className='rounded-xl border-2 border-amber-500/60 bg-amber-500/5 p-3.5 space-y-3'
        >
          <div className='flex flex-wrap items-center justify-between gap-2 border-b border-amber-500/20 pb-2.5'>
            <div>
              <div className='flex items-center gap-2'>
                <Badge className='bg-amber-500 text-zinc-950 hover:bg-amber-500 text-[10px]'>
                  VISTA PREVIA DE BORRADOR IA
                </Badge>
                <span className='text-xs font-semibold text-foreground'>
                  {previewDraft.bloques.length} bloques propuestos
                </span>
              </div>
              <p className='text-[11px] text-muted-foreground mt-0.5'>
                El documento actual aún no se ha modificado. Elige cómo quieres aplicar
                este borrador:
              </p>
            </div>

            <div className='flex flex-wrap items-center gap-1.5'>
              <Button
                type='button'
                size='sm'
                className='h-7 px-2.5 text-xs gap-1'
                onClick={() => {
                  onApplyDraft(previewDraft, 'replace')
                  setPreviewDraft(null)
                }}
                data-testid='btn-apply-draft-replace'
              >
                <Replace className='h-3.5 w-3.5' />
                <span>
                  {isContrato
                    ? 'Reemplazar alcance (conservar cláusulas legales)'
                    : 'Reemplazar bloques actuales'}
                </span>
              </Button>

              <Button
                type='button'
                variant='outline'
                size='sm'
                className='h-7 px-2.5 text-xs gap-1'
                onClick={() => {
                  onApplyDraft(previewDraft, 'append')
                  setPreviewDraft(null)
                }}
                data-testid='btn-apply-draft-append'
              >
                <Plus className='h-3.5 w-3.5' />
                <span>Añadir al final</span>
              </Button>
            </div>
          </div>

          {/* Título y subtítulo sugeridos */}
          <div className='rounded-lg border border-border/70 bg-background p-2.5 text-xs space-y-1'>
            <div className='flex flex-wrap items-center justify-between gap-1'>
              <span className='text-[11px] font-semibold text-muted-foreground'>
                Portada sugerida:
              </span>
              <span className='text-[10px] font-mono text-muted-foreground'>
                Modelo: {previewDraft.modeloUtilizado}
              </span>
            </div>
            <p className='font-bold text-foreground'>{previewDraft.titulo}</p>
            <p className='italic text-muted-foreground'>{previewDraft.subtitulo}</p>
          </div>

          {/* Lista de bloques generados en la vista previa */}
          <div className='space-y-2 max-h-[340px] overflow-y-auto pr-1'>
            {previewDraft.bloques.map((blk: DocumentoAiBloqueGeneradoDto, idx: number) => (
              <div
                key={`preview-blk-${idx}`}
                className='rounded-lg border border-border/80 bg-background p-3 space-y-1.5 text-xs'
              >
                <div className='flex items-center justify-between gap-2'>
                  <div className='flex items-center gap-1.5'>
                    <Layers className='h-3.5 w-3.5 text-amber-500' />
                    <span className='font-bold uppercase'>{blk.titulo}</span>
                  </div>
                  <Badge variant='outline' className='text-[10px]'>
                    {blk.tipo}
                  </Badge>
                </div>

                <div
                  className='prose prose-xs dark:prose-invert max-w-none text-xs leading-relaxed'
                  dangerouslySetInnerHTML={renderSafeHtml(blk.contenido)}
                />

                {blk.pendiente && blk.pendiente.length > 0 && (
                  <div className='rounded-md border border-amber-500/40 bg-amber-500/10 px-2.5 py-1.5 text-[11px] text-amber-900 dark:text-amber-200'>
                    <p className='font-semibold'>Pendiente de confirmar / completar:</p>
                    <ul className='list-disc pl-4 space-y-0.5'>
                      {blk.pendiente.map((p, pIdx) => (
                        <li key={pIdx}>{p}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            ))}
          </div>

          {/* Líneas de concepto sugeridas (solo en Presupuesto, sin precios inventados) */}
          {!isContrato &&
            previewDraft.lineasSugeridas &&
            previewDraft.lineasSugeridas.length > 0 && (
              <div className='rounded-lg border border-border/80 bg-background p-2.5 space-y-1.5 text-xs'>
                <p className='font-semibold text-[11px] text-muted-foreground uppercase'>
                  Líneas de concepto sugeridas (importe vacío / 0,00 € para rellenar):
                </p>
                <ul className='space-y-1'>
                  {previewDraft.lineasSugeridas.map(
                    (lin: DocumentoAiLineaSugeridaDto, lIdx: number) => (
                      <li
                        key={`sug-line-${lIdx}`}
                        className='flex items-center justify-between gap-2 border-b border-border/40 pb-1 last:border-0 last:pb-0'
                      >
                        <span>{lin.descripcion}</span>
                        <Badge variant='secondary' className='font-mono text-[10px]'>
                          Cant: {lin.cantidad} · Precio: a completar
                        </Badge>
                      </li>
                    )
                  )}
                </ul>
              </div>
            )}
        </div>
      )}

      {/* DIÁLOGO DE AVISO SI SE DETECTAN POSIBLES SECRETOS ANTES DE ENVIAR */}
      <AlertDialog open={secretDialogOpen} onOpenChange={setSecretDialogOpen}>
        <AlertDialogContent data-testid='dialog-secret-warning'>
          <AlertDialogHeader>
            <AlertDialogTitle className='flex items-center gap-2 text-amber-600'>
              <ShieldAlert className='h-5 w-5' />
              <span>Posibles datos sensibles o credenciales detectados</span>
            </AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className='space-y-2 text-xs text-muted-foreground'>
                <p>
                  Antes de enviar el texto a Google Gemini, el detector de seguridad ha
                  encontrado elementos que parecen claves, tokens o contraseñas:
                </p>
                <ul className='rounded-lg border border-border bg-muted/40 p-2.5 space-y-1 font-mono text-[11px]'>
                  {secretFindings.map((f, i) => (
                    <li key={i}>
                      <strong>{f.category}:</strong> {f.description} ({f.maskedPreview})
                    </li>
                  ))}
                </ul>
                <p>
                  ¿Deseas cancelar para revisarlo manualmente o eliminar automáticamente
                  esos valores (<code>[SECRETO_ELIMINADO]</code>) y continuar?
                </p>
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel data-testid='btn-cancel-secrets'>
              Cancelar envío
            </AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmSanitizeAndContinue}
              data-testid='btn-redact-and-continue'
            >
              Eliminar secretos y continuar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
