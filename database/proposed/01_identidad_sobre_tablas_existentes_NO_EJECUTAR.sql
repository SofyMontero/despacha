-- despacha.migration_version: 5D.20261026.01
-- NO EJECUTAR sin respaldo, revisión y aprobación.
--
-- Identidad del personal sobre las tablas que ya existen (users, roles, empresas).
-- No renombra ni borra tablas o columnas, no cambia valores históricos y no copia
-- password, api_token ni ops_api_token. Solo:
--   * agrega columnas nuevas a users y roles;
--   * da valor por defecto a users.direccion, users.rol y roles.rol_estado, que la API no escribe;
--   * crea las tablas nuevas de acceso (memberships, role_permissions, tenant_modules,
--     refresh_tokens, user_claims, user_logins, user_tokens, platform_audit_events,
--     tenant_databases) y esquema_versiones;
--   * llena las columnas nuevas (normalizados y sellos), asigna los roles a la única empresa,
--     marca las plantillas, activa Catalog y Customers y crea la membresía solo para id_rol 11.
--
-- Pensado para MariaDB 11.8 (Hostinger). Se ejecuta completo desde phpMyAdmin o el cliente
-- mysql, con la base ya seleccionada. Es idempotente: repetirlo no duplica nada.
-- MariaDB confirma cada ALTER/CREATE al ejecutarlo; ROLLBACK no los deshace.
-- Recuperación: restaurar el respaldo, o el bloque comentado del final.
--
-- Antes de ejecutarlo en otra base (producción), cambiar v_base_autorizada en el paso 1.

SELECT '5D.20261026.01' AS version_migracion, DATABASE() AS base, VERSION() AS version;

DELIMITER $$

-- 1. Validaciones. Cualquier fallo detiene el script antes de cambiar nada.
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
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('users', 'roles', 'empresas') AND ENGINE = 'InnoDB';
  IF v_count <> 3 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: faltan users, roles o empresas, o no son InnoDB.';
  END IF;

  SELECT COUNT(*) INTO v_count FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND (
       (TABLE_NAME = 'users' AND COLUMN_NAME = 'id' AND COLUMN_TYPE = 'int(11)' AND EXTRA LIKE '%auto_increment%')
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'usuario' AND DATA_TYPE = 'varchar' AND CHARACTER_MAXIMUM_LENGTH = 50)
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'password' AND DATA_TYPE = 'varchar')
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'email' AND CHARACTER_MAXIMUM_LENGTH = 100)
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'telefono' AND CHARACTER_MAXIMUM_LENGTH = 15)
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'nombre' AND CHARACTER_MAXIMUM_LENGTH = 50)
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'direccion')
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'rol')
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'id_rol')
    OR (TABLE_NAME = 'users' AND COLUMN_NAME = 'id_empresa')
    OR (TABLE_NAME = 'roles' AND COLUMN_NAME = 'id_rol' AND COLUMN_TYPE = 'int(11)' AND EXTRA LIKE '%auto_increment%')
    OR (TABLE_NAME = 'roles' AND COLUMN_NAME = 'rol_nombre' AND CHARACTER_MAXIMUM_LENGTH = 20)
    OR (TABLE_NAME = 'roles' AND COLUMN_NAME = 'rol_estado')
    OR (TABLE_NAME = 'empresas' AND COLUMN_NAME = 'id_empresa' AND COLUMN_TYPE = 'int(10) unsigned' AND EXTRA LIKE '%auto_increment%')
    OR (TABLE_NAME = 'empresas' AND COLUMN_NAME = 'slug' AND CHARACTER_MAXIMUM_LENGTH = 60)
    OR (TABLE_NAME = 'empresas' AND COLUMN_NAME = 'nombre' AND CHARACTER_MAXIMUM_LENGTH = 120)
    OR (TABLE_NAME = 'empresas' AND COLUMN_NAME = 'activa'));
  IF v_count <> 17 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: la estructura de users, roles o empresas no es la del volcado revisado.';
  END IF;

  SELECT COUNT(*) INTO v_count FROM empresas;
  IF v_count <> 1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: se esperaba una sola empresa; con varias hay que asignar los roles a mano.';
  END IF;

  SELECT COUNT(*) INTO v_count FROM (SELECT UPPER(usuario) FROM users GROUP BY UPPER(usuario) HAVING COUNT(*) > 1) d;
  IF v_count > 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: hay usuarios que solo difieren en mayúsculas.';
  END IF;

  SELECT COUNT(*) INTO v_count FROM (SELECT rol_nombre FROM roles GROUP BY rol_nombre HAVING COUNT(*) > 1) d;
  IF v_count > 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: hay nombres de rol repetidos.';
  END IF;

  SELECT COUNT(*) INTO v_count FROM roles WHERE rol_nombre = 'Administrador' AND id_rol = 11;
  IF v_count <> 1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DETENER: el rol 11 no es Administrador.';
  END IF;
END $$

DELIMITER ;

-- 2. Columnas nuevas. Los valores existentes no cambian.
ALTER TABLE users
  ADD COLUMN IF NOT EXISTS usuario_normalizado varchar(50) NULL,
  ADD COLUMN IF NOT EXISTS email_normalizado varchar(100) NULL,
  ADD COLUMN IF NOT EXISTS email_confirmado tinyint(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS password_hash longtext NULL,
  ADD COLUMN IF NOT EXISTS sello_seguridad longtext NULL,
  ADD COLUMN IF NOT EXISTS sello_concurrencia longtext NULL,
  ADD COLUMN IF NOT EXISTS telefono_confirmado tinyint(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS doble_factor tinyint(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS bloqueo_hasta datetime(6) NULL,
  ADD COLUMN IF NOT EXISTS bloqueo_habilitado tinyint(1) NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS intentos_fallidos int NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS activo tinyint(1) NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS requiere_cambio_clave tinyint(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS creado_en datetime(6) NULL,
  ALTER COLUMN direccion SET DEFAULT '',
  ALTER COLUMN rol SET DEFAULT 0;

ALTER TABLE roles
  ADD COLUMN IF NOT EXISTS id_empresa int(10) unsigned NULL,
  ADD COLUMN IF NOT EXISTS es_plantilla tinyint(1) NOT NULL DEFAULT 0,
  ALTER COLUMN rol_estado SET DEFAULT 1;

-- 3. Tablas nuevas. Las llaves usan el mismo tipo que la columna referida:
--    users.id int, roles.id_rol int, empresas.id_empresa int unsigned.
CREATE TABLE IF NOT EXISTS esquema_versiones (
  version varchar(30) NOT NULL,
  descripcion varchar(200) NOT NULL,
  aplicado_en datetime(6) NOT NULL,
  PRIMARY KEY (version)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS tenant_databases (
  Id bigint NOT NULL AUTO_INCREMENT,
  TenantId int(10) unsigned NOT NULL,
  Server varchar(255) NOT NULL,
  Port int NOT NULL,
  DatabaseName varchar(64) NOT NULL,
  SecretName varchar(100) NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY IX_tenant_databases_TenantId (TenantId),
  CONSTRAINT FK_tenant_databases_empresas FOREIGN KEY (TenantId) REFERENCES empresas (id_empresa) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS tenant_modules (
  TenantId int(10) unsigned NOT NULL,
  Module varchar(50) NOT NULL,
  Enabled tinyint(1) NOT NULL,
  PRIMARY KEY (TenantId, Module),
  CONSTRAINT FK_tenant_modules_empresas FOREIGN KEY (TenantId) REFERENCES empresas (id_empresa)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS role_permissions (
  RoleId int(11) NOT NULL,
  Permission varchar(100) NOT NULL,
  PRIMARY KEY (RoleId, Permission),
  CONSTRAINT FK_role_permissions_roles FOREIGN KEY (RoleId) REFERENCES roles (id_rol) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS memberships (
  Id bigint NOT NULL AUTO_INCREMENT,
  UserId int(11) NOT NULL,
  TenantId int(10) unsigned NOT NULL,
  RoleId int(11) NOT NULL,
  Status varchar(20) NOT NULL,
  CreatedAtUtc datetime(6) NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY IX_memberships_UserId_TenantId (UserId, TenantId),
  KEY IX_memberships_TenantId (TenantId),
  KEY IX_memberships_RoleId (RoleId),
  CONSTRAINT FK_memberships_users FOREIGN KEY (UserId) REFERENCES users (id),
  CONSTRAINT FK_memberships_empresas FOREIGN KEY (TenantId) REFERENCES empresas (id_empresa),
  CONSTRAINT FK_memberships_roles FOREIGN KEY (RoleId) REFERENCES roles (id_rol)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS refresh_tokens (
  Id bigint NOT NULL AUTO_INCREMENT,
  UserId int(11) NOT NULL,
  TenantId int(10) unsigned NULL,
  TokenHash char(64) NOT NULL,
  FamilyId char(36) NOT NULL,
  CreatedAtUtc datetime(6) NOT NULL,
  ExpiresAtUtc datetime(6) NOT NULL,
  RevokedAtUtc datetime(6) NULL,
  RevokedReason varchar(30) NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY IX_refresh_tokens_TokenHash (TokenHash),
  KEY IX_refresh_tokens_FamilyId (FamilyId),
  KEY IX_refresh_tokens_UserId (UserId),
  CONSTRAINT FK_refresh_tokens_users FOREIGN KEY (UserId) REFERENCES users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS user_claims (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int(11) NOT NULL,
  ClaimType longtext NULL,
  ClaimValue longtext NULL,
  PRIMARY KEY (Id),
  KEY IX_user_claims_UserId (UserId),
  CONSTRAINT FK_user_claims_users FOREIGN KEY (UserId) REFERENCES users (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS user_logins (
  LoginProvider varchar(128) NOT NULL,
  ProviderKey varchar(128) NOT NULL,
  ProviderDisplayName longtext NULL,
  UserId int(11) NOT NULL,
  PRIMARY KEY (LoginProvider, ProviderKey),
  KEY IX_user_logins_UserId (UserId),
  CONSTRAINT FK_user_logins_users FOREIGN KEY (UserId) REFERENCES users (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS user_tokens (
  UserId int(11) NOT NULL,
  LoginProvider varchar(128) NOT NULL,
  Name varchar(128) NOT NULL,
  Value longtext NULL,
  PRIMARY KEY (UserId, LoginProvider, Name),
  CONSTRAINT FK_user_tokens_users FOREIGN KEY (UserId) REFERENCES users (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS platform_audit_events (
  Id bigint NOT NULL AUTO_INCREMENT,
  OccurredAtUtc datetime(6) NOT NULL,
  Action varchar(50) NOT NULL,
  UserId bigint NULL,
  TenantId bigint NULL,
  Detail varchar(200) NULL,
  PRIMARY KEY (Id),
  KEY IX_platform_audit_events_OccurredAtUtc (OccurredAtUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- 4. Datos de las columnas nuevas. Solo se llenan las que están vacías.
--    password, api_token, ops_api_token, direccion, rol e id_rol no se tocan.
UPDATE users SET usuario_normalizado = UPPER(usuario) WHERE usuario_normalizado IS NULL;
UPDATE users SET email_normalizado = NULLIF(UPPER(TRIM(email)), '') WHERE email_normalizado IS NULL;
UPDATE users SET sello_seguridad = UPPER(REPLACE(UUID(), '-', '')) WHERE sello_seguridad IS NULL;
UPDATE users SET sello_concurrencia = UUID() WHERE sello_concurrencia IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS UserNameIndex ON users (usuario_normalizado);
CREATE INDEX IF NOT EXISTS EmailIndex ON users (email_normalizado);

-- Todos los roles pasan a la única empresa (validado en el paso 1).
UPDATE roles SET id_empresa = (SELECT MIN(id_empresa) FROM empresas) WHERE id_empresa IS NULL;
UPDATE roles SET es_plantilla = 1 WHERE rol_nombre IN ('Administrador', 'Vendedor', 'Domiciliario') AND es_plantilla = 0;

CREATE UNIQUE INDEX IF NOT EXISTS uq_roles_empresa_nombre ON roles (id_empresa, rol_nombre);

DELIMITER $$

BEGIN NOT ATOMIC
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'roles' AND CONSTRAINT_NAME = 'FK_roles_empresas'
  ) THEN
    ALTER TABLE roles ADD CONSTRAINT FK_roles_empresas FOREIGN KEY (id_empresa) REFERENCES empresas (id_empresa);
  END IF;
END $$

DELIMITER ;

-- Módulos con API lista en esta fase. Los demás quedan apagados hasta su fase.
INSERT IGNORE INTO tenant_modules (TenantId, Module, Enabled)
SELECT e.id_empresa, m.modulo, 1
FROM empresas e
CROSS JOIN (SELECT 'Catalog' AS modulo UNION ALL SELECT 'Customers') m;

-- Permisos de las plantillas (RoleTemplates.Defaults). Administrador no necesita filas:
-- recibe todos los permisos de los módulos activos.
INSERT IGNORE INTO role_permissions (RoleId, Permission)
SELECT r.id_rol, p.permiso
FROM roles r
JOIN (
  SELECT 'Vendedor' AS rol, 'fieldSales.workday.manage' AS permiso
  UNION ALL SELECT 'Vendedor', 'fieldSales.visit.create'
  UNION ALL SELECT 'Vendedor', 'fieldSales.order.create'
  UNION ALL SELECT 'Vendedor', 'customers.read.assigned'
  UNION ALL SELECT 'Vendedor', 'catalog.read'
  UNION ALL SELECT 'Vendedor', 'tracking.report'
  UNION ALL SELECT 'Vendedor', 'receivables.read.assigned'
  UNION ALL SELECT 'Domiciliario', 'deliveries.read.own'
  UNION ALL SELECT 'Domiciliario', 'deliveries.claim'
  UNION ALL SELECT 'Domiciliario', 'deliveries.updateStatus'
  UNION ALL SELECT 'Domiciliario', 'deliveries.evidence'
  UNION ALL SELECT 'Domiciliario', 'tracking.report'
) p ON p.rol = r.rol_nombre
WHERE r.es_plantilla = 1;

-- Membresía solo para el rol verificado: id_rol 11 (Administrador).
-- Los demás usuarios quedan sin acceso hasta que un administrador los agregue.
INSERT IGNORE INTO memberships (UserId, TenantId, RoleId, Status, CreatedAtUtc)
SELECT u.id, r.id_empresa, r.id_rol, 'Active', UTC_TIMESTAMP(6)
FROM users u
JOIN roles r ON r.id_rol = u.id_rol
WHERE u.id_rol = 11
  AND r.rol_nombre = 'Administrador'
  AND (u.id_empresa IS NULL OR u.id_empresa = r.id_empresa);

INSERT IGNORE INTO esquema_versiones (version, descripcion, aplicado_en)
VALUES ('5D.20261026.01', 'Identidad del personal sobre users, roles y empresas', UTC_TIMESTAMP(6));

-- 5. Comprobaciones posteriores (solo lectura). Comparar con lo esperado en TEST:
--    usuarios 6, con_hash_bcrypt 4, sin_bcrypt 2, membresias 1 (usuario 1),
--    roles_sin_empresa 0, plantillas 3, modulos 2.
SELECT COUNT(*) AS usuarios,
       SUM(password LIKE '$2_$%') AS con_hash_bcrypt,
       SUM(password NOT LIKE '$2_$%') AS sin_bcrypt,
       SUM(usuario_normalizado IS NULL) AS sin_normalizar,
       SUM(password_hash IS NOT NULL) AS con_hash_identity
FROM users;

SELECT (SELECT COUNT(*) FROM roles WHERE id_empresa IS NULL) AS roles_sin_empresa,
       (SELECT COUNT(*) FROM roles WHERE es_plantilla = 1) AS plantillas,
       (SELECT COUNT(*) FROM tenant_modules WHERE Enabled = 1) AS modulos,
       (SELECT COUNT(*) FROM memberships) AS membresias,
       (SELECT GROUP_CONCAT(UserId ORDER BY UserId) FROM memberships) AS usuarios_con_membresia;

SELECT version, aplicado_en FROM esquema_versiones ORDER BY aplicado_en;

-- ---------------------------------------------------------------------------
-- Recuperación (NO EJECUTAR salvo decisión explícita; preferir restaurar el respaldo).
-- Quita solo lo que este script agrega. Los datos históricos no se tocaron.
-- ---------------------------------------------------------------------------
-- DROP TABLE IF EXISTS memberships, role_permissions, tenant_modules, refresh_tokens,
--   user_claims, user_logins, user_tokens, platform_audit_events, tenant_databases;
-- DELETE FROM esquema_versiones WHERE version = '5D.20261026.01';
-- ALTER TABLE roles DROP FOREIGN KEY FK_roles_empresas;
-- ALTER TABLE roles DROP INDEX uq_roles_empresa_nombre,
--   DROP COLUMN id_empresa, DROP COLUMN es_plantilla, ALTER COLUMN rol_estado DROP DEFAULT;
-- ALTER TABLE users DROP INDEX UserNameIndex, DROP INDEX EmailIndex,
--   DROP COLUMN usuario_normalizado, DROP COLUMN email_normalizado, DROP COLUMN email_confirmado,
--   DROP COLUMN password_hash, DROP COLUMN sello_seguridad, DROP COLUMN sello_concurrencia,
--   DROP COLUMN telefono_confirmado, DROP COLUMN doble_factor, DROP COLUMN bloqueo_hasta,
--   DROP COLUMN bloqueo_habilitado, DROP COLUMN intentos_fallidos, DROP COLUMN activo,
--   DROP COLUMN requiere_cambio_clave, DROP COLUMN creado_en,
--   ALTER COLUMN direccion DROP DEFAULT, ALTER COLUMN rol DROP DEFAULT;
