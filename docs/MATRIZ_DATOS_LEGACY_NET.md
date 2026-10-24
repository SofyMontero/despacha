# Matriz de datos: PHP heredado → .NET

Estado: **Pendiente de aprobación**. Fecha: 2026-10-22.

Documentos relacionados: [ESTRATEGIA_MIGRACION_DATOS.md](ESTRATEGIA_MIGRACION_DATOS.md), [RIESGOS_COMPATIBILIDAD_DATOS.md](RIESGOS_COMPATIBILIDAD_DATOS.md) y el script [`database/legacy/inspeccion_solo_lectura.sql`](../database/legacy/inspeccion_solo_lectura.sql).

## 1. Alcance y evidencia

- **No hubo acceso a la base TEST.** No hay cadena de conexión configurada ni volcado del esquema en el repositorio. Todo lo que dice este documento sobre la base sale del código PHP (`sistema/`), de los scripts del prototipo (commit `92be7f2`, `sistema/sql/002`–`005`, retirados en `0ee81f3`) y de `docs/FASE_1_DIAGNOSTICO.md`.
- Por eso las columnas se marcan así:
  - **[C]** la usa el código PHP actual (leída o escrita);
  - **[P]** la crea un script del prototipo que se ejecutó (002, 003 y 004, según el registro de la Fase 1);
  - **[?]** el código la usa de forma contradictoria; hay que confirmarla con el bloque A del script de inspección.
- Tipos, longitudes, nulos, índices y claves foráneas **no se conocen**. El script de inspección los obtiene.
- En .NET solo existen hoy las entidades de la **base de control** (Fase 4B). Las entidades operativas son el diseño de la sección 8 de `FASE_2_ARQUITECTURA.md`; esta matriz las compara con ese diseño, no con código.

Clasificación usada:

- **R**: reutilización directa (copia con los mismos valores y, si aplica, el mismo id).
- **A**: adaptación o transformación (cambia forma, tipo, normalización o se divide en varias entidades).
- **N**: entidad nueva (sin origen en PHP, o con origen solo como referencia).

## 2. Inventario de tablas heredadas

| Tabla | Evidencia | Uso actual en PHP | Destino |
|---|---|---|---|
| `users` | [C] | Login del admin y gestión de personal | Base de control (Usuario + Membresía) y PersonalEmpresa |
| `clientes` | [C] | Clientes; el teléfono identifica al cliente en pedidos y WhatsApp | Cliente, ContactoCliente |
| `bares` | [C] | Lugar del cliente | Establecimiento |
| `pedidos` | [C] | **Dos cosas en la misma tabla:** pedidos y movimientos de caja | Pedido y MovimientoCaja |
| `detalle_pedidos` | [C] | Líneas del pedido | DetallePedido |
| `productos` | [C] | Catálogo | Producto |
| `categorias` | [C] | Catálogo | Categoría |
| `promociones` | [C] | Promociones con precio propio | Promoción |
| `estados` | [C] | Catálogo de estados **de caja** (`ped_estado` de las filas de caja) | Enumeración (sin tabla) |
| `medio_pago` | [C] | Catálogo de medios de pago de caja | Catálogo de medios de pago |
| `historial_pedidos` | [?] | El API inserta "si la tabla existe" | Sin destino definido (ver 3.13) |
| `registro` | [C] | Bitácora de mensajes de WhatsApp (contiene teléfonos y textos) | No se migra |
| `empresas` | [P] | Sin uso en el PHP actual | Base de control (Tenant) |
| `roles` | [P] o anterior | Sin uso en el PHP actual. El script 002 dice que ya existía con `id_rol, rol_nombre, rol_estado` | Roles plantilla (no se copia) |
| `jornadas`, `visitas`, `jornada_ubicaciones` | [P] | Sin uso en el PHP actual | Jornada, Visita, PuntoUbicación |
| `entregas`, `entrega_eventos` | Script 005, **no ejecutado** | Ninguno | Solo si el bloque A muestra que existen |

## 3. Matriz por entidad

### 3.1 Usuario, Membresía y PersonalEmpresa ← `users` (+ `roles`)

| Aspecto | Detalle |
|---|---|
| Origen | `users`: `id, usuario, password, email, telefono, direccion, nombre, rol` [C]; `id_empresa, id_rol, ops_api_token` [P] |
| Destino | Control: `users` (StaffUser), `memberships`, `roles`. Operativa: PersonalEmpresa (copia mínima) |
| Equivalencias | `usuario` → `UserName`; `email` → `Email`; `nombre` → `DisplayName`; `telefono` → `PhoneNumber` (E.164); `rol`/`id_rol` → Membresía con rol plantilla |
| Campos nuevos | Estado, intentos fallidos, `SecurityStamp`, membresía por empresa, token de renovación |
| Campos que se descartan | `password` (ver transformación), `direccion` (dato personal sin uso en la plataforma nueva), `ops_api_token` (credencial del prototipo) |
| Transformación | `users.rol` mezcla texto (`Administrador`, `Vendedor`, `Supervisor`) con números (`1`, `2`; el formulario viejo usa `2` = "Cliente"). Se mapea: Administrador → Administrador; Vendedor y Paseador → Vendedor; Domiciliario → Domiciliario; **Supervisor, Cajero y "Cliente" no tienen plantilla** (decisión pendiente) |
| Claves | El id de StaffUser es nuevo (la base de control es compartida entre empresas). Se guarda la correspondencia `users.id` → `StaffUser.Id` porque `jornadas`, `visitas` y `entregas` referencian `users.id` |
| Riesgos | Claves en dos formatos (texto plano y bcrypt, ver R-12); `usuario` y `email` posiblemente repetidos (sin índice único conocido) |
| Clase | **A** |

### 3.2 Cliente ← `clientes`

| Aspecto | Detalle |
|---|---|
| Origen | `id_cliente, cli_identificacion, cli_nombre, cli_telefono, cli_direccion, cli_Bar, cli_zona, cli_fecha_registro` [C]; `id_empresa` [P]. El módulo de caja usa además `clientes.id` y `clientes.nombre` [?] |
| Destino | Cliente (+ ContactoCliente, Establecimiento) |
| Equivalencias | `id_cliente` → `Cliente.Id` (**se conserva**); `cli_nombre` → razón social (así lo llama `BarModel`); `cli_identificacion` → identificación (opcional); `cli_fecha_registro` → fecha de alta |
| Campos nuevos | Estado (activo, inactivo), auditoría, CondicionCredito con valores por defecto |
| Campos que se descartan | `id_empresa` (la empresa la da la base); `cli_telefono`, `cli_direccion`, `cli_Bar` y `cli_zona` pasan a otras entidades |
| Transformación | Ninguna en el id. La identificación no es única ni obligatoria en PHP |
| Claves | `id_cliente` lo usan `pedidos.ped_cliente`, `visitas.id_cliente` y (quizás) caja |
| Riesgos | Clientes borrados con `DELETE` dejan pedidos huérfanos (R-05); teléfonos repetidos (R-06) |
| Clase | **R** con adaptación menor |

### 3.3 ContactoCliente ← `clientes.cli_telefono`

| Aspecto | Detalle |
|---|---|
| Origen | `cli_telefono` (con o sin `57`, según por dónde se creó); `pedidos.ped_numCliente` como copia histórica |
| Destino | ContactoCliente (teléfono E.164) |
| Transformación | Quitar símbolos; 10 dígitos que empiezan por 3 → `+57…`; `57` + 10 dígitos → `+57…`; el resto queda en cuarentena |
| Riesgos | El PHP busca al cliente por tres variantes del teléfono y toma el `id_cliente` más bajo; varios clientes con el mismo número rompen la identificación por WhatsApp (D-09) |
| Clase | **A** |

### 3.4 Establecimiento ← `bares` (+ `clientes.cli_direccion`, `cli_zona`)

| Aspecto | Detalle |
|---|---|
| Origen | `bares`: `id_bar, nombre_bar, direccion_bar` [C] |
| Destino | Establecimiento |
| Equivalencias | `id_bar` → id legado de Establecimiento (**se conserva**); `nombre_bar` → nombre; `direccion_bar` → dirección |
| Campos nuevos | Coordenadas, zona como catálogo, estado |
| Transformación | **La relación es al revés del diseño.** En PHP un bar tiene N clientes (`clientes.cli_Bar`), y el alta de cliente reutiliza un bar existente si el nombre coincide (`existeBar`, búsqueda `LIKE`). El diseño de Fase 2 dice "Cliente tiene N establecimientos". Hay que decidir si el establecimiento es compartido o si se duplica por cliente (ver la estrategia, decisión D-M3) |
| Riesgos | Bares borrados con `DELETE` dejan `cli_Bar` huérfano; el admin usa `INNER JOIN bares`, así que esos clientes y sus pedidos **no se ven** hoy; `cli_Bar` puede ser `NULL` (alta desde el API) |
| Clase | **A** |

### 3.5 Categoría ← `categorias`

| Aspecto | Detalle |
|---|---|
| Origen | `id_categoria, nombre_categoria, imagen_categoria` |
| Equivalencias | Directas; se conserva el id |
| Riesgos | Productos con `id_cate_producto` inexistente |
| Clase | **R** |

### 3.6 Producto ← `productos`

| Aspecto | Detalle |
|---|---|
| Origen | `id_producto, codigo_productos, descripcion_producto, precio_unidad_producto, precio_paca_producto, cantidad_paca_producto, id_cate_producto, acti_Unidad, imagen_producto, estado_producto, cantidad_venta` |
| Equivalencias | `id_producto` (**se conserva**); `codigo_productos` → código; `descripcion_producto` → nombre; `precio_unidad_producto` → precio unidad; `precio_paca_producto` → precio paca; `cantidad_paca_producto` → unidades por paca; `acti_Unidad` → se vende por unidad; `estado_producto` → activo; `imagen_producto` → ruta relativa |
| Campos nuevos | Auditoría de cambios de precio (no existe en PHP) |
| Campos que se descartan | `cantidad_venta`: contador que el PHP incrementa con cada línea, **incluidas las de promoción**; es derivable de los pedidos y no es fiable |
| Transformación | Precios a `DECIMAL(12,2)` si el tipo actual no lo es. `codigo_productos` **no es único** (el propio PHP lo resuelve con `ORDER BY estado_producto DESC, id_producto DESC LIMIT 1`); el índice nuevo no puede ser único sin depurar antes |
| Riesgos | Códigos repetidos (R-09); el precio histórico no existe: solo el actual |
| Clase | **R** con adaptación menor |

### 3.7 Promoción ← `promociones`

| Aspecto | Detalle |
|---|---|
| Origen | `id_promocion, titulo, descripcion, codigo, precio_unidad_producto, precio_paca_producto, acti_Unidad, imagen, estado, prioridad, creado_en` |
| Equivalencias | `id_promocion` (se conserva); precios, estado, prioridad, imagen y fecha directos |
| Transformación | La promoción se enlaza al producto **por texto**: `promociones.codigo = productos.codigo_productos` (o por descripción, o por id si el código es numérico). Se resuelve una vez al migrar a un `id_producto` opcional y se guarda el código original |
| Riesgos | Las promociones se borran con `DELETE`: las líneas históricas pueden apuntar a promociones que ya no existen; el código puede apuntar a varios productos |
| Clase | **A** |

### 3.8 Pedido ← `pedidos` (solo filas de pedido)

| Aspecto | Detalle |
|---|---|
| Origen | `id_pedido, ped_cliente, ped_estado, ped_fecha, ped_fecha_actualizacion, ped_numfac, ped_total, ped_numCliente, ped_observacion, ped_sede` [C]; `ped_origen, id_visita, fecha_entrega_acordada, id_empresa` [P] |
| Destino | Pedido |
| Equivalencias | `id_pedido` (**se conserva**); `ped_numfac` → **número de pedido** (no es factura); `ped_cliente` → cliente; `ped_total` → total histórico; `ped_observacion` → observación; `ped_numCliente` → teléfono de contacto del pedido; `ped_sede` → dirección de entrega (texto); `id_visita`, `fecha_entrega_acordada` y `ped_origen` directos |
| Campos nuevos | Establecimiento (FK), `id_vendedor`, condición de pago solicitada, estado de entrega y de facturación separados, marca "histórico" |
| Campos que se descartan | `id_empresa` (lo da la base) |
| Transformación | **Estados** (ver la tabla de abajo). **Fechas:** `ped_fecha` se guarda en hora de Colombia (el API resta 5 horas); `ped_fecha_actualizacion` se guarda con `NOW()` del servidor; se convierten a UTC con el desfase de cada una. **Establecimiento:** se toma de `clientes.cli_Bar` al migrar (no hay registro histórico del bar del pedido). `ped_origen` `NULL` → `cliente`; `paseador` → `vendedor` |
| Claves | `ped_numfac` con índice único: el número de PHP es **aleatorio** (`rand(1000,9999)` por día, con un solo reintento), así que puede haber repetidos y no es secuencial |
| Riesgos | Repetidos de `ped_numfac` (R-02); total enviado por el cliente sin recalcular (R-08); estados no documentados (R-04) |
| Clase | **A** |

Mapeo de `ped_estado` (filas de pedido):

| Valor PHP | Estado nuevo | Condición de pago |
|---|---|---|
| `1`, `pendiente` | Pendiente | |
| `2`, `aceptado` | Aprobado | |
| `rechazado` | Rechazado | |
| `enviado` | EnDespacho (o Entregado si el negocio lo confirma, ver la estrategia) | |
| `finalizaContado` | Entregado | Contado |
| `finalizaCredito` | Entregado | Crédito |
| `procesando`, `entregado`, `cancelado` | Aparecen en estadísticas del API; se mapean si el bloque C3 confirma que existen | |
| Otro | Cuarentena (no se migra sin decisión) | |

### 3.9 DetallePedido ← `detalle_pedidos`

| Aspecto | Detalle |
|---|---|
| Origen | `id_detalle, id_pedido, id_producto, nombre_producto, tipo_producto, cantidad, precio_unitario, subtotal` |
| Equivalencias | `id_detalle` (se conserva); `cantidad` → cantidad pedida; `precio_unitario` → precio congelado; `subtotal`; `nombre_producto` → nombre congelado |
| Campos nuevos | Referencia opcional a promoción, estado de resolución de la referencia |
| Transformación | **Las líneas de promoción no se distinguen.** La app envía `tipo` = `unidad` o `paca` y como `id` el del producto relacionado o, si no hay, el `id_promocion`. Por eso `id_producto` puede ser un id de **promoción** que coincide con el de otro producto. Resolución: si `nombre_producto` coincide con `productos.descripcion_producto` del mismo id → producto; si coincide con `promociones.titulo` → promoción; si no → se conserva el nombre y el precio, y la referencia queda "sin resolver". El tipo `promo` solo se asigna con evidencia |
| Riesgos | Atribución a un producto equivocado en reportes (R-03); precio 0 permitido (promoción o error) |
| Clase | **A** |

### 3.10 MovimientoCaja (y Caja, CierreCaja) ← `pedidos` (filas de caja), `estados`, `medio_pago`

| Aspecto | Detalle |
|---|---|
| Origen | Filas de `pedidos` escritas por `controllerCaja.php` y `cartera.php`: `ped_fecha, ped_cliente, ped_factura, ped_ingreso, ped_egreso, ped_estado, ped_medio_pago, ped_validado` |
| Destino | MovimientoCaja (tipo ingreso o egreso, valor, medio, validado, referencia de factura en texto). Caja: una caja "Histórica" por empresa. CierreCaja: no hay origen |
| Equivalencias | `ped_ingreso` → ingreso; `ped_egreso` → egreso; `ped_medio_pago` → medio (catálogo `medio_pago`); `ped_validado` → validado; `ped_factura` → **referencia de factura** (texto); `ped_estado` → estado según el catálogo `estados` |
| Campos nuevos | Concepto, usuario, cierre, movimiento contrario para anular |
| Transformación | Se identifican por `ped_numfac` vacío y columnas de caja con valor (bloque C1). **Su `ped_estado` es un id de `estados` y choca con los estados numéricos de pedido (`1`, `2`)**: nunca se clasifica una fila por su estado |
| Claves | Id legado de la fila conservado como referencia. Cliente: el PHP de caja hace `JOIN clientes C ON C.id = P.ped_cliente` y `SELECT id, nombre FROM clientes`, mientras el resto del sistema usa `id_cliente` y `cli_nombre`. O esas columnas existen, o el módulo de caja está roto; se confirma en A7 |
| Riesgos | Borrado físico de movimientos (`DELETE FROM pedidos WHERE ped_id`): no se puede reconstruir un saldo histórico confiable (R-07) |
| Clase | **A** (MovimientoCaja); **N** (Caja y CierreCaja) |

### 3.11 Factura, DetalleFactura, CuentaPorCobrar, Abono, AplicacionAbono, MovimientoFinanciero

| Aspecto | Detalle |
|---|---|
| Origen | **Ninguno.** El PHP no registra facturas ni abonos. La única referencia es `ped_factura` en las filas de caja |
| Destino | Factura histórica por saldo inicial de cada cliente (D-14), con su CuentaPorCobrar |
| Regla | **No se confunde número de pedido con número de factura:** `ped_numfac` (formato `PED-…`) va a `Pedido.Numero`; `ped_factura` es la factura real. Una factura solo se enlaza a un movimiento de caja si su número coincide exactamente |
| Clase | **N** |

### 3.12 Jornada, Visita, PuntoUbicación ← prototipo

| Entidad | Origen | Transformación | Clase |
|---|---|---|---|
| Jornada | `jornadas` | `id_paseador` → `id_vendedor` (por la tabla de correspondencia de usuarios); estados `en_curso, finalizada, cancelada` directos | **A** |
| Visita | `visitas` | `id_paseador` → `id_vendedor`; `id_cliente` → establecimiento del cliente; `resultado` directo; `id_pedido` se conserva | **A** |
| SesiónTracking / PuntoUbicación | `jornada_ubicaciones` | Una sesión de tipo jornada por cada jornada con puntos. Retención de D-22 | **A** |
| AsignacionCliente | Ninguno | | **N** |

Solo se migran si el bloque G2 muestra filas reales; si son de prueba, la base nueva arranca sin ellas.

### 3.13 Entrega, DetalleEntrega, EventoEntrega

| Aspecto | Detalle |
|---|---|
| Origen | Script 005 no ejecutado (se confirma en A7). Para el histórico: pedidos `enviado`, `finalizaContado` y `finalizaCredito` |
| Transformación | Una entrega histórica cerrada por pedido, con un DetalleEntrega por línea por la cantidad pedida, y un EventoEntrega con la fecha de `ped_fecha_actualizacion` si existe |
| `historial_pedidos` | Si existe y tiene filas, sus cambios de estado son la mejor fuente de fechas de envío y entrega. Fase 2 no tiene entidad para el historial de estados del pedido: se usa para fechar los eventos de entrega y el resto queda en el archivo de la base heredada |
| Clase | **N** (con datos derivados) |

### 3.14 Tenant y TenantDatabase ← `empresas`

| Aspecto | Detalle |
|---|---|
| Origen | `empresas` [P] (fila 1 = Multilicores) |
| Transformación | No se copia: el tenant de la Empresa A se crea con `provision-tenant` (Fase 4B). `slug` → `Code`. Todas las filas heredadas con `id_empresa` `NULL` o `1` pertenecen a la Empresa A (el PHP actual no llena `id_empresa`) |
| Empresa B | Sin datos en PHP según el diseño; si el bloque F2 o G2 muestra filas con `id_empresa` distinto de 1, se revisan antes de decidir |
| Clase | **N** (con referencia) |

### 3.15 Tablas sin destino

| Tabla | Decisión propuesta |
|---|---|
| `registro` | No se migra: bitácora técnica de WhatsApp con datos personales. Queda en el respaldo de la base heredada |
| `estados`, `roles` | Se convierten en enumeraciones o plantillas; no se copian como tablas |
| `ops_api_token` | Se descarta |
| Base de control .NET (`refresh_tokens`, `platform_audit_events`, `user_claims`, `user_logins`, `user_tokens`, `role_permissions`, `tenant_modules`) | **N**, sin origen heredado |

## 4. Imágenes y archivos

| Origen | Ubicación en Hostinger | Valor en base | Destino |
|---|---|---|---|
| Producto | `sistema/assets/img/licores/` | Solo el nombre (`prod_<uniqid>.<ext>` en altas nuevas) | Almacenamiento de objetos, conservando la ruta relativa |
| Promoción | `sistema/assets/img/licores/promos/` | Solo el nombre | Igual |
| Categoría | `sistema/assets/img/licores/` | Nombre | Igual |
| Imágenes de WhatsApp | `sistema/uploads/` | En `registro` o en los mensajes | No se migran |

El PHP busca una imagen en varias carpetas y, si no existe, muestra un logo por defecto (`helpers/promo_imagen.php`). Por eso la base puede apuntar a archivos que no existen sin que nadie lo note. Hace falta un inventario de archivos (lectura) además del script SQL.

## 5. Resumen de la clasificación

| Clase | Entidades |
|---|---|
| **R**: reutilización directa | Categoría; Producto y Cliente (con ajustes menores) |
| **A**: adaptación | Usuario, Membresía y PersonalEmpresa; ContactoCliente; Establecimiento; Promoción; Pedido; DetallePedido; MovimientoCaja; Jornada; Visita; PuntoUbicación |
| **N**: entidad nueva | CondicionCredito, AsignacionCliente, Consecutivo, Caja, CierreCaja, Entrega, DetalleEntrega, EventoEntrega, Factura, DetalleFactura, CuentaPorCobrar, Abono, AplicacionAbono, MovimientoFinanciero, y la base de control |
