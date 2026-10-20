<?php
/**
 * Bootstrap compartido para la API del catálogo cliente.
 */

header('Content-Type: application/json; charset=utf-8');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

require_once __DIR__ . '/../models/database.php';
require_once __DIR__ . '/../models/ProductoModel.php';
require_once __DIR__ . '/../models/PedidoModel.php';
require_once __DIR__ . '/../models/BarModel.php';
require_once __DIR__ . '/../helpers/promo_images.php';

function api_base_url(): string
{
    return promo_https_base();
}

function api_asset_url(string $relativePath): string
{
    $relativePath = ltrim(str_replace('\\', '/', $relativePath), '/');
    return api_base_url() . '/assets/img/licores/' . $relativePath;
}

function api_logo_url(): string
{
    return api_base_url() . '/assets/img/logoM.png';
}

function api_promo_image_url(?string $filename, $codigoProducto = null, string $descripcion = '', $promoId = 0): string
{
    return promo_image_absolute_url($filename, $codigoProducto, $descripcion, $promoId);
}

function api_json($data, int $status = 200): void
{
    http_response_code($status);
    echo json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

function api_error(string $message, int $status = 400, array $extra = []): void
{
    api_json(array_merge(['ok' => false, 'error' => $message], $extra), $status);
}

function api_read_json_body(): array
{
    $raw = file_get_contents('php://input');
    if ($raw === false || trim($raw) === '') {
        return $_POST ?: [];
    }

    $decoded = json_decode($raw, true);
    if (json_last_error() !== JSON_ERROR_NONE || !is_array($decoded)) {
        return $_POST ?: [];
    }

    return $decoded;
}

function api_normalize_phone(string $telefono): string
{
    $telefono = preg_replace('/\D+/', '', trim($telefono));
    if ($telefono === '') {
        return '';
    }
    if (strpos($telefono, '57') !== 0 && strlen($telefono) === 10) {
        $telefono = '57' . $telefono;
    }
    return $telefono;
}
