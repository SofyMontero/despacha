<?php
/**
 * Crea un pedido desde una visita de paseador.
 * Reutiliza Pedido::crearPedido (mismo núcleo que el catálogo)
 * y luego enriquece columnas aditivas de Fase 3 si existen.
 * NO modifica el contrato de sistema/api/pedido.php.
 */

require_once __DIR__ . '/../../models/PedidoModel.php';

class PedidoDesdeVisita
{
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

    /**
     * @param array $productos mismo shape que catálogo: id, nombre, tipo, cantidad, precio_unitario
     */
    public function crear(
        int $idVisita,
        int $idPaseador,
        array $productos,
        float $total,
        string $fechaEntregaAcordada,
        string $observaciones = ''
    ): array {
        $visitaRepo = new VisitaRepository($this->pdo);
        $visita = $visitaRepo->obtener($idVisita);
        if (!$visita) {
            throw new Exception('Visita no encontrada');
        }
        if ((int)$visita['id_paseador'] !== $idPaseador) {
            throw new Exception('La visita no pertenece a este paseador');
        }
        if (!empty($visita['id_pedido'])) {
            throw new Exception('Esta visita ya tiene un pedido asociado');
        }

        $idCliente = (int)$visita['id_cliente'];
        $cli = $this->pdo->prepare(
            'SELECT id_cliente, cli_telefono, cli_direccion FROM clientes WHERE id_cliente = ? LIMIT 1'
        );
        $cli->execute([$idCliente]);
        $cliente = $cli->fetch(PDO::FETCH_ASSOC);
        if (!$cliente) {
            throw new Exception('Cliente de la visita no encontrado');
        }

        if ($fechaEntregaAcordada === '' || !preg_match('/^\d{4}-\d{2}-\d{2}$/', $fechaEntregaAcordada)) {
            throw new Exception('fechaEntregaAcordada inválida (YYYY-MM-DD)');
        }
        if (empty($productos)) {
            throw new Exception('Debes incluir al menos un producto');
        }

        $pedidoModel = new Pedido();
        // Pedido::crearPedido devuelve id_pedido (int). No tocar api/pedido.php.
        $idPedido = (int)$pedidoModel->crearPedido(
            ['id_cliente' => $idCliente],
            $productos,
            $total,
            (string)$cliente['cli_telefono'],
            $observaciones !== '' ? $observaciones : 'Pedido desde visita paseador #' . $idVisita,
            $cliente['cli_direccion'] ?? null
        );
        if ($idPedido <= 0) {
            throw new Exception('No se pudo obtener el id del pedido creado');
        }

        $this->enriquecerPedido(
            $idPedido,
            $idVisita,
            (int)($visita['id_empresa'] ?? 1),
            $fechaEntregaAcordada
        );

        // Vincular visita → pedido
        if ($visitaRepo->esquemaListo()) {
            $upd = $this->pdo->prepare("
                UPDATE visitas
                SET id_pedido = ?, resultado = 'pedido_creado'
                WHERE id_visita = ?
            ");
            $upd->execute([$idPedido, $idVisita]);
        }

        return [
            'idPedido' => $idPedido,
            'idVisita' => $idVisita,
            'fechaEntregaAcordada' => $fechaEntregaAcordada,
            'origen' => 'paseador',
        ];
    }

    private function enriquecerPedido(
        int $idPedido,
        int $idVisita,
        int $idEmpresa,
        string $fechaEntrega
    ): void {
        $sets = [];
        $params = [];

        if ($this->columnaExiste('pedidos', 'ped_origen')) {
            $sets[] = 'ped_origen = ?';
            $params[] = 'paseador';
        }
        if ($this->columnaExiste('pedidos', 'id_visita')) {
            $sets[] = 'id_visita = ?';
            $params[] = $idVisita;
        }
        if ($this->columnaExiste('pedidos', 'fecha_entrega_acordada')) {
            $sets[] = 'fecha_entrega_acordada = ?';
            $params[] = $fechaEntrega;
        }
        if ($this->columnaExiste('pedidos', 'id_empresa')) {
            $sets[] = 'id_empresa = ?';
            $params[] = $idEmpresa > 0 ? $idEmpresa : 1;
        }

        if (!$sets) {
            return; // SQL Fase 3 aún no corrido; el pedido igual quedó creado
        }

        $params[] = $idPedido;
        $sql = 'UPDATE pedidos SET ' . implode(', ', $sets) . ' WHERE id_pedido = ?';
        $this->pdo->prepare($sql)->execute($params);
    }

    private function columnaExiste(string $tabla, string $columna): bool
    {
        $stmt = $this->pdo->prepare(
            'SELECT 1 FROM information_schema.columns
             WHERE table_schema = DATABASE() AND table_name = ? AND column_name = ? LIMIT 1'
        );
        $stmt->execute([$tabla, $columna]);
        return (bool)$stmt->fetchColumn();
    }
}
