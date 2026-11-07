# Checklist: Crear un Nuevo Proyecto de Cliente desde Payé Base

Sigue esta lista de comprobación cada vez que inicies un proyecto a medida para un cliente nuevo a partir de **Payé Base**.

---

## Fase 1: Creación del Repositorio Aislado del Cliente

- [ ] **1. Clonar la plantilla en una carpeta con el nombre del cliente**:
  ```powershell
  git clone <URL_REPO_PAYE_BASE> Paye_<NombreCliente>
  cd Paye_<NombreCliente>
  Remove-Item -Recurse -Force .git
  git init
  ```
- [ ] **2. Verificar que `.gitignore` y `.gitattributes` (LF) están presentes antes del primer commit**:
  ```powershell
  git add .gitignore .gitattributes
  ```
- [ ] **3. Crear un repositorio privado nuevo** exclusivo para el cliente y vincularlo como `origin`.

---

## Fase 2: Selección de Módulos

- [ ] **4. Eliminar los módulos de ejemplo o no contratados**:
  - Si el cliente no usa el módulo `Notas`, borra:
    - `src/Paye.Core/Modules/Notas`
    - `src/Paye.Infrastructure/Modules/Notas`
    - `src/Paye.Web/Modules/Notas`
    - `tests/Paye.Tests/Milestone3b`
  - Quita `using Paye.Web.Modules.Notas;` y `builder.Services.AddNotasModule();` en `src/Paye.Web/Program.cs`.
- [ ] **5. Implementar o activar los módulos contratados** (por ejemplo `Modules/Crm`, `Modules/Pos`) siguiendo [`docs/modulos.md`](./modulos.md).

---

## Fase 3: Frontend Moderno y Configuración del Negocio

- [ ] **6. Configurar y compilar el Frontend Moderno (`ClientApp`)**:
  - La arquitectura cliente utiliza **React 19 + TypeScript + Vite + Tailwind CSS v4 + Radix UI + Lucide**.
  - No requiere Node.js en producción; se compila directamente en `src/Paye.Web/wwwroot/spa/` (`facturacion-spa.js` y `facturacion-spa.css`).
  - Para desarrollar y personalizar las pantallas del cliente:
    ```powershell
    cd ClientApp
    npm install
    npm run build     # Genera el bundle optimizado en wwwroot/spa
    ```
  - La comunicación con el backend utiliza llamadas fetch/api autenticadas con cookie `httpOnly`, inyección automática de CSRF (`<meta name="csrf-token">`), y endpoints JSON con autorización granular.
- [ ] **7. Ajustar los colores de marca e identidad visual**:
  - Edita el tema o tokens en `ClientApp/src/index.css` y las vistas en `ClientApp/src/views/`.
  - Para pantallas de acceso público/login, personaliza `src/Paye.Web/wwwroot/css/site.css`.
  - Configura el logotipo del cliente en `src/Paye.Web/wwwroot/img/` y define `ClientSettings:LogoPath`.
- [ ] **8. Configurar datos en local mediante `dotnet user-secrets`** (nunca en `appsettings.json`):
  ```powershell
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=paye_cliente_dev;Username=postgres;Password=..." --project src/Paye.Web
  dotnet user-secrets set "ClientSettings:BusinessName" "Nombre Real del Cliente S.L." --project src/Paye.Web
  ```

---

## Fase 4: Verificación y Despliegue en Coolify

- [ ] **9. Ejecutar todas las pruebas automatizadas**:
  ```powershell
  dotnet test Paye.sln
  ```
- [ ] **10. Configurar PostgreSQL, volúmenes (`/var/paye/dpkeys` y `/var/paye/uploads`) y variables de entorno en Coolify** según [`docs/despliegue-coolify.md`](./despliegue-coolify.md).
- [ ] **11. Ejecutar migración inicial y siembra del primer `Propietario`**:
  ```bash
  dotnet Paye.Web.dll --migrate
  ```
- [ ] **12. Borrar inmediatamente `SEED_OWNER_EMAIL` y `SEED_OWNER_PASSWORD`** de Coolify y reiniciar el contenedor.
- [ ] **13. Verificar en producción**:
  - `GET /health` devuelve `200 OK` (`{"status":"ok"}`).
  - Cabeceras de seguridad (`Content-Security-Policy`, `Strict-Transport-Security`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`) activas.
  - El propietario puede iniciar sesión, activar su 2FA TOTP y gestionar sus documentos y módulos.
  - El backup diario de PostgreSQL en Coolify hacia S3 está activo y ha completado su primera ejecución con éxito.
