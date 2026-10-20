<?php
require_once "../models/database.php";
require_once "../models/ProductoModel.php";

header('Content-Type: application/json');

$termino = $_GET['q'] ?? '';

if (strlen($termino) < 2) {
    echo json_encode([]);
    exit;
}

$producto = new Producto();
$resultados = $producto->buscarSugerencias($termino);


echo json_encode($resultados);
