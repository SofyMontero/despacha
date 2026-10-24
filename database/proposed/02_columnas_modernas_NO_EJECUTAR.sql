-- despacha.migration_version: 5D.20261024.02
-- NO EJECUTAR.
-- Columnas nuevas sobre las tablas vivas de u633742531_Multi_test.
-- No crea catalog_* ni customers_*. No copia claves. No toca pedidos,
-- detalle_pedidos, entregas, jornadas, visitas, promos, registro ni empresas.
-- Generado el 2026-10-24 a partir del volcado del 2026-10-22.
-- Tal como está, este archivo solo lee. El ALTER y el UPDATE están comentados.
--
-- MariaDB confirma cada ALTER al ejecutarlo. ROLLBACK no lo deshace.
-- Los UPDATE de los pasos B y C sí pueden ir dentro de una transacción,
-- en una ejecución distinta del ALTER.
--
-- Cifras esperadas en el volcado, que la lectura en vivo debe repetir:
--   productos 258, categorias 11, promociones 8, bares 3, clientes 2
--   producto sin categoría: 1 (id 382, categoría 19)
--   promociones de código único: 7; ambigua: 1 (id 92, codigo 181)
--   clientes con fecha cero: 2

SELECT '5D.20261024.02' AS version_migracion,
       'NO_EJECUTAR' AS estado,
       'Descomentar un solo paso tras respaldo, lectura en vivo y aprobación' AS accion;

-- 1. Ambiente. Detenerse si control_base no es OK_TEST.
SELECT DATABASE() AS base,
       VERSION() AS version,
       CASE
         WHEN DATABASE() = 'u633742531_Multi_test' THEN 'OK_TEST'
         ELSE 'DETENER_BASE_INESPERADA'
       END AS control_base;

-- 2. Estructura previa. Estas columnas no existen en el volcado del 2026-10-22.
--    Después del cambio deben aparecer; hoy el resultado tiene que venir vacío.
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (
    (TABLE_NAME = 'categorias' AND COLUMN_NAME IN ('activo', 'version', 'origen_registro'))
    OR (TABLE_NAME = 'productos' AND COLUMN_NAME IN ('activo', 'version'))
    OR (TABLE_NAME = 'promociones' AND COLUMN_NAME = 'producto_id')
    OR (TABLE_NAME = 'bares' AND COLUMN_NAME = 'activo')
    OR (TABLE_NAME = 'clientes' AND COLUMN_NAME IN ('activo', 'telefono_original', 'identificacion_texto'))
  )
ORDER BY TABLE_NAME, COLUMN_NAME;

-- 5. Recuperación: restaurar el volcado anterior al DDL. ROLLBACK no deshace el ALTER.
--    El paso B, si se ejecuta solo y aún no hubo COMMIT, sí puede deshacerse.

-- ---------------------------------------------------------------------------
-- Paso A. Columnas nuevas. Los valores viejos no se modifican.
-- origen_registro queda 'historico' y version queda 0 por el DEFAULT.
-- creado_en nuevo queda NULL. promociones ya tiene su creado_en y no se duplica.
-- ---------------------------------------------------------------------------

-- ALTER TABLE categorias
--   ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
--   ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
--   ADD COLUMN IF NOT EXISTS activo tinyint(1) NOT NULL DEFAULT 1;

-- ALTER TABLE productos
--   ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
--   ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
--   ADD COLUMN IF NOT EXISTS activo tinyint(1) NULL DEFAULT NULL;

-- ALTER TABLE promociones
--   ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
--   ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
--   ADD COLUMN IF NOT EXISTS producto_id int NULL DEFAULT NULL;

-- ALTER TABLE bares
--   ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
--   ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
--   ADD COLUMN IF NOT EXISTS activo tinyint(1) NOT NULL DEFAULT 1;

-- ALTER TABLE clientes
--   ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
--   ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
--   ADD COLUMN IF NOT EXISTS activo tinyint(1) NOT NULL DEFAULT 1,
--   ADD COLUMN IF NOT EXISTS telefono_original varchar(15) NULL DEFAULT NULL,
--   ADD COLUMN IF NOT EXISTS identificacion_texto varchar(30) NULL DEFAULT NULL;

-- ---------------------------------------------------------------------------
-- Paso B. Rellenar solo columnas nuevas, dentro de una transacción.
-- estado_producto = '1' pasa a activo 1. '0' y vacío pasan a 0.
-- estado_producto se conserva.
-- producto_id solo cuando el código tiene exactamente un producto.
-- Un activo junto a un inactivo sigue fuera (codigo 181, promoción 92).
-- telefono_original copia cli_telefono. No lo normaliza.
-- identificacion_texto copia el entero solo cuando es distinto de 0.
-- bares.creado_en copia fecha_crear_bar. Esa columna se conserva.
-- clientes.creado_en no copia la fecha cero.
-- ---------------------------------------------------------------------------

-- START TRANSACTION;
--
-- UPDATE productos
-- SET activo = CASE WHEN estado_producto = '1' THEN 1 ELSE 0 END
-- WHERE activo IS NULL;
--
-- UPDATE promociones pr
-- JOIN (
--   SELECT codigo_productos, MIN(id_producto) AS id_producto
--   FROM productos
--   GROUP BY codigo_productos
--   HAVING COUNT(*) = 1
-- ) u ON u.codigo_productos = pr.codigo
-- SET pr.producto_id = u.id_producto
-- WHERE pr.producto_id IS NULL;
--
-- UPDATE clientes
-- SET telefono_original = cli_telefono
-- WHERE telefono_original IS NULL;
--
-- UPDATE clientes
-- SET identificacion_texto = CAST(cli_identificacion AS CHAR)
-- WHERE identificacion_texto IS NULL
--   AND cli_identificacion <> 0;
--
-- UPDATE bares
-- SET creado_en = fecha_crear_bar
-- WHERE creado_en IS NULL
--   AND CAST(fecha_crear_bar AS CHAR(19)) NOT LIKE '0000-00-00%';
--
-- -- Comprobar antes de COMMIT, con las cifras del volcado:
-- -- productos activos 248, no activos 10
-- -- promociones con producto_id 7, sin producto_id 1
-- -- clientes.creado_en sigue NULL
-- COMMIT;

-- ---------------------------------------------------------------------------
-- Paso C. Aprobación aparte. Las dos fechas de cliente del volcado son cero
-- y no son una fecha de alta. El conector de la aplicación no puede leerlas
-- como datetime. Este paso las deja NULL. No escribe la fecha de la migración.
-- ---------------------------------------------------------------------------

-- ALTER TABLE clientes
--   MODIFY cli_fecha_registro timestamp NULL DEFAULT NULL;
--
-- UPDATE clientes
-- SET cli_fecha_registro = NULL
-- WHERE CAST(cli_fecha_registro AS CHAR(19)) LIKE '0000-00-00%';

-- ---------------------------------------------------------------------------
-- Paso D. Ampliar varchar y permitir bar vacío. No encoge descripcion_producto:
-- sigue en 250 y el máximo observado es 34. No cambia codigo int a varchar.
-- cli_Bar pasa a NULL para los clientes nuevos sin establecimiento. Los valores
-- actuales se conservan. La API no abre la base hasta este paso, el decimal
-- del paso F y la fecha cero anulable del paso C.
-- ---------------------------------------------------------------------------

-- ALTER TABLE categorias
--   MODIFY nombre_categoria varchar(100) NOT NULL,
--   MODIFY imagen_categoria varchar(300) NOT NULL;
--
-- ALTER TABLE productos
--   MODIFY imagen_producto varchar(300) NOT NULL;
--
-- ALTER TABLE promociones
--   MODIFY titulo varchar(150) NOT NULL,
--   MODIFY imagen varchar(300) NULL;
--
-- ALTER TABLE bares
--   MODIFY nombre_bar varchar(150) NOT NULL,
--   MODIFY direccion_bar varchar(255) NULL;
--
-- ALTER TABLE clientes
--   MODIFY cli_nombre varchar(150) NOT NULL,
--   MODIFY cli_direccion varchar(255) NOT NULL,
--   MODIFY cli_zona varchar(60) NOT NULL,
--   MODIFY cli_telefono varchar(20) NOT NULL,
--   MODIFY cli_Bar int NULL;

-- ---------------------------------------------------------------------------
-- Paso E. Índices no únicos. El código repetido impide un índice único.
-- ---------------------------------------------------------------------------

-- CREATE INDEX IF NOT EXISTS idx_productos_codigo ON productos (codigo_productos);
-- CREATE INDEX IF NOT EXISTS idx_productos_categoria ON productos (id_cate_producto);
-- CREATE INDEX IF NOT EXISTS idx_promociones_codigo ON promociones (codigo);
-- CREATE INDEX IF NOT EXISTS idx_promociones_producto ON promociones (producto_id);
-- CREATE INDEX IF NOT EXISTS idx_clientes_bar ON clientes (cli_Bar);

-- ---------------------------------------------------------------------------
-- Paso F. Aprobación aparte. Precios int a decimal(12,2).
-- En el volcado el máximo de unidad es 1199971 y el de paca 7199826:
-- caben sin pérdida. El ALTER reconstruye la tabla. cantidad_venta no se toca.
-- ---------------------------------------------------------------------------

-- ALTER TABLE productos
--   MODIFY precio_unidad_producto decimal(12,2) NOT NULL,
--   MODIFY precio_paca_producto decimal(12,2) NOT NULL;
--
-- ALTER TABLE promociones
--   MODIFY precio_unidad_producto decimal(12,2) NOT NULL,
--   MODIFY precio_paca_producto decimal(12,2) NOT NULL;

-- ---------------------------------------------------------------------------
-- Paso G. Aprobación aparte. FK nuevas, solo con huérfanos en cero,
-- ON DELETE RESTRICT. No se agrega FK de categoría: el producto 382 apunta
-- a la categoría 19, que no existe, y ese id se conserva.
-- No se agrega FK de entregas: las seis apuntan a pedidos ausentes.
-- No se cambia el CASCADE ya existente de pedidos y detalle_pedidos.
-- ---------------------------------------------------------------------------

-- ALTER TABLE clientes
--   ADD CONSTRAINT fk_clientes_bar
--   FOREIGN KEY (cli_Bar) REFERENCES bares (id_bar) ON DELETE RESTRICT;
--
-- ALTER TABLE promociones
--   ADD CONSTRAINT fk_promociones_producto
--   FOREIGN KEY (producto_id) REFERENCES productos (id_producto) ON DELETE RESTRICT;
