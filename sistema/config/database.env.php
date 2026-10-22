<?php
/**
 * Configuración por ambiente del repositorio de migración.
 *
 * Todos los valores salen de archivos .env locales (no versionados):
 * 1) sistema/.env y sistema/config/.env (definen APP_ENV)
 * 2) sistema/config/.env.<APP_ENV>
 *
 * No hay credenciales por defecto. Sin APP_ENV explícito se usa "dev".
 * Las conexiones a servicios reales (bases remotas, WhatsApp Cloud API)
 * están bloqueadas salvo ALLOW_REAL_SERVICES=1.
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

    $env = multilicores_resolve_app_env();
    putenv('APP_ENV=' . $env);
    $_ENV['APP_ENV'] = $env;
    multilicores_load_dotenv($root . '/config/.env.' . $env);
}

function multilicores_resolve_app_env(): string
{
    $explicit = strtolower((string)multilicores_env('APP_ENV', ''));
    return $explicit === 'prod' ? 'prod' : 'dev';
}

function multilicores_app_env(): string
{
    multilicores_bootstrap_env();
    $env = strtolower((string)multilicores_env('APP_ENV', 'dev'));
    return $env === 'prod' ? 'prod' : 'dev';
}

function despacha_servicios_reales_habilitados(): bool
{
    multilicores_bootstrap_env();
    return multilicores_env('ALLOW_REAL_SERVICES', '0') === '1';
}

function despacha_registrar_bloqueo(string $servicio): void
{
    error_log("[despacha] {$servicio} bloqueado: ALLOW_REAL_SERVICES no está habilitado en este entorno.");
}

/**
 * @return array{token:string,phone_id:string,url:string}|null null si el envío está bloqueado o sin configurar
 */
function despacha_whatsapp_config(): ?array
{
    if (!despacha_servicios_reales_habilitados()) {
        return null;
    }
    $token = multilicores_env('WHATSAPP_TOKEN');
    $phoneId = multilicores_env('WHATSAPP_PHONE_ID');
    if ($token === null || $phoneId === null) {
        return null;
    }
    return [
        'token' => $token,
        'phone_id' => $phoneId,
        'url' => 'https://graph.facebook.com/v22.0/' . rawurlencode($phoneId) . '/messages',
    ];
}

function despacha_internal_token(): ?string
{
    multilicores_bootstrap_env();
    return multilicores_env('INTERNAL_API_TOKEN');
}

function despacha_base_url(): string
{
    multilicores_bootstrap_env();
    return rtrim((string)multilicores_env('APP_BASE_URL', 'http://localhost'), '/');
}

function despacha_host_es_local(string $host): bool
{
    return in_array(strtolower($host), ['localhost', '127.0.0.1', '::1'], true);
}

/**
 * @return array{host:string,port:int,dbname:string,username:string,password:string,charset:string,env:string}
 */
function multilicores_db_config(): array
{
    multilicores_bootstrap_env();
    $env = multilicores_app_env();

    $host = multilicores_env('DB_HOST', 'localhost');
    $port = (int)multilicores_env('DB_PORT', '3306');
    $username = multilicores_env('DB_USER');
    $password = multilicores_env('DB_PASS', '');
    $charset = multilicores_env('DB_CHARSET', 'utf8mb4');
    $dbname = multilicores_env('DB_NAME');

    if ($username === null || $dbname === null) {
        throw new RuntimeException('Configuración de base incompleta: defina DB_NAME y DB_USER en sistema/config/.env.' . $env);
    }
    if (!despacha_host_es_local($host) && !despacha_servicios_reales_habilitados()) {
        throw new RuntimeException('Conexión a base remota bloqueada: solo se permite localhost salvo ALLOW_REAL_SERVICES=1.');
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
