<?php
require_once __DIR__ . '/bootstrap.php';

ops_require_auth();

try {
    $repo = new RolRepository();
    $rows = $repo->listarActivos();
    ops_json([
        'ok' => true,
        'data' => array_map(static function ($r) {
            return [
                'idRol' => (int)$r['id_rol'],
                'nombre' => $r['rol_nombre'],
                'estado' => isset($r['rol_estado']) ? (int)$r['rol_estado'] : 1,
            ];
        }, $rows),
        'hint' => empty($rows)
            ? 'Tabla roles vacía o no migrada. Revisa sistema/sql/002_variante_b_fase1_empresas_roles.sql'
            : null,
    ]);
} catch (Exception $e) {
    ops_error($e->getMessage(), 500);
}
