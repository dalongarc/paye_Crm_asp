import React, { useEffect, useRef, useState } from 'react'
import {
  Bold,
  Italic,
  Underline,
  List,
  ListOrdered,
  Table as TableIcon,
  RotateCcw,
  Code2,
  Eye,
  Sparkles,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { toast } from 'sonner'
import { cleanPastedWordHtml, sanitizeDocumentoHtml } from '../utils/html-sanitizer'

export interface LightRichTextEditorProps {
  value: string
  onChange: (cleanHtml: string) => void
  originalHtml?: string | null
  onRestoreOriginal?: () => void
  placeholder?: string
}

const SIMPLE_TABLE_HTML = `<table><thead><tr><th>Concepto</th><th>Detalle</th><th>Importe</th></tr></thead><tbody><tr><td>Elemento 1</td><td>Descripción del entregable</td><td>Incluido</td></tr><tr><td>Elemento 2</td><td>Soporte y puesta en marcha</td><td>Incluido</td></tr></tbody></table><p></p>`

export const LightRichTextEditor: React.FC<LightRichTextEditorProps> = ({
  value,
  onChange,
  originalHtml,
  onRestoreOriginal,
  placeholder = 'Escribe el contenido de esta cláusula o sección...',
}) => {
  const editorRef = useRef<HTMLDivElement | null>(null)
  const [sourceMode, setSourceMode] = useState(false)

  const sanitizedValue = sanitizeDocumentoHtml(value)
  const canRestore =
    Boolean(originalHtml) &&
    Boolean(onRestoreOriginal) &&
    sanitizeDocumentoHtml(originalHtml || '') !== sanitizedValue

  useEffect(() => {
    if (sourceMode) return
    const el = editorRef.current
    if (!el) return
    const currentDomSanitized = sanitizeDocumentoHtml(el.innerHTML)
    if (currentDomSanitized !== sanitizedValue) {
      el.innerHTML = sanitizedValue
    }
  }, [sanitizedValue, sourceMode])

  const emitChangeFromDom = () => {
    const el = editorRef.current
    if (!el) return
    const cleaned = sanitizeDocumentoHtml(el.innerHTML)
    if (cleaned !== sanitizedValue) {
      onChange(cleaned)
    }
  }

  const execFormat = (command: string, commandVal?: string) => {
    if (sourceMode) return
    const el = editorRef.current
    if (!el) return
    el.focus()
    document.execCommand(command, false, commandVal)
    emitChangeFromDom()
  }

  const handleInsertTable = () => {
    if (sourceMode) {
      onChange(sanitizeDocumentoHtml(`${value}\n${SIMPLE_TABLE_HTML}`))
      return
    }
    execFormat('insertHTML', SIMPLE_TABLE_HTML)
  }

  const handlePaste = (e: React.ClipboardEvent<HTMLDivElement>) => {
    e.preventDefault()
    const htmlData = e.clipboardData.getData('text/html')
    const textData = e.clipboardData.getData('text/plain')

    const cleanHtml = cleanPastedWordHtml(htmlData, textData)
    if (!cleanHtml) return

    const el = editorRef.current
    if (!el) return
    el.focus()
    document.execCommand('insertHTML', false, cleanHtml)
    emitChangeFromDom()

    if (htmlData && /Mso|urn:schemas-microsoft-com|<o:p>/i.test(htmlData)) {
      toast.info('Formato de Word limpiado automáticamente con DOMPurify')
    }
  }

  return (
    <div className='rounded-xl border border-border/80 bg-background overflow-hidden shadow-2xs'>
      {/* Barra de herramientas enriquecida ligera */}
      <div className='flex flex-wrap items-center justify-between gap-1.5 border-b border-border/70 bg-muted/30 px-2.5 py-1.5'>
        <div className='flex flex-wrap items-center gap-0.5'>
          <Button
            type='button'
            variant='ghost'
            size='sm'
            className='h-7 w-7 p-0'
            title='Negrita (Ctrl+B)'
            disabled={sourceMode}
            onClick={() => execFormat('bold')}
          >
            <Bold className='h-3.5 w-3.5' />
          </Button>
          <Button
            type='button'
            variant='ghost'
            size='sm'
            className='h-7 w-7 p-0'
            title='Cursiva (Ctrl+I)'
            disabled={sourceMode}
            onClick={() => execFormat('italic')}
          >
            <Italic className='h-3.5 w-3.5' />
          </Button>
          <Button
            type='button'
            variant='ghost'
            size='sm'
            className='h-7 w-7 p-0'
            title='Subrayado (Ctrl+U)'
            disabled={sourceMode}
            onClick={() => execFormat('underline')}
          >
            <Underline className='h-3.5 w-3.5' />
          </Button>

          <span className='mx-1 h-4 w-px bg-border/80' />

          <Button
            type='button'
            variant='ghost'
            size='sm'
            className='h-7 w-7 p-0'
            title='Lista con viñetas'
            disabled={sourceMode}
            onClick={() => execFormat('insertUnorderedList')}
          >
            <List className='h-3.5 w-3.5' />
          </Button>
          <Button
            type='button'
            variant='ghost'
            size='sm'
            className='h-7 w-7 p-0'
            title='Lista numerada'
            disabled={sourceMode}
            onClick={() => execFormat('insertOrderedList')}
          >
            <ListOrdered className='h-3.5 w-3.5' />
          </Button>
          <Button
            type='button'
            variant='ghost'
            size='sm'
            className='h-7 px-2 text-xs gap-1'
            title='Insertar tabla simple'
            onClick={handleInsertTable}
          >
            <TableIcon className='h-3.5 w-3.5' />
            <span className='hidden sm:inline'>Tabla</span>
          </Button>
        </div>

        <div className='flex items-center gap-1'>
          {canRestore && (
            <Button
              type='button'
              variant='outline'
              size='sm'
              className='h-7 px-2 text-[11px] gap-1 border-amber-500/40 text-amber-700 dark:text-amber-300 hover:bg-amber-500/10'
              title='Restablecer al texto original de la plantilla'
              onClick={onRestoreOriginal}
              data-testid='btn-restablecer-bloque'
            >
              <RotateCcw className='h-3 w-3' />
              <span>Restablecer original</span>
            </Button>
          )}

          <Button
            type='button'
            variant='ghost'
            size='sm'
            className='h-7 px-2 text-[11px] gap-1 text-muted-foreground hover:text-foreground'
            onClick={() => setSourceMode((prev) => !prev)}
            title={sourceMode ? 'Volver al editor visual' : 'Ver HTML limpio'}
          >
            {sourceMode ? (
              <>
                <Eye className='h-3 w-3' />
                <span>Visual</span>
              </>
            ) : (
              <>
                <Code2 className='h-3 w-3' />
                <span>HTML</span>
              </>
            )}
          </Button>
        </div>
      </div>

      {/* Área editable con limpieza automática de Word y DOMPurify */}
      {sourceMode ? (
        <Textarea
          value={value}
          onChange={(e) => onChange(sanitizeDocumentoHtml(e.target.value))}
          rows={6}
          className='border-0 rounded-none font-mono text-xs focus-visible:ring-0'
          placeholder={placeholder}
        />
      ) : (
        <div
          ref={editorRef}
          contentEditable
          suppressContentEditableWarning
          onInput={emitChangeFromDom}
          onBlur={emitChangeFromDom}
          onPaste={handlePaste}
          data-placeholder={placeholder}
          className='min-h-[120px] max-h-[290px] overflow-y-auto px-3.5 py-2.5 text-xs leading-relaxed text-foreground focus:outline-none [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:list-decimal [&_ol]:pl-5 [&_p]:mb-1.5 [&_table]:w-full [&_table]:border-collapse [&_th]:border [&_th]:border-border [&_th]:bg-muted/50 [&_th]:px-2 [&_th]:py-1 [&_td]:border [&_td]:border-border [&_td]:px-2 [&_td]:py-1'
        />
      )}

      <div className='flex items-center justify-between bg-muted/20 px-3 py-1 border-t border-border/50 text-[10px] text-muted-foreground'>
        <span className='inline-flex items-center gap-1'>
          <Sparkles className='h-2.5 w-2.5 text-amber-500' />
          Pegado desde Word limpio automáticamente (DOMPurify activo)
        </span>
        <span>Etiquetas seguras permitidas</span>
      </div>
    </div>
  )
}
