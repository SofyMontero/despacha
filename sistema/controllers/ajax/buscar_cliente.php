<?php
require_once '../../models/database.php'; // tu conexión PDO
$telefono = $_GET['telefono'] ?? '';


header('Content-Type: application/json');



if (!empty($telefono)) {
    try {
        // Crear instancia de la clase Database y conectar
        $db = new Database();     // Asegúrate de que tu clase se llama así
        $pdo = $db->connect();

        // Consulta para obtener todos los clientes con ese teléfono
        $stmt = $pdo->prepare("SELECT id_cliente, cli_nombre, cli_direccion FROM clientes WHERE cli_telefono = ?");
        $stmt->execute([$telefono]);
        $clientes = $stmt->fetchAll(PDO::FETCH_ASSOC);

        if (count($clientes) > 0) {
            if (count($clientes) == 1) {
                // Solo hay un cliente
                echo json_encode([
                    'existe' => true, 
                    'nombre' => $clientes[0]['cli_nombre'],
                    'direccion' => $clientes[0]['cli_direccion'] ?? null,
                    'multiple' => false
                ]);
            } else {
                // Hay múltiples clientes
                echo json_encode([
                    'existe' => true,
                    'multiple' => true,
                    'clientes' => $clientes
                ]);
            }
        } else {
            echo json_encode(['existe' => false]);
        }

    } catch (Exception $e) {
        echo json_encode([
            'existe' => false,
            'error' => 'Error en la base de datos: ' . $e->getMessage()
        ]);
    }
} else {
    echo json_encode(['existe' => false, 'error' => 'Número no proporcionado']);
}