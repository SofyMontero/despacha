<?php
require_once __DIR__ . '/bootstrap.php';

ops_require_auth();

try {
    $repo = new EmpresaRepository();
    $rows = $repo->listarActivas();
    ops_json([
        'ok' => true,
        'data' => array_map(static function ($r) {
            return [
                'idEmpresa' => (int)$r['id_empresa'],
                'nombre' => $r['nombre'],
                'slug' => $r['slug'] ?? null,
                'modeloNegocio' => $r['modelo_negocio'] ?? null,
                'activa' => (int)($r['activa'] ?? 1) === 1,
            ];
        }, $rows),
        'hint' => empty($rows)
            ? 'Tabla empresas vacía o no migrada. Ejecuta sistema/sql/002_empresas_roles.sql'
            : null,
    ]);
} catch (Exception $e) {
    ops_error($e->getMessage(), 500);
}
