<?php
/**
 * Credenciales de base de datos.
 *
 * Prioridad:
 * 1) Variables de entorno / .env (recomendado)
 * 2) Fallback a los valores actuales de producción (Hostinger)
 *
 * Una sola base para catálogo, admin y operaciones.
 */

function multilicores_load_dotenv(string $path): void
{
    if (!is_readable($path)) {
        return;
    }
    $lines = file($path, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
    if ($lines === false) {
        return;
    }
    foreach ($lines as $line) {
        $line = trim($line);
        if ($line === '' || $line[0] === '#') {
            continue;
        }
        if (strpos($line, '=') === false) {
            continue;
        }
        [$key, $value] = explode('=', $line, 2);
        $key = trim($key);
        $value = trim($value, " \t\"'");
        if ($key === '') {
            continue;
        }
        if (getenv($key) === false) {
            putenv("{$key}={$value}");
            $_ENV[$key] = $value;
        }
    }
}

function multilicores_env(string $key, ?string $default = null): ?string
{
    $value = getenv($key);
    if ($value === false || $value === '') {
        $value = $_ENV[$key] ?? null;
    }
    if ($value === null || $value === '') {
        return $default;
    }
    return (string)$value;
}

/**
 * @return array{host:string,port:int,dbname:string,username:string,password:string,charset:string}
 */
function multilicores_db_config(): array
{
    static $loaded = false;
    if (!$loaded) {
        $root = dirname(__DIR__);
        multilicores_load_dotenv($root . '/.env');
        multilicores_load_dotenv($root . '/config/.env');
        $loaded = true;
    }

    // Fallbacks = comportamiento actual de producción (no romper Hostinger).
    $host = multilicores_env('DB_HOST', 'localhost');
    $port = (int)multilicores_env('DB_PORT', '3306');
    $username = multilicores_env('DB_USER', 'u633742531_Multilicores25');
    $password = multilicores_env('DB_PASS', 'Multilicores2025');
    $charset = multilicores_env('DB_CHARSET', 'utf8mb4');
    $dbname = multilicores_env('DB_NAME', 'u633742531_Multilicores');

    return [
        'host' => $host,
        'port' => $port,
        'dbname' => $dbname,
        'username' => $username,
        'password' => $password,
        'charset' => $charset,
    ];
}
