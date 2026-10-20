-- =============================================================================
-- Fase 4 — Entregas y domiciliario (ADITIVO)
-- NO ejecutar hasta tener Fase 3 migrada y OK explícito.
-- NO modifica tablas existentes. Solo crea tablas nuevas.
--
-- Entrega ≠ Pedido:
--   - Un pedido aceptado (ped_estado 'aceptado' / 2) queda disponible para entrega.
--   - La fila en `entregas` se crea cuando un domiciliario la toma o el admin la asigna.
--   - num_factura es la factura real; NO es pedidos.ped_numfac.
--   - Cuando la entrega pasa a 'salio', la API pone pedidos.ped_estado = 'enviado'
--     (mismo valor que ya usa el admin PHP). No se usan estados nuevos en pedidos.
-- =============================================================================

SET NAMES utf8mb4;

-- -----------------------------------------------------------------------------
-- Entrega de un pedido (máximo una por pedido)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS entregas (
  id_entrega INT UNSIGNED NOT NULL AUTO_INCREMENT,
  id_empresa INT UNSIGNED NOT NULL DEFAULT 1,
  id_pedido INT NOT NULL COMMENT 'FK pedidos.id_pedido',
  id_domiciliario INT NULL COMMENT 'FK users.id',
  asignado_por INT NULL COMMENT 'FK users.id del admin que asignó; NULL si la tomó el domiciliario',
  num_factura VARCHAR(50) NULL COMMENT 'Factura real; distinta de pedidos.ped_numfac',
  estado VARCHAR(20) NOT NULL DEFAULT 'disponible'
    COMMENT 'disponible | aceptado | salio | en_camino | entregado | no_entregado | cancelado',
  motivo VARCHAR(255) NULL COMMENT 'Obligatorio en no_entregado y cancelado',
  aceptada_en DATETIME NULL,
  salio_en DATETIME NULL,
  entregada_en DATETIME NULL,
  creado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  actualizado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id_entrega),
  UNIQUE KEY uq_entrega_pedido (id_pedido),
  KEY idx_entrega_domiciliario_estado (id_domiciliario, estado),
  KEY idx_entrega_empresa_estado (id_empresa, estado)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- -----------------------------------------------------------------------------
-- Historial de cambios de estado (quién, cuándo, dónde, por qué)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS entrega_eventos (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  id_entrega INT UNSIGNED NOT NULL,
  estado_anterior VARCHAR(20) NULL,
  estado_nuevo VARCHAR(20) NOT NULL,
  id_usuario INT NOT NULL COMMENT 'FK users.id que hizo el cambio',
  motivo VARCHAR(255) NULL,
  lat DECIMAL(10,7) NULL,
  lng DECIMAL(10,7) NULL,
  registrado_en DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_entrega_evento (id_entrega, registrado_en)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
