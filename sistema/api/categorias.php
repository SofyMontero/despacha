<?php
require_once __DIR__ . '/bootstrap.php';

if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
    api_error('Método no permitido', 405);
}

try {
    $producto = new Producto();
    $categorias = $producto->obtenerCategorias();

    $data = array_map(static function ($cat) {
        $imagen = $cat['imagen_categoria'] ?? '';
        return [
            'id' => (int)($cat['id_categoria'] ?? 0),
            'nombre' => $cat['nombre_categoria'] ?? '',
            'imagen' => $imagen !== '' ? api_asset_url($imagen) : api_asset_url('placeholder.jpg'),
        ];
    }, $categorias ?: []);

    api_json(['ok' => true, 'data' => $data]);
} catch (Exception $e) {
    error_log('API categorias: ' . $e->getMessage());
    api_error('No se pudieron cargar las categorías', 500);
}
