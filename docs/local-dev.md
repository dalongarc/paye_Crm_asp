# Guía de Ejecución Local: Modo A y Modo B (docs/local-dev.md)

Payé Base permite dos modos de ejecución local estandarizados y reproducibles. Ambos se conectan a una base de datos PostgreSQL 17 en contenedor Docker efímero/persistente y respetan la política estricta de seguridad sin secretos en el repositorio ni contraseñas fijas.

---

## 1. Requisitos Previos

- **.NET 10 SDK** (LTS)
- **Node.js 20+** y **pnpm** (`pnpm -v >= 10.x`)
- **Docker Desktop** o daemon Docker compatible (para PostgreSQL 17)
- Certificado de desarrollo HTTPS de confianza:
  ```powershell
  dotnet dev-certs https --trust
  ```

---

## 2. Base de Datos Local (PostgreSQL 17 en Docker)

Arrancar el contenedor de PostgreSQL 17 dedicado a desarrollo (puerto `5434` para evitar colisiones con instancias locales estándar):

```powershell
docker run -d `
  --name paye-postgres `
  -e POSTGRES_USER=paye_app `
  -e POSTGRES_PASSWORD=paye_secure_dev_pass_2026 `
  -e POSTGRES_DB=paye_local `
  -p 127.0.0.1:5434:5432 `
  -v paye_pgdata_dev:/var/lib/postgresql/data `
  postgres:17-alpine
```

### Configuración de Secretos Locales (`dotnet user-secrets`)
Prohibido almacenar cadenas de conexión o secretos en `appsettings.json`. Configurar en `src/Paye.Web`:

```powershell
cd src/Paye.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5434;Database=paye_local;Username=paye_app;Password=paye_secure_dev_pass_2026"
cd ../..
```

### Migraciones y Sembrado del Propietario
Para aplicar migraciones y sembrar el usuario Propietario inicial:

```powershell
dotnet run --project src/Paye.Web -c Release -- --migrate --seed
```

> **Contraseña del Propietario**: La aplicación genera una contraseña criptográfica aleatoria de 20 caracteres y la imprime **UNA sola vez** en la consola al sembrar el usuario. Si deseas definir una contraseña específica en desarrollo, exporta la variable de entorno `PAYE_OWNER_PASSWORD` antes de ejecutar `--seed`.

Usuario inicial sembrado:
- **Email**: `propietario@payedigital.local`
- **Rol**: `Propietario` (permisos completos: `Clientes.Ver`, `Clientes.Crear`, `Clientes.Editar`, `Clientes.Eliminar`, etc.)

---

## 3. Modo A: Desarrollo (Frontend Vite con HMR + Backend API)

Ideal para desarrollo activo del frontend y backend con recarga en caliente (Hot Module Replacement):

1. **Terminal 1 - Backend Kestrel (HTTPS)**:
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:ASPNETCORE_URLS = "https://localhost:5001"
   dotnet run --project src/Paye.Web -c Release
   ```
   - OpenAPI disponible en `https://localhost:5001/openapi/v1.json` (solo en Development).

2. **Terminal 2 - Frontend Vite**:
   ```powershell
   cd web
   $env:VITE_USE_MOCKS = "false"
   pnpm dev
   ```
   - Vite arranca en `http://localhost:5173`.
   - Las peticiones `/api/*` se redirigen por proxy al backend en `https://localhost:5001`.
   - Las cookies de sesión `__Host-Paye.Auth` y antiforgery funcionan fluidamente bajo el mismo origen proxy.

---

## 4. Modo B: Producción-Local (Monolito Integrado / Single-Origin)

Este modo reproduce con fidelidad absoluta lo que ve y experimenta el cliente final en producción: el frontend compilado se sirve estáticamente desde `src/Paye.Web/wwwroot`, Kestrel ejecuta bajo `ASPNETCORE_ENVIRONMENT="Production"`, y se aplica la cabecera real y estricta de Content-Security-Policy (sin `unsafe-inline` ni `unsafe-eval`).

### Paso 1: Compilar la SPA y desplegar en wwwroot
```powershell
pnpm --dir web build
Copy-Item -Path "web/dist/*" -Destination "src/Paye.Web/wwwroot" -Recurse -Force
```

### Paso 2: Arrancar Kestrel en Modo Producción
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "https://localhost:5001;http://localhost:5000"
dotnet run --project src/Paye.Web -c Release --no-build
```

### Paso 3: Probar en el Navegador
Abre en tu navegador:
👉 **`https://localhost:5001/sign-in`**

- **Usuario**: `propietario@payedigital.local`
- **Contraseña**: `<CONTRASEÑA_ALEATORIA_IMPRESA_POR_SEED>`

---

## 5. Módulos Conectados vs. Módulos con Datos de Ejemplo

En la versión actual de la integración vertical:
- **Autenticación (`/sign-in`, `/api/auth/*`)**: 100% Real (Cookie HttpOnly `__Host-Paye.Auth`, bloqueo tras 5 intentos, rate limiting IP).
- **Sesión y Permisos (`/api/me`, `/api/me/navigation`)**: 100% Real (filtrado dinámico de navegación según permisos de ASP.NET Core Identity).
- **Gestión de Clientes (`/clientes`, `/api/clientes/*`)**: 100% Real (PostgreSQL, concurrencia optimista HTTP 409 con `xmin`, soft-delete, antiforgery token `X-XSRF-TOKEN`).
- **Dashboard (`/`) y Tareas (`/tasks`)**: Cuentan con un distintivo visible `"Datos de ejemplo"` en la cabecera/sección, indicando que aún utilizan mocks tipados pendientes de conectar a sus respectivos módulos de backend en fases posteriores.

---

## 6. Verificación Automatizada de Seguridad y CSP (Playwright)

Para auditar el flujo completo de Modo B y certificar que no existen violaciones de CSP (`style-src 'self'`, `script-src 'self'`):

```powershell
node web/scripts/verify-modo-b.mjs
```

El script ejecuta de forma autónoma:
1. Login autenticado con ASP.NET Core Identity.
2. Comprobación del Dashboard y badge "Datos de ejemplo".
3. Navegación a Gestión de Clientes.
4. Alta de nuevo cliente con validación Zod y persistencia en PostgreSQL.
5. Edición de cliente.
6. Simulación de conflicto de concurrencia optimista HTTP 409 abriendo una segunda pestaña en paralelo.
7. Borrado lógico (soft-delete).
8. Cierre de sesión (logout).
9. Generación de 22 capturas en modo claro y oscuro (`screenshots/modo_b/`) y verificación de 0 violaciones de CSP.
