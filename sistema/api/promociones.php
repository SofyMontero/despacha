<?php
require_once __DIR__ . '/bootstrap.php';

if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
    api_error('Método no permitido', 405);
}

try {
    $producto = new Producto();
    $promociones = $producto->getConnection();

    $activas = array_values(array_filter($promociones ?: [], static function ($promo) {
        return isset($promo['estado']) && (int)$promo['estado'] === 1;
    }));

    usort($activas, static function ($a, $b) {
        $pa = (int)($a['prioridad'] ?? 0);
        $pb = (int)($b['prioridad'] ?? 0);
        if ($pa !== $pb) {
            return $pb - $pa;
        }
        return strtotime($b['creado_en'] ?? 'now') <=> strtotime($a['creado_en'] ?? 'now');
    });

    $limit = isset($_GET['limit']) ? max(1, (int)$_GET['limit']) : null;
    if ($limit !== null) {
        $activas = array_slice($activas, 0, $limit);
    }

    $data = array_map(static function ($promo) {
        $imagen = $promo['imagen'] ?? '';
        $actiUnidad = $promo['acti_Unidad'] ?? '0';
        $codigo = $promo['codigo'] ?? '';
        $descripcion = (string)($promo['descripcion'] ?? '');
        $promoId = (int)($promo['id_promocion'] ?? $promo['id'] ?? 0);

        $prodRelacionado = promo_find_product($codigo, $descripcion);
        $productoId = (int)($prodRelacionado['id_producto'] ?? 0);
        if ($productoId <= 0 && ctype_digit((string)$codigo)) {
            $productoId = (int)$codigo;
        }

        return [
            'id' => $productoId > 0 ? $productoId : $promoId,
            'promoId' => $promoId,
            'productoId' => $productoId,
            'titulo' => $promo['titulo'] ?? $promo['nombre'] ?? '',
            'descripcion' => $descripcion,
            'prioridad' => (int)($promo['prioridad'] ?? 0),
            'precioUnidad' => (float)($promo['precio_unidad_producto'] ?? 0),
            'precioPaca' => (float)($promo['precio_paca_producto'] ?? 0),
            'vendeUnidad' => (int)$actiUnidad !== 0,
            'actiUnidad' => $actiUnidad,
            'imagen' => api_promo_image_url($imagen, $codigo, $descripcion, $promoId),
            'estado' => (int)($promo['estado'] ?? 0),
            'creadoEn' => $promo['creado_en'] ?? null,
        ];
    }, $activas);

    api_json([
        'ok' => true,
        'data' => $data,
        'meta' => ['total' => count($data)],
    ]);
} catch (Exception $e) {
    error_log('API promociones: ' . $e->getMessage());
    api_error('No se pudieron cargar las promociones', 500);
}
