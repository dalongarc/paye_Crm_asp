# Seguridad y Autorización por Permisos

**Payé Base** aplica seguridad por defecto en todas las capas de la aplicación.

---

## 1. Denegación por Defecto (Deny-by-Default) y Arranque Fail-Closed

- **Política global (`FallbackPolicy`)**: Configurada en `Program.cs` para exigir siempre un usuario autenticado. Únicamente los endpoints marcados explícitamente con `[AllowAnonymous]` son accesibles sin sesión (`/health`, `/account/login`, `/account/login-2fa`, `/account/forgot-password`, `/account/reset-password`, `/error`).
- **Validación al arrancar (`ValidateOnStart`)**: Las secciones `ConnectionStrings`, `ClientSettings`, `Security` y `Smtp` se validan con `DataAnnotations` e `IValidatableObject` antes de abrir el puerto HTTP. Si falta una cadena de conexión o contiene valores de plantilla (`CHANGE_ME`), el servidor aborta el arranque.

---

## 2. Autenticación con ASP.NET Core Identity

- **Sin código criptográfico casero**: Todo el ciclo de contraseñas (PBKDF2 con sal aleatoria), tokens de recuperación, segundo factor TOTP (2FA), bloqueo por fuerza bruta (`MaxFailedAccessAttempts = 5`, bloqueo de 15 minutos) y cookies de sesión lo gestiona `ASP.NET Core Identity`.
- **Cookies endurecidas**:
  - Cookie de sesión: `__Host-Paye.Auth` (`HttpOnly = true`, `SameSite = Lax`, `SecurePolicy = Always` en producción, expiración de 8 horas).
  - Cookie antiforgery: `__Host-Paye.Antiforgery` (`HttpOnly = true`, `SameSite = Strict`, `SecurePolicy = Always` en producción), validada globalmente mediante `AutoValidateAntiforgeryTokenAttribute`.
- **Invalidación inmediata de sesiones (`SecurityStampValidator`)**:
  - `ValidationInterval = TimeSpan.Zero` junto con el evento `OnValidatePrincipal` comprueba en cada petición que el `SecurityStamp` sea vigente y que `ApplicationUser.IsActive == true`.
  - Si un administrador desactiva a un empleado o cambia sus roles/contraseña, su sesión abierta queda invalidada en la siguiente petición sin esperar a que caduque la cookie.

---

## 3. Autorización Granular por Permisos (Claims de Rol)

En lugar de acoplar los controladores a nombres de rol fijos (`[Authorize(Roles = "Encargado")]`), Payé Base autoriza por **permisos granulares**:

1. **Declaración**: El núcleo declara `CorePermissions` (`Usuarios.Ver`, `Usuarios.Crear`, `Usuarios.Editar`, `Roles.Gestionar`, `Auditoria.Ver`, `Archivos.Subir`, `Archivos.Descargar`) y cada módulo declara los suyos (p. ej. `Notas.Ver`, `Notas.Crear`, `Notas.Editar`, `Notas.Borrar`) mediante `IPermissionDefinitionProvider`.
2. **Almacenamiento**: Los permisos se guardan en la tabla estándar `AspNetRoleClaims` de Identity con el tipo de claim `paye.permission`.
3. **Evaluación en servidor**:
   - `PermissionPolicyProvider` construye dinámicamente políticas para cualquier permiso registrado.
   - `PermissionAuthorizationHandler` verifica que el usuario tenga el claim `paye.permission` correspondiente.
4. **Roles base preconfigurados**:
   - `Propietario`: Posee todos los permisos del sistema y de todos los módulos registrados.
   - `Encargado`: Permisos operativos de gestión (configurable desde `/admin/users`).
   - `Empleado`: Permisos operativos básicos de consulta y creación (configurable desde `/admin/users`).
5. **Protección del último `Propietario`**: El sistema impide en servidor desactivar o retirar el rol `Propietario` al único propietario activo restante.

---

## 4. Cabeceras HTTP y CSP Estricta

El middleware `SecurityHeadersMiddleware` inyecta en todas las respuestas:
- `Content-Security-Policy`: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'` (sin `unsafe-inline` ni `unsafe-eval`).
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()`
- Configuración HTMX compatible con CSP estricta: `<meta name="htmx-config" content='{"includeIndicatorStyles":false}' />`.

---

## 5. Subida Privada de Archivos y RGPD

- **Archivos privados (`LocalPrivateFileStorage`)**:
  - Se guardan fuera de `wwwroot` (en `Security:PrivateUploadsPath`), con nombre aleatorio generado por el servidor (`Guid`).
  - Se valida tamaño máximo y **magic bytes** reales de la cabecera binaria (PDF, PNG, JPEG, WEBP). Se rechazan ejecutables o scripts aunque se renombren.
  - Solo se descargan tras pasar por autenticación y autorización (`GET /files/{id:guid}`) con `Content-Disposition: attachment` y `X-Content-Type-Options: nosniff`.
- **RGPD (`GdprUserDataService`)**:
  - Exportación estructurada en JSON de los datos de un usuario (`POST /admin/users/{id}/gdpr-export`).
  - Anonimización irreversible de datos personales de usuarios inactivos manteniendo la integridad referencial del historial (`POST /admin/users/{id}/gdpr-anonymize`).

---

## 6. Modo Demostración Aislado (`DemoSandbox`)

Para entornos locales o contenedores de vista previa comercial, **Payé Base** incluye un **Sandbox Demo Aislado** (`DemoSandboxSettings`):

1. **Desactivado por defecto (`Fail-closed`)**: `DemoSandbox:Enabled` es `false` por defecto. Si no se activa explícitamente (`DemoSandbox__Enabled=true`), el botón no se renderiza en `/account/login` y `POST /account/demo-login` devuelve `404 Not Found`.
2. **Sesiones efímeras sin contraseñas fijas**: Al pulsar *"Entrar en Modo Demo Aislado"*, `DemoSandboxService` crea un usuario efímero con identificador `demo-<guid>` y una contraseña aleatoria criptográfica de 32 bytes que se descarta en el acto (nadie puede autenticarse por contraseña contra esa cuenta).
3. **Permisos restringidos**: A la cuenta `demo-<guid>` solo se le otorgan los permisos de módulos declarados por `IDemoModuleDataProvider` (p. ej. `Notas.Ver`, `Notas.Crear`, `Notas.Editar`, `Notas.Borrar`). `PermissionAuthorizationHandler` bloquea explícitamente todos los `CorePermissions` (`Usuarios.*`, `Roles.*`, `Auditoria.*`), y `PrivateFilesController` bloquea cualquier subida o descarga en disco físico.
4. **Aislamiento total de registros**: Los servicios de módulo (`NotasService`) filtran todas las consultas y mutaciones para que un usuario `demo-*` únicamente vea y modifique registros donde `CreatedBy == demoUserId`, y los usuarios reales nunca vean registros creados por cuentas `demo-*`.
5. **Purga automática al cerrar sesión**: Al ejecutar `POST /account/logout` (además de una purga preventiva de sesiones demo caducadas de más de 2 horas), `DemoSandboxService.CleanupDemoSessionAsync` elimina físicamente en PostgreSQL todos los datos del módulo de esa sesión, sus entradas en `audit_logs` y la propia cuenta `ApplicationUser`.

