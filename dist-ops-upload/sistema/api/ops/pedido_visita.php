<?php
/**
 * Pedido originado en visita de paseador.
 * POST action=crear
 * Body: idVisita, productos[], total, fechaEntregaAcordada, observaciones?
 */
require_once __DIR__ . '/bootstrap.php';

$user = ops_require_auth();
$body = ops_read_json_body();
$action = $_GET['action'] ?? ($body['action'] ?? 'crear');

try {
    $rolNombre = strtolower((string)($user['_rol']['nombre'] ?? ''));
    if (!in_array($rolNombre, ['paseador', 'administrador', 'supervisor'], true)) {
        ops_error('Rol no autorizado', 403);
    }

    if ($action === 'crear' && $_SERVER['REQUEST_METHOD'] === 'POST') {
        $service = new PedidoDesdeVisita();
        $data = $service->crear(
            (int)($body['idVisita'] ?? 0),
            (int)$user['id'],
            is_array($body['productos'] ?? null) ? $body['productos'] : [],
            (float)($body['total'] ?? 0),
            trim((string)($body['fechaEntregaAcordada'] ?? '')),
            trim((string)($body['observaciones'] ?? ''))
        );
        ops_json(['ok' => true, 'data' => $data]);
    }

    ops_error('Acción no válida', 404);
} catch (Exception $e) {
    ops_error($e->getMessage(), 400);
}
