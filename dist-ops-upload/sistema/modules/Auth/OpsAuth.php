<?php
/**
 * Autenticación por token para API ops (Variante B).
 * Independiente de $_SESSION["usuario"] del admin PHP.
 */

class OpsAuth
{
    private PDO $pdo;

    /** Roles ops permitidos (por nombre en tabla roles o users.rol legado). */
    public const ROLES_OPS = ['administrador', 'paseador', 'domiciliario', 'supervisor'];

    public function __construct(?PDO $pdo = null)
    {
        if ($pdo) {
            $this->pdo = $pdo;
        } else {
            $db = new Database('ops');
            $this->pdo = $db->connect();
        }
    }

    public function login(string $usuario, string $password): array
    {
        $usuario = trim($usuario);
        if ($usuario === '' || $password === '') {
            throw new Exception('Usuario y clave requeridos');
        }

        $stmt = $this->pdo->prepare('SELECT * FROM users WHERE usuario = ? LIMIT 1');
        $stmt->execute([$usuario]);
        $user = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$user) {
            throw new Exception('Usuario o clave incorrectos');
        }

        if (!$this->passwordMatches($password, (string)($user['password'] ?? ''))) {
            throw new Exception('Usuario o clave incorrectos');
        }

        $rolInfo = $this->resolveRol($user);
        if (!$rolInfo || !$this->esRolOps($rolInfo['nombre'])) {
            throw new Exception('Este usuario no tiene acceso a operaciones');
        }

        $token = bin2hex(random_bytes(32));
        $this->guardarToken((int)$user['id'], $token);

        return [
            'id' => (int)$user['id'],
            'nombre' => $user['nombre'] ?? $user['usuario'],
            'usuario' => $user['usuario'],
            'telefono' => $user['telefono'] ?? '',
            'idEmpresa' => isset($user['id_empresa']) ? (int)$user['id_empresa'] : null,
            'idRol' => $rolInfo['id'],
            'rol' => $rolInfo['nombre'],
            'token' => $token,
        ];
    }

    public function requireUser(): array
    {
        $token = $this->bearerToken();
        if ($token === '') {
            throw new Exception('No autorizado');
        }

        if (!$this->columnaExiste('users', 'ops_api_token')) {
            throw new Exception('Falta columna ops_api_token. Ejecuta el SQL de Fase 1.');
        }

        $stmt = $this->pdo->prepare('SELECT * FROM users WHERE ops_api_token = ? LIMIT 1');
        $stmt->execute([$token]);
        $user = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$user) {
            throw new Exception('Sesión inválida o expirada');
        }

        $rolInfo = $this->resolveRol($user);
        if (!$rolInfo || !$this->esRolOps($rolInfo['nombre'])) {
            throw new Exception('No autorizado');
        }

        $user['_rol'] = $rolInfo;
        return $user;
    }

    private function bearerToken(): string
    {
        $header = $_SERVER['HTTP_AUTHORIZATION'] ?? $_SERVER['REDIRECT_HTTP_AUTHORIZATION'] ?? '';
        if (stripos($header, 'Bearer ') === 0) {
            return trim(substr($header, 7));
        }
        $body = [];
        $raw = file_get_contents('php://input');
        if ($raw) {
            $decoded = json_decode($raw, true);
            if (is_array($decoded)) {
                $body = $decoded;
            }
        }
        return (string)($body['token'] ?? $_GET['token'] ?? '');
    }

    private function passwordMatches(string $plain, string $stored): bool
    {
        if ($stored === '') {
            return false;
        }
        if (strpos($stored, '$2y$') === 0 || strpos($stored, '$2a$') === 0) {
            return password_verify($plain, $stored);
        }
        return hash_equals($stored, $plain);
    }

    /**
     * Resuelve rol desde id_rol (nuevo) o users.rol (legado string/id).
     * @return array{id:?int,nombre:string}|null
     */
    private function resolveRol(array $user): ?array
    {
        if ($this->tablaExiste('roles')) {
            if (!empty($user['id_rol'])) {
                $stmt = $this->pdo->prepare(
                    'SELECT id_rol, rol_nombre FROM roles WHERE id_rol = ? LIMIT 1'
                );
                $stmt->execute([(int)$user['id_rol']]);
                $row = $stmt->fetch(PDO::FETCH_ASSOC);
                if ($row) {
                    return [
                        'id' => (int)$row['id_rol'],
                        'nombre' => (string)$row['rol_nombre'],
                    ];
                }
            }

            $legado = trim((string)($user['rol'] ?? ''));
            if ($legado !== '') {
                $stmt = $this->pdo->prepare(
                    'SELECT id_rol, rol_nombre FROM roles
                     WHERE id_rol = ? OR LOWER(TRIM(rol_nombre)) = LOWER(?)
                     LIMIT 1'
                );
                $stmt->execute([ctype_digit($legado) ? (int)$legado : 0, $legado]);
                $row = $stmt->fetch(PDO::FETCH_ASSOC);
                if ($row) {
                    return [
                        'id' => (int)$row['id_rol'],
                        'nombre' => (string)$row['rol_nombre'],
                    ];
                }
                return ['id' => null, 'nombre' => $legado];
            }
        }

        $legado = trim((string)($user['rol'] ?? ''));
        return $legado !== '' ? ['id' => null, 'nombre' => $legado] : null;
    }

    private function esRolOps(string $nombre): bool
    {
        return in_array(mb_strtolower(trim($nombre)), self::ROLES_OPS, true);
    }

    private function guardarToken(int $userId, string $token): void
    {
        if (!$this->columnaExiste('users', 'ops_api_token')) {
            throw new Exception('Falta columna ops_api_token. Ejecuta el SQL de Fase 1.');
        }
        $stmt = $this->pdo->prepare('UPDATE users SET ops_api_token = ? WHERE id = ?');
        $stmt->execute([$token, $userId]);
    }

    private function tablaExiste(string $tabla): bool
    {
        $stmt = $this->pdo->prepare(
            'SELECT 1 FROM information_schema.tables
             WHERE table_schema = DATABASE() AND table_name = ? LIMIT 1'
        );
        $stmt->execute([$tabla]);
        return (bool)$stmt->fetchColumn();
    }

    private function columnaExiste(string $tabla, string $columna): bool
    {
        $stmt = $this->pdo->prepare(
            'SELECT 1 FROM information_schema.columns
             WHERE table_schema = DATABASE() AND table_name = ? AND column_name = ?
             LIMIT 1'
        );
        $stmt->execute([$tabla, $columna]);
        return (bool)$stmt->fetchColumn();
    }
}
