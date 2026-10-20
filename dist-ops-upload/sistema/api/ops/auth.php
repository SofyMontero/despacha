<?php
require_once __DIR__ . '/bootstrap.php';

$action = $_GET['action'] ?? (ops_read_json_body()['action'] ?? '');

try {
    $auth = new OpsAuth();

    if ($action === 'login' && $_SERVER['REQUEST_METHOD'] === 'POST') {
        $body = ops_read_json_body();
        $data = $auth->login(
            (string)($body['usuario'] ?? ''),
            (string)($body['password'] ?? '')
        );
        ops_json(['ok' => true, 'data' => $data]);
    }

    if ($action === 'me' && $_SERVER['REQUEST_METHOD'] === 'GET') {
        $user = ops_require_auth();
        $rol = $user['_rol'] ?? null;
        ops_json([
            'ok' => true,
            'data' => [
                'id' => (int)$user['id'],
                'nombre' => $user['nombre'] ?? $user['usuario'],
                'usuario' => $user['usuario'],
                'idEmpresa' => isset($user['id_empresa']) ? (int)$user['id_empresa'] : null,
                'idRol' => $rol['id'] ?? null,
                'rol' => $rol['nombre'] ?? null,
            ],
        ]);
    }

    if ($action === 'logout' && $_SERVER['REQUEST_METHOD'] === 'POST') {
        $user = ops_require_auth();
        $pdo = (new Database())->connect();
        $stmt = $pdo->prepare('UPDATE users SET ops_api_token = NULL WHERE id = ?');
        $stmt->execute([(int)$user['id']]);
        ops_json(['ok' => true]);
    }

    ops_error('Acción no válida', 404);
} catch (Exception $e) {
    $msg = $e->getMessage();
    $code = (stripos($msg, 'incorrectos') !== false || stripos($msg, 'acceso') !== false) ? 401 : 400;
    if (stripos($msg, 'autoriz') !== false || stripos($msg, 'Sesión') !== false) {
        $code = 401;
    }
    ops_error($msg, $code);
}
