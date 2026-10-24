# Fase 5A: diseño de catálogo, clientes y admin web

Estado: **Aprobado** el 2026-10-24 con estas respuestas: se aprueban todas las recomendaciones; teléfono repetido permitido con advertencia (D5-05); categoría con `IsActive`; tabla `promos` ignorada en la Fase 5; el usuario instalará MariaDB 11.8 local.

Base: [FASE_2_ARQUITECTURA.md](FASE_2_ARQUITECTURA.md), [MATRIZ_DATOS_LEGACY_NET.md](MATRIZ_DATOS_LEGACY_NET.md) y [RIESGOS_COMPATIBILIDAD_DATOS.md](RIESGOS_COMPATIBILIDAD_DATOS.md).

## 1. Estado actual verificado

| Pieza | Estado |
|---|---|
| Base de control | `ControlDbContext` con Identity, empresas, membresías, roles, permisos y auditoría (Fase 4B) |
| Base de la empresa | Solo infraestructura: `ITenantContext` (empresa tomada del token, D-05), `TenantConnectionProvider` (servidor y base desde control, credencial desde secretos) e `ITenantDbContextFactory<T>`. **No hay ningún DbContext operativo ni migraciones por empresa** |
| Migrator | Solo migra la base de control y da de alta empresas. No migra bases de empresa |
| Módulos `Catalog` y `Customers` | Vacíos (`ModuleBase`) |
| Permisos existentes | `catalog.read` y `customers.read.assigned` (pensados para el Vendedor). No hay permisos de administración de catálogo ni de clientes |
| Errores | `DespachaException` → ProblemDetails con `title`; validación con `ValidationProblem` (diccionario de errores) |
| OpenAPI | Expuesto en `/openapi/v1.json` (solo fuera de Prod) |
| Login | `POST /api/auth/login` → `POST /api/auth/tenant` (elige empresa, el token lleva `tid`) → `GET /api/auth/me`; refresco con cookie `__Secure-despacha-rt` |
| Admin web | `admin-app/` está vacío (el prototipo se retiró en `0ee81f3`). La Fase 2 ubica el admin en `web/admin/` |
| Pruebas | SQLite para integración; MariaDB preparada pero sin ejecutar (no hay Docker, P4C) |

## 2. Lo que dice el PHP (reglas confirmadas)

| Tema | Regla en PHP | Fuente |
|---|---|---|
| Producto: obligatorios | código, descripción y precio unidad > 0 | `ProductoController` |
| Producto: paca | precio paca ≥ 0; unidades por paca con 1 por defecto; `acti_Unidad = 0` significa "solo paca" | `ProductoController`, `Subir_excel_producto.php` |
| Producto: código | Al crear y editar se rechaza un código ya usado por otro producto; aun así hay duplicados históricos | `ProductoController` líneas 265 y 369 |
| Producto: estado | `estado_producto` 1/0; no se borra | `ProductoController` |
| Producto: imagen | jpg, jpeg, png, gif, webp; máximo 3 MB; nombre `prod_<uniqid>` en `assets/img/licores/` | `ProductoController` |
| Producto: carga masiva | CSV que crea o actualiza por id | `ProductoController` |
| Categoría | id, nombre, imagen; **sin estado** | `categorias` |
| Promoción | título, descripción, código **numérico** (`(int)`), precio unidad y paca **enteros**, `acti_Unidad`, estado, prioridad, imagen; se borra físicamente | `promoController`, `Promo.php` |
| Promoción ↔ producto | Solo por texto: `promociones.codigo = productos.codigo_productos` | `api/promociones.php` |
| Tabla `promos` | **Ningún archivo PHP la consulta** (solo existe la carpeta de imágenes `licores/promos/`). Su estructura no se conoce | búsqueda en `sistema/` |
| Cliente | nombre (razón social) y teléfono obligatorios; identificación, dirección, zona y bar opcionales | `api/cliente.php` |
| Cliente ↔ bar | `clientes.cli_Bar` → `bares.id_bar`: **un bar con N clientes**; el alta reutiliza un bar por nombre parecido (`LIKE`) | `BarModel` |
| Bar | nombre y dirección | `bares` |
| Zona | Texto de una lista fija en el formulario (Chapinero, Centro, Zona T…) | `clientes.php` |
| Teléfono | Repetidos permitidos; formatos con y sin `57` | `PedidoModel`, `buscar_cliente.php` |

## 3. Diseño propuesto

### 3.1 Persistencia por empresa

- **Un DbContext por módulo**: `CatalogDbContext` y `CustomersDbContext`, `internal`, creados con `ITenantDbContextFactory<T>` sobre la base de la empresa activa.
- Cada uno con su tabla de historial de migraciones (`__ef_catalog`, `__ef_customers`) para que los módulos sigan separados (D-01) y se migren por separado.
- Tablas con prefijo de módulo (`catalog_*`, `customers_*`); así no chocan nunca con los nombres de tablas heredadas (`productos`, `clientes`…).
- **Migrator**: comandos nuevos `tenant-script --tenant <código>` y `tenant-apply --tenant <código> --confirm`. Usan la misma protección de bases (`DatabaseSafetyGuard`) y, además, **se niegan a migrar una base que tenga tablas heredadas** (`pedidos`, `productos`, `clientes`), para que sea imposible apuntar a la base de Hostinger por error.
- Ids `BIGINT` autoincrementales; permiten insertar los ids heredados en la migración de datos (Fase 11).
- Concurrencia optimista con una columna de versión: dos administradores editando lo mismo reciben un 409.
- Auditoría mínima en cada fila: creado y modificado (fecha UTC e id de usuario del token). Sin FK a la base de control.

### 3.2 Catálogo

| Entidad | Campos | Origen | Nuevo |
|---|---|---|---|
| Category (`catalog_categories`) | Id, Name (≤ 100), ImageKey, **IsActive**, auditoría, versión | `categorias` | `IsActive` (D5-07) |
| Product (`catalog_products`) | Id, Code (≤ 50, índice **no único**), Name (≤ 200), CategoryId (FK, opcional), UnitPrice, PackPrice, UnitsPerPack, SellsByUnit, IsActive, ImageKey, auditoría, versión | `productos` | Auditoría y versión. No se incluye `cantidad_venta` |
| Promotion (`catalog_promotions`) | Id, Title, Description, Code (texto, como en PHP), ProductId (opcional, resuelto), UnitPrice, PackPrice, SellsByUnit, IsActive, Priority, ImageKey, CreatedAt, auditoría, versión | `promociones` | `ProductId` (D5-10) |

Reglas:

- Precios `DECIMAL(12,2)` en pesos; precio unidad > 0; precio paca ≥ 0; unidades por paca ≥ 1. Son las reglas del PHP.
- Código obligatorio y sin repetir al crear o editar. La validación está en el servicio; el índice de la base no es único para aceptar duplicados históricos (R-09).
- "Presentaciones" = solo **unidad** y **paca** (con unidades por paca). No se crea una entidad de presentaciones: no existe en el sistema actual.
- Nada se borra: categorías, productos y promociones se activan o desactivan (D5-08).
- Un producto no puede tener una categoría inactiva al crearse. Desactivar una categoría no desactiva sus productos.

### 3.3 Clientes

| Entidad | Campos | Origen | Nuevo |
|---|---|---|---|
| Establishment (`customers_establishments`) | Id, Name (≤ 150), Address (≤ 255), IsActive, auditoría, versión | `bares` | `IsActive` |
| Customer (`customers_customers`) | Id, Name (razón social, ≤ 150), Identification (opcional, ≤ 30), Phone (E.164), Address, Zone (texto ≤ 60), EstablishmentId (opcional), RegisteredAt, IsActive, auditoría, versión | `clientes` | `IsActive`, teléfono normalizado |

- Relación como en PHP: **un establecimiento tiene N clientes** y el cliente puede no tener establecimiento (D5-04).
- Teléfono: un solo teléfono, como en PHP. Se normaliza a E.164 de Colombia (`+57` y 10 dígitos) y se permite repetir con advertencia (D5-05). `ContactoCliente` con varios teléfonos queda para cuando haya una necesidad confirmada.
- Búsqueda por nombre, teléfono, identificación y establecimiento; filtros por estado, zona y establecimiento.
- No se incluyen `CondicionCredito` (es cartera, excluida), coordenadas ni asignación a vendedores (FieldSales).
- El alta de cliente **no crea establecimientos por nombre parecido**, como hace el PHP: el establecimiento se elige o se crea de forma explícita.

### 3.4 Imágenes

- Interfaz `IImageStorage` en `Integrations/Storage`. La base guarda solo una clave relativa (`catalog/products/<guid>.webp`).
- Adaptador **local** para Local y pruebas, con una carpeta configurada **fuera del repositorio**. El proveedor en la nube queda pendiente (D-18).
- Imágenes heredadas: clave `legacy/licores/<archivo>` o `legacy/licores/promos/<archivo>`, que se resuelve con `Storage:LegacyBaseUrl` (URL pública de Hostinger, solo lectura). No se copian archivos ahora.
- Subida: jpg, png, webp (y gif por compatibilidad), máximo 3 MB, tipo verificado por contenido y no solo por la extensión.

### 3.5 Permisos nuevos (D5-02)

| Código | Módulo | Uso |
|---|---|---|
| `catalog.manage` | Catalog | Crear, editar y activar o desactivar categorías, productos y promociones |
| `customers.read` | Customers | Ver todos los clientes y establecimientos (admin) |
| `customers.manage` | Customers | Crear, editar y activar o desactivar clientes y establecimientos |

El Administrador los recibe solo (tiene todos los de los módulos activos). Vendedor y Domiciliario no cambian. `catalog.read` sigue sirviendo para leer el catálogo.

### 3.6 API

- Rutas `/api/catalog/categories|products|promotions` y `/api/customers/customers|establishments`: `GET` lista, `GET {id}`, `POST`, `PUT {id}` y `PATCH {id}/status`. Imágenes con `PUT {id}/image` (multipart) y `DELETE {id}/image`.
- Paginación `page` y `pageSize` (máximo 100); respuesta `{ items, total, page, pageSize }`; búsqueda `q` y filtros por campo.
- Errores: 400 `ValidationProblem` por campo, 404, 409 (código repetido o versión desactualizada), 403 sin permiso o con el módulo inactivo.
- Pruebas: unitarias (validaciones, normalización de teléfono), de integración con SQLite (CRUD, permisos, aislamiento entre dos empresas, concurrencia) y de arquitectura (los DbContext y entidades son `internal`, y Catalog y Customers no se usan entre sí). MariaDB: pruebas preparadas, **no ejecutadas** si sigue sin Docker.

### 3.7 Admin web (5C)

- Ubicación `web/admin/`, según la Fase 2 (no `admin-app/`).
- React 19 + TypeScript + Vite + React Router + TanStack Query + Tailwind; formularios con React Hook Form + Zod.
- Cliente HTTP tipado con tipos generados desde el OpenAPI (`openapi-typescript` y `openapi-fetch`). El paquete compartido `packages/api-client` se separa cuando exista la app móvil.
- En desarrollo, el proxy de Vite hace que la API y el admin tengan el mismo origen (la cookie de refresco funciona sin CORS). El token de acceso vive solo en memoria.
- Pantallas: login con elección de empresa, layout con menú lateral según permisos, y listados con búsqueda, filtros, paginación y formularios para las cinco entidades. Las rutas y botones se ocultan según los permisos de `/api/auth/me`, y el servidor vuelve a validar.

## 4. Decisiones que necesitan aprobación

| Id | Decisión | Recomendación |
|---|---|---|
| D5-01 | Persistencia por empresa | Un DbContext por módulo, historial propio, prefijo de tabla y comandos `tenant-*` en el Migrator |
| D5-02 | Permisos nuevos | `catalog.manage`, `customers.read`, `customers.manage` |
| D5-03 | Tabla `promos` | **Ignorada en la Fase 5** (no la usa el PHP actual) |
| D5-04 | Cliente ↔ establecimiento | N clientes por establecimiento, opcional (como PHP) |
| D5-05 | Teléfono | E.164 de Colombia; se permite repetir el teléfono, como en PHP, y la API devuelve una advertencia con los clientes activos que lo comparten |
| D5-06 | Código de producto | Obligatorio y sin repetir en altas y ediciones; índice no único |
| D5-07 | Estado de categoría | Agregar `IsActive` (no existe en PHP) |
| D5-08 | Borrado | Sin borrado físico; solo activar o desactivar |
| D5-09 | Imágenes | `IImageStorage` con adaptador local fuera del repo y URL base de solo lectura para las heredadas |
| D5-10 | Promoción ↔ producto | Conservar `Code` y agregar `ProductId` opcional elegido por el admin |
| D5-11 | Admin web | `web/admin/` con el stack de la Fase 2 y tipos generados desde OpenAPI |
| D5-12 | Fuera de alcance | Carga masiva CSV, envío masivo de promociones, historial de precios, coordenadas, crédito |

## 5. Riesgos pendientes

| Id | Riesgo | Efecto | Mitigación |
|---|---|---|---|
| P5-01 | Sin MariaDB local ni Docker | Las migraciones de los módulos solo se prueban en SQLite; tipos como `DECIMAL` y la concurrencia se comportan distinto | Revisar el SQL de `tenant-script`; ejecutar las pruebas MariaDB cuando haya Docker. No se declara compatibilidad confirmada |
| P5-02 | Sin base local de empresa, el admin no se puede probar de punta a punta contra la API real | Solo pruebas automáticas | Instalar MariaDB 11.8 local o Docker |
| P5-03 | El resultado del script de inspección no se ha recibido | Longitudes y tipos propuestos son estimados | Ajustar longitudes antes de la migración de datos; no afecta el esquema nuevo |
| P5-04 | Tabla `promos` desconocida | Puede contener datos que haya que conservar | Ejecutar el bloque A3 del script y revisar su uso |
| P5-05 | Duplicados históricos de teléfono y código | La migración los trae; el sistema nuevo no crea códigos repetidos y advierte los teléfonos repetidos | Reportes de duplicados en la Fase 11 |
| P5-06 | Las Fases 4B y 4C siguen pendientes de aprobación | La Fase 5 se apoya en ellas | Aprobarlas o registrar observaciones |
