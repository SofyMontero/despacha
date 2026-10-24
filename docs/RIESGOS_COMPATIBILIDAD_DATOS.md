# Riesgos de compatibilidad de datos

Estado: **Pendiente de aprobación**. Fecha: 2026-10-22.

Relacionados: [MATRIZ_DATOS_LEGACY_NET.md](MATRIZ_DATOS_LEGACY_NET.md) y [ESTRATEGIA_MIGRACION_DATOS.md](ESTRATEGIA_MIGRACION_DATOS.md). La columna "Cómo se mide" apunta al bloque de [`inspeccion_solo_lectura.sql`](../database/legacy/inspeccion_solo_lectura.sql).

Impacto: **Alto** = pérdida o atribución incorrecta de información histórica, o bloqueo de la migración; **Medio** = datos inconsistentes que se pueden marcar y conservar; **Bajo** = ajuste técnico.

## 1. Riesgos

| Id | Riesgo | Evidencia | Impacto | Cómo se mide | Mitigación |
|---|---|---|---|---|---|
| R-01 | **El esquema real no se conoce.** Tipos, nulos, índices y claves foráneas solo se deducen del código | Sin acceso a TEST ni volcado | Alto | Bloque A | Ejecutar el script antes de implementar; actualizar la matriz |
| R-02 | **`ped_numfac` es aleatorio y puede repetirse.** Es el número de pedido (no la factura) y no es secuencial | `PedidoModel::crearPedido`: `rand(1000,9999)` por día con un solo reintento | Alto (choque con el índice único) | C4 | D-M5; Consecutivo nuevo según la estrategia (sección 6) |
| R-03 | **Las líneas de promoción no están marcadas**, y `id_producto` puede ser un id de promoción que coincide con otro producto | `CheckoutPage.jsx` envía `tipo` unidad o paca; `api/promociones.php` usa como `id` el producto relacionado o el `id_promocion` | Alto (ventas atribuidas al producto equivocado) | D (coincidencias de id y nombre), E4 | Resolución por nombre; referencia vacía si no se resuelve (D-M9) |
| R-04 | **Estados mezclados:** texto y números en pedidos (`1`, `2`, `pendiente`, `aceptado`…), y en caja `ped_estado` es un id de `estados` que choca con esos números | `solicitudModel.php`, `controllerCaja.php` | Alto | C3 | Clasificar primero la fila (pedido o caja) y luego mapear; valores desconocidos a cuarentena |
| R-05 | **Borrados físicos** de clientes, bares, promociones y movimientos de caja: hay huérfanos y no hay historial de lo borrado | `BarModel` (`DELETE FROM clientes`, `DELETE FROM bares`), `Promo.php`, `cartera.php` | Alto | D | Clientes históricos inactivos (D-M4); líneas con nombre y precio congelados |
| R-06 | **Teléfonos repetidos y en varios formatos** (con o sin `57`); el PHP elige el `id_cliente` más bajo | `PedidoModel`, `buscar_cliente.php`, `api/cliente.php` | Alto (el acceso por WhatsApp, D-09, necesita un teléfono → un cliente) | F1 | Normalizar a E.164; lista de repetidos para depurar con el negocio antes del corte; sin índice único hasta entonces |
| R-07 | **Caja en la tabla `pedidos`, con columnas inconsistentes** (`ped_id` frente a `id_pedido`; `clientes.id` y `clientes.nombre` frente a `id_cliente` y `cli_nombre`) y borrado físico | `cartera.php`, `controllerCaja.php` | Alto (puede que la caja no funcione o que el cliente de caja no sea el mismo) | A7, C1, C2, D | Confirmar columnas; separar por clase de fila; sin saldo histórico de caja, solo movimientos |
| R-08 | **Precios y total enviados por el cliente**, sin recálculo en el servidor | `api/pedido.php`, `PedidoModel` | Medio | E1, E3, E4 | Copiar tal cual y reportar diferencias; D-11 aplica solo a pedidos nuevos |
| R-09 | **`codigo_productos` repetido**, y la promoción se enlaza por ese texto | `ProductoModel` (actualización por código), subconsulta con `LIMIT 1` | Medio | E5, E6, D | Índice no único hasta depurar; la promoción guarda el código original y el producto resuelto |
| R-10 | **Zonas horarias mezcladas:** `ped_fecha` en hora de Colombia (servidor − 5 h) y `ped_fecha_actualizacion` con `NOW()` del servidor | `PedidoModel` | Medio (fechas de reportes y de corte mal calculadas) | A1, C8 | Conversión a UTC por columna, documentada |
| R-11 | **La relación bar–cliente es al revés del diseño** (un bar con N clientes); el admin usa `INNER JOIN bares`, así que oculta pedidos de clientes sin bar válido | `BarModel`, `solicitudModel.php` | Medio (cambio de modelo; pedidos "invisibles" hoy) | D, F2 | D-M3; reportar pedidos ocultos aparte en la equivalencia |
| R-12 | **Claves en dos formatos:** el login compara en texto plano, pero el alta y la edición de `User.php` guardan bcrypt; esos usuarios hoy no pueden ingresar | `controllers/index.php`, `models/User.php` | Medio (seguridad) | F3 | D-M1: no migrar claves |
| R-13 | **`users.rol` mezcla nombres y números**, y existen roles sin plantilla (Supervisor, Cajero, "Cliente") | `user-list.php`, `user-new.php`, script 002 | Medio | F3, B | D-M2 |
| R-14 | **Columnas del prototipo vacías desde su retiro:** el PHP actual no llena `id_empresa` ni `ped_origen`; las tablas `jornadas` y `visitas` pueden tener solo datos de prueba | Scripts 002–004, `0ee81f3` | Medio | C9, F2, G2 | `NULL` = Empresa A; D-M8 |
| R-15 | **Imágenes ausentes:** la base guarda solo el nombre; el PHP busca en varias carpetas y oculta las ausentes con un logo | `helpers/promo_imagen.php` | Medio | G1 e inventario de archivos | Reportar ausentes; migrar la fila sin imagen |
| R-16 | **Codificación:** tablas posiblemente en `latin1` o con texto ya mal convertido | Desconocido | Medio | A2, A3, G4 | Convertir a `utf8mb4` en la copia; corregir solo los casos detectados con la regla documentada |
| R-17 | **Tipos inesperados** (precios o fechas en texto, `ped_estado` en texto) | Valores mixtos en el código | Medio | A3 | Conversión con validación; los que no conviertan → cuarentena |
| R-18 | **`cantidad_venta` inflado** (las promociones también lo incrementan) | `PedidoModel` | Bajo | No aplica | No se migra |
| R-19 | **No hay historial de precios:** solo existe el precio actual del producto | Esquema de `productos` | Bajo | No aplica | El precio histórico vive en cada línea del pedido |
| R-20 | **El establecimiento del pedido se infiere** del bar actual del cliente | `ped_sede` es texto | Bajo | No aplica | Marcar como inferido; conservar `ped_sede` como dirección de entrega |
| R-21 | **Datos personales en la copia de trabajo** | El volcado incluye clientes y personal | Alto (cumplimiento) | No aplica | Autorización expresa; copia cifrada, fuera del repositorio, borrada al terminar; reportes sin datos personales |
| R-22 | **Duplicación entre ejecuciones** si el destino no se recrea | Proceso | Medio | Repetibilidad (estrategia, sección 7) | Destino recreado en cada ejecución; hash por tabla |
| R-23 | **Ids `INT` frente a `BIGINT`** y continuidad de autoincrementales | Diseño D: ids `BIGINT` | Bajo | E8 | Insertar ids explícitos; ajustar `AUTO_INCREMENT` al máximo + 1 |

## 2. Diferencias con lo documentado

| Documento | Decía | Lo que muestra el código |
|---|---|---|
| Fase 2, sección 8 | Cliente tiene N establecimientos | Un bar tiene N clientes (R-11) |
| Fase 2, sección 16 | "Se conservan los id y el `ped_numfac`" | `ped_numfac` puede estar repetido (R-02) |
| Fase 2, D-08 | Convertir claves legadas a hash | Restricción de 4B: no copiar claves; además hay dos formatos (R-12) |
| Fase 2, sección 16 | Usuarios: `Vendedor` → Vendedor | También hay `Supervisor`, `Cajero`, `Paseador` y valores numéricos (R-13) |
| Fase 2, DetallePedido | Tipo unidad, paca o promo | En los datos históricos no hay tipo promo (R-03) |
| Fase 1, sección 6 | `pedidos` tiene id `id_pedido` y `clientes` tiene `id_cliente` | Caja usa `ped_id`, `clientes.id` y `clientes.nombre` (R-07) |
| Fase 1 | Login en texto plano | Correcto, pero el alta y la edición guardan bcrypt (R-12) |
| Prototipo 005 | Entrega única por pedido | No se ejecutó; el diseño nuevo usa N por pedido |

## 3. Riesgos que bloquean la implementación

R-01 (sin el esquema real no se puede escribir el Migrator), R-21 (necesita autorización) y las decisiones D-M1 a D-M9 de la estrategia.
