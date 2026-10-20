<?php
/**
 * Diagnóstico de esquema Fase 1 (solo lectura).
 * No modifica la base de datos.
 *
 * GET /sistema/api/ops/schema.php
 */
require_once __DIR__ . '/bootstrap.php';

try {
    $db = new Database();
    $pdo = $db->connect();

    $checks = [
        'tabla_empresas' => tablaExiste($pdo, 'empresas'),
        'tabla_roles' => tablaExiste($pdo, 'roles'),
        'users_id_empresa' => columnaExiste($pdo, 'users', 'id_empresa'),
        'users_id_rol' => columnaExiste($pdo, 'users', 'id_rol'),
        'users_ops_api_token' => columnaExiste($pdo, 'users', 'ops_api_token'),
        'clientes_id_empresa' => columnaExiste($pdo, 'clientes', 'id_empresa'),
    ];

    $empresaSeed = false;
    if ($checks['tabla_empresas']) {
        $stmt = $pdo->query('SELECT COUNT(*) FROM empresas WHERE id_empresa = 1');
        $empresaSeed = ((int)$stmt->fetchColumn()) > 0;
    }
    $checks['empresa_multilicores_id_1'] = $empresaSeed;

    $rolesEsperados = ['administrador', 'paseador', 'domiciliario'];
    $rolesPresentes = [];
    if ($checks['tabla_roles']) {
        $rows = $pdo->query('SELECT rol_nombre FROM roles')->fetchAll(PDO::FETCH_COLUMN);
        foreach ($rows as $nombre) {
            $rolesPresentes[] = strtolower(trim((string)$nombre));
        }
    }
    foreach ($rolesEsperados as $rol) {
        $checks['rol_' . $rol] = in_array($rol, $rolesPresentes, true);
    }

    $listos = !in_array(false, $checks, true);

    ops_json([
        'ok' => true,
        'fase1Listo' => $listos,
        'checks' => $checks,
        'sql' => 'sistema/sql/002_empresas_roles.sql',
        'nota' => $listos
            ? 'Esquema Fase 1 completo. Puedes probar login ops.'
            : 'Faltan piezas. Ejecuta el SQL de Fase 1 en phpMyAdmin (con backup).',
    ]);
} catch (Exception $e) {
    ops_error('No se pudo inspeccionar el esquema: ' . $e->getMessage(), 500);
}

function tablaExiste(PDO $pdo, string $tabla): bool
{
    $stmt = $pdo->prepare(
        'SELECT 1 FROM information_schema.tables
         WHERE table_schema = DATABASE() AND table_name = ? LIMIT 1'
    );
    $stmt->execute([$tabla]);
    return (bool)$stmt->fetchColumn();
}

function columnaExiste(PDO $pdo, string $tabla, string $columna): bool
{
    $stmt = $pdo->prepare(
        'SELECT 1 FROM information_schema.columns
         WHERE table_schema = DATABASE() AND table_name = ? AND column_name = ? LIMIT 1'
    );
    $stmt->execute([$tabla, $columna]);
    return (bool)$stmt->fetchColumn();
}
