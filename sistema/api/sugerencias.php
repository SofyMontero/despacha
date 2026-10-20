<?php
require_once __DIR__ . '/bootstrap.php';

if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
    api_error('Método no permitido', 405);
}

$q = trim((string)($_GET['q'] ?? ''));
if ($q === '') {
    api_json(['ok' => true, 'data' => []]);
}

try {
    $producto = new Producto();
    $rows = $producto->buscarSugerencias($q);

    $data = array_map(static function ($row) {
        return [
            'id' => (int)($row['id_producto'] ?? 0),
            'nombre' => $row['descripcion_producto'] ?? '',
        ];
    }, $rows ?: []);

    api_json(['ok' => true, 'data' => $data]);
} catch (Exception $e) {
    error_log('API sugerencias: ' . $e->getMessage());
    api_error('No se pudieron cargar sugerencias', 500);
}
