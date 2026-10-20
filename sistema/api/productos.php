<?php
require_once __DIR__ . '/bootstrap.php';

if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
    api_error('Método no permitido', 405);
}

try {
    $categoria = $_GET['categoria'] ?? '';
    $busqueda = $_GET['id'] ?? '';
    $q = trim((string)($_GET['q'] ?? ''));
    $page = max(1, (int)($_GET['page'] ?? 1));
    $limit = min(48, max(1, (int)($_GET['limit'] ?? 12)));
    $offset = ($page - 1) * $limit;

    $producto = new Producto();

    if ($q !== '') {
        $sugerencias = $producto->buscarSugerencias($q);
        $ids = array_column($sugerencias ?: [], 'id_producto');
        $items = [];
        foreach ($ids as $id) {
            $prod = $producto->obtenerProductoPorId($id);
            if ($prod && (string)($prod['estado_producto'] ?? '') === '1') {
                $items[] = map_producto($prod);
            }
        }
        api_json([
            'ok' => true,
            'data' => $items,
            'meta' => [
                'page' => 1,
                'limit' => count($items),
                'total' => count($items),
                'totalPages' => 1,
            ],
        ]);
    }

    $total = $producto->contarProductos($categoria);
    $rows = $producto->obtenerProductos($categoria, $busqueda, $limit, $offset);
    $items = array_map('map_producto', $rows ?: []);

    api_json([
        'ok' => true,
        'data' => $items,
        'meta' => [
            'page' => $page,
            'limit' => $limit,
            'total' => $total,
            'totalPages' => (int)ceil($total / max(1, $limit)),
            'categoria' => $categoria,
        ],
    ]);
} catch (Exception $e) {
    error_log('API productos: ' . $e->getMessage());
    api_error('No se pudieron cargar los productos', 500);
}

function map_producto(array $prod): array
{
    $imagen = $prod['imagen_producto'] ?? '';
    return [
        'id' => (int)($prod['id_producto'] ?? 0),
        'codigo' => $prod['codigo_productos'] ?? '',
        'nombre' => $prod['descripcion_producto'] ?? '',
        'categoriaId' => (int)($prod['id_cate_producto'] ?? 0),
        'precioUnidad' => (float)($prod['precio_unidad_producto'] ?? 0),
        'precioPaca' => (float)($prod['precio_paca_producto'] ?? 0),
        'cantidadPaca' => (int)($prod['cantidad_paca_producto'] ?? 0),
        'vendeUnidad' => (int)($prod['acti_Unidad'] ?? 0) !== 0,
        'actiUnidad' => $prod['acti_Unidad'] ?? '0',
        'imagen' => $imagen !== '' ? api_asset_url($imagen) : api_asset_url('placeholder.jpg'),
        'estado' => $prod['estado_producto'] ?? '1',
    ];
}
