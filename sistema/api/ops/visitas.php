<?php
/**
 * API ops — visitas.
 * POST action=registrar
 * GET  action=por_jornada&idJornada=
 */
require_once __DIR__ . '/bootstrap.php';

$user = ops_require_auth();
$body = ops_read_json_body();
$action = $_GET['action'] ?? ($body['action'] ?? '');

try {
    $repo = new VisitaRepository();
    $idUser = (int)$user['id'];
    $idEmpresa = isset($user['id_empresa']) && $user['id_empresa'] !== null
        ? (int)$user['id_empresa']
        : 1;
    $rolNombre = strtolower((string)($user['_rol']['nombre'] ?? ''));

    if ($action === 'por_jornada' && $_SERVER['REQUEST_METHOD'] === 'GET') {
        $idJornada = (int)($_GET['idJornada'] ?? 0);
        if ($idJornada <= 0) {
            ops_error('idJornada requerido');
        }
        if (!$repo->esquemaListo()) {
            ops_json(['ok' => true, 'data' => [], 'hint' => 'Tabla visitas no migrada']);
        }

        // Paseador solo ve su jornada; admin/supervisor cualquiera
        if (!in_array($rolNombre, ['administrador', 'supervisor'], true)) {
            $jornada = (new JornadaRepository())->obtener($idJornada);
            if (!$jornada || (int)$jornada['id_paseador'] !== $idUser) {
                ops_error('No autorizado para esta jornada', 403);
            }
        }

        $rows = $repo->listarPorJornada($idJornada);
        ops_json(['ok' => true, 'data' => array_map('map_visita', $rows)]);
    }

    if ($action === 'registrar' && $_SERVER['REQUEST_METHOD'] === 'POST') {
        if (!in_array($rolNombre, ['paseador', 'administrador', 'supervisor'], true)) {
            ops_error('Rol no autorizado para registrar visitas', 403);
        }

        $idJornada = (int)($body['idJornada'] ?? 0);
        if ($idJornada <= 0) {
            $activa = (new JornadaRepository())->actualDelPaseador($idUser);
            $idJornada = $activa ? (int)$activa['id_jornada'] : 0;
        }
        if ($idJornada <= 0) {
            ops_error('Debes tener una jornada en curso');
        }

        $row = $repo->registrar(
            $idJornada,
            $idUser,
            $idEmpresa,
            (int)($body['idCliente'] ?? 0),
            (string)($body['resultado'] ?? 'no_requiere_mercancia'),
            isset($body['idPedido']) ? (int)$body['idPedido'] : null,
            isset($body['lat']) ? (float)$body['lat'] : null,
            isset($body['lng']) ? (float)$body['lng'] : null,
            isset($body['precision']) ? (float)$body['precision'] : null,
            trim((string)($body['notas'] ?? ''))
        );

        ops_json(['ok' => true, 'data' => map_visita($row)]);
    }

    ops_error('Acción no válida', 404);
} catch (Exception $e) {
    $code = stripos($e->getMessage(), 'autoriz') !== false ? 403 : 400;
    ops_error($e->getMessage(), $code);
}

function map_visita(array $r): array
{
    return [
        'idVisita' => (int)$r['id_visita'],
        'idJornada' => (int)$r['id_jornada'],
        'idEmpresa' => (int)($r['id_empresa'] ?? 1),
        'idPaseador' => (int)$r['id_paseador'],
        'idCliente' => (int)$r['id_cliente'],
        'nombreCliente' => $r['cli_nombre'] ?? null,
        'direccionCliente' => $r['cli_direccion'] ?? null,
        'resultado' => $r['resultado'] ?? null,
        'idPedido' => isset($r['id_pedido']) && $r['id_pedido'] !== null ? (int)$r['id_pedido'] : null,
        'lat' => isset($r['lat']) ? (float)$r['lat'] : null,
        'lng' => isset($r['lng']) ? (float)$r['lng'] : null,
        'visitadaEn' => $r['visitada_en'] ?? null,
        'notas' => $r['notas'] ?? null,
    ];
}
