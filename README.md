# Payé Base — Plantilla Modular Segura (.NET 10 + PostgreSQL)

Plantilla base single-tenant de **Payé Digital** para aplicaciones de gestión a medida de comercios y pequeñas empresas (CRM, Facturación, TPV/POS, inventario, reservas y documentos).

---

## Características Principales

- **.NET 10 LTS + ASP.NET Core MVC (Razor) + PostgreSQL (`Npgsql`)**.
- **Seguridad por defecto (Fail-Closed & Deny-by-Default)**:
  - Toda ruta exige usuario autenticado salvo `[AllowAnonymous]` explícito (`/health`, login y recuperación).
  - Validación obligatoria de configuración al arrancar (`ValidateOnStart`).
  - Cabeceras de seguridad y **CSP estricta** sin `unsafe-inline` ni `unsafe-eval` (`includeIndicatorStyles=false` para HTMX).
  - `ASP.NET Core Identity` con bloqueo por intentos fallidos, 2FA (TOTP), invalidación inmediata de sesiones (`SecurityStampValidator`) y protección del último `Propietario`.
  - Permisos granulares como claims de rol (`paye.permission`) descubiertos automáticamente desde cada módulo (`IPermissionDefinitionProvider`).
- **Modularidad en compilación (`Modules/<NombreModulo>/`)**:
  - Cada módulo vive en `Paye.Core`, `Paye.Infrastructure` y `Paye.Web` y se registra con una sola línea (`builder.Services.Add<Modulo>Module();`).
  - Quitar un módulo es borrar sus tres carpetas y su línea de registro en `Program.cs`.
  - Módulos incluidos:
    - **`Modules/Notas`**: Módulo de referencia (CRUD, archivado, borrado lógico, concurrencia `xmin`).
    - **`Modules/Facturacion`**: Presupuestos, borradores de factura, emisión inmutable protegida por triggers PostgreSQL (`ERRCODE 55000`), numeración correlativa atómica sin huecos (`UPDATE ... RETURNING`), facturas rectificativas (`SELECT ... FOR UPDATE`), catálogo de impuestos configurable (IVA, Recargo de Equivalencia, IRPF con códigos AEAT VERI*FACTU), generador PDF 1.4 nativo sin dependencias externas y contratos `IInvoiceIssuer` / `ITaxCatalogReader` para el futuro módulo `Pos`.
- **Capacidades transversales listas para producción**:
  - Auditoría automática (`AuditableEntity` + `AuditLog`), borrado lógico con filtro global (`ISoftDeletable`) y concurrencia optimista nativa de PostgreSQL (`xmin`).
  - Tipos monetarios seguros (`Money` con `decimal(18,2)` y redondeo centralizado en `Money.DefaultRoundingMode`, y `VatRate` para tipos de IVA españoles), cultura `es-ES` y conversión horaria `Europe/Madrid` (`MadridTimeConverter`).
  - Almacenamiento privado de archivos fuera de `wwwroot` con validación de firmas binarias (*magic bytes*).
  - Exportación y anonimización RGPD (`GdprUserDataService`).
  - **Sandbox Demo Aislado opcional (`DemoSandbox__Enabled=true`, desactivado por defecto)**: Crea sesiones efímeras (`demo-<guid>`) con datos de ejemplo precargados, aislamiento estricto por usuario (`CreatedBy`), bloqueo de administración/emisión fiscal/archivos físicos y borrado total automático en base de datos al cerrar sesión.
- **Imagen Docker Chiseled Rootless (`noble-chiseled-extra`)**:
  - Consumo en reposo ~38–105 MB de RAM, ideal para VPS de 1–2 GB con Coolify.

---

## Documentación en `/docs`

1. [`docs/guia-completa-implementacion.md`](./docs/guia-completa-implementacion.md) — **Guía técnica completa de todo lo implementado** (mapa de clases, esquema de BD, rutas HTTP, flujos, Sandbox Demo y catálogo de las **66 pruebas automatizadas**).
2. [`docs/modulo-facturacion.md`](./docs/modulo-facturacion.md) — **Documentación técnica del módulo `Facturacion`** (modelo fiscal, triggers de inmutabilidad en PostgreSQL, numeración atómica, rectificativas, catálogo de impuestos AEAT, generador PDF 1.4 nativo y contratos para `Pos`).
3. [`docs/arquitectura.md`](./docs/arquitectura.md) — Estructura de proyectos, decisiones técnicas y flujo de datos.
4. [`docs/modulos.md`](./docs/modulos.md) — Cómo crear un módulo nuevo y cómo borrar un módulo existente sin romper nada.
5. [`docs/seguridad-y-permisos.md`](./docs/seguridad-y-permisos.md) — Identity, permisos por claims de rol, CSP, subida de archivos, RGPD y Sandbox Demo Aislado.
6. [`docs/despliegue-coolify.md`](./docs/despliegue-coolify.md) — Variables de entorno, migraciones (`--migrate`), siembra del primer `Propietario`, backups en S3 y restauración.
7. [`docs/nuevo-proyecto-cliente.md`](./docs/nuevo-proyecto-cliente.md) — Checklist paso a paso para iniciar el proyecto de un cliente nuevo.

---

## Comandos Rápidos de Desarrollo y Pruebas

```powershell
# Compilar toda la solución (0 advertencias, 0 errores)
dotnet build Paye.sln -m:1

# Ejecutar la batería completa de 66 pruebas (unitarias + integración real con PostgreSQL 17 en Testcontainers)
dotnet test Paye.sln -m:1
```
