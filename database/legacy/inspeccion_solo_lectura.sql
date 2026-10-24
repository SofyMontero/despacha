-- =============================================================================
-- Despacha: inspección de la base heredada (TEST). SOLO LECTURA.
--
-- * No contiene INSERT, UPDATE, DELETE, DDL ni CALL.
-- * Devuelve metadatos y agregados (conteos, mínimos y máximos de id y fechas,
--   valores de catálogos). No devuelve nombres, teléfonos, direcciones,
--   identificaciones, correos, claves ni mensajes.
-- * Ejecutar en TEST con un usuario de solo lectura (phpMyAdmin o cliente
--   MariaDB), sobre la base heredada seleccionada (USE <base>).
-- * Ejecutar el bloque A completo primero. Los bloques B a G dependen de
--   columnas que el bloque A debe confirmar: si una consulta falla por
--   "Unknown column", anotar el error (es un hallazgo) y seguir con la siguiente.
-- =============================================================================

SET SESSION max_statement_time = 30;
SET SESSION TRANSACTION READ ONLY;
START TRANSACTION READ ONLY;

-- -----------------------------------------------------------------------------
-- A. Metadatos del esquema
-- -----------------------------------------------------------------------------

-- A1. Motor, base y configuración que afecta la migración
SELECT VERSION() AS version, DATABASE() AS base,
       @@character_set_database AS charset_base, @@collation_database AS collation_base,
       @@time_zone AS zona_sesion, @@system_time_zone AS zona_sistema,
       NOW() AS ahora_servidor, UTC_TIMESTAMP() AS ahora_utc,
       @@sql_mode AS sql_mode, @@lower_case_table_names AS lower_case_table_names;

-- A2. Tablas y vistas (TABLE_ROWS es una estimación en InnoDB)
SELECT TABLE_NAME, TABLE_TYPE, ENGINE, TABLE_ROWS, AUTO_INCREMENT,
       TABLE_COLLATION, CREATE_TIME, UPDATE_TIME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME;

-- A3. Columnas
SELECT TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE,
       COLUMN_DEFAULT, COLUMN_KEY, EXTRA, CHARACTER_SET_NAME, COLLATION_NAME,
       COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- A4. Índices y claves únicas
SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE, SEQ_IN_INDEX, COLUMN_NAME, INDEX_TYPE
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;

-- A5. Claves foráneas declaradas (puede no haber ninguna)
SELECT k.TABLE_NAME, k.COLUMN_NAME, k.CONSTRAINT_NAME,
       k.REFERENCED_TABLE_NAME, k.REFERENCED_COLUMN_NAME,
       r.UPDATE_RULE, r.DELETE_RULE
FROM information_schema.KEY_COLUMN_USAGE k
JOIN information_schema.REFERENTIAL_CONSTRAINTS r
  ON r.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA AND r.CONSTRAINT_NAME = k.CONSTRAINT_NAME
WHERE k.TABLE_SCHEMA = DATABASE() AND k.REFERENCED_TABLE_NAME IS NOT NULL
ORDER BY k.TABLE_NAME, k.CONSTRAINT_NAME;

-- A6. Triggers, rutinas, vistas y eventos (solo nombres)
SELECT 'trigger' AS tipo, TRIGGER_NAME AS nombre, EVENT_OBJECT_TABLE AS tabla,
       CONCAT(ACTION_TIMING, ' ', EVENT_MANIPULATION) AS detalle
FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE()
UNION ALL
SELECT 'rutina', ROUTINE_NAME, NULL, ROUTINE_TYPE
FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE()
UNION ALL
SELECT 'vista', TABLE_NAME, NULL, NULL
FROM information_schema.VIEWS WHERE TABLE_SCHEMA = DATABASE()
UNION ALL
SELECT 'evento', EVENT_NAME, NULL, STATUS
FROM information_schema.EVENTS WHERE EVENT_SCHEMA = DATABASE();

-- A7. Presencia de las tablas y columnas que usa el código PHP y el prototipo
SELECT t.tabla, t.columna,
       EXISTS (SELECT 1 FROM information_schema.COLUMNS c
               WHERE c.TABLE_SCHEMA = DATABASE() AND c.TABLE_NAME = t.tabla
                 AND (t.columna = '*' OR c.COLUMN_NAME = t.columna)) AS existe
FROM (
  SELECT 'users' tabla, 'id' columna UNION ALL SELECT 'users','rol' UNION ALL
  SELECT 'users','id_empresa' UNION ALL SELECT 'users','id_rol' UNION ALL SELECT 'users','ops_api_token' UNION ALL
  SELECT 'clientes','id_cliente' UNION ALL SELECT 'clientes','id' UNION ALL SELECT 'clientes','nombre' UNION ALL
  SELECT 'clientes','cli_Bar' UNION ALL SELECT 'clientes','cli_identificacion' UNION ALL SELECT 'clientes','id_empresa' UNION ALL
  SELECT 'bares','*' UNION ALL
  SELECT 'pedidos','id_pedido' UNION ALL SELECT 'pedidos','ped_id' UNION ALL SELECT 'pedidos','ped_numfac' UNION ALL
  SELECT 'pedidos','ped_factura' UNION ALL SELECT 'pedidos','ped_ingreso' UNION ALL SELECT 'pedidos','ped_egreso' UNION ALL
  SELECT 'pedidos','ped_medio_pago' UNION ALL SELECT 'pedidos','ped_validado' UNION ALL
  SELECT 'pedidos','ped_fecha_actualizacion' UNION ALL SELECT 'pedidos','ped_origen' UNION ALL
  SELECT 'pedidos','id_visita' UNION ALL SELECT 'pedidos','fecha_entrega_acordada' UNION ALL SELECT 'pedidos','id_empresa' UNION ALL
  SELECT 'detalle_pedidos','*' UNION ALL SELECT 'productos','*' UNION ALL SELECT 'categorias','*' UNION ALL
  SELECT 'promociones','*' UNION ALL SELECT 'estados','*' UNION ALL SELECT 'medio_pago','*' UNION ALL
  SELECT 'registro','*' UNION ALL SELECT 'historial_pedidos','*' UNION ALL
  SELECT 'empresas','*' UNION ALL SELECT 'roles','*' UNION ALL SELECT 'jornadas','*' UNION ALL
  SELECT 'visitas','*' UNION ALL SELECT 'jornada_ubicaciones','*' UNION ALL
  SELECT 'entregas','*' UNION ALL SELECT 'entrega_eventos','*'
) t;

-- -----------------------------------------------------------------------------
-- B. Catálogos sin datos personales (contenido completo)
-- -----------------------------------------------------------------------------
SELECT * FROM estados;
SELECT * FROM medio_pago;
SELECT * FROM roles;                       -- tabla del prototipo o anterior
SELECT id_empresa, slug, modelo_negocio, activa FROM empresas;
SELECT id_categoria, nombre_categoria FROM categorias ORDER BY id_categoria;

-- -----------------------------------------------------------------------------
-- C. pedidos: separar pedidos de movimientos de caja
-- -----------------------------------------------------------------------------

-- C1. Clasificación de filas
SELECT CASE
         WHEN ped_numfac IS NOT NULL AND ped_numfac <> '' THEN 'pedido'
         WHEN COALESCE(ped_ingreso, 0) <> 0 OR COALESCE(ped_egreso, 0) <> 0 THEN 'caja'
         ELSE 'indeterminada'
       END AS clase,
       COUNT(*) AS filas, MIN(ped_fecha) AS desde, MAX(ped_fecha) AS hasta,
       SUM(ped_fecha IS NULL OR ped_fecha < '1971-01-01') AS fechas_invalidas
FROM pedidos GROUP BY clase;

-- C2. Filas que tienen columnas de pedido y de caja a la vez
SELECT COUNT(*) AS mixtas FROM pedidos
WHERE ped_numfac IS NOT NULL AND ped_numfac <> ''
  AND (COALESCE(ped_ingreso, 0) <> 0 OR COALESCE(ped_egreso, 0) <> 0 OR ped_factura IS NOT NULL);

-- C3. Valores de estado por clase (incluye los números de `estados`)
SELECT CASE WHEN ped_numfac IS NOT NULL AND ped_numfac <> '' THEN 'pedido' ELSE 'otra' END AS clase,
       ped_estado, COUNT(*) AS filas
FROM pedidos GROUP BY clase, ped_estado ORDER BY clase, filas DESC;

-- C4. Número de pedido: formato y duplicados
SELECT COUNT(*) AS con_numero,
       COUNT(DISTINCT ped_numfac) AS distintos,
       SUM(ped_numfac NOT REGEXP '^PED-[0-9]{8}-[0-9]{4}$') AS formato_distinto,
       SUM(SUBSTRING(ped_numfac, 5, 8) <> DATE_FORMAT(ped_fecha, '%Y%m%d')) AS fecha_no_coincide
FROM pedidos WHERE ped_numfac IS NOT NULL AND ped_numfac <> '';

-- C5. Referencia de factura en caja: presencia y repeticiones (sin valores)
SELECT COUNT(ped_factura) AS con_factura,
       COUNT(DISTINCT ped_factura) AS distintas,
       SUM(ped_factura REGEXP '^PED-') AS parecen_numero_de_pedido
FROM pedidos WHERE ped_factura IS NOT NULL AND ped_factura <> '';

-- C6. Totales de caja por mes, medio y validación
SELECT DATE_FORMAT(ped_fecha, '%Y-%m') AS mes, ped_medio_pago, ped_validado,
       COUNT(*) AS filas, SUM(ped_ingreso) AS ingresos, SUM(ped_egreso) AS egresos
FROM pedidos
WHERE (ped_numfac IS NULL OR ped_numfac = '')
  AND (COALESCE(ped_ingreso, 0) <> 0 OR COALESCE(ped_egreso, 0) <> 0)
GROUP BY mes, ped_medio_pago, ped_validado ORDER BY mes;

-- C7. Totales de pedidos por mes y estado (base de la conciliación)
SELECT DATE_FORMAT(ped_fecha, '%Y-%m') AS mes, ped_estado,
       COUNT(*) AS pedidos, SUM(ped_total) AS total
FROM pedidos WHERE ped_numfac IS NOT NULL AND ped_numfac <> ''
GROUP BY mes, ped_estado ORDER BY mes;

-- C8. Diferencia de zona horaria entre fecha y actualización
SELECT COUNT(*) AS con_actualizacion,
       SUM(ped_fecha_actualizacion < ped_fecha) AS actualizacion_anterior_a_fecha,
       MIN(TIMESTAMPDIFF(MINUTE, ped_fecha, ped_fecha_actualizacion)) AS min_minutos
FROM pedidos WHERE ped_fecha_actualizacion IS NOT NULL;

-- C9. Columnas del prototipo en pedidos
SELECT id_empresa, ped_origen, COUNT(*) AS filas,
       SUM(id_visita IS NOT NULL) AS con_visita,
       SUM(fecha_entrega_acordada IS NOT NULL) AS con_fecha_acordada
FROM pedidos GROUP BY id_empresa, ped_origen;

-- -----------------------------------------------------------------------------
-- D. Integridad entre tablas (huérfanos)
-- -----------------------------------------------------------------------------
SELECT 'pedido sin cliente' AS chequeo, COUNT(*) AS filas
FROM pedidos p LEFT JOIN clientes c ON c.id_cliente = p.ped_cliente
WHERE p.ped_numfac IS NOT NULL AND p.ped_numfac <> '' AND c.id_cliente IS NULL
UNION ALL
SELECT 'caja sin cliente (por id_cliente)', COUNT(*)
FROM pedidos p LEFT JOIN clientes c ON c.id_cliente = p.ped_cliente
WHERE (p.ped_numfac IS NULL OR p.ped_numfac = '') AND p.ped_cliente IS NOT NULL AND c.id_cliente IS NULL
UNION ALL
SELECT 'pedido sin detalle', COUNT(*)
FROM pedidos p LEFT JOIN detalle_pedidos d ON d.id_pedido = p.id_pedido
WHERE p.ped_numfac IS NOT NULL AND p.ped_numfac <> '' AND d.id_detalle IS NULL
UNION ALL
SELECT 'detalle sin pedido', COUNT(*)
FROM detalle_pedidos d LEFT JOIN pedidos p ON p.id_pedido = d.id_pedido
WHERE p.id_pedido IS NULL
UNION ALL
SELECT 'detalle con id de producto inexistente', COUNT(*)
FROM detalle_pedidos d LEFT JOIN productos pr ON pr.id_producto = d.id_producto
WHERE pr.id_producto IS NULL
UNION ALL
SELECT 'detalle cuyo id existe en productos y en promociones', COUNT(*)
FROM detalle_pedidos d
JOIN productos pr ON pr.id_producto = d.id_producto
JOIN promociones pm ON pm.id_promocion = d.id_producto
UNION ALL
SELECT 'detalle con nombre distinto al del producto', COUNT(*)
FROM detalle_pedidos d JOIN productos pr ON pr.id_producto = d.id_producto
WHERE TRIM(d.nombre_producto) <> TRIM(pr.descripcion_producto)
UNION ALL
SELECT 'detalle cuyo nombre coincide con una promoción', COUNT(*)
FROM detalle_pedidos d
WHERE EXISTS (SELECT 1 FROM promociones pm WHERE TRIM(pm.titulo) = TRIM(d.nombre_producto))
UNION ALL
SELECT 'cliente sin bar', COUNT(*) FROM clientes WHERE cli_Bar IS NULL OR cli_Bar = 0
UNION ALL
SELECT 'cliente con bar inexistente', COUNT(*)
FROM clientes c LEFT JOIN bares b ON b.id_bar = c.cli_Bar
WHERE c.cli_Bar IS NOT NULL AND c.cli_Bar <> 0 AND b.id_bar IS NULL
UNION ALL
SELECT 'pedido oculto en el admin (cliente sin bar válido)', COUNT(*)
FROM pedidos p
LEFT JOIN clientes c ON c.id_cliente = p.ped_cliente
LEFT JOIN bares b ON b.id_bar = c.cli_Bar
WHERE p.ped_numfac IS NOT NULL AND p.ped_numfac <> '' AND b.id_bar IS NULL
UNION ALL
SELECT 'promoción sin producto por código', COUNT(*)
FROM promociones pm LEFT JOIN productos pr ON pr.codigo_productos = pm.codigo
WHERE pr.id_producto IS NULL
UNION ALL
SELECT 'producto con categoría inexistente', COUNT(*)
FROM productos pr LEFT JOIN categorias c ON c.id_categoria = pr.id_cate_producto
WHERE c.id_categoria IS NULL;

-- -----------------------------------------------------------------------------
-- E. Totales y precios
-- -----------------------------------------------------------------------------

-- E1. Línea cuyo subtotal no es cantidad por precio
SELECT COUNT(*) AS lineas,
       SUM(ABS(subtotal - cantidad * precio_unitario) > 0.01) AS subtotal_inconsistente,
       SUM(precio_unitario = 0) AS precio_cero,
       SUM(precio_unitario < 0 OR cantidad <= 0) AS valores_invalidos,
       SUM(cantidad <> FLOOR(cantidad)) AS cantidad_fraccionaria
FROM detalle_pedidos;

-- E2. Tipos de línea
SELECT tipo_producto, COUNT(*) AS lineas FROM detalle_pedidos GROUP BY tipo_producto;

-- E3. Pedido cuyo total no coincide con la suma de sus líneas
SELECT COUNT(*) AS pedidos_con_diferencia, SUM(ABS(p.ped_total - s.suma)) AS diferencia_total
FROM pedidos p
JOIN (SELECT id_pedido, SUM(subtotal) AS suma FROM detalle_pedidos GROUP BY id_pedido) s
  ON s.id_pedido = p.id_pedido
WHERE ABS(p.ped_total - s.suma) > 0.01;

-- E4. Precio de la línea comparado con el precio actual del producto
SELECT d.tipo_producto,
       SUM(d.precio_unitario = CASE d.tipo_producto WHEN 'paca' THEN pr.precio_paca_producto
                                                    ELSE pr.precio_unidad_producto END) AS igual_al_actual,
       SUM(d.precio_unitario <> CASE d.tipo_producto WHEN 'paca' THEN pr.precio_paca_producto
                                                     ELSE pr.precio_unidad_producto END) AS distinto_al_actual
FROM detalle_pedidos d JOIN productos pr ON pr.id_producto = d.id_producto
GROUP BY d.tipo_producto;

-- E5. Productos: códigos y precios
SELECT COUNT(*) AS productos,
       COUNT(DISTINCT codigo_productos) AS codigos_distintos,
       SUM(codigo_productos IS NULL OR codigo_productos = '') AS sin_codigo,
       SUM(precio_unidad_producto IS NULL OR precio_unidad_producto <= 0) AS sin_precio_unidad,
       SUM(precio_paca_producto IS NULL OR precio_paca_producto <= 0) AS sin_precio_paca,
       SUM(cantidad_paca_producto IS NULL OR cantidad_paca_producto <= 0) AS sin_unidades_paca,
       SUM(acti_Unidad = 1) AS venta_por_unidad,
       SUM(estado_producto = 1) AS activos
FROM productos;

-- E6. Grupos de código de producto repetido
SELECT COUNT(*) AS codigos_repetidos, SUM(n) AS productos_afectados
FROM (SELECT codigo_productos, COUNT(*) n FROM productos
      WHERE codigo_productos IS NOT NULL AND codigo_productos <> ''
      GROUP BY codigo_productos HAVING COUNT(*) > 1) x;

-- E7. Promociones
SELECT COUNT(*) AS promociones, SUM(estado = 1) AS activas,
       COUNT(DISTINCT codigo) AS codigos_distintos,
       SUM(precio_unidad_producto IS NULL OR precio_unidad_producto <= 0) AS sin_precio_unidad,
       SUM(precio_paca_producto IS NULL OR precio_paca_producto <= 0) AS sin_precio_paca,
       MIN(id_promocion) AS id_min, MAX(id_promocion) AS id_max
FROM promociones;

-- E8. Rango de id de productos y promociones (para detectar solapamiento)
SELECT 'productos' AS tabla, MIN(id_producto) AS id_min, MAX(id_producto) AS id_max, COUNT(*) AS filas FROM productos
UNION ALL SELECT 'clientes', MIN(id_cliente), MAX(id_cliente), COUNT(*) FROM clientes
UNION ALL SELECT 'bares', MIN(id_bar), MAX(id_bar), COUNT(*) FROM bares
UNION ALL SELECT 'pedidos', MIN(id_pedido), MAX(id_pedido), COUNT(*) FROM pedidos
UNION ALL SELECT 'detalle_pedidos', MIN(id_detalle), MAX(id_detalle), COUNT(*) FROM detalle_pedidos
UNION ALL SELECT 'categorias', MIN(id_categoria), MAX(id_categoria), COUNT(*) FROM categorias
UNION ALL SELECT 'users', MIN(id), MAX(id), COUNT(*) FROM users;

-- -----------------------------------------------------------------------------
-- F. Clientes y usuarios (solo conteos)
-- -----------------------------------------------------------------------------

-- F1. Teléfonos de clientes: formatos y duplicados tras normalizar
SELECT COUNT(*) AS clientes,
       SUM(cli_telefono IS NULL OR cli_telefono = '') AS sin_telefono,
       SUM(cli_telefono REGEXP '^57[0-9]{10}$') AS con_57,
       SUM(cli_telefono REGEXP '^3[0-9]{9}$') AS sin_57,
       SUM(cli_telefono REGEXP '[^0-9]') AS con_simbolos,
       SUM(cli_identificacion IS NULL OR cli_identificacion = '') AS sin_identificacion
FROM clientes;

SELECT COUNT(*) AS telefonos_repetidos, SUM(n) AS clientes_afectados
FROM (SELECT RIGHT(REGEXP_REPLACE(cli_telefono, '[^0-9]', ''), 10) AS tel, COUNT(*) n
      FROM clientes WHERE cli_telefono IS NOT NULL AND cli_telefono <> ''
      GROUP BY tel HAVING COUNT(*) > 1) x;

SELECT COUNT(*) AS identificaciones_repetidas
FROM (SELECT cli_identificacion FROM clientes
      WHERE cli_identificacion IS NOT NULL AND cli_identificacion <> ''
      GROUP BY cli_identificacion HAVING COUNT(*) > 1) x;

-- F2. Relación clientes por bar
SELECT clientes_por_bar, COUNT(*) AS bares
FROM (SELECT b.id_bar, COUNT(c.id_cliente) AS clientes_por_bar
      FROM bares b LEFT JOIN clientes c ON c.cli_Bar = b.id_bar GROUP BY b.id_bar) x
GROUP BY clientes_por_bar ORDER BY clientes_por_bar;

SELECT COUNT(*) AS nombres_de_bar_repetidos
FROM (SELECT LOWER(TRIM(nombre_bar)) FROM bares GROUP BY LOWER(TRIM(nombre_bar)) HAVING COUNT(*) > 1) x;

SELECT cli_zona, COUNT(*) AS clientes FROM clientes GROUP BY cli_zona;
SELECT id_empresa, COUNT(*) AS clientes FROM clientes GROUP BY id_empresa;

-- F3. Usuarios: roles y formato de la clave (nunca el valor)
SELECT rol, id_rol, id_empresa, COUNT(*) AS usuarios FROM users GROUP BY rol, id_rol, id_empresa;

SELECT CASE
         WHEN password REGEXP '^\\$2[aby]\\$[0-9]{2}\\$' THEN 'bcrypt'
         WHEN password REGEXP '^\\$argon2' THEN 'argon2'
         WHEN password REGEXP '^[0-9a-f]{32}$' THEN 'posible md5'
         WHEN password REGEXP '^[0-9a-f]{40}$' THEN 'posible sha1'
         WHEN password IS NULL OR password = '' THEN 'vacía'
         ELSE 'texto plano u otro'
       END AS formato, COUNT(*) AS usuarios
FROM users GROUP BY formato;

SELECT COUNT(*) - COUNT(DISTINCT usuario) AS usuarios_repetidos,
       SUM(email IS NULL OR email = '') AS sin_email,
       COUNT(email) - COUNT(DISTINCT LOWER(TRIM(email))) AS emails_repetidos,
       SUM(ops_api_token IS NOT NULL) AS con_token_ops
FROM users;

-- -----------------------------------------------------------------------------
-- G. Imágenes, prototipo y tablas auxiliares
-- -----------------------------------------------------------------------------

-- G1. Rutas de imagen
SELECT 'productos' AS tabla, COUNT(*) AS filas,
       SUM(imagen_producto IS NULL OR imagen_producto = '') AS sin_imagen,
       SUM(imagen_producto LIKE '%/%' OR imagen_producto LIKE '%\\\\%') AS con_carpeta,
       SUM(imagen_producto LIKE 'http%') AS url_absoluta
FROM productos
UNION ALL
SELECT 'promociones', COUNT(*), SUM(imagen IS NULL OR imagen = ''),
       SUM(imagen LIKE '%/%' OR imagen LIKE '%\\\\%'), SUM(imagen LIKE 'http%')
FROM promociones
UNION ALL
SELECT 'categorias', COUNT(*), SUM(imagen_categoria IS NULL OR imagen_categoria = ''),
       SUM(imagen_categoria LIKE '%/%' OR imagen_categoria LIKE '%\\\\%'), SUM(imagen_categoria LIKE 'http%')
FROM categorias;

SELECT LOWER(SUBSTRING_INDEX(imagen_producto, '.', -1)) AS extension, COUNT(*) AS archivos
FROM productos WHERE imagen_producto IS NOT NULL AND imagen_producto <> ''
GROUP BY extension;

SELECT COUNT(*) - COUNT(DISTINCT imagen_producto) AS imagenes_compartidas
FROM productos WHERE imagen_producto IS NOT NULL AND imagen_producto <> '';

-- G2. Tablas del prototipo
SELECT 'jornadas' AS tabla, COUNT(*) AS filas, MIN(iniciada_en) AS desde, MAX(iniciada_en) AS hasta FROM jornadas
UNION ALL SELECT 'visitas', COUNT(*), MIN(visitada_en), MAX(visitada_en) FROM visitas
UNION ALL SELECT 'jornada_ubicaciones', COUNT(*), MIN(registrado_en), MAX(registrado_en) FROM jornada_ubicaciones;

SELECT id_empresa, COUNT(*) AS jornadas FROM jornadas GROUP BY id_empresa;
SELECT resultado, COUNT(*) AS visitas, SUM(id_pedido IS NOT NULL) AS con_pedido FROM visitas GROUP BY resultado;

SELECT COUNT(*) AS visitas_con_usuario_inexistente
FROM visitas v LEFT JOIN users u ON u.id = v.id_paseador WHERE u.id IS NULL;

-- Solo si A7 confirma que existen (005 no se habría ejecutado)
-- SELECT estado, COUNT(*) FROM entregas GROUP BY estado;
-- SELECT COUNT(*) FROM entrega_eventos;

-- G3. Auxiliares con datos personales: solo conteo y rango
SELECT 'registro' AS tabla, COUNT(*) AS filas, MIN(fecha_hora) AS desde, MAX(fecha_hora) AS hasta FROM registro;
SELECT estado_anterior, estado_nuevo, COUNT(*) AS cambios FROM historial_pedidos GROUP BY estado_anterior, estado_nuevo;

-- G4. Codificación: textos con caracteres mal convertidos (solo conteo)
SELECT 'clientes.cli_nombre' AS campo, SUM(cli_nombre REGEXP 'Ã|Â|�') AS sospechosos FROM clientes
UNION ALL SELECT 'productos.descripcion_producto', SUM(descripcion_producto REGEXP 'Ã|Â|�') FROM productos
UNION ALL SELECT 'bares.nombre_bar', SUM(nombre_bar REGEXP 'Ã|Â|�') FROM bares
UNION ALL SELECT 'detalle_pedidos.nombre_producto', SUM(nombre_producto REGEXP 'Ã|Â|�') FROM detalle_pedidos;

ROLLBACK;
