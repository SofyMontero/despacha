<?php
/**
 * Credenciales de base de datos por ambiente.
 *
 * APP_ENV=prod (por defecto, Hostinger sin archivos .env)
 *   → base de producción. No subir sistema/config/.env ni .env.dev.
 *
 * APP_ENV=dev
 *   → base de test. Credenciales en sistema/config/.env.dev (no va a git).
 *
 * Prioridad:
 * 1) sistema/.env y sistema/config/.env (definen APP_ENV)
 * 2) sistema/config/.env.prod o sistema/config/.env.dev
 * 3) Fallback de producción solo si APP_ENV=prod
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

function multilicores_bootstrap_env(): void
{
    static $loaded = false;
    if ($loaded) {
        return;
    }
    $loaded = true;

    $root = dirname(__DIR__);
    multilicores_load_dotenv($root . '/.env');
    multilicores_load_dotenv($root . '/config/.env');

    $env = strtolower((string)multilicores_env('APP_ENV', 'prod'));
    $env = $env === 'dev' ? 'dev' : 'prod';
    multilicores_load_dotenv($root . '/config/.env.' . $env);
}

function multilicores_app_env(): string
{
    multilicores_bootstrap_env();
    $env = strtolower((string)multilicores_env('APP_ENV', 'prod'));
    return $env === 'dev' ? 'dev' : 'prod';
}

/**
 * @return array{host:string,port:int,dbname:string,username:string,password:string,charset:string,env:string}
 */
function multilicores_db_config(): array
{
    multilicores_bootstrap_env();
    $env = multilicores_app_env();

    if ($env === 'dev') {
        $host = multilicores_env('DB_HOST', 'localhost');
        $port = (int)multilicores_env('DB_PORT', '3306');
        $username = multilicores_env('DB_USER', '');
        $password = multilicores_env('DB_PASS', '');
        $charset = multilicores_env('DB_CHARSET', 'utf8mb4');
        $dbname = multilicores_env('DB_NAME', '');
        if ($dbname === '' || $username === '' || $password === '') {
            throw new RuntimeException(
                'APP_ENV=dev requiere DB_NAME, DB_USER y DB_PASS en sistema/config/.env.dev'
            );
        }
    } else {
        // Fallbacks = producción actual (Hostinger sin .env no cambia de base).
        $host = multilicores_env('DB_HOST', 'localhost');
        $port = (int)multilicores_env('DB_PORT', '3306');
        $username = multilicores_env('DB_USER', 'u633742531_Multilicores25');
        $password = multilicores_env('DB_PASS', 'Multilicores2025');
        $charset = multilicores_env('DB_CHARSET', 'utf8mb4');
        $dbname = multilicores_env('DB_NAME', 'u633742531_Multilicores');
    }

    return [
        'host' => $host,
        'port' => $port,
        'dbname' => $dbname,
        'username' => $username,
        'password' => $password,
        'charset' => $charset,
        'env' => $env,
    ];
}
