# Guía de Módulos Autocontenidos (`Modules/<NombreModulo>/`)

En **Payé Base**, las funcionalidades de negocio (como `Notas`, y en futuros desarrollos `Crm` o `Pos`) se encapsulan en módulos autocontenidos.

---

## 1. Anatomía de un Módulo

Un módulo llamado `<Modulo>` reparte sus archivos exclusivamente en tres carpetas:

```text
src/
 ├── Paye.Core/Modules/<Modulo>/
 │    ├── <Entidad>.cs                  # Hereda de AuditableEntity (+ ISoftDeletable opcional)
 │    ├── <Modulo>Permissions.cs        # Constantes + IPermissionDefinitionProvider
 │    └── <Modulo>Contracts.cs          # DTOs, comandos y contrato I<Modulo>Service
 │
 ├── Paye.Infrastructure/Modules/<Modulo>/
 │    ├── <Entidad>EntityConfiguration.cs  # IEntityTypeConfiguration<Entidad> (autodescubierta)
 │    ├── <Modulo>Service.cs               # Implementación con dbContext.Set<Entidad>()
 │    ├── <Modulo>DemoDataProvider.cs      # IDemoModuleDataProvider (datos efímeros para Sandbox Demo)
 │    └── <Modulo>InfrastructureExtensions.cs
 │
 └── Paye.Web/Modules/<Modulo>/
      ├── <Modulo>ModuleExtensions.cs   # Método único Add<Modulo>Module()
      ├── <Modulo>NavigationProvider.cs # IModuleNavigationProvider (aporta enlace al menú lateral)
      ├── <Modulo>ViewModels.cs         # ViewModels/InputModels con DataAnnotations
      ├── <Modulo>Controller.cs         # Rutas protegidas con [Authorize(Policy = ...)]
      └── Views/                        # Vistas Razor del módulo (/Modules/<Modulo>/Views/*.cshtml)
```

---

## 2. Reglas de Aislamiento entre Módulos

1. **Sin `DbSet<T>` en `PayeDbContext`**: `PayeDbContext` aplica automáticamente todas las clases `IEntityTypeConfiguration<T>` del ensamblado `Paye.Infrastructure`. Dentro del servicio del módulo se consulta mediante `dbContext.Set<Entidad>()`.
2. **Un módulo nunca accede a las tablas internas de otro**: Si el módulo `Pos` necesita consultar datos de un cliente del módulo `Crm`, debe hacerlo a través de un contrato público en `Paye.Core` y nunca haciendo `JOIN` directo a tablas privadas de otro módulo.
3. **Navegación desacoplada**: El menú lateral (`SidebarNavigationViewComponent`) y la portada (`Home/Index.cshtml`) descubren los módulos registrados mediante `IEnumerable<IModuleNavigationProvider>`. Ninguna vista base tiene enlaces fijos a módulos opcionales.

---

## 3. Cómo Crear un Módulo Nuevo Paso a Paso

1. **En `Paye.Core/Modules/<Modulo>/`**:
   - Crea la entidad heredando de `AuditableEntity` (y `ISoftDeletable` si requiere papelera/borrado lógico).
   - Crea `<Modulo>Permissions` implementando `IPermissionDefinitionProvider` para que el sembrador y la pantalla de administración de roles descubran los permisos automáticamente.
   - Define los DTOs y la interfaz `I<Modulo>Service`.
2. **En `Paye.Infrastructure/Modules/<Modulo>/`**:
   - Crea `<Entidad>EntityConfiguration : IEntityTypeConfiguration<Entidad>` indicando nombre de tabla, índices y longitudes máximas.
   - Implementa `<Modulo>Service` usando `PayeDbContext` (`dbContext.Set<Entidad>()`) e `IAuditLogger`.
   - Expón `Add<Modulo>Infrastructure(this IServiceCollection services)`.
3. **En `Paye.Web/Modules/<Modulo>/`**:
   - Crea `<Modulo>NavigationProvider : IModuleNavigationProvider` indicando título, ruta, icono, permiso requerido (`RequiredPermission`) y orden.
   - Crea `<Modulo>ModuleExtensions.Add<Modulo>Module(this IServiceCollection services)` registrando `Add<Modulo>Infrastructure()`, el proveedor de permisos y el proveedor de navegación.
   - Crea el controlador, los ViewModels de entrada/salida y las vistas en `src/Paye.Web/Modules/<Modulo>/Views/`.
4. **Registro y migración**:
   - Añade `builder.Services.Add<Modulo>Module();` en `src/Paye.Web/Program.cs`.
   - Genera la migración aditiva:
     ```powershell
     dotnet ef migrations add Add<Modulo>Module --project src/Paye.Infrastructure --startup-project src/Paye.Infrastructure --output-dir Data/Migrations
     ```
   - Revisa el código de la migración generada antes de aplicarla.

---

## 4. Cómo Eliminar un Módulo que un Cliente No Necesita

Si un cliente no contrata un módulo (por ejemplo, el módulo de ejemplo `Notas`):

1. Borra las tres carpetas del módulo:
   - `src/Paye.Core/Modules/Notas`
   - `src/Paye.Infrastructure/Modules/Notas`
   - `src/Paye.Web/Modules/Notas`
   *(y sus pruebas asociadas en `tests/Paye.Tests/Milestone3b` si no aplican al proyecto del cliente)*.
2. Quita el `using Paye.Web.Modules.Notas;` y la línea `builder.Services.AddNotasModule();` en `src/Paye.Web/Program.cs`.
3. Ejecuta `dotnet build Paye.sln`: la solución compila limpia y el enlace desaparece automáticamente del menú lateral y del panel de inicio.
