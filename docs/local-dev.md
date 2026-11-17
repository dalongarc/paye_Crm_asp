# Guía de Ejecución Local: Docker PWA, Modo A y Modo B (`docs/local-dev.md`)

Payé CRM dispone de un flujo de arranque local dockerizado (`docker-compose.dev.yml` + `scripts/iniciar-paye.ps1`) además de los modos de desarrollo con recarga en caliente (Modo A) y monolito local (Modo B). Todos se conectan a PostgreSQL 17 en Docker y respetan la política estricta de seguridad sin secretos en el repositorio.

---

## 1. Arranque Rápido Recomendado en Windows 11 (Docker + PWA)

En Windows 11, el *Control Inteligente de Aplicaciones (Smart App Control / WDAC)* puede bloquear la carga de DLLs locales sin firmar (`0x800711C7`) cuando se utiliza un SDK portable de .NET. Para evitar cualquier fricción, `docker-compose.dev.yml` ejecuta tanto **PostgreSQL 17** (`paye-crm-postgres`) como el servidor **Kestrel (`paye-crm-web`)** en contenedores aislados, sirviendo el frontend compilado en `src/Paye.Web/wwwroot` como volumen en vivo.

### Iniciar con un solo comando

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\iniciar-paye.ps1
```

¿Qué hace automáticamente `scripts/iniciar-paye.ps1`?
1. Exporta el certificado HTTPS de desarrollo local (`storage/certs/aspnetcore-https.pfx`, excluido de Git) si aún no existe.
2. Verifica o levanta `paye-crm-postgres` (`127.0.0.1:5435`) y `paye-crm-web` (`http://localhost:5000` y `https://localhost:5001`).
3. Ejecuta las migraciones aditivas e idempotentes (`dotnet Paye.Web.dll --migrate`) dentro del contenedor.
4. Abre Payé CRM en ventana de aplicación **PWA** independiente (`--app=https://localhost:5001`).

### Actualizar cambios en el contenedor local

- **Si cambias solo el frontend (`/web`)**:
  ```powershell
  pnpm --dir web build
  Remove-Item -Path "src/Paye.Web/wwwroot/assets" -Recurse -Force -ErrorAction SilentlyContinue
  Copy-Item -Path "web/dist/*" -Destination "src/Paye.Web/wwwroot" -Recurse -Force
  ```
  Como `./src/Paye.Web/wwwroot` está montado en `/app/wwwroot:ro` dentro de `paye-crm-web`, los cambios se reflejan al instante al recargar el navegador sin reconstruir la imagen Docker.
- **Si cambias el backend C# (`src/Paye.*`)**:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\scripts\iniciar-paye.ps1 --rebuild
  ```

---

## 2. Requisitos Previos para Desarrollo Local

- **Docker Desktop** (para PostgreSQL 17 y el contenedor `paye-crm-web`)
- **Node.js 20+** y **pnpm** (`pnpm -v >= 10.x`)
- **.NET 10 SDK** (LTS)

---

## 3. Modo A: Desarrollo Frontend Vite con HMR (`http://localhost:5173`)

Ideal para iterar rápidamente en la interfaz React 19 con Hot Module Replacement (HMR):

1. **Asegurar que el backend esté activo en `https://localhost:5001`** (vía `scripts/iniciar-paye.ps1` o `docker compose -f docker-compose.dev.yml up -d`).
2. **Arrancar Vite en `/web`**:
   ```powershell
   cd web
   $env:VITE_USE_MOCKS = "false"
   pnpm dev
   ```
   - Vite arranca en `http://localhost:5173`.

---

## 4. Modo B: Ejecución Nativa en Host (Sin Docker para `Paye.Web`)

Si tienes instalado el SDK oficial MSI de .NET 10 en `C:\Program Files\dotnet` (o en Linux/macOS):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "https://localhost:5001;http://localhost:5000"
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5435;Database=paye_crm_asp;Username=paye_app;Password=paye_secure_dev_pass_2026"
dotnet run --project src/Paye.Web -c Release
```

---

## 5. Estado de Módulos e Integración Full-Stack

- **Autenticación y Sesión (`/sign-in`, `/api/auth/*`, `/api/me`)**: 100% Real con ASP.NET Core Identity, Cookie `HttpOnly`, invalidación inmediata por `SecurityStampValidator` y navegación filtrada por permisos.
- **Gestión de Clientes (`/clientes`, `/api/clientes/*`)**: 100% Real (PostgreSQL, borradores de departamentos, presupuestos y facturas vinculadas, concurrencia optimista HTTP 409 con `xmin`, soft-delete, token antiforgery).
- **Facturación VERI\*FACTU, Presupuestos, Gastos y Trimestre (`/facturas`, `/gastos`, `/trimestre`, `/api/facturacion/*`)**: Emisión inmutable con triggers PostgreSQL, numeración correlativa sin huecos, facturas rectificativas, previsualización y exportación PDF A4 y resumen fiscal trimestral orientativo (Modelos 303 y 130).
- **Documentación Corporativa A4 (`/documentacion`, `/api/documentacion/*`)**: 100% Real (plantillas multipágina A4, identidad visual y paleta de colores de empresa, series documentales, bloques estructurados, asistente IA Gemini y exportación PDF A4).
- **Notas Operativas (`/notas`, `/api/notas/*`)**: 100% Real (CRUD, fijado, archivado, etiquetas de color, papelera y concurrencia `xmin`).
- **Administración de Usuarios (`/users`, `/api/users/*`)**: 100% Real (gestión de cuentas, roles y estado activo/inactivo).
