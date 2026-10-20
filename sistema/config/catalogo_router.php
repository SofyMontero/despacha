<?php
/**
 * Helpers para enrutar el catálogo (PHP vs React) sin cambiar el link de WhatsApp.
 */

function catalogo_config(): array
{
    static $config = null;
    if ($config === null) {
        $config = require __DIR__ . '/catalogo.php';
    }
    return $config;
}

function catalogo_version_activa(): string
{
    $version = catalogo_config()['version'] ?? 'php';
    return $version === 'react' ? 'react' : 'php';
}

function catalogo_es_react(): bool
{
    return catalogo_version_activa() === 'react';
}

/**
 * Link estable para WhatsApp: siempre categorias.php (no cambia al cambiar de versión).
 */
function catalogo_whatsapp_link(string $telefonoCliente): string
{
    $https = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
        || (isset($_SERVER['SERVER_PORT']) && (int)$_SERVER['SERVER_PORT'] === 443)
        || (isset($_SERVER['HTTP_X_FORWARDED_PROTO']) && $_SERVER['HTTP_X_FORWARDED_PROTO'] === 'https');

    $scheme = $https ? 'https' : 'http';
    $host = $_SERVER['HTTP_HOST'] ?? 'multilicoreschapinero.com';
    $entry = catalogo_config()['php_entry'] ?? '/sistema/views/categorias.php';

    return $scheme . '://' . $host . $entry . '?idCli=' . rawurlencode($telefonoCliente);
}

/**
 * Si la versión activa es React, redirige a la app conservando query string (idCli, etc.).
 */
function catalogo_redirigir_si_react(?string $reactPath = ''): void
{
    if (!catalogo_es_react()) {
        return;
    }

    $config = catalogo_config();
    $base = rtrim($config['react_base'] ?? '/sistema/app/', '/') . '/';
    $path = ltrim((string)$reactPath, '/');

    $query = $_GET;
    $qs = http_build_query($query);
    $target = $base . $path . ($qs !== '' ? ('?' . $qs) : '');

    header('Location: ' . $target, true, 302);
    exit;
}
