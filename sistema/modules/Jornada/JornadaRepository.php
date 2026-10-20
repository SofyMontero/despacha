<?php
/**
 * Jornadas del paseador.
 * Requiere tablas de 003_jornadas_visitas.sql
 */

class JornadaRepository
{
    private PDO $pdo;

    public function __construct(?PDO $pdo = null)
    {
        if ($pdo) {
            $this->pdo = $pdo;
        } else {
            $db = new Database();
            $this->pdo = $db->connect();
        }
    }

    public function esquemaListo(): bool
    {
        return $this->tablaExiste('jornadas');
    }

    public function actualDelPaseador(int $idPaseador): ?array
    {
        $this->assertEsquema();
        $stmt = $this->pdo->prepare("
            SELECT * FROM jornadas
            WHERE id_paseador = ? AND estado = 'en_curso'
            ORDER BY iniciada_en DESC
            LIMIT 1
        ");
        $stmt->execute([$idPaseador]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }

    public function iniciar(int $idPaseador, int $idEmpresa, ?float $lat, ?float $lng): array
    {
        $this->assertEsquema();
        $activa = $this->actualDelPaseador($idPaseador);
        if ($activa) {
            throw new Exception('Ya tienes una jornada en curso');
        }

        $stmt = $this->pdo->prepare("
            INSERT INTO jornadas (
                id_empresa, id_paseador, estado, iniciada_en, lat_inicio, lng_inicio
            ) VALUES (?, ?, 'en_curso', NOW(), ?, ?)
        ");
        $stmt->execute([
            $idEmpresa > 0 ? $idEmpresa : 1,
            $idPaseador,
            $lat,
            $lng,
        ]);

        $id = (int)$this->pdo->lastInsertId();
        return $this->obtener($id);
    }

    public function finalizar(int $idJornada, int $idPaseador, ?float $lat, ?float $lng): array
    {
        $this->assertEsquema();
        $jornada = $this->obtener($idJornada);
        if (!$jornada) {
            throw new Exception('Jornada no encontrada');
        }
        if ((int)$jornada['id_paseador'] !== $idPaseador) {
            throw new Exception('No puedes finalizar la jornada de otro paseador');
        }
        if ($jornada['estado'] !== 'en_curso') {
            throw new Exception('La jornada no está en curso');
        }

        $stmt = $this->pdo->prepare("
            UPDATE jornadas
            SET estado = 'finalizada',
                finalizada_en = NOW(),
                lat_fin = ?,
                lng_fin = ?
            WHERE id_jornada = ?
        ");
        $stmt->execute([$lat, $lng, $idJornada]);

        return $this->obtener($idJornada);
    }

    public function obtener(int $idJornada): ?array
    {
        $this->assertEsquema();
        $stmt = $this->pdo->prepare('SELECT * FROM jornadas WHERE id_jornada = ? LIMIT 1');
        $stmt->execute([$idJornada]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }

    public function listarPorEmpresa(int $idEmpresa, string $fecha = ''): array
    {
        $this->assertEsquema();
        if ($fecha === '') {
            $fecha = date('Y-m-d');
        }
        $stmt = $this->pdo->prepare("
            SELECT j.*, u.nombre AS paseador_nombre
            FROM jornadas j
            LEFT JOIN users u ON u.id = j.id_paseador
            WHERE j.id_empresa = ?
              AND DATE(j.iniciada_en) = ?
            ORDER BY j.iniciada_en DESC
            LIMIT 200
        ");
        $stmt->execute([$idEmpresa > 0 ? $idEmpresa : 1, $fecha]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    private function assertEsquema(): void
    {
        if (!$this->esquemaListo()) {
            throw new Exception('Falta tabla jornadas. Ejecuta sistema/sql/003_jornadas_visitas.sql');
        }
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
}
