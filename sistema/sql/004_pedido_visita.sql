-- =============================================================================
-- Fase 3 — Pedido originado en visita (ADITIVO)
-- NO ejecutar hasta tener Fase 1 + 2 migradas.
-- NO modifica ni elimina columnas existentes.
-- =============================================================================

SET NAMES utf8mb4;

-- Origen del pedido (cliente | admin | paseador | vendedor)
ALTER TABLE pedidos
  ADD COLUMN ped_origen VARCHAR(20) NULL
  COMMENT 'cliente|admin|paseador|vendedor'
  AFTER ped_observacion;

-- Relación opcional con visita del paseador
ALTER TABLE pedidos
  ADD COLUMN id_visita INT UNSIGNED NULL
  COMMENT 'FK visitas.id_visita'
  AFTER ped_origen;

-- Fecha acordada de entrega (no implica despacho inmediato)
ALTER TABLE pedidos
  ADD COLUMN fecha_entrega_acordada DATE NULL
  COMMENT 'Entrega programada acordada con la tienda'
  AFTER id_visita;

-- Scope multiempresa (nullable → backfill a 1)
ALTER TABLE pedidos
  ADD COLUMN id_empresa INT UNSIGNED NULL
  COMMENT 'FK empresas.id_empresa'
  AFTER fecha_entrega_acordada;

UPDATE pedidos SET id_empresa = 1 WHERE id_empresa IS NULL;
UPDATE pedidos SET ped_origen = 'cliente' WHERE ped_origen IS NULL;

-- Índices (omitir si ya existen)
ALTER TABLE pedidos ADD INDEX idx_pedidos_visita (id_visita);
ALTER TABLE pedidos ADD INDEX idx_pedidos_empresa_fecha (id_empresa, ped_fecha);
ALTER TABLE pedidos ADD INDEX idx_pedidos_entrega_acordada (fecha_entrega_acordada);
