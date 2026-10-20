<?php
/**
 * Entregas de pedidos (Variante B).
 * Requiere tablas de 005_variante_b_fase4_entregas.sql
 *
 * Entrega ≠ Pedido. Un pedido aceptado queda disponible; la fila en
 * `entregas` nace cuando un domiciliario la toma o el admin la asigna.
 * Al pasar a 'salio' el pedido queda 'enviado' (mismo valor del admin PHP).
 */

class EntregaRepository
{
    public const ESTADOS = [
        'disponible', 'aceptado', 'salio', 'en_camino',
        'entregado', 'no_entregado', 'cancelado',
    ];

    /** Desde cada estado, a cuáles se puede pasar. */
    private const TRANSICIONES = [
        'disponible' => ['cancelado'],
        'aceptado' => ['salio', 'disponible', 'cancelado'],
        'salio' => ['en_camino', 'entregado', 'no_entregado'],
        'en_camino' => ['entregado', 'no_entregado'],
        'no_entregado' => ['disponible', 'cancelado'],
        'entregado' => [],
        'cancelado' => [],
    ];

    /** Cambios que solo hace administración. */
    private const SOLO_ADMIN = ['disponible', 'cancelado'];

    private const REQUIEREN_MOTIVO = ['no_entregado', 'cancelado'];

    private const ESTADOS_PEDIDO_ACEPTADO = ['aceptado', '2'];

    private PDO $pdo;

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
        return $this->tablaExiste('entregas') && $this->tablaExiste('entrega_eventos');
    }

    /**
     * Pedidos aceptados sin entrega + entregas reabiertas (estado disponible).
     */
    public function disponibles(int $idEmpresa): array
    {
        $this->assertEsquema();
        $stmt = $this->pdo->prepare("
            SELECT p.id_pedido, p.ped_numfac, p.ped_total, p.ped_fecha, p.ped_sede,
                   p.ped_numCliente, p.ped_observacion, p.fecha_entrega_acordada,
                   c.cli_nombre, c.cli_direccion,
                   e.id_entrega, e.estado, e.num_factura
            FROM pedidos p
            LEFT JOIN entregas e ON e.id_pedido = p.id_pedido
            LEFT JOIN clientes c ON c.id_cliente = p.ped_cliente
            WHERE COALESCE(p.id_empresa, 1) = ?
              AND (
                (e.id_entrega IS NULL AND p.ped_estado IN ('aceptado', '2'))
                OR e.estado = 'disponible'
              )
            ORDER BY p.fecha_entrega_acordada IS NULL, p.fecha_entrega_acordada, p.ped_fecha
            LIMIT 200
        ");
        $stmt->execute([$this->empresa($idEmpresa)]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    /** Entregas activas del domiciliario + las cerradas hoy. */
    public function delDomiciliario(int $idDomiciliario): array
    {
        $this->assertEsquema();
        $stmt = $this->pdo->prepare($this->selectBase() . "
            WHERE e.id_domiciliario = ?
              AND (
                e.estado IN ('aceptado', 'salio', 'en_camino')
                OR DATE(e.actualizado_en) = CURDATE()
              )
            ORDER BY FIELD(e.estado, 'aceptado', 'salio', 'en_camino') DESC, e.actualizado_en DESC
        ");
        $stmt->execute([$idDomiciliario]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    public function listarPorEmpresa(int $idEmpresa, string $estado = ''): array
    {
        $this->assertEsquema();
        $sql = $this->selectBase() . ' WHERE e.id_empresa = ?';
        $params = [$this->empresa($idEmpresa)];
        if ($estado !== '') {
            $sql .= ' AND e.estado = ?';
            $params[] = $estado;
        }
        $sql .= ' ORDER BY e.actualizado_en DESC LIMIT 200';
        $stmt = $this->pdo->prepare($sql);
        $stmt->execute($params);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    public function obtener(int $idEntrega): ?array
    {
        $this->assertEsquema();
        $stmt = $this->pdo->prepare($this->selectBase() . ' WHERE e.id_entrega = ? LIMIT 1');
        $stmt->execute([$idEntrega]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }

    public function eventos(int $idEntrega): array
    {
        $this->assertEsquema();
        $stmt = $this->pdo->prepare("
            SELECT ev.*, u.nombre AS usuario_nombre
            FROM entrega_eventos ev
            LEFT JOIN users u ON u.id = ev.id_usuario
            WHERE ev.id_entrega = ?
            ORDER BY ev.registrado_en, ev.id
        ");
        $stmt->execute([$idEntrega]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    /** El domiciliario toma un pedido disponible. Solo uno gana. */
    public function tomar(int $idPedido, int $idDomiciliario, int $idEmpresa, string $numFactura = ''): array
    {
        return $this->ocupar($idPedido, $idDomiciliario, null, $idEmpresa, $numFactura);
    }

    /** El admin asigna (o reasigna antes de salir) un pedido a un domiciliario. */
    public function asignar(int $idPedido, int $idDomiciliario, int $idAdmin, int $idEmpresa, string $numFactura = ''): array
    {
        if (!$this->esDomiciliarioDeEmpresa($idDomiciliario, $idEmpresa)) {
            throw new Exception('El usuario no es domiciliario de esta empresa');
        }
        return $this->ocupar($idPedido, $idDomiciliario, $idAdmin, $idEmpresa, $numFactura);
    }

    public function cambiarEstado(
        int $idEntrega,
        int $idUsuario,
        bool $esAdmin,
        string $nuevo,
        string $motivo = '',
        string $numFactura = '',
        ?float $lat = null,
        ?float $lng = null
    ): array {
        $this->assertEsquema();
        $entrega = $this->obtener($idEntrega);
        if (!$entrega) {
            throw new Exception('Entrega no encontrada');
        }
        $actual = (string)$entrega['estado'];

        if (!in_array($nuevo, self::ESTADOS, true)) {
            throw new Exception('Estado no válido');
        }
        if (!in_array($nuevo, self::TRANSICIONES[$actual] ?? [], true)) {
            throw new Exception("No se puede pasar de '$actual' a '$nuevo'");
        }
        if (!$esAdmin) {
            if (in_array($nuevo, self::SOLO_ADMIN, true)) {
                throw new Exception('Solo administración autoriza ese cambio');
            }
            if ((int)$entrega['id_domiciliario'] !== $idUsuario) {
                throw new Exception('No autorizado: la entrega es de otro domiciliario');
            }
        }

        $motivo = trim($motivo);
        if (in_array($nuevo, self::REQUIEREN_MOTIVO, true) && $motivo === '') {
            throw new Exception('Debes escribir el motivo');
        }

        $numFactura = trim($numFactura);
        if ($numFactura === '') {
            $numFactura = trim((string)($entrega['num_factura'] ?? ''));
        }
        if ($nuevo === 'salio' && $numFactura === '') {
            throw new Exception('Escribe el número de factura antes de salir');
        }

        $sets = ['estado = ?', 'num_factura = ?', 'motivo = ?'];
        $params = [$nuevo, $numFactura !== '' ? $numFactura : null, $motivo !== '' ? $motivo : null];
        if ($nuevo === 'salio') {
            $sets[] = 'salio_en = NOW()';
        }
        if ($nuevo === 'entregado') {
            $sets[] = 'entregada_en = NOW()';
        }
        if ($nuevo === 'disponible') {
            $sets[] = 'id_domiciliario = NULL';
            $sets[] = 'asignado_por = NULL';
            $sets[] = 'aceptada_en = NULL';
            $sets[] = 'salio_en = NULL';
        }

        $this->pdo->beginTransaction();
        try {
            $params[] = $idEntrega;
            $params[] = $actual;
            $upd = $this->pdo->prepare(
                'UPDATE entregas SET ' . implode(', ', $sets) . ' WHERE id_entrega = ? AND estado = ?'
            );
            $upd->execute($params);
            if ($upd->rowCount() === 0) {
                throw new Exception('La entrega cambió mientras tanto. Recarga e intenta de nuevo');
            }

            if ($nuevo === 'salio') {
                $this->marcarPedidoEnviado((int)$entrega['id_pedido']);
            }

            $this->registrarEvento($idEntrega, $actual, $nuevo, $idUsuario, $motivo, $lat, $lng);
            $this->pdo->commit();
        } catch (Exception $e) {
            $this->pdo->rollBack();
            throw $e;
        }

        return $this->obtener($idEntrega);
    }

    public function domiciliarios(int $idEmpresa): array
    {
        $stmt = $this->pdo->prepare($this->selectDomiciliarios() . ' ORDER BY u.nombre');
        $stmt->execute([$this->empresa($idEmpresa)]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    private function ocupar(
        int $idPedido,
        int $idDomiciliario,
        ?int $idAdmin,
        int $idEmpresa,
        string $numFactura
    ): array {
        $this->assertEsquema();
        $pedido = $this->pedidoDeEmpresa($idPedido, $idEmpresa);
        $numFactura = trim($numFactura);

        $stmt = $this->pdo->prepare('SELECT * FROM entregas WHERE id_pedido = ? LIMIT 1');
        $stmt->execute([$idPedido]);
        $existente = $stmt->fetch(PDO::FETCH_ASSOC);

        $this->pdo->beginTransaction();
        try {
            if (!$existente) {
                if (!in_array((string)$pedido['ped_estado'], self::ESTADOS_PEDIDO_ACEPTADO, true)) {
                    throw new Exception('El pedido no está aceptado');
                }
                $ins = $this->pdo->prepare("
                    INSERT INTO entregas (
                        id_empresa, id_pedido, id_domiciliario, asignado_por,
                        num_factura, estado, aceptada_en
                    ) VALUES (?, ?, ?, ?, ?, 'aceptado', NOW())
                ");
                try {
                    $ins->execute([
                        $this->empresa($idEmpresa),
                        $idPedido,
                        $idDomiciliario,
                        $idAdmin,
                        $numFactura !== '' ? $numFactura : null,
                    ]);
                } catch (PDOException $e) {
                    if ($e->getCode() === '23000') {
                        throw new Exception('Otro domiciliario ya tomó esta entrega');
                    }
                    throw $e;
                }
                $idEntrega = (int)$this->pdo->lastInsertId();
                $anterior = null;
            } else {
                $idEntrega = (int)$existente['id_entrega'];
                $anterior = (string)$existente['estado'];
                // El domiciliario solo toma reabiertas; el admin también puede reasignar antes de salir.
                $permitidos = $idAdmin !== null ? ['disponible', 'aceptado'] : ['disponible'];
                if (!in_array($anterior, $permitidos, true)) {
                    throw new Exception('Esta entrega ya no está disponible');
                }
                $upd = $this->pdo->prepare("
                    UPDATE entregas
                    SET id_domiciliario = ?, asignado_por = ?, estado = 'aceptado',
                        aceptada_en = NOW(), motivo = NULL,
                        num_factura = COALESCE(?, num_factura)
                    WHERE id_entrega = ? AND estado = ?
                ");
                $upd->execute([
                    $idDomiciliario,
                    $idAdmin,
                    $numFactura !== '' ? $numFactura : null,
                    $idEntrega,
                    $anterior,
                ]);
                if ($upd->rowCount() === 0) {
                    throw new Exception('Otro domiciliario ya tomó esta entrega');
                }
            }

            $nota = $idAdmin !== null ? 'Asignada por administración' : '';
            $this->registrarEvento($idEntrega, $anterior, 'aceptado', $idAdmin ?? $idDomiciliario, $nota, null, null);
            $this->pdo->commit();
        } catch (Exception $e) {
            $this->pdo->rollBack();
            throw $e;
        }

        return $this->obtener($idEntrega);
    }

    private function marcarPedidoEnviado(int $idPedido): void
    {
        $stmt = $this->pdo->prepare("
            UPDATE pedidos SET ped_estado = 'enviado'
            WHERE id_pedido = ? AND ped_estado IN ('aceptado', '2')
        ");
        $stmt->execute([$idPedido]);
    }

    private function registrarEvento(
        int $idEntrega,
        ?string $anterior,
        string $nuevo,
        int $idUsuario,
        string $motivo,
        ?float $lat,
        ?float $lng
    ): void {
        $stmt = $this->pdo->prepare("
            INSERT INTO entrega_eventos (
                id_entrega, estado_anterior, estado_nuevo, id_usuario, motivo, lat, lng
            ) VALUES (?, ?, ?, ?, ?, ?, ?)
        ");
        $stmt->execute([$idEntrega, $anterior, $nuevo, $idUsuario, $motivo !== '' ? $motivo : null, $lat, $lng]);
    }

    private function pedidoDeEmpresa(int $idPedido, int $idEmpresa): array
    {
        $stmt = $this->pdo->prepare('
            SELECT id_pedido, ped_estado FROM pedidos
            WHERE id_pedido = ? AND COALESCE(id_empresa, 1) = ?
            LIMIT 1
        ');
        $stmt->execute([$idPedido, $this->empresa($idEmpresa)]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            throw new Exception('Pedido no encontrado');
        }
        return $row;
    }

    private function esDomiciliarioDeEmpresa(int $idUsuario, int $idEmpresa): bool
    {
        $stmt = $this->pdo->prepare($this->selectDomiciliarios() . ' AND u.id = ? LIMIT 1');
        $stmt->execute([$this->empresa($idEmpresa), $idUsuario]);
        return (bool)$stmt->fetch(PDO::FETCH_ASSOC);
    }

    /** Rol por users.id_rol o, si no tiene, por el texto legado users.rol. */
    private function selectDomiciliarios(): string
    {
        return "
            SELECT u.id, u.nombre
            FROM users u
            LEFT JOIN roles r ON r.id_rol = u.id_rol
            LEFT JOIN roles rl ON u.id_rol IS NULL
                AND LOWER(TRIM(rl.rol_nombre)) = LOWER(TRIM(u.rol))
            WHERE COALESCE(u.id_empresa, 1) = ?
              AND LOWER(TRIM(COALESCE(r.rol_nombre, rl.rol_nombre, u.rol))) = 'domiciliario'
        ";
    }

    private function selectBase(): string
    {
        return "
            SELECT e.*,
                   p.ped_numfac, p.ped_total, p.ped_fecha, p.ped_sede, p.ped_numCliente,
                   p.ped_observacion, p.ped_estado, p.fecha_entrega_acordada,
                   c.cli_nombre, c.cli_direccion,
                   u.nombre AS domiciliario_nombre
            FROM entregas e
            INNER JOIN pedidos p ON p.id_pedido = e.id_pedido
            LEFT JOIN clientes c ON c.id_cliente = p.ped_cliente
            LEFT JOIN users u ON u.id = e.id_domiciliario
        ";
    }

    private function empresa(int $idEmpresa): int
    {
        return $idEmpresa > 0 ? $idEmpresa : 1;
    }

    private function assertEsquema(): void
    {
        if (!$this->esquemaListo()) {
            throw new Exception('Faltan tablas de entregas. Ejecuta sistema/sql/005_variante_b_fase4_entregas.sql');
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
