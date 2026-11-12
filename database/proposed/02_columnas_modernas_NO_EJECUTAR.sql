-- despacha.migration_version: 5D.20261024.02
-- NO EJECUTAR sin respaldo, revisión y aprobación.
--
-- Columnas modernas sobre categorias, productos, promociones, bares y clientes.
-- No crea catalog_* ni customers_*. No copia claves. No toca pedidos,
-- detalle_pedidos, entregas, jornadas, visitas, promos, registro ni empresas.
-- No agrega claves foráneas: hay huérfanos (producto 382 → categoría 19;
-- entregas hacia pedidos ausentes).
--
-- Pensado para MariaDB 11.8 (Hostinger). Idempotente: repetirlo no duplica datos.
-- MariaDB confirma cada ALTER al ejecutarlo. ROLLBACK no deshace el DDL.
-- Recuperación: restaurar el respaldo.
--
-- Antes de ejecutarlo en otra base (producción), cambiar v_base_autorizada en el paso 1.
-- Cifras del volcado del 2026-10-22, solo como referencia de la lectura posterior:
--   productos 258 (activos 248), categorias 11, promociones 8 (producto único 7),
--   bares 3, clientes 2.

SELECT '5D.20261024.02' AS version_migracion, DATABASE() AS base, VERSION() AS version;

DELIMITER $$

BEGIN NOT ATOMIC
  DECLARE v_base_autorizada VARCHAR(64) DEFAULT 'u633742531_Multi_test';
  DECLARE v_count INT;

  IF DATABASE() IS NULL OR DATABASE() <> v_base_autorizada THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: la base seleccionada no es la autorizada en v_base_autorizada.';
  END IF;

  IF VERSION() NOT LIKE '%MariaDB%' OR CAST(SUBSTRING_INDEX(VERSION(), '.', 1) AS UNSIGNED) < 11 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: se esperaba MariaDB 11 o superior.';
  END IF;

  SELECT COUNT(*) INTO v_count FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME IN ('categorias', 'productos', 'promociones', 'bares', 'clientes')
    AND ENGINE = 'InnoDB';
  IF v_count <> 5 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: faltan tablas de catálogo o clientes, o no son InnoDB.';
  END IF;
END $$

DELIMITER ;

-- Fechas 0000-00-00: se leen y se anulan antes de cambiar el tipo.
SET @despacha_sql_mode = @@SESSION.sql_mode;
SET SESSION sql_mode = '';

-- ---------------------------------------------------------------------------
-- Paso A. Columnas nuevas. Los valores viejos no se modifican aquí.
-- ---------------------------------------------------------------------------

ALTER TABLE categorias
  ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
  ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS activo tinyint(1) NOT NULL DEFAULT 1;

ALTER TABLE productos
  ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
  ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS activo tinyint(1) NULL DEFAULT NULL;

ALTER TABLE promociones
  ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
  ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS producto_id int NULL DEFAULT NULL;

ALTER TABLE bares
  ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
  ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS activo tinyint(1) NOT NULL DEFAULT 1;

ALTER TABLE clientes
  ADD COLUMN IF NOT EXISTS origen_registro varchar(20) NOT NULL DEFAULT 'historico',
  ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS creado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_en datetime(6) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS actualizado_por bigint NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS version int NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS activo tinyint(1) NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS telefono_original varchar(20) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS identificacion_texto varchar(30) NULL DEFAULT NULL;

-- ---------------------------------------------------------------------------
-- Paso B. Rellenar solo columnas nuevas. estado_producto se conserva.
-- producto_id solo cuando el código tiene exactamente un producto.
-- ---------------------------------------------------------------------------

UPDATE productos
SET activo = CASE WHEN estado_producto = '1' THEN 1 ELSE 0 END
WHERE activo IS NULL;

UPDATE promociones pr
JOIN (
  SELECT codigo_productos, MIN(id_producto) AS id_producto
  FROM productos
  GROUP BY codigo_productos
  HAVING COUNT(*) = 1
) u ON u.codigo_productos = pr.codigo
SET pr.producto_id = u.id_producto
WHERE pr.producto_id IS NULL;

UPDATE clientes
SET telefono_original = cli_telefono
WHERE telefono_original IS NULL;

UPDATE clientes
SET identificacion_texto = CAST(cli_identificacion AS CHAR)
WHERE identificacion_texto IS NULL
  AND cli_identificacion <> 0;

UPDATE bares
SET creado_en = fecha_crear_bar
WHERE creado_en IS NULL
  AND CAST(fecha_crear_bar AS CHAR(19)) NOT LIKE '0000-00-00%';

UPDATE clientes
SET cli_fecha_registro = NULL
WHERE CAST(cli_fecha_registro AS CHAR(19)) LIKE '0000-00-00%';

SET SESSION sql_mode = @despacha_sql_mode;

-- ---------------------------------------------------------------------------
-- Paso C y D. Tipos que exige la API. No encoge descripcion_producto.
-- El código sigue siendo int. Los precios caben en decimal(12,2).
-- ---------------------------------------------------------------------------

ALTER TABLE categorias
  MODIFY nombre_categoria varchar(100) NOT NULL,
  MODIFY imagen_categoria varchar(300) NOT NULL;

ALTER TABLE productos
  MODIFY imagen_producto varchar(300) NOT NULL,
  MODIFY activo tinyint(1) NOT NULL DEFAULT 0,
  MODIFY precio_unidad_producto decimal(12,2) NOT NULL,
  MODIFY precio_paca_producto decimal(12,2) NOT NULL;

ALTER TABLE promociones
  MODIFY titulo varchar(150) NOT NULL,
  MODIFY imagen varchar(300) NULL,
  MODIFY precio_unidad_producto decimal(12,2) NOT NULL,
  MODIFY precio_paca_producto decimal(12,2) NOT NULL;

ALTER TABLE bares
  MODIFY nombre_bar varchar(150) NOT NULL,
  MODIFY direccion_bar varchar(255) NULL;

ALTER TABLE clientes
  MODIFY cli_nombre varchar(150) NOT NULL,
  MODIFY cli_direccion varchar(255) NOT NULL,
  MODIFY cli_zona varchar(60) NOT NULL,
  MODIFY cli_telefono varchar(20) NOT NULL,
  MODIFY cli_Bar int NULL,
  MODIFY cli_fecha_registro datetime(6) NULL DEFAULT NULL;

CREATE INDEX IF NOT EXISTS idx_productos_codigo ON productos (codigo_productos);
CREATE INDEX IF NOT EXISTS idx_productos_categoria ON productos (id_cate_producto);
CREATE INDEX IF NOT EXISTS idx_promociones_codigo ON promociones (codigo);
CREATE INDEX IF NOT EXISTS idx_promociones_producto ON promociones (producto_id);
CREATE INDEX IF NOT EXISTS idx_clientes_bar ON clientes (cli_Bar);

CREATE TABLE IF NOT EXISTS esquema_versiones (
  version varchar(30) NOT NULL,
  descripcion varchar(200) NOT NULL,
  aplicado_en datetime(6) NOT NULL,
  PRIMARY KEY (version)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT IGNORE INTO esquema_versiones (version, descripcion, aplicado_en)
VALUES ('5D.20261024.02', 'Columnas modernas de catálogo y clientes', UTC_TIMESTAMP(6));

SELECT
  (SELECT COUNT(*) FROM productos) AS productos,
  (SELECT COUNT(*) FROM productos WHERE activo = 1) AS productos_activos,
  (SELECT COUNT(*) FROM categorias) AS categorias,
  (SELECT COUNT(*) FROM promociones) AS promociones,
  (SELECT COUNT(*) FROM promociones WHERE producto_id IS NOT NULL) AS promociones_con_producto,
  (SELECT COUNT(*) FROM bares) AS bares,
  (SELECT COUNT(*) FROM clientes) AS clientes,
  (SELECT COUNT(*) FROM clientes WHERE cli_fecha_registro IS NULL) AS clientes_sin_fecha;
