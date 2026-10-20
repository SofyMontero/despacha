<?php
require_once __DIR__ . '/bootstrap.php';

$method = $_SERVER['REQUEST_METHOD'];

try {
    if ($method === 'GET') {
        $telefono = api_normalize_phone((string)($_GET['telefono'] ?? $_GET['idCli'] ?? ''));
        if ($telefono === '') {
            api_error('Teléfono requerido');
        }

        $producto = new Producto();

        // Buscar con y sin prefijo 57 (datos legacy)
        $sinPrefijo = (strpos($telefono, '57') === 0 && strlen($telefono) > 10)
            ? substr($telefono, 2)
            : $telefono;

        $clientes = $producto->obtenerClientesPorTelefono($telefono);
        if (empty($clientes) && $sinPrefijo !== $telefono) {
            $clientes = $producto->obtenerClientesPorTelefono($sinPrefijo);
        }
        if (empty($clientes) && $sinPrefijo === $telefono) {
            $clientes = $producto->obtenerClientesPorTelefono('57' . $telefono);
        }

        $status = 'no_existe';
        if (count($clientes) === 1) {
            $status = 'existe';
        } elseif (count($clientes) > 1) {
            $status = 'multiple';
        }

        $data = array_map(static function ($c) {
            return [
                'id' => (int)($c['id_cliente'] ?? 0),
                'nombre' => $c['cli_nombre'] ?? '',
                'telefono' => $c['cli_telefono'] ?? '',
                'direccion' => $c['cli_direccion'] ?? '',
            ];
        }, $clientes ?: []);

        api_json([
            'ok' => true,
            'status' => $status,
            'telefono' => $telefono,
            'data' => $data,
        ]);
    }

    if ($method === 'POST') {
        $body = api_read_json_body();

        $cli_identificacion = $body['identificacion'] ?? $body['cli_identificacion'] ?? null;
        $cli_nombre = trim((string)($body['nombre'] ?? $body['cli_nombre'] ?? ''));
        $telefono = api_normalize_phone((string)($body['telefono'] ?? $body['cli_telefono'] ?? ''));
        $cli_direccion = trim((string)($body['direccion'] ?? $body['cli_direccion'] ?? ''));
        $cli_zona = $body['zona'] ?? $body['cli_zona'] ?? null;
        $nombre_bar = trim((string)($body['bar'] ?? $body['cli_bar'] ?? ''));
        $bar_id = $body['barId'] ?? $body['bar_id'] ?? '';

        if ($cli_nombre === '' || $telefono === '') {
            api_error('Nombre y teléfono son obligatorios');
        }

        $db = new Database();
        $pdo = $db->connect();
        $barModel = new Bar();

        if (empty($bar_id) && $nombre_bar !== '') {
            if (!$barModel->existeBar($nombre_bar)) {
                $barCreado = $barModel->insertarBar($nombre_bar, $cli_direccion);
                if (!$barCreado) {
                    throw new Exception('No se pudo crear el bar.');
                }
                $bar_id = $barModel->obtenerUltimoIdInsertado();
            } else {
                $barExistente = $barModel->buscarBaresPorNombre($nombre_bar);
                $bar_id = $barExistente[0]['id_bar'] ?? null;
                if (!$bar_id) {
                    throw new Exception('No se pudo encontrar el bar existente.');
                }
            }
        }

        $stmt = $pdo->prepare("
            INSERT INTO clientes (cli_identificacion, cli_nombre, cli_telefono, cli_direccion, cli_Bar, cli_zona)
            VALUES (?, ?, ?, ?, ?, ?)
        ");
        $stmt->execute([
            $cli_identificacion,
            $cli_nombre,
            $telefono,
            $cli_direccion,
            $bar_id ?: null,
            $cli_zona,
        ]);

        api_json([
            'ok' => true,
            'data' => [
                'id' => (int)$pdo->lastInsertId(),
                'nombre' => $cli_nombre,
                'telefono' => $telefono,
                'direccion' => $cli_direccion,
            ],
        ], 201);
    }

    api_error('Método no permitido', 405);
} catch (Exception $e) {
    error_log('API cliente: ' . $e->getMessage());
    api_error($e->getMessage(), 500);
}
