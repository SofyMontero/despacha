<?php
/**
 * Interruptor del catálogo cliente.
 *
 * El link de WhatsApp SIEMPRE apunta a categorias.php.
 * Aquí decides qué versión ve el cliente:
 *   - 'php'   → catálogo PHP clásico (views/)
 *   - 'react' → app React (sistema/app/)
 *
 * Solo una versión queda activa a la vez.
 */
return [
    'version' => 'php', // cámbialo a 'react' cuando quieras activar la app

    'react_base' => '/sistema/app/',
    'php_entry' => '/sistema/views/categorias.php',
];
