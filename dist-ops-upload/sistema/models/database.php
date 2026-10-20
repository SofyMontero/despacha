<?php
require_once __DIR__ . '/../config/database.env.php';

class Database {
    /** @var PDO|null Una sola conexión PDO por petición (Hostinger limita las concurrentes). */
    private static $pdo = null;

    /** Variante de DSN: default | ops (hoy suelen ser la misma BD). */
    private $variant;

    public function __construct(string $variant = 'default')
    {
        $this->variant = $variant === 'ops' ? 'ops' : 'default';
    }

    public function connect() {
        if (self::$pdo instanceof PDO) {
            return self::$pdo;
        }

        $cfg = multilicores_db_config($this->variant);

        $options = [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_TIMEOUT => 5,
        ];

        // localhost usa socket Unix; si el host lo bloquea (HY000/2002), se fuerza TCP.
        $hosts = [$cfg['host']];
        if ($cfg['host'] === 'localhost') {
            $hosts[] = '127.0.0.1';
        }

        $lastError = null;
        foreach ($hosts as $host) {
            try {
                $dsn = sprintf(
                    'mysql:host=%s;port=%d;dbname=%s;charset=%s',
                    $host,
                    $cfg['port'],
                    $cfg['dbname'],
                    $cfg['charset']
                );
                self::$pdo = new PDO($dsn, $cfg['username'], $cfg['password'], $options);
                return self::$pdo;
            } catch (PDOException $e) {
                $lastError = $e;
            }
        }

        throw new PDOException(
            'Error de conexión: ' . ($lastError ? $lastError->getMessage() : 'No se pudo conectar a MySQL'),
            (int)($lastError ? $lastError->getCode() : 0),
            $lastError
        );
    }
}
