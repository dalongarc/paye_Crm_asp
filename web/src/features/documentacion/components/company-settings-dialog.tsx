import React, { useEffect, useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Building2, ShieldAlert, RotateCcw, Sparkles } from 'lucide-react'
import type {
  DocumentacionCompanySettingsDto,
  UpdateDocumentacionCompanySettingsRequest,
} from '@/api/types'
import {
  DEFAULT_LEGAL_NOTICE_TEXT,
  DEFAULT_LEGAL_NOTICE_TITLE,
  DOCUMENTACION_ASSETS,
} from '../config/default-templates'

export interface CompanySettingsDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  settings: DocumentacionCompanySettingsDto
  isSaving?: boolean
  onSave: (dto: UpdateDocumentacionCompanySettingsRequest) => Promise<void>
}

export const CompanySettingsDialog: React.FC<CompanySettingsDialogProps> = ({
  open,
  onOpenChange,
  settings,
  isSaving = false,
  onSave,
}) => {
  const [form, setForm] = useState<UpdateDocumentacionCompanySettingsRequest>({
    nombreEmpresa: settings.nombreEmpresa,
    cifNif: settings.cifNif,
    direccion: settings.direccion,
    telefono: settings.telefono,
    email: settings.email,
    web: settings.web,
    iban: settings.iban,
    logoUrl: settings.logoUrl,
    textoLegalTitulo: settings.textoLegalTitulo,
    textoLegalContenido: settings.textoLegalContenido,
    modeloIa: settings.modeloIa || 'gemini-3.8-flash',
    rowVersion: settings.rowVersion,
  })

  useEffect(() => {
    if (open) {
      setForm({
        nombreEmpresa: settings.nombreEmpresa,
        cifNif: settings.cifNif,
        direccion: settings.direccion,
        telefono: settings.telefono,
        email: settings.email,
        web: settings.web,
        iban: settings.iban,
        logoUrl: settings.logoUrl,
        textoLegalTitulo: settings.textoLegalTitulo,
        textoLegalContenido: settings.textoLegalContenido,
        modeloIa: settings.modeloIa || 'gemini-3.8-flash',
        rowVersion: settings.rowVersion,
      })
    }
  }, [open, settings])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    await onSave(form)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='max-w-2xl max-h-[90vh] overflow-y-auto'>
        <DialogHeader>
          <DialogTitle className='flex items-center gap-2 text-base'>
            <Building2 className='h-4 w-4 text-primary' />
            <span>Configuración de Empresa y Pie Legal (Documentación)</span>
          </DialogTitle>
          <DialogDescription className='text-xs'>
            Estos datos alimentan la cabecera, los cuadros de partes contratantes, el logo de
            portada/pie y el aviso de Propiedad Intelectual y Confidencialidad.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className='space-y-4 py-2'>
          <div className='grid grid-cols-1 sm:grid-cols-2 gap-3'>
            <div className='space-y-1.5'>
              <Label className='text-xs'>Nombre comercial / Razón Social</Label>
              <Input
                value={form.nombreEmpresa}
                onChange={(e) => setForm({ ...form, nombreEmpresa: e.target.value })}
                placeholder='Payé Digital'
                className='h-8 text-xs'
              />
            </div>

            <div className='space-y-1.5'>
              <Label className='text-xs'>CIF / NIF</Label>
              <Input
                value={form.cifNif}
                onChange={(e) => setForm({ ...form, cifNif: e.target.value })}
                placeholder='B12345678'
                className='h-8 text-xs'
              />
            </div>

            <div className='space-y-1.5 sm:col-span-2'>
              <Label className='text-xs'>Dirección fiscal</Label>
              <Input
                value={form.direccion}
                onChange={(e) => setForm({ ...form, direccion: e.target.value })}
                placeholder='Calle Real 10, 15003 A Coruña'
                className='h-8 text-xs'
              />
            </div>

            <div className='space-y-1.5'>
              <Label className='text-xs'>Teléfono</Label>
              <Input
                value={form.telefono}
                onChange={(e) => setForm({ ...form, telefono: e.target.value })}
                placeholder='+34 600 000 000'
                className='h-8 text-xs'
              />
            </div>

            <div className='space-y-1.5'>
              <Label className='text-xs'>Email de contacto</Label>
              <Input
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                placeholder='hola@payedigital.com'
                className='h-8 text-xs'
              />
            </div>

            <div className='space-y-1.5'>
              <Label className='text-xs'>Sitio Web</Label>
              <Input
                value={form.web}
                onChange={(e) => setForm({ ...form, web: e.target.value })}
                placeholder='www.payedigital.com'
                className='h-8 text-xs'
              />
            </div>

            <div className='space-y-1.5'>
              <Label className='text-xs'>IBAN para pagos</Label>
              <Input
                value={form.iban}
                onChange={(e) => setForm({ ...form, iban: e.target.value })}
                placeholder='ES00 0000 0000 0000 0000 0000'
                className='h-8 text-xs font-mono'
              />
            </div>

            <div className='space-y-1.5 sm:col-span-2'>
              <div className='flex items-center justify-between'>
                <Label className='text-xs'>
                  Ruta local del Logo (solo ruta interna `/images/...` o `data:image/...`)
                </Label>
                <Button
                  type='button'
                  variant='ghost'
                  size='sm'
                  className='h-6 px-2 text-[11px]'
                  onClick={() =>
                    setForm({
                      ...form,
                      logoUrl: DOCUMENTACION_ASSETS.logoHorizontalDark,
                    })
                  }
                >
                  Usar logo horizontal oficial
                </Button>
              </div>
              <Input
                value={form.logoUrl}
                onChange={(e) => setForm({ ...form, logoUrl: e.target.value })}
                placeholder='/images/documentacion/logo-paye-horizontal.png'
                className='h-8 text-xs font-mono'
              />
            </div>
          </div>

          <div className='rounded-xl border border-border/80 bg-muted/20 p-3.5 space-y-3'>
            <div className='flex items-center justify-between gap-2'>
              <div className='flex items-center gap-1.5 text-xs font-semibold'>
                <ShieldAlert className='h-3.5 w-3.5 text-amber-500' />
                <span>Texto Legal del Pie de Página</span>
              </div>
              <Button
                type='button'
                variant='outline'
                size='sm'
                className='h-6 px-2 text-[11px] gap-1'
                onClick={() =>
                  setForm({
                    ...form,
                    textoLegalTitulo: DEFAULT_LEGAL_NOTICE_TITLE,
                    textoLegalContenido: DEFAULT_LEGAL_NOTICE_TEXT,
                  })
                }
              >
                <RotateCcw className='h-3 w-3' />
                <span>Restaurar texto del PDF</span>
              </Button>
            </div>

            <div className='space-y-1.5'>
              <Label className='text-xs'>Título del bloque legal</Label>
              <Input
                value={form.textoLegalTitulo}
                onChange={(e) => setForm({ ...form, textoLegalTitulo: e.target.value })}
                className='h-8 text-xs font-semibold'
              />
            </div>

            <div className='space-y-1.5'>
              <Label className='text-xs'>
                Contenido de confidencialidad y propiedad intelectual
              </Label>
              <Textarea
                rows={4}
                value={form.textoLegalContenido}
                onChange={(e) =>
                  setForm({ ...form, textoLegalContenido: e.target.value })
                }
                className='text-xs leading-relaxed'
              />
            </div>
          </div>

          <div className='rounded-xl border border-amber-500/40 bg-amber-500/5 p-3.5 space-y-2.5'>
            <div className='flex items-center gap-1.5 text-xs font-semibold'>
              <Sparkles className='h-3.5 w-3.5 text-amber-500' />
              <span>Modelo de IA (Google Gemini) para Redacción de Textos</span>
            </div>
            <p className='text-[11px] text-muted-foreground'>
              Selecciona el modelo de Gemini que utilizará el servidor al generar borradores
              o regenerar bloques desde la pestaña «Origen de datos».
            </p>
            <div className='grid grid-cols-1 sm:grid-cols-2 gap-2.5'>
              <div className='space-y-1'>
                <Label className='text-[11px]'>Modelos recomendados</Label>
                <select
                  aria-label='Seleccionar modelo de IA'
                  value={form.modeloIa || 'gemini-3.8-flash'}
                  onChange={(e) => setForm({ ...form, modeloIa: e.target.value })}
                  className='w-full h-8 rounded-md border border-input bg-background px-2.5 text-xs font-mono'
                  data-testid='select-modelo-ia-ajustes'
                >
                  <option value='gemini-3.8-flash'>
                    gemini-3.8-flash (Actual · Recomendado · Rápido y preciso)
                  </option>
                  <option value='gemini-3.8-pro'>
                    gemini-3.8-pro (Actual · Máxima calidad de redacción)
                  </option>
                  <option value='gemini-2.5-flash'>
                    gemini-2.5-flash (Estable · Rápido y equilibrado)
                  </option>
                  <option value='gemini-2.5-pro'>
                    gemini-2.5-pro (Estable · Redacción profunda)
                  </option>
                  <option value='gemini-2.5-flash-lite'>
                    gemini-2.5-flash-lite (Ultra rápido y económico)
                  </option>
                  <option value='gemini-2.0-flash'>
                    gemini-2.0-flash (Versión 2.0)
                  </option>
                </select>
              </div>
              <div className='space-y-1'>
                <Label className='text-[11px]'>Identificador del modelo activo</Label>
                <Input
                  value={form.modeloIa || 'gemini-3.8-flash'}
                  onChange={(e) => setForm({ ...form, modeloIa: e.target.value })}
                  placeholder='gemini-3.8-flash'
                  className='h-8 text-xs font-mono'
                  data-testid='input-modelo-ia-ajustes'
                />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button
              type='button'
              variant='ghost'
              size='sm'
              onClick={() => onOpenChange(false)}
            >
              Cancelar
            </Button>
            <Button type='submit' size='sm' disabled={isSaving}>
              {isSaving ? 'Guardando...' : 'Guardar configuración'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
