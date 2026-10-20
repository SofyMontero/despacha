<?php
class solicitud
{
    private $db;
    private $pdo;

    public function __construct()
    {
        $this->db = new Database();
        $this->pdo = $this->db->connect();
    }

    /**
     * Obtener todos los pedidos pendientes
     */
    public function obtenerPedidosPendientes($fechaIni,$fechaFin)
    {
        $query = $this->pdo->prepare("
             SELECT 
                 P.id_pedido, 
                 P.ped_numfac, 
                 P.ped_cliente, 
                 P.ped_fecha, 
                 P.ped_total, 
                 P.ped_estado,
                 P.ped_numCliente,
                 P.ped_sede,
                 B.nombre_bar
             FROM pedidos P
             INNER JOIN clientes C ON C.id_cliente = P.ped_cliente
             INNER JOIN bares B ON B.id_bar = C.cli_Bar 
             WHERE (
                 ped_estado = 'pendiente' 
                 OR ped_estado = '1' 
                 OR ped_estado = 1
             ) AND DATE(ped_fecha) >= :inicio AND DATE(ped_fecha) <= :fin             
             ORDER BY ped_fecha DESC
            ");

        $query->execute([
            'inicio' => $fechaIni,
            'fin' => $fechaFin
        ]);
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    /**
     * Obtener todos los pedidos aceptados
     */
    public function obtenerPedidosAceptados($fechaIni,$fechaFin)
    {
        $query = $this->pdo->prepare("
            SELECT 
                P.id_pedido, 
                P.ped_numfac, 
                P.ped_cliente, 
                P.ped_fecha, 
                P.ped_total, 
                P.ped_estado,
                P.ped_numCliente,
                P.ped_sede,
                B.nombre_bar
            FROM pedidos P
            INNER JOIN clientes C ON C.id_cliente = P.ped_cliente
            INNER JOIN bares B ON B.id_bar = C.cli_Bar 
            WHERE (ped_estado = 'aceptado' OR ped_estado = '2' OR ped_estado = 2 OR ped_estado = 'enviado' OR ped_estado in ( 'finalizaCredito','finalizaContado')) AND DATE(ped_fecha) >= :inicio AND DATE(ped_fecha) <= :fin
            ORDER BY ped_fecha DESC
        ");

        $query->execute([
            'inicio' => $fechaIni,
            'fin' => $fechaFin
        ]);
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    /**
     * Obtener productos de un pedido específico
     */
    public function obtenerProductosPedido($pedido_id)
    {
        $query = $this->pdo->prepare("         

                 SELECT 
                      DT.id_producto,
                      DT.nombre_producto,
                      DT.tipo_producto,
                      DT.cantidad,
                      DT.precio_unitario,
                      DT.subtotal,
                      COALESCE(P.codigo_productos, PR.codigo) AS codigo_productos,
                      PD.ped_observacion
                 FROM detalle_pedidos DT
                 LEFT JOIN productos P ON DT.id_producto    = P.id_producto
                LEFT JOIN promociones PR ON DT.id_producto    = PR.codigo
                INNER JOIN pedidos    PD ON PD.id_pedido     = DT.id_pedido
                WHERE DT.id_pedido = :pedido_id
                ORDER BY DT.id_detalle;
                ");

        $query->execute(['pedido_id' => $pedido_id]);
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    /**
     * Obtener un pedido específico por ID
     */
    public function obtenerPedidoPorId($pedido_id)
    {
        $query = $this->pdo->prepare("
            SELECT * FROM pedidos 
            WHERE id_pedido = :pedido_id
        ");

        $query->execute(['pedido_id' => $pedido_id]);
        return $query->fetch(PDO::FETCH_ASSOC);
    }

    /**
     * Aceptar un pedido (versión simplificada)
     */
    public function aceptarPedido($pedido_id)
    {
        $query = $this->pdo->prepare("
            UPDATE pedidos 
            SET ped_estado = 'aceptado'
            WHERE id_pedido = :pedido_id
        ");

        return $query->execute(['pedido_id' => $pedido_id]);
    }

    /**
     * Rechazar un pedido (versión simplificada)
     */
    public function rechazarPedido($pedido_id)
    {
        $query = $this->pdo->prepare("
            UPDATE pedidos 
            SET ped_estado = 'rechazado'
            WHERE id_pedido = :pedido_id
        ");

        return $query->execute(['pedido_id' => $pedido_id]);
    }

    /**
     * Cambiar estado de un pedido
     */
    public function cambiarEstadoPedido($pedido_id, $nuevo_estado)
    {
        $query = $this->pdo->prepare("
            UPDATE pedidos 
            SET ped_estado = :nuevo_estado
            WHERE id_pedido = :pedido_id
        ");

        return $query->execute([
            'pedido_id' => $pedido_id,
            'nuevo_estado' => $nuevo_estado
        ]);
    }

    /**
     * Contar pedidos por estado
     */
    public function contarPedidosPorEstado($estado)
    {
        if ($estado === 'pendiente') {
            $query = $this->pdo->prepare("
                SELECT COUNT(*) as total 
                FROM pedidos 
                WHERE ped_estado = 'pendiente' OR ped_estado = '1' OR ped_estado = 1
            ");
        } else {
            $query = $this->pdo->prepare("
                SELECT COUNT(*) as total 
                FROM pedidos 
                WHERE ped_estado = :estado
            ");
            $query->bindParam(':estado', $estado);
        }

        $query->execute();
        $resultado = $query->fetch(PDO::FETCH_ASSOC);
        return $resultado['total'];
    }

    /**
     * Obtener estadísticas de pedidos
     */
    public function obtenerEstadisticasPedidos()
    {
        $query = $this->pdo->prepare("
            SELECT 
                ped_estado,
                COUNT(*) as cantidad,
                SUM(ped_total) as total_ventas
            FROM pedidos 
            GROUP BY ped_estado
        ");

        $query->execute();
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    /**
     * Buscar pedidos por cliente
     */
    public function buscarPedidosPorCliente($cliente)
    {
        $query = $this->pdo->prepare("
            SELECT 
                id_pedido, 
                ped_numfac, 
                ped_cliente, 
                ped_fecha, 
                ped_total, 
                ped_estado
            FROM pedidos 
            WHERE ped_cliente LIKE :cliente
            ORDER BY ped_fecha DESC
        ");

        $query->execute(['cliente' => "%$cliente%"]);
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    /**
     * Obtener pedidos por rango de fechas
     */
    public function obtenerPedidosPorFecha($fecha_inicio, $fecha_fin, $estado = null)
    {
        $sql = "
            SELECT 
                id_pedido, 
                ped_numfac, 
                ped_cliente, 
                ped_fecha, 
                ped_total, 
                ped_estado
            FROM pedidos 
            WHERE DATE(ped_fecha) BETWEEN :fecha_inicio AND :fecha_fin
        ";

        $params = [
            'fecha_inicio' => $fecha_inicio,
            'fecha_fin' => $fecha_fin
        ];

        if ($estado) {
            $sql .= " AND ped_estado = :estado";
            $params['estado'] = $estado;
        }

        $sql .= " ORDER BY ped_fecha DESC";

        $query = $this->pdo->prepare($sql);
        $query->execute($params);
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }
    public function obtenerPedidosRechazados($fechaIni,$fechaFin)
    {
        $query = $this->pdo->prepare("
            SELECT 
                P.id_pedido, 
                P.ped_numfac, 
                P.ped_cliente, 
                P.ped_fecha, 
                P.ped_total, 
                P.ped_estado,
                P.ped_numCliente,
                P.ped_sede,
                B.nombre_bar
            FROM pedidos P
            INNER JOIN clientes C ON C.id_cliente = P.ped_cliente
            INNER JOIN bares B ON B.id_bar = C.cli_Bar 
            WHERE (ped_estado = 'rechazado' OR ped_estado = '3' OR ped_estado = 3) AND DATE(ped_fecha) >= :inicio AND DATE(ped_fecha) <= :fin
            ORDER BY ped_fecha DESC
        ");

        $query->execute([
            'inicio' => $fechaIni,
            'fin' => $fechaFin
        ]);
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }
    public function enviarPromo($idPromo, $descripcion, $imagen, $telefono, $plantilla): array
    {
        $url = "https://multilicoreschapinero.com/sistema/services/enviarWhatsapp.php";

        $data = [
            'telefono' => $telefono,
            'texto' => "$descripcion",
            'imagen1' => "", // opcional
            'plantilla' => "$plantilla"
        ];

        $data_json = json_encode($data);

        $curl = curl_init();

        curl_setopt_array($curl, [
            CURLOPT_URL => $url,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $data_json,
            CURLOPT_HTTPHEADER => [
                'Content-Type: application/json',
                'Authorization: Bearer Multilicoreslicor25'
            ],
        ]);

        $response = curl_exec($curl);
        $error = curl_error($curl);
        curl_close($curl);

        $resultado = $error ?: $response;

        $resultados[] = [
            'cliente' => $telefono,
            'telefono' => $telefono,
            'resultado' => $resultado
        ];

        // 📝 Crear log de la solicitud
        $logData = "=============================\n";
        $logData .= "Fecha: " . date("Y-m-d H:i:s") . "\n";
        $logData .= "Telefono: $telefono\n";
        $logData .= "Plantilla: $plantilla\n";
        $logData .= "Texto: $descripcion\n";
        $logData .= "Data JSON Enviado: $data_json\n";
        $logData .= "Respuesta: $resultado\n";
        $logData .= "=============================\n\n";

        file_put_contents(__DIR__ . "/log_envios.txt", $logData, FILE_APPEND);

        return $resultados;
    }
    public function buscarSugerencias($termino)
    {
        $query = $this->pdo->prepare("
            SELECT id_cliente, cli_nombre 
            FROM clientes 
            WHERE cli_nombre LIKE :termino 
            ORDER BY cli_nombre 
            LIMIT 10
        ");
        $query->bindValue(':termino', '%' . $termino . '%');
        $query->execute();

        return $query->fetchAll(PDO::FETCH_ASSOC);
    }
}
