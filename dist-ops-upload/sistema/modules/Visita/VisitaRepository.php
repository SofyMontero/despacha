<?php
/**
 * Visitas del paseador (independientes del pedido).
 */

class VisitaRepository
{
    private PDO $pdo;

    public const RESULTADOS = [
        'pedido_creado',
        'no_requiere_mercancia',
        'no_atendido',
        'otro',
    ];

    public function __construct(?PDO $pdo = null)
    {
        if ($pdo) {
            $this->pdo = $pdo;
        } else {
            $db = new Database('ops');
            $this->pdo = $db->connect();
        }
    }

    public function esquemaListo(): bool
    {
        return $this->tablaExiste('visitas');
    }

    public function registrar(
        int $idJornada,
        int $idPaseador,
        int $idEmpresa,
        int $idCliente,
        string $resultado,
        ?int $idPedido,
        ?float $lat,
        ?float $lng,
        ?float $precisionM,
        string $notas
    ): array {
        $this->assertEsquema();

        $resultado = strtolower(trim($resultado));
        if (!in_array($resultado, self::RESULTADOS, true)) {
            throw new Exception('Resultado de visita no válido');
        }
        if ($idCliente <= 0) {
            throw new Exception('idCliente requerido');
        }

        // Validar jornada del paseador
        $jornadaRepo = new JornadaRepository($this->pdo);
        $jornada = $jornadaRepo->obtener($idJornada);
        if (!$jornada) {
            throw new Exception('Jornada no encontrada');
        }
        if ((int)$jornada['id_paseador'] !== $idPaseador) {
            throw new Exception('La jornada no pertenece a este paseador');
        }
        if ($jornada['estado'] !== 'en_curso') {
            throw new Exception('Solo se registran visitas en jornada en curso');
        }

        if ($resultado === 'pedido_creado' && (!$idPedido || $idPedido <= 0)) {
            // En Fase 2 permitimos visita marcada como pedido_creado sin pedido
            // hasta que exista Fase 3; id_pedido queda NULL.
            $idPedido = null;
        }
        if ($resultado !== 'pedido_creado') {
            $idPedido = null;
        }

        $stmt = $this->pdo->prepare("
            INSERT INTO visitas (
                id_jornada, id_empresa, id_paseador, id_cliente,
                resultado, id_pedido, lat, lng, precision_m, visitada_en, notas
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, NOW(), ?)
        ");
        $stmt->execute([
            $idJornada,
            $idEmpresa > 0 ? $idEmpresa : 1,
            $idPaseador,
            $idCliente,
            $resultado,
            $idPedido,
            $lat,
            $lng,
            $precisionM,
            $notas !== '' ? $notas : null,
        ]);

        return $this->obtener((int)$this->pdo->lastInsertId());
    }

    public function listarPorJornada(int $idJornada): array
    {
        $this->assertEsquema();
        $stmt = $this->pdo->prepare("
            SELECT v.*, c.cli_nombre, c.cli_direccion
            FROM visitas v
            LEFT JOIN clientes c ON c.id_cliente = v.id_cliente
            WHERE v.id_jornada = ?
            ORDER BY v.visitada_en DESC
        ");
        $stmt->execute([$idJornada]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    public function obtener(int $idVisita): ?array
    {
        $this->assertEsquema();
        $stmt = $this->pdo->prepare("
            SELECT v.*, c.cli_nombre, c.cli_direccion
            FROM visitas v
            LEFT JOIN clientes c ON c.id_cliente = v.id_cliente
            WHERE v.id_visita = ?
            LIMIT 1
        ");
        $stmt->execute([$idVisita]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }

    private function assertEsquema(): void
    {
        if (!$this->esquemaListo()) {
            throw new Exception('Falta tabla visitas. Ejecuta sistema/sql/003_variante_b_fase2_jornadas_visitas.sql');
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
