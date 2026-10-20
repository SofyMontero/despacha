<?php
/**
 * API ops — jornadas del paseador.
 * POST action=iniciar | finalizar
 * GET  action=actual | listar&fecha=
 */
require_once __DIR__ . '/bootstrap.php';

$user = ops_require_auth();
$body = ops_read_json_body();
$action = $_GET['action'] ?? ($body['action'] ?? '');

try {
    $repo = new JornadaRepository();
    $idUser = (int)$user['id'];
    $idEmpresa = isset($user['id_empresa']) && $user['id_empresa'] !== null
        ? (int)$user['id_empresa']
        : 1;
    $rolNombre = strtolower((string)($user['_rol']['nombre'] ?? ''));

    if ($action === 'actual' && $_SERVER['REQUEST_METHOD'] === 'GET') {
        if (!$repo->esquemaListo()) {
            ops_json([
                'ok' => true,
                'data' => null,
                'hint' => 'Tabla jornadas no migrada aún',
            ]);
        }
        $row = $repo->actualDelPaseador($idUser);
        ops_json(['ok' => true, 'data' => $row ? map_jornada($row) : null]);
    }

    if ($action === 'listar' && $_SERVER['REQUEST_METHOD'] === 'GET') {
        if (!in_array($rolNombre, ['administrador', 'supervisor'], true)) {
            ops_error('Solo administración puede listar jornadas', 403);
        }
        $fecha = (string)($_GET['fecha'] ?? date('Y-m-d'));
        $rows = $repo->listarPorEmpresa($idEmpresa, $fecha);
        ops_json([
            'ok' => true,
            'data' => array_map('map_jornada', $rows),
        ]);
    }

    if ($action === 'iniciar' && $_SERVER['REQUEST_METHOD'] === 'POST') {
        if (!in_array($rolNombre, ['paseador', 'administrador', 'supervisor'], true)) {
            ops_error('Rol no autorizado para iniciar jornada', 403);
        }
        $lat = isset($body['lat']) ? (float)$body['lat'] : null;
        $lng = isset($body['lng']) ? (float)$body['lng'] : null;
        $row = $repo->iniciar($idUser, $idEmpresa, $lat, $lng);
        ops_json(['ok' => true, 'data' => map_jornada($row)]);
    }

    if ($action === 'finalizar' && $_SERVER['REQUEST_METHOD'] === 'POST') {
        $idJornada = (int)($body['idJornada'] ?? 0);
        if ($idJornada <= 0) {
            $activa = $repo->actualDelPaseador($idUser);
            $idJornada = $activa ? (int)$activa['id_jornada'] : 0;
        }
        if ($idJornada <= 0) {
            ops_error('No hay jornada en curso');
        }
        $lat = isset($body['lat']) ? (float)$body['lat'] : null;
        $lng = isset($body['lng']) ? (float)$body['lng'] : null;
        $row = $repo->finalizar($idJornada, $idUser, $lat, $lng);
        ops_json(['ok' => true, 'data' => map_jornada($row)]);
    }

    ops_error('Acción no válida', 404);
} catch (Exception $e) {
    $code = stripos($e->getMessage(), 'autoriz') !== false ? 403 : 400;
    ops_error($e->getMessage(), $code);
}

function map_jornada(array $r): array
{
    return [
        'idJornada' => (int)$r['id_jornada'],
        'idEmpresa' => (int)($r['id_empresa'] ?? 1),
        'idPaseador' => (int)$r['id_paseador'],
        'paseadorNombre' => $r['paseador_nombre'] ?? null,
        'estado' => $r['estado'] ?? null,
        'iniciadaEn' => $r['iniciada_en'] ?? null,
        'finalizadaEn' => $r['finalizada_en'] ?? null,
        'latInicio' => isset($r['lat_inicio']) ? (float)$r['lat_inicio'] : null,
        'lngInicio' => isset($r['lng_inicio']) ? (float)$r['lng_inicio'] : null,
        'latFin' => isset($r['lat_fin']) ? (float)$r['lat_fin'] : null,
        'lngFin' => isset($r['lng_fin']) ? (float)$r['lng_fin'] : null,
        'notas' => $r['notas'] ?? null,
    ];
}
