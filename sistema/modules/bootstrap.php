<?php
/**
 * Bootstrap de módulos de operaciones (jornadas, visitas, entregas).
 * Misma base y misma aplicación que el catálogo.
 */

require_once __DIR__ . '/../models/database.php';
require_once __DIR__ . '/Auth/OpsAuth.php';
require_once __DIR__ . '/Empresa/EmpresaRepository.php';
require_once __DIR__ . '/Rol/RolRepository.php';
require_once __DIR__ . '/Jornada/JornadaRepository.php';
require_once __DIR__ . '/Visita/VisitaRepository.php';
require_once __DIR__ . '/PedidoOps/PedidoDesdeVisita.php';
require_once __DIR__ . '/Entrega/EntregaRepository.php';



