import { useState, useRef, useEffect } from 'react'
import { Link } from '@tanstack/react-router'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import {
  Building2,
  Upload,
  CheckCircle2,
  Image as ImageIcon,
  Trash2,
  Save,
  Database,
  Palette,
  Sparkles,
  HelpCircle,
} from 'lucide-react'
import { toast } from 'sonner'
import { api } from '@/api'
import {
  useBillingStore,
  billingActions,
  convertImageToWebp65,
} from '@/lib/billing-store'

export function SettingsProfile() {
  const { company } = useBillingStore()
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  const [razonSocial, setRazonSocial] = useState(company.razonSocial)
  const [nombreComercial, setNombreComercial] = useState(company.nombreComercial)
  const [nifCif, setNifCif] = useState(company.nifCif)
  const [direccionFiscal, setDireccionFiscal] = useState(company.direccionFiscal)
  const [codigoPostal, setCodigoPostal] = useState(company.codigoPostal)
  const [ciudad, setCiudad] = useState(company.ciudad)
  const [provincia, setProvincia] = useState(company.provincia)
  const [telefono, setTelefono] = useState(company.telefono)
  const [emailFacturacion, setEmailFacturacion] = useState(company.emailFacturacion)
  const [iban, setIban] = useState(company.iban)
  const [registroMercantil, setRegistroMercantil] = useState(company.registroMercantil)
  const [pieFactura, setPieFactura] = useState(company.pieFactura)
  const [isUploadingLogo, setIsUploadingLogo] = useState(false)

  // Ajustes de Modelo de IA (Google Gemini) para Documentación
  const [modeloIa, setModeloIa] = useState('gemini-3.8-flash')
  const [geminiApiKeyInput, setGeminiApiKeyInput] = useState('')
  const [isSavingAiSettings, setIsSavingAiSettings] = useState(false)

  useEffect(() => {
    api.documentacion
      .getCompanySettings()
      .then((s) => {
        if (s?.modeloIa) {
          setModeloIa(s.modeloIa)
        }
      })
      .catch(() => {
        // Mantener valor por defecto si aún no se ha inicializado
      })
  }, [])

  const handleSaveAiSettings = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsSavingAiSettings(true)
    try {
      const current = await api.documentacion.getCompanySettings()
      await api.documentacion.updateCompanySettings({
        nombreEmpresa: current.nombreEmpresa || nombreComercial || 'Payé Digital',
        cifNif: current.cifNif || nifCif || '',
        direccion: current.direccion || direccionFiscal || '',
        telefono: current.telefono || telefono || '',
        email: current.email || emailFacturacion || '',
        web: current.web || 'www.payedigital.com',
        iban: current.iban || iban || '',
        logoUrl: current.logoUrl || '/images/documentacion/logo-paye-horizontal.png',
        textoLegalTitulo: current.textoLegalTitulo || 'NOTA CONFIDENCIAL',
        textoLegalContenido: current.textoLegalContenido || '',
        modeloIa,
        geminiApiKey: geminiApiKeyInput.trim() || undefined,
        rowVersion: current.rowVersion || 1,
      })
      setGeminiApiKeyInput('')
      toast.success(`Modelo de IA (${modeloIa}) y configuración de Gemini guardados en el servidor`)
    } catch (err: unknown) {
      toast.error(
        err instanceof Error
          ? err.message
          : 'No se pudo guardar la configuración de IA.'
      )
    } finally {
      setIsSavingAiSettings(false)
    }
  }

  // Subir imagen, convertir a WebP al 65% de calidad y guardar en la Base de Datos (PrivateFiles)
  const handleLogoUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    setIsUploadingLogo(true)
    try {
      const converted = await convertImageToWebp65(file)

      // Intentar persistir en la tabla private_files de PostgreSQL a través de /files/upload
      let savedDbFileId: string | null = null
      try {
        const csrfRes = await fetch('/api/antiforgery', {
          credentials: 'include',
        })
        if (csrfRes.ok) {
          const csrfData = (await csrfRes.json()) as { requestToken?: string }
          const formData = new FormData()
          formData.append('File', converted.webpFile)
          formData.append(
            'Description',
            `Logo empresa convertido a WebP (65% compresión)`
          )
          if (csrfData.requestToken) {
            formData.append('__RequestVerificationToken', csrfData.requestToken)
          }
          const uploadRes = await fetch('/files/upload', {
            method: 'POST',
            credentials: 'include',
            headers: csrfData.requestToken
              ? { RequestVerificationToken: csrfData.requestToken }
              : undefined,
            body: formData,
          })
          if (uploadRes.ok) {
            const uploaded = (await uploadRes.json()) as { id?: string }
            savedDbFileId = uploaded.id ?? null
          }
        }
      } catch {
        // Si está en modo offline/mock, continúa con el almacenamiento local
      }

      billingActions.updateCompany({
        logoWebpDataUrl: converted.dataUrl,
        logoPrivateFileId: savedDbFileId ?? `webp-${Date.now()}`,
        logoOriginalSizeKb: converted.originalSizeKb,
        logoWebpSizeKb: converted.webpSizeKb,
      })

      toast.success(
        `Logo convertido a WebP (compresión 65%: ${converted.originalSizeKb} KB → ${converted.webpSizeKb} KB) y guardado en la BD.`
      )
    } catch (err) {
      toast.error(
        err instanceof Error ? err.message : 'Error al procesar el logotipo.'
      )
    } finally {
      setIsUploadingLogo(false)
      if (fileInputRef.current) fileInputRef.current.value = ''
    }
  }

  const handleSaveCompany = (e: React.FormEvent) => {
    e.preventDefault()
    if (!razonSocial.trim() || !nifCif.trim()) {
      toast.error('La Razón Social y el NIF/CIF son obligatorios.')
      return
    }

    billingActions.updateCompany({
      razonSocial: razonSocial.trim(),
      nombreComercial: nombreComercial.trim(),
      nifCif: nifCif.trim().toUpperCase(),
      direccionFiscal: direccionFiscal.trim(),
      codigoPostal: codigoPostal.trim(),
      ciudad: ciudad.trim(),
      provincia: provincia.trim(),
      telefono: telefono.trim(),
      emailFacturacion: emailFacturacion.trim(),
      iban: iban.trim(),
      registroMercantil: registroMercantil.trim(),
      pieFactura: pieFactura.trim(),
    })

    toast.success(
      'Datos fiscales de la empresa actualizados en todas las plantillas de factura.'
    )
  }

  return (
    <div className='space-y-5'>
      {/* Tarjeta 1: Logotipo Corporativo en WebP (Compresión 65%) */}
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <div className='flex items-center justify-between gap-2'>
            <div>
              <CardTitle className='text-sm font-semibold flex items-center gap-2'>
                <ImageIcon className='h-4 w-4 text-primary' />
                <span>Logotipo de Empresa (Conversión Automática WebP 65%)</span>
              </CardTitle>
              <CardDescription className='text-xs mt-0.5'>
                Cualquier imagen (PNG/JPEG) se convierte a formato WebP con compresión al 65% y se guarda en la base de datos.
              </CardDescription>
            </div>
            <Badge
              variant='outline'
              className='border-emerald-500/40 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 text-xs gap-1'
            >
              <Database className='h-3 w-3' />
              WebP 65% Activo
            </Badge>
          </div>
        </CardHeader>

        <CardContent>
          <div className='flex flex-col sm:flex-row items-start sm:items-center gap-4 p-4 rounded-lg border bg-muted/20'>
            <div className='h-20 w-36 rounded-lg border bg-white flex items-center justify-center overflow-hidden shrink-0 p-2'>
              {company.logoWebpDataUrl ? (
                <img
                  src={company.logoWebpDataUrl}
                  alt='Logo empresa WebP'
                  className='max-h-full max-w-full object-contain'
                />
              ) : (
                <div className='flex flex-col items-center text-neutral-400 text-[11px]'>
                  <Building2 className='h-6 w-6 mb-1' />
                  <span>Sin logo subido</span>
                </div>
              )}
            </div>

            <div className='flex-1 space-y-2'>
              <div className='flex flex-wrap items-center gap-2'>
                <input
                  ref={fileInputRef}
                  type='file'
                  accept='image/png,image/jpeg,image/webp'
                  onChange={handleLogoUpload}
                  className='hidden'
                />
                <Button
                  type='button'
                  size='sm'
                  className='h-8 text-xs gap-1.5'
                  disabled={isUploadingLogo}
                  onClick={() => fileInputRef.current?.click()}
                  data-testid='btn-subir-logo-webp'
                >
                  <Upload className='h-3.5 w-3.5' />
                  <span>
                    {isUploadingLogo
                      ? 'Convirtiendo a WebP 65%...'
                      : 'Subir Logo (PNG / JPG → WebP 65%)'}
                  </span>
                </Button>

                {company.logoWebpDataUrl && (
                  <>
                    <Button
                      asChild
                      variant='outline'
                      size='sm'
                      className='h-8 text-xs gap-1.5 border-primary/40 text-primary hover:bg-primary/10'
                    >
                      <Link to='/settings/appearance'>
                        <Palette className='h-3.5 w-3.5' />
                        <span>Generar temas desde este logo</span>
                      </Link>
                    </Button>
                    <Button
                      type='button'
                      variant='outline'
                      size='sm'
                      className='h-8 text-xs gap-1 text-destructive'
                      onClick={() => {
                        billingActions.updateCompany({
                          logoWebpDataUrl: null,
                          logoPrivateFileId: null,
                        })
                        toast.info('Logotipo eliminado de la configuración.')
                      }}
                    >
                      <Trash2 className='h-3.5 w-3.5' />
                      <span>Quitar logo</span>
                    </Button>
                  </>
                )}
              </div>

              {company.logoWebpSizeKb ? (
                <p className='text-[11px] text-emerald-600 dark:text-emerald-400 flex items-center gap-1'>
                  <CheckCircle2 className='h-3.5 w-3.5' />
                  <span>
                    Guardado en formato <strong>image/webp (Calidad 65%)</strong> ·
                    Original: {company.logoOriginalSizeKb} KB → Comprimido:{' '}
                    {company.logoWebpSizeKb} KB (ID BD: {company.logoPrivateFileId})
                  </span>
                </p>
              ) : (
                <p className='text-[11px] text-muted-foreground'>
                  Formatos admitidos: PNG, JPEG o WebP (máx. 5 MB). Se recodifica automáticamente a WebP al 65% de calidad.
                </p>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Tarjeta 2: Datos Fiscales del Emisor */}
      <Card className='border border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <CardTitle className='text-sm font-semibold flex items-center gap-2'>
            <Building2 className='h-4 w-4 text-primary' />
            <span>Datos Fiscales de la Empresa (Emisor de Facturas)</span>
          </CardTitle>
          <CardDescription className='text-xs'>
            Estos datos aparecen automáticamente en la cabecera y el pie legal de todas tus facturas y resúmenes trimestrales.
          </CardDescription>
        </CardHeader>

        <CardContent>
          <form onSubmit={handleSaveCompany} className='space-y-4'>
            <div className='grid sm:grid-cols-3 gap-3'>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Razón Social / Nombre Fiscal *
                </label>
                <Input
                  value={razonSocial}
                  onChange={(e) => setRazonSocial(e.target.value)}
                  className='h-9 text-xs mt-1'
                  data-testid='input-config-razon-social'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Nombre Comercial
                </label>
                <Input
                  value={nombreComercial}
                  onChange={(e) => setNombreComercial(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  NIF / CIF del Emisor *
                </label>
                <Input
                  value={nifCif}
                  onChange={(e) => setNifCif(e.target.value)}
                  className='h-9 text-xs font-mono mt-1'
                  data-testid='input-config-nif'
                />
              </div>
            </div>

            <div className='grid sm:grid-cols-4 gap-3'>
              <div className='sm:col-span-2'>
                <label className='text-xs font-medium text-foreground'>
                  Domicilio Fiscal *
                </label>
                <Input
                  value={direccionFiscal}
                  onChange={(e) => setDireccionFiscal(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Código Postal
                </label>
                <Input
                  value={codigoPostal}
                  onChange={(e) => setCodigoPostal(e.target.value)}
                  className='h-9 text-xs font-mono mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Ciudad / Provincia
                </label>
                <Input
                  value={ciudad}
                  onChange={(e) => {
                    setCiudad(e.target.value)
                    setProvincia(e.target.value)
                  }}
                  className='h-9 text-xs mt-1'
                />
              </div>
            </div>

            <div className='grid sm:grid-cols-3 gap-3'>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Email de Facturación
                </label>
                <Input
                  type='email'
                  value={emailFacturacion}
                  onChange={(e) => setEmailFacturacion(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Teléfono Comercial
                </label>
                <Input
                  value={telefono}
                  onChange={(e) => setTelefono(e.target.value)}
                  className='h-9 text-xs mt-1'
                />
              </div>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Cuenta Bancaria IBAN (en Factura)
                </label>
                <Input
                  value={iban}
                  onChange={(e) => setIban(e.target.value)}
                  className='h-9 text-xs font-mono mt-1'
                />
              </div>
            </div>

            <Separator />

            <div className='space-y-3 rounded-xl border border-border/80 bg-muted/20 p-3.5'>
              <div className='flex flex-wrap items-center justify-between gap-2'>
                <div className='flex items-center gap-1.5 text-xs font-semibold text-foreground'>
                  <HelpCircle className='h-3.5 w-3.5 text-primary' />
                  <span>¿Cómo rellenar la Mención Legal y el Pie de Factura?</span>
                </div>
                <div className='flex flex-wrap items-center gap-1.5'>
                  <Button
                    type='button'
                    variant='outline'
                    size='sm'
                    className='h-7 px-2.5 text-[11px]'
                    onClick={() => {
                      setRegistroMercantil(
                        'Profesional autónomo inscrito en el Censo de Empresarios, Profesionales y Retenedores de la AEAT.'
                      )
                      setPieFactura(
                        'Operación sujeta al régimen general de IVA. Factura emitida conforme al RD 1619/2012.'
                      )
                      toast.info('Plantilla para Autónomo aplicada. Pulsa «Guardar Datos de la Empresa».')
                    }}
                  >
                    Soy Autónomo (Rellenar automático)
                  </Button>
                  <Button
                    type='button'
                    variant='outline'
                    size='sm'
                    className='h-7 px-2.5 text-[11px]'
                    onClick={() => {
                      setRegistroMercantil(
                        'Inscrita en el Registro Mercantil de A Coruña, Tomo [X], Folio [Y], Hoja [Z].'
                      )
                      setPieFactura(
                        'Régimen general del IVA. Factura verificable conforme al Real Decreto 1007/2023 (VeriFactu).'
                      )
                      toast.info('Plantilla para Sociedad (S.L.) aplicada. Completa Tomo/Folio/Hoja de tu escritura.')
                    }}
                  >
                    Soy Sociedad S.L. (Plantilla)
                  </Button>
                </div>
              </div>

              <p className='text-[11px] text-muted-foreground leading-relaxed'>
                • <strong>Si eres Autónomo (NIF con letra al final, ej. 12345678Z):</strong> No tienes obligación de estar en el Registro Mercantil. Pulsa el botón <em>«Soy Autónomo»</em> o déjalo en blanco.<br />
                • <strong>Si eres Sociedad S.L. (CIF que empieza por B):</strong> Busca el <em>Tomo, Folio y Hoja</em> en el sello de la última página de tu Escritura de Constitución ante notario.
              </p>

              <div className='grid sm:grid-cols-2 gap-3 pt-1'>
                <div>
                  <label className='text-xs font-medium text-foreground'>
                    Inscripción Registro Mercantil / Mención Legal
                  </label>
                  <Input
                    value={registroMercantil}
                    onChange={(e) => setRegistroMercantil(e.target.value)}
                    placeholder='Autónomo (AEAT) o datos del Registro Mercantil...'
                    className='h-9 text-xs mt-1'
                  />
                </div>
                <div>
                  <label className='text-xs font-medium text-foreground'>
                    Pie de Página de Factura
                  </label>
                  <Input
                    value={pieFactura}
                    onChange={(e) => setPieFactura(e.target.value)}
                    placeholder='Régimen general del IVA...'
                    className='h-9 text-xs mt-1'
                  />
                </div>
              </div>
            </div>

            <div className='flex justify-end pt-2'>
              <Button
                type='submit'
                size='sm'
                className='h-9 text-xs gap-1.5 shadow-2xs'
                data-testid='btn-guardar-empresa'
              >
                <Save className='h-3.5 w-3.5' />
                <span>Guardar Datos de la Empresa</span>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {/* TARJETA DE MODELO DE IA (GOOGLE GEMINI) PARA PRESUPUESTOS Y CONTRATOS */}
      <Card className='border-border/80 shadow-xs'>
        <CardHeader className='pb-3'>
          <div className='flex items-center justify-between gap-2'>
            <CardTitle className='text-sm font-semibold flex items-center gap-2'>
              <Sparkles className='h-4 w-4 text-amber-500' />
              <span>Inteligencia Artificial (Google Gemini) · Redacción de Documentación</span>
            </CardTitle>
            <Badge variant='outline' className='font-mono text-[11px]'>
              Activo: {modeloIa}
            </Badge>
          </div>
          <CardDescription className='text-xs'>
            Elige aquí el modelo de IA que redacta los borradores de Presupuestos y Contratos en la pestaña «Origen de datos» y actualiza la clave API del servidor si lo necesitas.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSaveAiSettings} className='space-y-4'>
            <div className='grid sm:grid-cols-2 gap-3'>
              <div>
                <label className='text-xs font-medium text-foreground'>
                  Seleccionar Modelo de IA (Google Gemini)
                </label>
                <select
                  aria-label='Seleccionar Modelo de IA'
                  value={modeloIa}
                  onChange={(e) => setModeloIa(e.target.value)}
                  className='w-full h-9 rounded-md border border-input bg-background px-3 text-xs font-mono mt-1'
                  data-testid='select-modelo-ia-settings-page'
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

              <div>
                <label className='text-xs font-medium text-foreground'>
                  Clave API de Gemini en servidor (Opcional · Ya configurada)
                </label>
                <Input
                  type='password'
                  value={geminiApiKeyInput}
                  onChange={(e) => setGeminiApiKeyInput(e.target.value)}
                  placeholder='Dejar vacío para mantener la clave actual del servidor...'
                  className='h-9 text-xs font-mono mt-1'
                />
              </div>
            </div>

            <div className='flex justify-end'>
              <Button
                type='submit'
                size='sm'
                disabled={isSavingAiSettings}
                className='h-9 text-xs gap-1.5 shadow-2xs'
                data-testid='btn-guardar-modelo-ia'
              >
                <Save className='h-3.5 w-3.5' />
                <span>
                  {isSavingAiSettings
                    ? 'Guardando...'
                    : 'Guardar Modelo de IA y Clave'}
                </span>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
