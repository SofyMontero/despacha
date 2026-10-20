<?php
/**
 * Helpers de imágenes de promociones (compartidos API + vistas PHP).
 */

require_once __DIR__ . '/../models/database.php';
require_once __DIR__ . '/../models/ProductoModel.php';

function promo_https_base(): string
{
    $https = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
        || (isset($_SERVER['SERVER_PORT']) && (int)$_SERVER['SERVER_PORT'] === 443)
        || (isset($_SERVER['HTTP_X_FORWARDED_PROTO']) && $_SERVER['HTTP_X_FORWARDED_PROTO'] === 'https');

    $scheme = $https ? 'https' : 'http';
    $host = $_SERVER['HTTP_HOST'] ?? 'multilicoreschapinero.com';

    return $scheme . '://' . $host . '/sistema';
}

function promo_find_product($codigoProducto = null, string $descripcion = ''): ?array
{
    static $producto = null;
    static $cache = [];

    $codigo = trim((string)$codigoProducto);
    $cacheKey = $codigo . "\0" . $descripcion;
    if (array_key_exists($cacheKey, $cache)) {
        return $cache[$cacheKey];
    }

    try {
        if ($producto === null) {
            $producto = new Producto();
        }

        if ($codigo !== '' && ctype_digit($codigo)) {
            $prod = $producto->obtenerProductoPorId((int)$codigo);
            if ($prod) {
                return $cache[$cacheKey] = $prod;
            }
        }

        if ($codigo !== '') {
            $prod = $producto->obtenerProductoPorCodigo($codigo);
            if ($prod) {
                return $cache[$cacheKey] = $prod;
            }
        }

        $needle = trim(preg_split('/\s*[+×xX*]\s*/u', $descripcion)[0] ?? '');
        $needle = preg_replace('/\s+/', ' ', $needle);
        if (function_exists('mb_strlen') ? mb_strlen($needle) >= 4 : strlen($needle) >= 4) {
            $sugerencias = $producto->buscarSugerencias($needle);
            if (!empty($sugerencias[0]['id_producto'])) {
                $prod = $producto->obtenerProductoPorId((int)$sugerencias[0]['id_producto']);
                if ($prod) {
                    return $cache[$cacheKey] = $prod;
                }
            }
        }
    } catch (Exception $e) {
        error_log('promo_find_product: ' . $e->getMessage());
    }

    return $cache[$cacheKey] = null;
}

function promo_image_filename(?string $filename): string
{
    $filename = str_replace('\\', '/', trim((string)$filename));
    if ($filename === '') {
        return '';
    }

    return basename($filename);
}

/**
 * Ruta relativa desde /sistema/views/. Usa el mismo nombre guardado en BD y en disco.
 */
function promo_image_web_path(?string $filename, $codigoProducto = null, string $descripcion = '', $promoId = 0): string
{
    $filename = promo_image_filename($filename);
    if ($filename !== '') {
        return '../assets/img/licores/promos/' . $filename;
    }

    $prod = promo_find_product($codigoProducto, $descripcion);
    $img = $prod['imagen_producto'] ?? '';
    if ($img !== '') {
        return '../assets/img/licores/' . ltrim(str_replace('\\', '/', $img), '/');
    }

    return '../assets/img/logoM.png';
}

/**
 * URL absoluta (API / React). Misma carpeta y mismo nombre de archivo.
 */
function promo_image_absolute_url(?string $filename, $codigoProducto = null, string $descripcion = '', $promoId = 0): string
{
    $filename = promo_image_filename($filename);
    $base = promo_https_base();

    if ($filename !== '') {
        return $base . '/assets/img/licores/promos/' . rawurlencode($filename);
    }

    $prod = promo_find_product($codigoProducto, $descripcion);
    $img = $prod['imagen_producto'] ?? '';
    if ($img !== '') {
        return $base . '/assets/img/licores/' . ltrim(str_replace('\\', '/', $img), '/');
    }

    return $base . '/assets/img/logoM.png';
}
