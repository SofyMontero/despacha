<?php
require_once "../models/database.php";
require_once "../models/solicitudModel.php";

header('Content-Type: application/json');

// Instanciar la clase solicitud
$solicitudModel = new solicitud();

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $action = $_POST['action'] ?? '';
    
    try {
        switch ($action) {
            case 'obtener_productos':
                obtenerProductosPedido();
                break;
                
            case 'aceptar_pedido':
                aceptarPedido();
                break;
                
            case 'rechazar_pedido':
                rechazarPedido();
                break;
                
            case 'obtener_pedidos_pendientes':
                obtenerPedidosPendientes();
                break;
                
            case 'obtener_pedidos_aceptados':
                obtenerPedidosAceptados();
                break;
                
            case 'obtener_estadisticas':
                obtenerEstadisticas();
                break;
                
            default:
                echo json_encode(['success' => false, 'message' => 'Acción no válida']);
                break;
        }
    } catch (Exception $e) {
        echo json_encode([
            'success' => false, 
            'message' => 'Error del servidor: ' . $e->getMessage()
        ]);
    }
}

function obtenerProductosPedido() {
    global $solicitudModel;
    
    $pedido_id = $_POST['pedido_id'] ?? '';
    
    if (empty($pedido_id)) {
        echo json_encode(['success' => false, 'message' => 'ID de pedido requerido']);
        return;
    }
    
    $productos = $solicitudModel->obtenerProductosPedido($pedido_id);
    
    echo json_encode([
        'success' => true,
        'productos' => $productos
    ]);
}

function aceptarPedido() {
    global $solicitudModel;
    
    $pedido_id = $_POST['pedido_id'] ?? '';
    $respuesta = $_POST['respuesta'] ?? '';
    $productos_sin_stock = $_POST['productos_sin_stock'] ?? null;
    
    if (empty($pedido_id) || empty($respuesta)) {
        echo json_encode(['success' => false, 'message' => 'Datos incompletos']);
        return;
    }
    
    $resultado = $solicitudModel->aceptarPedido($pedido_id, $respuesta, $productos_sin_stock);
    
    if ($resultado) {
        echo json_encode([
            'success' => true,
            'message' => 'Pedido aceptado exitosamente'
        ]);
    } else {
        echo json_encode([
            'success' => false,
            'message' => 'Error al aceptar el pedido'
        ]);
    }
}

function rechazarPedido() {
    global $solicitudModel;
    
    $pedido_id = $_POST['pedido_id'] ?? '';
    $motivo_rechazo = $_POST['motivo_rechazo'] ?? '';
    
    if (empty($pedido_id) || empty($motivo_rechazo)) {
        echo json_encode(['success' => false, 'message' => 'Datos incompletos']);
        return;
    }
    
    $resultado = $solicitudModel->rechazarPedido($pedido_id, $motivo_rechazo);
    
    if ($resultado) {
        echo json_encode([
            'success' => true,
            'message' => 'Pedido rechazado exitosamente'
        ]);
    } else {
        echo json_encode([
            'success' => false,
            'message' => 'Error al rechazar el pedido'
        ]);
    }
}

function obtenerPedidosPendientes() {
    global $solicitudModel;
    
    $pedidos = $solicitudModel->obtenerPedidosPendientes();
    
    echo json_encode([
        'success' => true,
        'pedidos' => $pedidos,
        'total' => count($pedidos)
    ]);
}

function obtenerPedidosAceptados() {
    global $solicitudModel;
    
    $pedidos = $solicitudModel->obtenerPedidosAceptados();
    
    echo json_encode([
        'success' => true,
        'pedidos' => $pedidos,
        'total' => count($pedidos)
    ]);
}

function obtenerEstadisticas() {
    global $solicitudModel;
    
    $estadisticas = $solicitudModel->obtenerEstadisticasPedidos();
    $pendientes = $solicitudModel->contarPedidosPorEstado('pendiente');
    $aceptados = $solicitudModel->contarPedidosPorEstado('aceptado');
    
    echo json_encode([
        'success' => true,
        'estadisticas' => $estadisticas,
        'contadores' => [
            'pendientes' => $pendientes,
            'aceptados' => $aceptados
        ]
    ]);
}
?>