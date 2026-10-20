<?php
require_once __DIR__ . '/bootstrap.php';

api_json([
    'ok' => true,
    'name' => 'Multilicores Catálogo API',
    'version' => '1.0',
    'endpoints' => [
        'GET /categorias.php',
        'GET /productos.php?categoria=&page=&limit=',
        'GET /promociones.php?limit=',
        'GET /sugerencias.php?q=',
        'GET /cliente.php?telefono=',
        'POST /cliente.php',
        'POST /pedido.php',
    ],
]);
