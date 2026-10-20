<?php
/**
 * Bootstrap API ops (Variante B).
 * Separado de sistema/api/bootstrap.php del catálogo.
 */

header('Content-Type: application/json; charset=utf-8');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

require_once __DIR__ . '/../../modules/bootstrap.php';

function ops_json($data, int $status = 200): void
{
    http_response_code($status);
    echo json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

function ops_error(string $message, int $status = 400, array $extra = []): void
{
    ops_json(array_merge(['ok' => false, 'error' => $message], $extra), $status);
}

function ops_read_json_body(): array
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

function ops_require_auth(): array
{
    try {
        $auth = new OpsAuth();
        return $auth->requireUser();
    } catch (Exception $e) {
        $code = (stripos($e->getMessage(), 'autoriz') !== false
            || stripos($e->getMessage(), 'Sesión') !== false) ? 401 : 400;
        ops_error($e->getMessage(), $code);
    }
}
