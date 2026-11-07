# Reglas Operativas y de Seguridad para Agentes de IA (AGENTS.md)

Este repositorio contiene **Payé Base**, la plantilla base single-tenant de Payé Digital para aplicaciones a medida de comercios y pequeñas empresas. Todo agente de IA que inspeccione o modifique este código debe cumplir estas reglas sin excepción:

## 1. Datos y entornos
- **Solo datos ficticios y entornos de desarrollo/pruebas**: Está prohibido conectarse a bases de datos de producción o utilizar datos reales de clientes, empleados o facturación.
- **Separación estricta de BD de pruebas**: Las pruebas automatizadas (`Paye.Tests`) exigen una base de datos exclusiva (`TEST_DATABASE_URL` o contenedor efímero de Testcontainers) que **nunca** puede coincidir con `ConnectionStrings__DefaultConnection` ni `DATABASE_URL`.
- **Ningún secreto en el repositorio**: Prohibido incluir contraseñas, claves API, tokens, certificados o correos reales en código fuente, migraciones, pruebas o `.env.example`.

## 2. Base de datos y migraciones
- **Prohibido ejecutar SQL arbitrario o destructivo**: Prohibido `DROP`, `TRUNCATE`, `DELETE` sin `WHERE` o `Database.EnsureDeleted()` fuera de contenedores efímeros de prueba.
- **Migraciones solo aditivas y revisadas por un humano**: Toda migración EF Core debe ser revisada por un humano antes de aplicarse en un entorno de cliente. La aplicación en producción **nunca** ejecuta `Database.Migrate()` automáticamente al arrancar.
- **Consultas siempre parametrizadas**: Usa LINQ / EF Core parametrizado. Prohibido concatenar cadenas en SQL.

## 3. Autenticación, autorización y seguridad HTTP
- **ASP.NET Core Identity obligatorio**: Prohibido programar a mano hashing de contraseñas, sesiones, tokens o cookies de login.
- **Prohibido crear usuarios demo con contraseñas fijas, PIN o bypass de autenticación**: El primer usuario `Propietario` se siembra exclusivamente mediante variables de entorno/comando explícito y dichas variables se eliminan tras el primer arranque. Para entornos locales o de demostración se permite únicamente el **Sandbox Demo Aislado** (`DemoSandbox__Enabled=true`, desactivado por defecto en producción), que genera cuentas efímeras `demo-<guid>` sin credenciales fijas, restringe permisos administrativos/subida de archivos, aísla los registros por sesión y elimina todos los datos creados y la cuenta al cerrar sesión.
- **Deny by default y Fail-closed**: Toda ruta exige autenticación salvo `[AllowAnonymous]` explícito (`/health`, login y recuperación). Si falta una configuración obligatoria al arrancar (`ValidateOnStart`), el servidor no arranca.
- **CSP estricta sin `unsafe-inline` ni `unsafe-eval`**: Todo CSS y JavaScript debe servirse desde archivos estáticos en `wwwroot`. Si se usa HTMX, debe mantenerse `includeIndicatorStyles=false`.
- **Sin asignación masiva (Mass Assignment)**: Prohibido recibir entidades EF Core directamente en acciones de controlador; usa siempre ViewModels o DTOs validados.

## 4. Modularidad y dependencias
- **Módulos autocontenidos (`Modules/<NombreModulo>/`)**: Un módulo no accede a las tablas internas de otro. Quitar un módulo debe reducirse a borrar sus carpetas en `Core`, `Infrastructure` y `Web` y su línea `Add<Modulo>Module()` en `Program.cs`.
- **Prohibido añadir dependencias NuGet sin justificación escrita y aprobación previa**: Usa los componentes nativos de ASP.NET Core (.NET 10 LTS) y la lista cerrada aprobada (`Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `xUnit`, `Testcontainers.PostgreSql`, Bootstrap, HTMX). Prohibido MediatR, AutoMapper, Redis, Hangfire o repositorios genéricos.

## 5. Reglas Operativas de Frontend (`/web`)
- **Aislamiento**: Todo el frontend vive estrictamente en `/web`. Prohibido crear o editar vistas Razor (`.cshtml`), partials, `ViewComponents` o `ViewModels`.
- **Stack y Versiones**: Plantilla `shadcn-admin` (Satnaing), React 19 (`^19.2.x`), TypeScript, Vite, Tailwind CSS v4 (`@tailwindcss/vite`), `@tanstack/react-router`, `@tanstack/react-query` v5. Prohibido `react-router-dom` y SDKs de terceros (`@clerk/react`, Supabase, Firebase).
- **Gestor de paquetes exclusivo**: Únicamente `pnpm`. Prohibido `npm` o `yarn` en `/web`. Solo debe existir `pnpm-lock.yaml`.
- **Arquitectura de API y Mocks**: Prohibido hardcodear mocks en componentes. Todo acceso a datos pasa por `/web/src/api/` con interfaces (`IAuthApi`, `IClientesApi`, etc.) y dos implementaciones (`mock/` y `real/`). Los mocks deben eliminarse por completo del build de producción cuando `VITE_USE_MOCKS=false`.
- **Seguridad**: Cero tokens o credenciales en `localStorage`, `sessionStorage` o cookies accesibles por JS. Autenticación exclusivamente por Cookie `HttpOnly` gestionada por el servidor y reflejada por `IAuthApi`.
- **Diseño**: Sin CSS manual ni CSS-in-JS ni estilos inline. Usar componentes shadcn/ui e iconos locales `lucide-react`. Cero CDNs.
- **CSP**: Advertencia de `<style>` inline en `sonner` y `chart.tsx` identificados para desacoplamiento o nonce antes de producción.

