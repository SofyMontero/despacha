<?php
ini_set('display_errors', 0);
ini_set('log_errors', 1);
error_reporting(E_ALL);

header('Content-Type: application/json');

require_once "../models/database.php";
require_once "../models/BarModel.php";

if (!isset($_POST['query']) || empty(trim($_POST['query']))) {
    echo json_encode([]);
    exit;
}

try {
    $bar = new Bar();
    $termino = trim($_POST['query']);
    
    $bares = $bar->buscarBaresPorNombre($termino);
    
    $resultados = [];
    foreach ($bares as $barData) {
        $resultados[] = [
            'id' => $barData['id_bar'],
            'value' => $barData['nombre_bar'],
            'label' => $barData['nombre_bar'],
            'direccion' => $barData['direccion'],
            'telefono' => $barData['telefono'],
            'email' => $barData['email']
        ];
    }
    
    echo json_encode($resultados);

} catch (Exception $e) {
    // Podrías loguearlo con error_log($e->getMessage());
    echo json_encode([]);
}
