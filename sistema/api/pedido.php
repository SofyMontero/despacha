<?php
session_start();
require_once __DIR__ . '/bootstrap.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    api_error('Método no permitido', 405);
}

function pedidoTokenUsado(string $token): bool
{
    if ($token === '' || !isset($_SESSION['pedidos_procesados'][$token])) {
        return false;
    }

    $datos = $_SESSION['pedidos_procesados'][$token];
    if (($datos['tipo'] ?? '') === 'processing') {
        $creado = (int)($datos['creado_en'] ?? 0);
        if ($creado > 0 && (time() - $creado) > 90) {
            unset($_SESSION['pedidos_procesados'][$token]);
            return false;
        }
    }

    return true;
}

function marcarPedidoProcesado(string $token, array $datos): void
{
    if (!isset($_SESSION['pedidos_procesados'])) {
        $_SESSION['pedidos_procesados'] = [];
    }
    if (count($_SESSION['pedidos_procesados']) > 30) {
        $_SESSION['pedidos_procesados'] = array_slice($_SESSION['pedidos_procesados'], -20, null, true);
    }
    $_SESSION['pedidos_procesados'][$token] = $datos;
}

function buscarPedidoRecienteDuplicado($numCliente, $totalGeneral, $productos)
{
    try {
        $telefono = api_normalize_phone((string)$numCliente);
        $db = new Database();
        $pdo = $db->connect();
        if (!$pdo) {
            return null;
        }

        $desde = date('Y-m-d H:i:s', strtotime('-5 hours -2 minutes'));
        $sql = "SELECT id_pedido, ped_total, ped_observacion
                FROM pedidos
                WHERE ped_numCliente = ?
                  AND ped_total = ?
                  AND ped_fecha >= ?
                ORDER BY id_pedido DESC
                LIMIT 5";
        $stmt = $pdo->prepare($sql);
        $stmt->execute([$telefono, $totalGeneral, $desde]);
        $candidatos = $stmt->fetchAll(PDO::FETCH_ASSOC);
        if (empty($candidatos)) {
            return null;
        }

        $firmaActual = [];
        foreach ($productos as $p) {
            $firmaActual[] = ($p['id'] ?? '') . '|' . ($p['tipo'] ?? '') . '|' . (int)($p['cantidad'] ?? 0);
        }
        sort($firmaActual);
        $firmaActual = implode(';', $firmaActual);

        foreach ($candidatos as $candidato) {
            $sqlDet = "SELECT id_producto, tipo_producto, cantidad
                       FROM detalle_pedidos
                       WHERE id_pedido = ?";
            $stmtDet = $pdo->prepare($sqlDet);
            $stmtDet->execute([$candidato['id_pedido']]);
            $detalles = $stmtDet->fetchAll(PDO::FETCH_ASSOC);

            $firmaPedido = [];
            foreach ($detalles as $d) {
                $firmaPedido[] = ($d['id_producto'] ?? '') . '|' . ($d['tipo_producto'] ?? '') . '|' . (int)($d['cantidad'] ?? 0);
            }
            sort($firmaPedido);
            $firmaPedido = implode(';', $firmaPedido);

            if ($firmaPedido === $firmaActual) {
                return (int)$candidato['id_pedido'];
            }
        }
    } catch (Exception $e) {
        error_log('API pedido duplicado: ' . $e->getMessage());
    }

    return null;
}

try {
    $body = api_read_json_body();
    $productos = $body['productos'] ?? [];
    $totalGeneral = (float)($body['total'] ?? $body['total_general'] ?? 0);
    $observaciones = (string)($body['observaciones'] ?? '');
    $numCliente = api_normalize_phone((string)($body['telefono'] ?? $body['numCliente'] ?? ''));
    $clienteId = (int)($body['clienteId'] ?? $body['id_cliente'] ?? 0);
    $ped_sede = $body['direccion'] ?? $body['ped_sede'] ?? null;
    $pedidoToken = preg_replace('/[^a-zA-Z0-9]/', '', (string)($body['pedidoToken'] ?? $body['pedido_token'] ?? ''));

    if ($pedidoToken === '') {
        $pedidoToken = bin2hex(random_bytes(16));
    }

    if (empty($productos)) {
        api_error('No se recibieron productos');
    }
    if ($numCliente === '') {
        api_error('Teléfono del cliente requerido');
    }

    foreach ($productos as $index => $producto) {
        if (
            empty($producto['id']) || empty($producto['nombre']) ||
            empty($producto['tipo']) || empty($producto['cantidad'])
        ) {
            api_error("Producto en posición $index tiene datos incompletos");
        }
    }

    if (pedidoTokenUsado($pedidoToken)) {
        $prev = $_SESSION['pedidos_procesados'][$pedidoToken];
        api_json([
            'ok' => ($prev['tipo'] ?? '') === 'success',
            'duplicado' => true,
            'pedidoToken' => $pedidoToken,
            'idPedido' => $prev['idPedido'] ?? null,
            'mensaje' => $prev['mensaje'] ?? 'Pedido ya procesado',
            'total' => $prev['totalGeneral'] ?? $totalGeneral,
        ]);
    }

    marcarPedidoProcesado($pedidoToken, [
        'tipo' => 'processing',
        'mensaje' => 'Procesando pedido...',
        'idPedido' => null,
        'totalGeneral' => $totalGeneral,
        'productos' => $productos,
        'observaciones' => $observaciones,
        'numCliente' => $numCliente,
        'creado_en' => time(),
    ]);

    $datosCliente = [
        'nombre' => 'Cliente App',
        'email' => 'pedido@multilicores.com',
        'id_cliente' => $clienteId > 0 ? $clienteId : null,
    ];

    $pedidoModel = new Pedido();
    $idDuplicado = buscarPedidoRecienteDuplicado($numCliente, $totalGeneral, $productos);

    if ($idDuplicado) {
        $idPedido = $idDuplicado;
        $mensaje = 'Pedido creado exitosamente';
    } else {
        $idPedido = $pedidoModel->crearPedido(
            $datosCliente,
            $productos,
            $totalGeneral,
            $numCliente,
            $observaciones,
            $ped_sede
        );

        if (!$idPedido) {
            throw new Exception('Error al crear el pedido');
        }

        $mensaje = 'Pedido creado exitosamente';
        $totalPedido = number_format($totalGeneral, 0, ',', '.');
        $resumenPedido = 'Valor total pedido $' . $totalPedido;
        $pedidoModel->enviarConfirmacion($idPedido, "$resumenPedido ", '', "$numCliente", 'pedido_recepcionado');
    }

    marcarPedidoProcesado($pedidoToken, [
        'tipo' => 'success',
        'mensaje' => $mensaje,
        'idPedido' => $idPedido,
        'totalGeneral' => $totalGeneral,
        'productos' => $productos,
        'observaciones' => $observaciones,
        'numCliente' => $numCliente,
        'creado_en' => time(),
    ]);

    api_json([
        'ok' => true,
        'pedidoToken' => $pedidoToken,
        'idPedido' => (int)$idPedido,
        'mensaje' => $mensaje,
        'total' => $totalGeneral,
        'duplicado' => (bool)$idDuplicado,
    ], 201);
} catch (Exception $e) {
    error_log('API pedido: ' . $e->getMessage());

    if (!empty($pedidoToken)) {
        marcarPedidoProcesado($pedidoToken, [
            'tipo' => 'error',
            'mensaje' => $e->getMessage(),
            'idPedido' => null,
            'totalGeneral' => $totalGeneral ?? 0,
            'productos' => $productos ?? [],
            'observaciones' => $observaciones ?? '',
            'numCliente' => $numCliente ?? '',
            'creado_en' => time(),
        ]);
    }

    api_error('Error al procesar el pedido: ' . $e->getMessage(), 500);
}
