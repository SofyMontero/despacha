-- =============================================================================
-- Variante B · Fase 1 — Empresas + roles (ADITIVO)
-- NO ejecuta DROP / RENAME / cambios destructivos.
-- Si una columna o fila ya existe, omite esa sentencia y continúa.
-- Revisar en phpMyAdmin antes de correr en producción.
-- =============================================================================

SET NAMES utf8mb4;

-- -----------------------------------------------------------------------------
-- 1) Empresas (tenant / modelo de negocio)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS empresas (
  id_empresa INT UNSIGNED NOT NULL AUTO_INCREMENT,
  nombre VARCHAR(120) NOT NULL,
  slug VARCHAR(60) NOT NULL,
  modelo_negocio VARCHAR(40) NOT NULL DEFAULT 'bares'
    COMMENT 'bares | tiendas | mixto',
  activa TINYINT(1) NOT NULL DEFAULT 1,
  creado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id_empresa),
  UNIQUE KEY uq_empresas_slug (slug)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO empresas (id_empresa, nombre, slug, modelo_negocio, activa)
SELECT 1, 'Multilicores', 'multilicores', 'bares', 1
FROM (SELECT 1 AS _) AS tmp
WHERE NOT EXISTS (SELECT 1 FROM empresas WHERE id_empresa = 1);

-- -----------------------------------------------------------------------------
-- 2) Roles — NO recrear si ya existe (estructura observada: id_rol, rol_nombre, rol_estado)
--    Solo crear si la tabla faltara por completo.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS roles (
  id_rol INT NOT NULL AUTO_INCREMENT,
  rol_nombre VARCHAR(20) NOT NULL,
  rol_estado INT(2) NOT NULL DEFAULT 1,
  PRIMARY KEY (id_rol)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO roles (rol_nombre, rol_estado)
SELECT tmp.rol_nombre, tmp.rol_estado
FROM (
  SELECT 'Administrador' AS rol_nombre, 1 AS rol_estado UNION ALL
  SELECT 'Paseador', 1 UNION ALL
  SELECT 'Domiciliario', 1 UNION ALL
  SELECT 'Supervisor', 1 UNION ALL
  SELECT 'Vendedor', 1 UNION ALL
  SELECT 'Cajero', 1
) AS tmp
WHERE NOT EXISTS (
  SELECT 1 FROM roles r WHERE LOWER(TRIM(r.rol_nombre)) = LOWER(TRIM(tmp.rol_nombre))
);

-- -----------------------------------------------------------------------------
-- 3) users — columnas nuevas (omitir si ya existen)
-- -----------------------------------------------------------------------------
-- ALTER TABLE users ADD COLUMN id_empresa INT UNSIGNED NULL;
-- ALTER TABLE users ADD COLUMN id_rol INT NULL;
-- ALTER TABLE users ADD COLUMN ops_api_token VARCHAR(64) NULL;

-- Ejecutar uno a uno si el anterior falla por "duplicate column":

ALTER TABLE users
  ADD COLUMN id_empresa INT UNSIGNED NULL COMMENT 'FK empresas.id_empresa';

ALTER TABLE users
  ADD COLUMN id_rol INT NULL COMMENT 'FK roles.id_rol';

ALTER TABLE users
  ADD COLUMN ops_api_token VARCHAR(64) NULL COMMENT 'Token API ops Variante B';

-- Backfill suave (no masivo destructivo): empresa default
UPDATE users SET id_empresa = 1 WHERE id_empresa IS NULL;

-- Intentar mapear id_rol desde users.rol legado (nombre o id)
UPDATE users u
INNER JOIN roles r ON (
  r.id_rol = u.rol
  OR LOWER(TRIM(r.rol_nombre)) = LOWER(TRIM(u.rol))
)
SET u.id_rol = r.id_rol
WHERE u.id_rol IS NULL AND u.rol IS NOT NULL AND u.rol != '';

-- -----------------------------------------------------------------------------
-- 4) clientes — scope por empresa
-- -----------------------------------------------------------------------------
ALTER TABLE clientes
  ADD COLUMN id_empresa INT UNSIGNED NULL COMMENT 'FK empresas.id_empresa';

UPDATE clientes SET id_empresa = 1 WHERE id_empresa IS NULL;

-- -----------------------------------------------------------------------------
-- 5) Índices útiles (omitir si ya existen)
-- -----------------------------------------------------------------------------
-- ALTER TABLE users ADD INDEX idx_users_empresa (id_empresa);
-- ALTER TABLE users ADD INDEX idx_users_ops_token (ops_api_token);
-- ALTER TABLE clientes ADD INDEX idx_clientes_empresa (id_empresa);
