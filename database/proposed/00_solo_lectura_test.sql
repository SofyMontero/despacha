-- Despacha — lectura de TEST para phpMyAdmin.
-- Base esperada: u633742531_Multi_test.
-- Volcado de referencia: 2026-10-22 21:01:02, MariaDB 11.8.9-log.
-- No contiene INSERT, UPDATE, DELETE, DDL ni CALL.
-- No devuelve claves, tokens, correos, teléfonos, nombres ni textos de mensajes.
-- Ejecutar con la base TEST ya seleccionada. Detenerse si control_base no es OK_TEST.
-- No se ejecutó al preparar este archivo.

SET SESSION TRANSACTION READ ONLY;
START TRANSACTION READ ONLY;

-- 0. Identidad del servidor.
SELECT DATABASE() AS base,
       VERSION() AS version,
       @@character_set_database AS charset_base,
       @@collation_database AS collation_base,
       @@time_zone AS zona_sesion,
       @@system_time_zone AS zona_sistema,
       @@sql_mode AS sql_mode,
       CASE
         WHEN DATABASE() = 'u633742531_Multi_test' THEN 'OK_TEST'
         ELSE 'DETENER_BASE_INESPERADA'
       END AS control_base;

-- 1. Tablas. El volcado tenía 18, todas InnoDB.
SELECT TABLE_NAME, ENGINE, TABLE_COLLATION, AUTO_INCREMENT, TABLE_ROWS
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME;

-- 2. Columnas que la migración puede tocar o debe dejar quietas.
SELECT TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_KEY, EXTRA
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN (
    'users','roles','empresas','categorias','productos','promociones','promos',
    'clientes','bares','pedidos','detalle_pedidos','registro',
    'jornadas','jornada_ubicaciones','visitas','entregas','entrega_eventos','entrega_ubicaciones'
  )
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- 2b. Columnas de caja que este volcado de TEST no tenía. PRODUCCIÓN puede tenerlas.
SELECT COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'pedidos'
  AND COLUMN_NAME IN (
    'ped_ingreso','ped_factura','ped_egreso','ped_medio_pago',
    'ped_validado','ped_fecha_actualizacion','ped_id'
  )
ORDER BY COLUMN_NAME;

-- 3. Índices.
SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE, SEQ_IN_INDEX, COLUMN_NAME
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN (
    'users','roles','categorias','productos','promociones','clientes','bares',
    'pedidos','detalle_pedidos','entregas','jornadas','visitas'
  )
ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;

-- 4. Claves foráneas de toda la base. El volcado solo tenía dos, ninguna sobre users o roles.
SELECT k.TABLE_NAME, k.COLUMN_NAME, k.CONSTRAINT_NAME,
       k.REFERENCED_TABLE_NAME, k.REFERENCED_COLUMN_NAME,
       r.UPDATE_RULE, r.DELETE_RULE
FROM information_schema.KEY_COLUMN_USAGE k
JOIN information_schema.REFERENTIAL_CONSTRAINTS r
  ON r.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA
 AND r.CONSTRAINT_NAME = k.CONSTRAINT_NAME
 AND r.TABLE_NAME = k.TABLE_NAME
WHERE k.TABLE_SCHEMA = DATABASE()
  AND k.REFERENCED_TABLE_NAME IS NOT NULL
ORDER BY k.REFERENCED_TABLE_NAME, k.TABLE_NAME;

-- 5. Vistas, triggers, rutinas y eventos. El archivo exportado no traía ninguno.
--    phpMyAdmin puede haberlos omitido: este resultado manda.
SELECT 'vista' AS tipo, TABLE_NAME AS nombre, LEFT(VIEW_DEFINITION, 180) AS detalle
FROM information_schema.VIEWS
WHERE TABLE_SCHEMA = DATABASE()
UNION ALL
SELECT 'trigger', TRIGGER_NAME, CONCAT(EVENT_OBJECT_TABLE, ' ', ACTION_TIMING, ' ', EVENT_MANIPULATION)
FROM information_schema.TRIGGERS
WHERE TRIGGER_SCHEMA = DATABASE()
UNION ALL
SELECT 'rutina', ROUTINE_NAME, ROUTINE_TYPE
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
UNION ALL
SELECT 'evento', EVENT_NAME, STATUS
FROM information_schema.EVENTS
WHERE EVENT_SCHEMA = DATABASE();

-- 6. Columnas que guardan un usuario o un rol aunque no haya FK.
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (
    COLUMN_NAME IN (
      'id_usuario','usuario_id','user_id','id_user','id_rol','rol_id',
      'id_paseador','id_domiciliario','id_vendedor'
    )
    OR COLUMN_NAME LIKE '%usuario%'
    OR COLUMN_NAME LIKE '%\_rol' ESCAPE '\\'
  )
ORDER BY TABLE_NAME, COLUMN_NAME;

-- 7. users: conteos y forma de la clave. No devuelve la clave, el login, el correo ni el token.
SELECT COUNT(*) AS usuarios,
       SUM(password IS NULL OR password = '') AS sin_clave,
       SUM(password LIKE '$2y$%' OR password LIKE '$2a$%' OR password LIKE '$2b$%') AS bcrypt,
       SUM(password IS NOT NULL AND password <> ''
           AND password NOT LIKE '$2y$%' AND password NOT LIKE '$2a$%' AND password NOT LIKE '$2b$%') AS no_bcrypt,
       SUM(api_token IS NOT NULL AND api_token <> '') AS con_api_token,
       SUM(ops_api_token IS NOT NULL AND ops_api_token <> '') AS con_ops_token
FROM users;

SELECT id,
       id_rol,
       rol AS rol_entero,
       CASE
         WHEN password IS NULL OR password = '' THEN 'vacia'
         WHEN password LIKE '$2y$%' OR password LIKE '$2a$%' OR password LIKE '$2b$%' THEN 'bcrypt'
         ELSE 'no_bcrypt'
       END AS forma_clave,
       CASE WHEN api_token IS NULL OR api_token = '' THEN 0 ELSE 1 END AS tiene_api_token,
       CASE WHEN ops_api_token IS NULL OR ops_api_token = '' THEN 0 ELSE 1 END AS tiene_ops_token
FROM users
ORDER BY id;

SELECT id_rol, COUNT(*) AS filas
FROM users
GROUP BY id_rol
ORDER BY id_rol;

SELECT COUNT(*) AS logins_repetidos
FROM (
  SELECT usuario FROM users GROUP BY usuario HAVING COUNT(*) > 1
) d;

-- 8. Catálogo de roles. Son nombres de función, no personas.
SELECT id_rol, rol_nombre, rol_estado
FROM roles
ORDER BY id_rol;

-- 9. Códigos repetidos. Mas de un producto, aunque uno solo esté activo, es ambiguo.
SELECT codigo_productos, COUNT(*) AS productos,
       SUM(estado_producto = '1') AS activos,
       SUM(estado_producto <> '1') AS no_activos
FROM productos
GROUP BY codigo_productos
HAVING COUNT(*) > 1
ORDER BY codigo_productos;

SELECT
  CASE
    WHEN pr.codigo IS NULL OR pr.codigo = 0 THEN 'sin_codigo'
    WHEN COALESCE(m.productos, 0) = 0 THEN 'sin_producto'
    WHEN m.productos = 1 THEN 'unico'
    ELSE 'ambiguo'
  END AS correspondencia,
  COUNT(*) AS promociones
FROM promociones pr
LEFT JOIN (
  SELECT codigo_productos, COUNT(*) AS productos
  FROM productos
  GROUP BY codigo_productos
) m ON m.codigo_productos = pr.codigo
GROUP BY correspondencia;

-- 10. Huérfanos. Solo se cuentan.
SELECT 'producto_sin_categoria' AS relacion, COUNT(*) AS filas
FROM productos p
LEFT JOIN categorias c ON c.id_categoria = p.id_cate_producto
WHERE c.id_categoria IS NULL
UNION ALL
SELECT 'cliente_sin_bar', COUNT(*)
FROM clientes x
LEFT JOIN bares b ON b.id_bar = x.cli_Bar
WHERE b.id_bar IS NULL
UNION ALL
SELECT 'detalle_sin_pedido', COUNT(*)
FROM detalle_pedidos d
LEFT JOIN pedidos p ON p.id_pedido = d.id_pedido
WHERE p.id_pedido IS NULL
UNION ALL
SELECT 'detalle_sin_producto', COUNT(*)
FROM detalle_pedidos d
LEFT JOIN productos p ON p.id_producto = d.id_producto
WHERE d.id_producto IS NOT NULL AND p.id_producto IS NULL
UNION ALL
SELECT 'pedido_sin_cliente', COUNT(*)
FROM pedidos p
LEFT JOIN clientes c ON c.id_cliente = p.ped_cliente
WHERE c.id_cliente IS NULL
UNION ALL
SELECT 'entrega_sin_pedido', COUNT(*)
FROM entregas e
LEFT JOIN pedidos p ON p.id_pedido = e.id_pedido
WHERE p.id_pedido IS NULL
UNION ALL
SELECT 'jornada_sin_usuario', COUNT(*)
FROM jornadas j
LEFT JOIN users u ON u.id = j.id_paseador
WHERE u.id IS NULL
UNION ALL
SELECT 'visita_sin_usuario', COUNT(*)
FROM visitas v
LEFT JOIN users u ON u.id = v.id_paseador
WHERE u.id IS NULL
UNION ALL
SELECT 'visita_sin_cliente', COUNT(*)
FROM visitas v
LEFT JOIN clientes c ON c.id_cliente = v.id_cliente
WHERE c.id_cliente IS NULL
UNION ALL
SELECT 'usuario_sin_rol', COUNT(*)
FROM users u
LEFT JOIN roles r ON r.id_rol = u.id_rol
WHERE u.id_rol IS NOT NULL AND r.id_rol IS NULL;

-- Ids concretos que el volcado dejó como huérfanos, sin nombres.
SELECT 'producto' AS origen, p.id_producto AS id, p.id_cate_producto AS referencia
FROM productos p
LEFT JOIN categorias c ON c.id_categoria = p.id_cate_producto
WHERE c.id_categoria IS NULL
UNION ALL
SELECT 'detalle', d.id_detalle, d.id_producto
FROM detalle_pedidos d
LEFT JOIN productos p ON p.id_producto = d.id_producto
WHERE d.id_producto IS NOT NULL AND p.id_producto IS NULL
UNION ALL
SELECT 'entrega', e.id_entrega, e.id_pedido
FROM entregas e
LEFT JOIN pedidos p ON p.id_pedido = e.id_pedido
WHERE p.id_pedido IS NULL
ORDER BY origen, id;

-- 11. Estados, sin convertirlos.
SELECT estado_producto, COUNT(*) AS filas FROM productos GROUP BY estado_producto;
SELECT acti_Unidad, COUNT(*) AS filas FROM productos GROUP BY acti_Unidad;
SELECT estado, COUNT(*) AS filas FROM promociones GROUP BY estado;
SELECT ped_estado, COUNT(*) AS filas FROM pedidos GROUP BY ped_estado;
SELECT tipo_producto, COUNT(*) AS filas FROM detalle_pedidos GROUP BY tipo_producto;

-- 12. Fechas que no son una fecha de negocio. El cero no se copia a creado_en.
SELECT COUNT(*) AS clientes,
       SUM(CAST(cli_fecha_registro AS CHAR(19)) LIKE '0000-00-00%') AS fecha_cero,
       SUM(cli_identificacion = 0) AS identificacion_cero
FROM clientes;

SELECT MAX(CHAR_LENGTH(nombre_categoria)) AS categoria_nombre,
       MAX(CHAR_LENGTH(imagen_categoria)) AS categoria_imagen
FROM categorias;

SELECT MAX(CHAR_LENGTH(descripcion_producto)) AS producto_nombre,
       MAX(CHAR_LENGTH(imagen_producto)) AS producto_imagen,
       MAX(precio_unidad_producto) AS precio_unidad,
       MAX(precio_paca_producto) AS precio_paca
FROM productos;

SELECT MAX(CHAR_LENGTH(titulo)) AS promo_titulo,
       MAX(CHAR_LENGTH(descripcion)) AS promo_descripcion,
       SUM(descripcion IS NULL OR descripcion = '') AS promo_sin_descripcion
FROM promociones;

-- 13. Nombres de archivo. Hay que buscarlos en el disco del servidor, no solo en este repositorio.
SELECT 'categoria' AS origen, imagen_categoria AS archivo, COUNT(*) AS filas
FROM categorias
GROUP BY imagen_categoria
UNION ALL
SELECT 'producto', imagen_producto, COUNT(*)
FROM productos
GROUP BY imagen_producto
UNION ALL
SELECT 'promocion', imagen, COUNT(*)
FROM promociones
GROUP BY imagen
UNION ALL
SELECT 'promo_legacy', pro_imagen, COUNT(*)
FROM promos
GROUP BY pro_imagen
ORDER BY origen, archivo;

-- 14. El catálogo paralelo no debe existir, ni tampoco el renombre todavía.
SELECT TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND (
    TABLE_NAME LIKE 'catalog\_%'
    OR TABLE_NAME LIKE 'customers\_%'
    OR TABLE_NAME IN ('usuarios_legacy', 'roles_legacy', '__ef_catalog', '__ef_customers')
  )
ORDER BY TABLE_NAME;

-- 15. Cuadre con el volcado del 2026-10-22. DIFIERE significa que TEST cambió y hay que releer antes de migrar.
SELECT 'productos' AS tabla, COUNT(*) AS filas,
       CASE WHEN COUNT(*) = 258 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END AS control
FROM productos
UNION ALL SELECT 'categorias', COUNT(*), CASE WHEN COUNT(*) = 11 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM categorias
UNION ALL SELECT 'promociones', COUNT(*), CASE WHEN COUNT(*) = 8 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM promociones
UNION ALL SELECT 'promos', COUNT(*), CASE WHEN COUNT(*) = 21 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM promos
UNION ALL SELECT 'bares', COUNT(*), CASE WHEN COUNT(*) = 3 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM bares
UNION ALL SELECT 'clientes', COUNT(*), CASE WHEN COUNT(*) = 2 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM clientes
UNION ALL SELECT 'pedidos', COUNT(*), CASE WHEN COUNT(*) = 14 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM pedidos
UNION ALL SELECT 'detalle_pedidos', COUNT(*), CASE WHEN COUNT(*) = 18 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM detalle_pedidos
UNION ALL SELECT 'entregas', COUNT(*), CASE WHEN COUNT(*) = 6 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM entregas
UNION ALL SELECT 'users', COUNT(*), CASE WHEN COUNT(*) = 6 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM users
UNION ALL SELECT 'roles', COUNT(*), CASE WHEN COUNT(*) = 6 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM roles
UNION ALL SELECT 'jornadas', COUNT(*), CASE WHEN COUNT(*) = 2 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM jornadas
UNION ALL SELECT 'visitas', COUNT(*), CASE WHEN COUNT(*) = 1 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM visitas
UNION ALL SELECT 'empresas', COUNT(*), CASE WHEN COUNT(*) = 1 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM empresas
UNION ALL SELECT 'registro', COUNT(*), CASE WHEN COUNT(*) = 560 THEN 'IGUAL_AL_VOLCADO' ELSE 'DIFIERE_DEL_VOLCADO_2026_10_22' END FROM registro;

COMMIT;
