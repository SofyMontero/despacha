<?php
/**
 * API ops — entregas y domiciliario.
 * GET  action=disponibles | mias | listar&estado= | detalle&idEntrega= | domiciliarios
 * POST action=tomar | asignar | estado
 */
require_once __DIR__ . '/bootstrap.php';

$user = ops_require_auth();
$body = ops_read_json_body();
$action = $_GET['action'] ?? ($body['action'] ?? '');

try {
    $repo = new EntregaRepository();
    $idUser = (int)$user['id'];
    $idEmpresa = isset($user['id_empresa']) && $user['id_empresa'] !== null
        ? (int)$user['id_empresa']
        : 1;
    $rolNombre = strtolower((string)($user['_rol']['nombre'] ?? ''));
    $esAdmin = in_array($rolNombre, ['administrador', 'supervisor'], true);
    $esDomiciliario = $rolNombre === 'domiciliario';
    $method = $_SERVER['REQUEST_METHOD'];

    if (!$repo->esquemaListo()) {
        ops_error('Tablas de entregas no migradas aún', 503, [
            'sql' => 'sistema/sql/005_entregas.sql',
        ]);
    }

    if ($action === 'disponibles' && $method === 'GET') {
        if (!$esAdmin && !$esDomiciliario) {
            ops_error('Rol no autorizado para ver entregas', 403);
        }
        ops_json(['ok' => true, 'data' => array_map('map_disponible', $repo->disponibles($idEmpresa))]);
    }

    if ($action === 'mias' && $method === 'GET') {
        ops_json(['ok' => true, 'data' => array_map('map_entrega', $repo->delDomiciliario($idUser))]);
    }

    if ($action === 'listar' && $method === 'GET') {
        if (!$esAdmin) {
            ops_error('Solo administración puede listar entregas', 403);
        }
        $estado = (string)($_GET['estado'] ?? '');
        if ($estado !== '' && !in_array($estado, EntregaRepository::ESTADOS, true)) {
            ops_error('Estado no válido');
        }
        ops_json(['ok' => true, 'data' => array_map('map_entrega', $repo->listarPorEmpresa($idEmpresa, $estado))]);
    }

    if ($action === 'detalle' && $method === 'GET') {
        $row = $repo->obtener((int)($_GET['idEntrega'] ?? 0));
        if (!$row || (int)$row['id_empresa'] !== $idEmpresa) {
            ops_error('Entrega no encontrada', 404);
        }
        if (!$esAdmin && (int)$row['id_domiciliario'] !== $idUser) {
            ops_error('No autorizado para ver esta entrega', 403);
        }
        $data = map_entrega($row);
        $data['eventos'] = array_map('map_evento', $repo->eventos((int)$row['id_entrega']));
        ops_json(['ok' => true, 'data' => $data]);
    }

    if ($action === 'domiciliarios' && $method === 'GET') {
        if (!$esAdmin) {
            ops_error('Solo administración puede ver domiciliarios', 403);
        }
        $rows = $repo->domiciliarios($idEmpresa);
        ops_json([
            'ok' => true,
            'data' => array_map(fn ($r) => ['id' => (int)$r['id'], 'nombre' => $r['nombre']], $rows),
        ]);
    }

    if ($action === 'tomar' && $method === 'POST') {
        if (!$esDomiciliario) {
            ops_error('Solo un domiciliario puede tomar entregas', 403);
        }
        $idPedido = (int)($body['idPedido'] ?? 0);
        if ($idPedido <= 0) {
            ops_error('idPedido requerido');
        }
        $row = $repo->tomar($idPedido, $idUser, $idEmpresa, (string)($body['numFactura'] ?? ''));
        ops_json(['ok' => true, 'data' => map_entrega($row)]);
    }

    if ($action === 'asignar' && $method === 'POST') {
        if (!$esAdmin) {
            ops_error('Solo administración puede asignar entregas', 403);
        }
        $idPedido = (int)($body['idPedido'] ?? 0);
        $idDomiciliario = (int)($body['idDomiciliario'] ?? 0);
        if ($idPedido <= 0 || $idDomiciliario <= 0) {
            ops_error('idPedido e idDomiciliario requeridos');
        }
        $row = $repo->asignar($idPedido, $idDomiciliario, $idUser, $idEmpresa, (string)($body['numFactura'] ?? ''));
        ops_json(['ok' => true, 'data' => map_entrega($row)]);
    }

    if ($action === 'estado' && $method === 'POST') {
        $idEntrega = (int)($body['idEntrega'] ?? 0);
        $entrega = $idEntrega > 0 ? $repo->obtener($idEntrega) : null;
        if (!$entrega || (int)$entrega['id_empresa'] !== $idEmpresa) {
            ops_error('Entrega no encontrada', 404);
        }
        $row = $repo->cambiarEstado(
            $idEntrega,
            $idUser,
            $esAdmin,
            (string)($body['estado'] ?? ''),
            (string)($body['motivo'] ?? ''),
            (string)($body['numFactura'] ?? ''),
            isset($body['lat']) ? (float)$body['lat'] : null,
            isset($body['lng']) ? (float)$body['lng'] : null
        );
        ops_json(['ok' => true, 'data' => map_entrega($row)]);
    }

    ops_error('Acción no válida', 404);
} catch (Exception $e) {
    $code = stripos($e->getMessage(), 'autoriz') !== false ? 403 : 400;
    ops_error($e->getMessage(), $code);
}

function map_pedido_resumen(array $r): array
{
    return [
        'idPedido' => (int)$r['id_pedido'],
        'numPedido' => $r['ped_numfac'] ?? null,
        'total' => isset($r['ped_total']) ? (float)$r['ped_total'] : null,
        'fechaPedido' => $r['ped_fecha'] ?? null,
        'fechaEntregaAcordada' => $r['fecha_entrega_acordada'] ?? null,
        'clienteNombre' => $r['cli_nombre'] ?? null,
        'direccion' => ($r['ped_sede'] ?? '') !== '' ? $r['ped_sede'] : ($r['cli_direccion'] ?? null),
        'telefono' => $r['ped_numCliente'] ?? null,
        'observacion' => $r['ped_observacion'] ?? null,
    ];
}

function map_disponible(array $r): array
{
    return map_pedido_resumen($r) + [
        'idEntrega' => isset($r['id_entrega']) ? (int)$r['id_entrega'] : null,
        'numFactura' => $r['num_factura'] ?? null,
    ];
}

function map_entrega(array $r): array
{
    return [
        'idEntrega' => (int)$r['id_entrega'],
        'estado' => $r['estado'],
        'numFactura' => $r['num_factura'] ?? null,
        'motivo' => $r['motivo'] ?? null,
        'idDomiciliario' => isset($r['id_domiciliario']) ? (int)$r['id_domiciliario'] : null,
        'domiciliarioNombre' => $r['domiciliario_nombre'] ?? null,
        'asignadoPor' => isset($r['asignado_por']) ? (int)$r['asignado_por'] : null,
        'aceptadaEn' => $r['aceptada_en'] ?? null,
        'salioEn' => $r['salio_en'] ?? null,
        'entregadaEn' => $r['entregada_en'] ?? null,
        'actualizadoEn' => $r['actualizado_en'] ?? null,
        'estadoPedido' => $r['ped_estado'] ?? null,
    ] + map_pedido_resumen($r);
}

function map_evento(array $r): array
{
    return [
        'estadoAnterior' => $r['estado_anterior'] ?? null,
        'estadoNuevo' => $r['estado_nuevo'],
        'usuarioNombre' => $r['usuario_nombre'] ?? null,
        'motivo' => $r['motivo'] ?? null,
        'lat' => isset($r['lat']) ? (float)$r['lat'] : null,
        'lng' => isset($r['lng']) ? (float)$r['lng'] : null,
        'registradoEn' => $r['registrado_en'],
    ];
}
