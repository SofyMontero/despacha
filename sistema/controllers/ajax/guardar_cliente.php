<?php
require_once '../../models/database.php';
require_once '../../models/BarModel.php';

header('Content-Type: application/json');

try {
    $db = new Database();
    $pdo = $db->connect();
    $barModel = new Bar();  

    $cli_identificacion = $_POST['cli_identificacion'] ?? null;
    $cli_nombre         = $_POST['cli_nombre'] ?? null;
    $telefono           = $_POST['cli_telefono'];
    $cli_direccion      = $_POST['cli_direccion'] ?? null;
    $cli_zona           = $_POST['cli_zona'] ?? null;
    $nombre_bar         = trim($_POST['cli_bar']);
    $bar_id             = $_POST['bar_id'] ?? '';

    // 🧠 1. Verificar si ya viene el ID del bar (por autocompletado)
    if (empty($bar_id)) {
        if (!$barModel->existeBar($nombre_bar)) {
            $barCreado = $barModel->insertarBar($nombre_bar, $cli_direccion);
            if ($barCreado) {
                $bar_id = $barModel->obtenerUltimoIdInsertado();
            } else {
                throw new Exception("No se pudo crear el bar.");
            }
        } else {
            $barExistente = $barModel->buscarBaresPorNombre($nombre_bar);
            $bar_id = $barExistente[0]['id_bar'] ?? null;

            if (!$bar_id) {
                throw new Exception("No se pudo encontrar el bar existente.");
            }
        }
    }

    // 2. Insertar cliente
    $stmt = $pdo->prepare("
        INSERT INTO clientes (cli_identificacion, cli_nombre, cli_telefono, cli_direccion, cli_Bar, cli_zona)
        VALUES (?, ?, ?, ?, ?, ?)
    ");
    $stmt->execute([
        $cli_identificacion,
        $cli_nombre,
        $telefono,
        $cli_direccion,
        $bar_id,
        $cli_zona
    ]);

    echo json_encode(['exito' => true]);

} catch (Exception $e) {
    echo json_encode([
        'exito' => false,
        'mensaje' => $e->getMessage()
    ]);
}
