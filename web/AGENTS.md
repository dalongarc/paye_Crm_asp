# Reglas Operativas de Frontend (Payé Base / Web)

1. **Ubicación y Aislamiento**:
   - Todo el frontend vive estrictamente en `/web`.
   - Prohibido crear o editar vistas Razor (`.cshtml`), partials, `ViewComponents` o `ViewModels`.

2. **Stack Cerrado y Versiones Fijas**:
   - Plantilla base: `shadcn-admin` (Satnaing).
   - React 19 (`^19.2.x`), TypeScript, Vite, Tailwind CSS v4 (`@tailwindcss/vite`).
   - Enrutador exclusivo: `@tanstack/react-router`. Prohibido añadir `react-router-dom`.
   - Estado del servidor: `@tanstack/react-query` v5.
   - Prohibido cualquier SDK de terceros (eliminar `@clerk/react`, Supabase o Firebase).

3. **Arquitectura de Datos y Mocks (Paso 6 Zero-Refactor)**:
   - PROHIBIDO hardcodear datos mock dentro de componentes o JSX.
   - Todo acceso a datos pasa exclusivamente por un cliente tipado en `/web/src/api/`.
   - Cada recurso define su interfaz (ej. `IAuthApi`, `IClientesApi`, `IFacturasApi`) con dos implementaciones intercambiables:
     - `/web/src/api/mock/`: devuelven promesas con datos tipados simulados.
     - `/web/src/api/real/`: llamadas `fetch` con credenciales de cookie.
   - Un flag central `VITE_USE_MOCKS=true` determina qué cliente se inyecta en los hooks de TanStack Query.
   - Los componentes consumen exclusivamente los custom hooks de TanStack Query (`useClientesQuery`, `useCreateClienteMutation`, etc.).

4. **Diseño y Restricciones Estéticas**:
   - No inventar componentes ni escribir CSS manual: usar exclusivamente componentes de shadcn/ui.
   - Prohibido CSS-in-JS y estilos inline (`style={{...}}`).
   - Iconos locales (`lucide-react`). Cero CDNs.

5. **Puntos Críticos de CSP (Content Security Policy)**:
   - La CSP de producción de Payé Base bloquea `unsafe-inline` en `style-src`.
   - Componentes identificados que inyectan `<style>`:
     - `sonner` (`Toaster`): inyecta estilos CSS en tiempo de ejecución.
     - `chart.tsx`: inyecta `<ChartStyle>` dinámicamente.
   - Directiva: En la fase de prototipado en dev (puerto Vite) funcionan normalmente. En el Paso 6, se desacoplarán o se servirán con nonce / CSS estático puro en el build de producción.

6. **Seguridad de Sesión**:
   - Cero tokens o datos de autenticación en `localStorage` o `sessionStorage`.
   - Autenticación exclusivamente por Cookie `HttpOnly` gestionada por el servidor.
