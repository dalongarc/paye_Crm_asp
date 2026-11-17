# Payé CRM & Base — Plataforma Modular Segura (.NET 10 + React 19 + PostgreSQL 17)

Plataforma single-tenant de **Payé Digital** para aplicaciones de gestión comercial, CRM, facturación **VERI\*FACTU** y documentación corporativa a medida para comercios, autónomos y pequeñas empresas.

---

## Novedades y Características Principales

- **Backend .NET 10 LTS + ASP.NET Core + PostgreSQL 17 (`Npgsql`)**:
  - Arquitectura modular autocontenida en compilación (`Modules/<NombreModulo>/` en `Paye.Core`, `Paye.Infrastructure` y `Paye.Web`).
  - Auditoría automática (`AuditableEntity` + `AuditLog`), borrado lógico con filtro global (`ISoftDeletable`) y concurrencia optimista nativa de PostgreSQL (`xmin` → HTTP `409 Conflict`).
- **Frontend SPA React 19 + Apple HIG + PWA (`/web`)**:
  - Construido con **React 19**, **TypeScript**, **Vite 8**, **Tailwind CSS v4**, **TanStack Router** y **TanStack Query v5** siguiendo principios de **Apple Human Interface Guidelines (HIG)**.
  - **PWA Instalable (Windows 11 / Escritorio / Móvil)**: `manifest.webmanifest`, Service Worker (`sw.js`) y lanzador directo en modo aplicación (`scripts/iniciar-paye.ps1`).
  - Arquitectura de datos desacoplada (`/web/src/api/` con contratos tipados e implementaciones `real/` y `mock/` excluidas en compilación de producción).
- **Seguridad por Defecto (Fail-Closed & Deny-by-Default)**:
  - Toda ruta exige usuario autenticado salvo `[AllowAnonymous]` explícito (`/health`, login y recuperación).
  - Validación obligatoria de configuración al arrancar (`ValidateOnStart`).
  - Cabeceras de seguridad y **CSP estricta** (`default-src 'self'; script-src 'self'; style-src 'self'`) con cero `unsafe-inline`, cero `unsafe-eval` y fuentes autoalojadas (`@fontsource`).
  - `ASP.NET Core Identity` con cookies `HttpOnly`, bloqueo por intentos fallidos, 2FA (TOTP), invalidación inmediata de sesiones (`SecurityStampValidator`) y protección del último `Propietario`.
  - Permisos granulares como claims de rol (`paye.permission`) descubiertos automáticamente desde cada módulo (`IPermissionDefinitionProvider`).

---

## Módulos Incluidos

1. **`Modules/Clientes` (`/clientes`)**:
   - Ficha integral de clientes y proveedores (NIF/CIF, contacto, dirección fiscal, estado y notas).
   - Gestión de presupuestos, facturas emitidas y borradores de departamentos vinculados al cliente.
   - Control de concurrencia optimista (`xmin`) con resolución interactiva de conflictos HTTP `409`.
2. **`Modules/Facturacion` y Resumen Trimestral (`/facturas`, `/gastos`, `/trimestre`)**:
   - Presupuestos, borradores de factura y **emisión inmutable VERI\*FACTU** protegida por triggers PostgreSQL (`ERRCODE 55000`).
   - Numeración correlativa atómica sin huecos (`UPDATE ... RETURNING`) y facturas rectificativas (`SELECT ... FOR UPDATE`).
   - Catálogo de impuestos configurable (IVA, Recargo de Equivalencia, IRPF con códigos AEAT), generador y previsualizador PDF A4 nativo.
   - Registro de gastos deducibles y cuadro de **Liquidación Trimestral** orientativa (IVA Modelo 303 e IRPF Modelo 130).
3. **`Modules/Documentacion` (`/documentacion`)**:
   - Editor y generador de informes, propuestas y manuales corporativos en formato **A4 multipágina** listo para impresión y exportación PDF (`documentacion-a4.css`).
   - Configuración de identidad visual corporativa y extracción/análisis de paleta de color (`documentacion_ajustes_empresa`).
   - Series documentales, bloques estructurados (`documentacion_lineas`), vinculación opcional con clientes y redacción asistida por IA (`IDocumentoGeminiCompletionProvider`).
4. **`Modules/Notas` (`/notas`)**:
   - Notas operativas con fijado (*pin*), archivado, etiquetas de color, papelera con restauración (`ISoftDeletable`) y concurrencia `xmin`.
5. **Administración de Usuarios y Roles (`/users`)**:
   - Alta de usuarios, asignación de roles (`Propietario`, `Administrador`, `Empleado`, `Lectura`) y activación/desactivación inmediata de sesiones activas.

---

## Arranque Rápido en Entorno Local (Docker + PWA Windows 11)

El proyecto incluye un stack Docker de desarrollo local (`docker-compose.dev.yml`) que ejecuta tanto **PostgreSQL 17** (`127.0.0.1:5435`) como el servidor **Kestrel (`Paye.Web`)** (`http://localhost:5000` y `https://localhost:5001`), evitando bloqueos de DLLs locales por *Smart App Control* en Windows 11.

```powershell
# Iniciar PostgreSQL + Paye.Web en Docker, aplicar migraciones y abrir la PWA
powershell -ExecutionPolicy Bypass -File .\scripts\iniciar-paye.ps1

# Forzar reconstrucción de la imagen tras cambios en el backend o frontend
powershell -ExecutionPolicy Bypass -File .\scripts\iniciar-paye.ps1 --rebuild
```

También puedes gestionar el entorno manualmente con Docker Compose:

```powershell
docker compose -f docker-compose.dev.yml up -d --build
```

---

## Documentación en `/docs`

1. [`docs/local-dev.md`](./docs/local-dev.md) — **Guía de ejecución local** (Stack Docker con `iniciar-paye.ps1`, Modo A con Vite HMR y Modo B monolito de producción).
2. [`docs/guia-completa-implementacion.md`](./docs/guia-completa-implementacion.md) — **Guía técnica completa** (mapa de clases, esquema de BD, rutas HTTP, flujos, Sandbox Demo y catálogo de pruebas automatizadas).
3. [`docs/modulo-facturacion.md`](./docs/modulo-facturacion.md) — **Documentación técnica del módulo `Facturacion`** (modelo fiscal, triggers de inmutabilidad en PostgreSQL, numeración atómica, rectificativas, catálogo AEAT y PDF).
4. [`docs/arquitectura.md`](./docs/arquitectura.md) — Estructura de proyectos Backend (.NET 10) + Frontend (`/web` React 19), decisiones técnicas y flujo de datos.
5. [`docs/modulos.md`](./docs/modulos.md) — Cómo crear un módulo nuevo y cómo desactivar o borrar un módulo existente sin romper nada.
6. [`docs/reglas-frontend.md`](./docs/reglas-frontend.md) — Reglas operativas del frontend en `/web` (React 19, Tailwind v4, contratos API, CSP sin estilos inline).
7. [`docs/seguridad-y-permisos.md`](./docs/seguridad-y-permisos.md) — Identity, permisos por claims de rol, CSP, subida de archivos, RGPD y Sandbox Demo Aislado.
8. [`docs/despliegue-coolify.md`](./docs/despliegue-coolify.md) — Variables de entorno, migraciones (`--migrate`), siembra del primer `Propietario`, backups en S3 y restauración.
9. [`docs/nuevo-proyecto-cliente.md`](./docs/nuevo-proyecto-cliente.md) — Checklist paso a paso para iniciar el proyecto de un cliente nuevo.

---

## Comandos de Compilación y Pruebas

```powershell
# Compilar frontend SPA (/web) y sincronizar artefactos en src/Paye.Web/wwwroot
pnpm --dir web build
Remove-Item -Path "src/Paye.Web/wwwroot/assets" -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item -Path "web/dist/*" -Destination "src/Paye.Web/wwwroot" -Recurse -Force

# Compilar toda la solución .NET 10 (0 advertencias, 0 errores)
dotnet build Paye.sln -m:1

# Ejecutar la batería completa de pruebas automatizadas (xUnit + PostgreSQL 17 en Testcontainers)
dotnet test Paye.sln -m:1
```
