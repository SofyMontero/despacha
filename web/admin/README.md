# Administración Despacha

Panel React del bloque 5C. Habla con la API ASP.NET Core que ya existe. No migra bases, no toca el PHP y no escribe en Hostinger.

## Estructura

```text
web/admin
  index.html
  vite.config.ts          proxy /api y /media hacia localhost:5080
  src/main.tsx            arranque
  src/app.tsx             rutas y permisos de pantalla
  src/api/types.ts        DTO escritos a mano desde los records C#
  src/api/client.ts       fetch, cookie de refresh y token solo en memoria
  src/api/errors.ts       400, 401, 403, 409 y errores de campo
  src/auth/session.tsx    login, empresa, cierre y permisos
  src/components          layout, tabla, diálogo e imagen de catálogo
  src/pages               login, inicio, categorías, productos, promociones, clientes, establecimientos
  src/lib                 precios, teléfonos y filtros de listado
  src/test                pruebas de flujos
```

El token de acceso no se guarda en `localStorage` ni `sessionStorage`. El refresh de la web viaja en la cookie HttpOnly `__Secure-despacha-rt`. El navegador y la API se ven como el mismo origen porque Vite reenvía `/api` y `/media`.

Las imágenes del catálogo se muestran con la URL que devuelve la API (`/media/...` o la URL heredada). Ese camino no se usa para documentos privados.

## Ejecutarlo en local

Hacen falta dos terminales. La API no aplica migraciones al arrancar: la base de la empresa tiene que existir ya. Este bloque no ejecuta migraciones.

En `backend`, el refresh solo acepta el origen si está en la configuración CORS. En desarrollo se agrega con user-secrets, sin cambiar `appsettings.json`:

```powershell
cd backend
dotnet user-secrets --project src/Despacha.Api set "Cors:AllowedOrigins:0" "http://localhost:5173"
dotnet run --project src/Despacha.Api
```

La API queda en `http://localhost:5080`. En otra terminal:

```powershell
cd web/admin
npm install
npm run dev
```

El panel abre `http://localhost:5173`. El puerto es fijo: si está ocupado, Vite se detiene en lugar de cambiar de origen y romper la cookie.

```powershell
npm test
npm run build
```

## Permisos

El menú usa los permisos de `GET /api/auth/me`. Aun así, cada acción llama a la API y muestra el 403 si el servidor la rechaza.

| Pantalla | Ver | Modificar |
| --- | --- | --- |
| Categorías, productos, promociones | `catalog.read` | `catalog.manage` |
| Clientes, establecimientos | `customers.read` | `customers.manage` |

Un vendedor con `customers.read.assigned` no ve la administración de clientes: ese permiso no abre estos endpoints.

## Pendientes y riesgos

- La compatibilidad con MariaDB 11.8.9 sigue sin ejecutarse. No hay prueba de punta a punta contra una base real desde este panel.
- Los tipos están copiados de los records. Cuando la API pueda publicar OpenAPI sin una base real, `src/api/types.ts` se reemplaza por la generación.
- Los desplegables de categorías, productos y establecimientos piden 100 registros. Si hay más, el formulario lo dice y no inventa un endpoint de búsqueda.
- Los identificadores grandes se tratan como número de JavaScript. Por encima de 2^53 dejan de ser exactos.
- El código de producto repetido llega como 400 en el campo `Code`. El 409 es solo el choque de versión.
- La condición del bloque 5B sigue abierta: collation, charset y la carrera de códigos únicos se corrigen en el backend cuando se autorice, antes del primer `tenant-apply` real.
- Pedidos, facturación, cartera y entregas no entran en esta fase. La tabla legacy `promos` tampoco.
