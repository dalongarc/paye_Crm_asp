# Despliegue en VPS con Coolify y Copias de Seguridad

Esta guía describe cómo desplegar una aplicación basada en **Payé Base** en un VPS Linux económico (4–6 €/mes, 1–2 GB de RAM, p. ej. Hetzner CX22 o Netcup) gestionado con **Coolify**.

---

## 1. Requisitos del Servidor y Arquitectura del Contenedor

- **Imagen Docker**: `Dockerfile` multi-stage basado en `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra` (Ubuntu 24.04 rootless/chiseled, usuario sin privilegios `APP_UID`, puerto interno `8080`).
- **Consumo de memoria**: ~75–110 MB de RAM en reposo/carga ligera, dejando amplio margen en un VPS de 1–2 GB para PostgreSQL y el agente de Coolify.
- **Healthcheck**: El propio binario ejecuta `dotnet Paye.Web.dll --healthcheck` contra `http://127.0.0.1:8080/health` (sin instalar `curl` ni `wget` dentro de la imagen de producción).

---

## 2. Volúmenes Persistentes Obligatorios en Coolify

Configura dos volúmenes persistentes montados en el contenedor de la aplicación para que los reinicios o redespliegues no invaliden las sesiones ni borren documentos subidos:

| Ruta en el contenedor | Propósito | Permisos requeridos |
| :--- | :--- | :--- |
| `/var/paye/dpkeys` | Claves de cifrado de ASP.NET Core Data Protection (cookies y tokens) | Lectura/Escritura para `APP_UID` (`1654`) |
| `/var/paye/uploads` | Almacenamiento privado de archivos subidos (fuera de `wwwroot`) | Lectura/Escritura para `APP_UID` (`1654`) |

---

## 3. Variables de Entorno en Coolify

Configura las siguientes variables en la pestaña **Environment Variables** del recurso en Coolify (toma `.env.example` como referencia, nunca subas valores reales al repositorio):

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

# Base de datos PostgreSQL del cliente
ConnectionStrings__DefaultConnection=Host=postgres-cliente;Port=5432;Database=paye_cliente;Username=paye_user;Password=<PASSWORD_FUERTE>

# Datos del negocio
ClientSettings__BusinessName=Comercio Ejemplo S.L.
ClientSettings__ContactEmail=contacto@comercioejemplo.es
ClientSettings__ContactPhone=+34 910 000 000
ClientSettings__TimeZoneId=Europe/Madrid
ClientSettings__DefaultCulture=es-ES

# Seguridad HTTP y rutas de volúmenes
Security__DataProtectionKeysPath=/var/paye/dpkeys
Security__PrivateUploadsPath=/var/paye/uploads
Security__MaxUploadFileSizeBytes=5242880
Security__AllowedOrigins__0=https://gestion.comercioejemplo.es
Security__TrustedProxies__0=172.18.0.1

# SMTP (opcional; si Enabled=false la app avisa explícitamente y no simula envíos)
Smtp__Enabled=false
```

---

## 4. Aplicación de Migraciones y Siembra del Primer `Propietario`

Por seguridad (regla de `AGENTS.md`), **la aplicación en producción nunca ejecuta `Database.Migrate()` automáticamente en cada arranque normal**.

### Paso 1: Aplicar migraciones y sembrar el primer Propietario
En el primer despliegue (o cuando una actualización incluya una nueva migración revisada):

1. Añade temporalmente en Coolify las variables de siembra del propietario inicial:
   ```ini
   SEED_OWNER_EMAIL=propietario@comercioejemplo.es
   SEED_OWNER_PASSWORD=<CONTRASEÑA_MIN_12_CARACTERES_MAYUS_MINUS_NUM_SIMBOLO>
   SEED_OWNER_FULLNAME=Nombre del Propietario
   ```
2. Ejecuta desde la terminal del contenedor en Coolify el comando explícito de migración y siembra:
   ```bash
   dotnet Paye.Web.dll --migrate
   ```
   *(Este comando aplica las migraciones pendientes, crea los roles `Propietario`, `Encargado` y `Empleado` con sus permisos, y da de alta al primer `Propietario`).*

### Paso 2 (OBLIGATORIO): Eliminar `SEED_OWNER_EMAIL` y `SEED_OWNER_PASSWORD`
> [!IMPORTANT]
> **Elimina inmediatamente `SEED_OWNER_EMAIL`, `SEED_OWNER_PASSWORD` y `SEED_OWNER_FULLNAME` de las variables de entorno de Coolify tras el primer arranque y reinicia el contenedor.**
> Aunque el sembrador es idempotente e ignora esas variables si ya existe un `Propietario` (emitiendo una advertencia en el log), ninguna contraseña en texto plano debe permanecer almacenada en las variables de entorno del panel de despliegue.

---

## 5. Copias de Seguridad Automáticas (PostgreSQL + Archivos) y Restauración

### Configuración del Backup Automático en Coolify
1. En Coolify, abre el recurso de base de datos **PostgreSQL** del cliente y entra en **Backups**.
2. Conecta un destino **S3 Compatible** económico (Cloudflare R2, Hetzner Object Storage o Backblaze B2) con credenciales exclusivas para ese cliente.
3. Activa la programación diaria (p. ej. `0 3 * * *` — 03:00 AM) con retención de 14 a 30 días.
4. Para el volumen `/var/paye/uploads`, activa el backup de volúmenes del servidor o sincronización periódica cifrada (`rclone` / `restic`) al mismo bucket del cliente.

### Procedimiento de Restauración Verificado
Para restaurar un volcado `.sql` o `.dump` generado por `pg_dump` (o comprobar periódicamente que los backups son válidos):

```bash
# 1. Crear una base de datos limpia de destino (o en un contenedor de verificación)
createdb -h <HOST> -U <USER> paye_restaurada

# 2. Restaurar el volcado SQL generado por pg_dump
psql -h <HOST> -U <USER> -d paye_restaurada -v ON_ERROR_STOP=1 -f backup_cliente.sql
```

> [!NOTE]
> La suite automatizada de **Payé Base** (`AcceptanceAndOperationsTests.PostgreSqlBackupAndRestore_PgDumpAndPsqlRestore_PreservesAllApplicationData`) ejecuta `pg_dump` y `psql` dentro de un contenedor PostgreSQL real para garantizar que el esquema de Identity, auditoría y módulos se respalda y restaura sin pérdida de datos.
