-- despacha.migration_version: 5D.20261024.01
-- NO EJECUTAR.
-- Renombre futuro de users y roles en u633742531_Multi_test.
-- Generado el 2026-10-24 a partir del volcado del 2026-10-22.
-- Tal como está, este archivo solo devuelve una fila. El RENAME está comentado.
--
-- MariaDB confirma RENAME TABLE al ejecutarlo. ROLLBACK no lo deshace.
-- El inverso vale únicamente antes de crear las tablas nuevas users y roles.
-- Si esas tablas ya existen, la recuperación es el volcado previo.
--
-- Precondiciones, leídas con 00_solo_lectura_test.sql en la misma base:
--   * DATABASE() = 'u633742531_Multi_test' y el nombre no contiene prod
--   * referencias_fk = 0
--   * no existen usuarios_legacy, roles_legacy, ni otra tabla users de identidad
--   * vistas, triggers, rutinas y eventos revisados
--   * existe el mysqldump de respaldo, fuera del repositorio
--
-- En el volcado de TEST las FK entrantes a users y roles eran cero.
-- Las columnas id_paseador, id_domiciliario e id_usuario guardan el número
-- y no llevan restricción: el renombre no las rompe si el id se conserva
-- en la importación posterior.
--
-- Si referencias_fk es mayor que cero, no se renombra. Se revisa cada
-- restricción y se decide otra vez. PRODUCCIÓN puede tener FK que TEST no tiene.

SELECT '5D.20261024.01' AS version_migracion,
       'NO_EJECUTAR' AS estado,
       'Descomentar el RENAME solo tras respaldo, lectura en vivo y aprobación' AS accion;

-- SELECT COUNT(*) AS referencias_fk
-- FROM information_schema.KEY_COLUMN_USAGE
-- WHERE TABLE_SCHEMA = DATABASE()
--   AND REFERENCED_TABLE_NAME IN ('users', 'roles');

-- SELECT TABLE_NAME
-- FROM information_schema.TABLES
-- WHERE TABLE_SCHEMA = DATABASE()
--   AND TABLE_NAME IN ('usuarios_legacy', 'roles_legacy');

-- RENAME TABLE `users` TO `usuarios_legacy`, `roles` TO `roles_legacy`;

-- Inverso, solo si todavía no existen las tablas nuevas users y roles:
-- RENAME TABLE `usuarios_legacy` TO `users`, `roles_legacy` TO `roles`;
