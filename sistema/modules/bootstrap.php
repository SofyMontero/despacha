<?php
/**
 * Bootstrap de módulos Variante B (ops).
 * No altera el bootstrap de sistema/api/ (catálogo A).
 */

require_once __DIR__ . '/../models/database.php';
require_once __DIR__ . '/Auth/OpsAuth.php';
require_once __DIR__ . '/Empresa/EmpresaRepository.php';
require_once __DIR__ . '/Rol/RolRepository.php';
require_once __DIR__ . '/Jornada/JornadaRepository.php';
require_once __DIR__ . '/Visita/VisitaRepository.php';
require_once __DIR__ . '/PedidoOps/PedidoDesdeVisita.php';
require_once __DIR__ . '/Entrega/EntregaRepository.php';



