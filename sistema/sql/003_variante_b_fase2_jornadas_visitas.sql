-- =============================================================================
-- Variante B · Fase 2 — Jornadas y visitas (ADITIVO)
-- NO ejecutar hasta cerrar Fase 1 (empresas/roles + login ops).
-- NO hace DROP / RENAME / cambios destructivos.
-- =============================================================================

SET NAMES utf8mb4;

-- -----------------------------------------------------------------------------
-- Jornada del paseador (un turno de trabajo)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS jornadas (
  id_jornada INT UNSIGNED NOT NULL AUTO_INCREMENT,
  id_empresa INT UNSIGNED NOT NULL DEFAULT 1,
  id_paseador INT NOT NULL COMMENT 'FK users.id',
  estado VARCHAR(20) NOT NULL DEFAULT 'en_curso'
    COMMENT 'en_curso | finalizada | cancelada',
  iniciada_en DATETIME NOT NULL,
  finalizada_en DATETIME NULL,
  lat_inicio DECIMAL(10,7) NULL,
  lng_inicio DECIMAL(10,7) NULL,
  lat_fin DECIMAL(10,7) NULL,
  lng_fin DECIMAL(10,7) NULL,
  notas VARCHAR(255) NULL,
  creado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  actualizado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id_jornada),
  KEY idx_jornada_paseador (id_paseador, iniciada_en),
  KEY idx_jornada_empresa_estado (id_empresa, estado)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- -----------------------------------------------------------------------------
-- Visita a tienda/cliente (independiente del pedido)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS visitas (
  id_visita INT UNSIGNED NOT NULL AUTO_INCREMENT,
  id_jornada INT UNSIGNED NOT NULL,
  id_empresa INT UNSIGNED NOT NULL DEFAULT 1,
  id_paseador INT NOT NULL,
  id_cliente INT NOT NULL COMMENT 'FK clientes.id_cliente',
  resultado VARCHAR(40) NOT NULL DEFAULT 'sin_pedido'
    COMMENT 'pedido_creado | no_requiere_mercancia | no_atendido | otro',
  id_pedido INT NULL COMMENT 'FK pedidos.id_pedido si hubo pedido',
  lat DECIMAL(10,7) NULL,
  lng DECIMAL(10,7) NULL,
  precision_m DECIMAL(8,2) NULL,
  visitada_en DATETIME NOT NULL,
  notas VARCHAR(255) NULL,
  creado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id_visita),
  KEY idx_visita_jornada (id_jornada),
  KEY idx_visita_cliente (id_cliente),
  KEY idx_visita_pedido (id_pedido),
  KEY idx_visita_paseador_fecha (id_paseador, visitada_en)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- -----------------------------------------------------------------------------
-- Recorrido GPS de la jornada (historial; no solo inicio/fin)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS jornada_ubicaciones (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  id_jornada INT UNSIGNED NOT NULL,
  id_paseador INT NOT NULL,
  lat DECIMAL(10,7) NOT NULL,
  lng DECIMAL(10,7) NOT NULL,
  precision_m DECIMAL(8,2) NULL,
  registrado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_jornada_ubicacion_fecha (id_jornada, registrado_en)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
