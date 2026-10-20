<?php
require_once __DIR__ . '/bootstrap.php';

ops_json([
    'ok' => true,
    'name' => 'Multilicores Ops API',
    'variant' => 'B',
    'version' => '0.1.0',
    'endpoints' => [
        'GET  /ops/index.php',
        'GET  /ops/schema.php  (diagnóstico Fase 1, solo lectura)',
        'POST /ops/auth.php?action=login',
        'GET  /ops/auth.php?action=me  (Bearer)',
        'GET  /ops/empresas.php',
        'GET  /ops/roles.php',
        'GET  /ops/jornadas.php?action=actual',
        'GET  /ops/jornadas.php?action=listar&fecha=',
        'POST /ops/jornadas.php  action=iniciar|finalizar',
        'GET  /ops/visitas.php?action=por_jornada&idJornada=',
        'POST /ops/visitas.php  action=registrar',
        'POST /ops/pedido_visita.php action=crear',
    ],



    'note' => 'No modifica contratos de /sistema/api/ del catálogo (Variante A).',
]);
