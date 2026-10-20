
<?php
class Pedido
{
    private $db;
    private $pdo;

    public function __construct()
    {
        $this->db = new Database();
        $this->pdo = $this->db->connect();

        // Verificar conexión
        if (!$this->pdo) {
            throw new Exception('No se pudo conectar a la base de datos');
        }
    }


    public function crearPedido($datosCliente, $productos, $total, $numCliente, $observaciones, $ped_sede = null)
    {
        try {
            // Iniciar transacción
            $this->pdo->beginTransaction();

            $telefono = preg_replace('/\D+/', '', trim((string)$numCliente));
            $telefonoSin57 = (strpos($telefono, '57') === 0 && strlen($telefono) > 10)
                ? substr($telefono, 2)
                : $telefono;
            $telefonoCon57 = (strpos($telefono, '57') === 0) ? $telefono : ('57' . $telefono);

            // Resolver cliente por ID explícito o por teléfono (con/sin 57)
            $clienteInfo = null;
            if (!empty($datosCliente['id_cliente'])) {
                $clienteInfo = (int)$datosCliente['id_cliente'];
            }

            if (!$clienteInfo) {
                $sqlidClinte = "SELECT id_cliente FROM clientes
                    WHERE cli_telefono = ? OR cli_telefono = ? OR cli_telefono = ?
                    ORDER BY id_cliente ASC
                    LIMIT 1";
                $stmtCheck = $this->pdo->prepare($sqlidClinte);
                $stmtCheck->execute([$telefono, $telefonoSin57, $telefonoCon57]);
                $clienteInfo = $stmtCheck->fetchColumn();
            }

            if (!$clienteInfo) {
                throw new Exception('No se encontró el cliente con el teléfono indicado. Verifica o regístrate de nuevo.');
            }

            // Generar número de factura único
            $numeroFactura = 'PED-' . date('Ymd') . '-' . sprintf('%04d', rand(1000, 9999));

            // Verificar que el número no exista (prevenir duplicados)
            $sqlCheck = "SELECT COUNT(*) FROM pedidos WHERE ped_numfac = ?";
            $stmtCheck = $this->pdo->prepare($sqlCheck);
            $stmtCheck->execute([$numeroFactura]);

            if ($stmtCheck->fetchColumn() > 0) {
                // Si existe, generar uno nuevo
                $numeroFactura = 'PED-' . date('Ymd') . '-' . sprintf('%04d', rand(1000, 9999));
            }

            // Insertar pedido principal
            $sqlPedido = "INSERT INTO pedidos (
            ped_cliente, 
            ped_estado, 
            ped_fecha, 
            ped_numfac,
            ped_total,
            ped_numCliente,
            ped_observacion,
            ped_sede
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)";

            $stmtPedido = $this->pdo->prepare($sqlPedido);
            $stmtPedido->execute([
                $clienteInfo,
                1,
                date("Y-m-d H:i:s", strtotime("-5 hours")),
                $numeroFactura,
                $total,        // puede ser 0 (pedido solo con promos)
                $telefonoCon57,
                $observaciones,
                $ped_sede      // dirección del cliente seleccionado
            ]);

            $idPedido = $this->pdo->lastInsertId();
            if (!$idPedido) {
                throw new Exception('No se pudo obtener el ID del pedido');
            }

            // Insertar detalles del pedido
            $sqlDetalle = "INSERT INTO detalle_pedidos (
            id_pedido, 
            id_producto, 
            nombre_producto,
            tipo_producto, 
            cantidad, 
            precio_unitario, 
            subtotal
        ) VALUES (?, ?, ?, ?, ?, ?, ?)";

            $stmtDetalle = $this->pdo->prepare($sqlDetalle);

            // Agregar cantidad de ventas por producto
            $sqlVentas = "UPDATE productos SET 
            cantidad_venta = cantidad_venta + ? 
            WHERE id_producto = ?";

            $stmtVentas = $this->pdo->prepare($sqlVentas);

            foreach ($productos as $producto) {
                // Datos del producto (permitimos 0 en precios)
                $idProducto      = $producto['id'] ?? null;
                $nombre          = $producto['nombre'] ?? '';
                $tipo            = $producto['tipo'] ?? '';
                $cantidad        = (int)($producto['cantidad'] ?? 0);
                $precioUnitario  = isset($producto['precio_unitario']) && is_numeric($producto['precio_unitario'])
                    ? (float)$producto['precio_unitario'] : 0.0;

                // Recalcular subtotal en servidor (0 si promo)
                $subtotal = $precioUnitario * $cantidad;

                // Validaciones: 0 es válido, negativo NO
                if ($nombre === '' || $cantidad <= 0) {
                    throw new Exception("Datos inválidos en producto: $nombre");
                }
                if ($precioUnitario < 0 || $subtotal < 0) {
                    throw new Exception("Importes negativos en producto: $nombre");
                }

                // Insert detalle
                $stmtDetalle->execute([
                    $idPedido,
                    $idProducto,
                    $nombre,
                    $tipo,
                    $cantidad,
                    $precioUnitario,   // puede ser 0 (promo)
                    $subtotal          // puede ser 0 (promo)
                ]);

                // Actualizar contador de ventas (si no quieres sumar promos, pon un if($precioUnitario>0){...})
                $stmtVentas->execute([
                    $cantidad,
                    $idProducto
                ]);
            }

            // Confirmar transacción
            $this->pdo->commit();

            // Log del pedido creado
            error_log("Pedido creado exitosamente: ID $idPedido, Factura: $numeroFactura, Total: $total");

            return $idPedido;
        } catch (Exception $e) {
            // Revertir transacción en caso de error
            $this->pdo->rollBack();
            error_log("Error en crearPedido: " . $e->getMessage());
            throw $e; // Re-lanzar la excepción para manejo en el controlador
        }
    }


    public function obtenerPedidosCliente($nombreCliente, $limite = 50)
    {
        try {
            $sql = "SELECT p.*, COUNT(dp.id_detalle) as total_productos 
                    FROM pedidos p 
                    LEFT JOIN detalle_pedidos dp ON p.id_pedido = dp.id_pedido 
                    WHERE p.ped_cliente LIKE ? 
                    GROUP BY p.id_pedido 
                    ORDER BY p.ped_fecha DESC 
                    LIMIT ?";
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute(['%' . $nombreCliente . '%', $limite]);
            return $stmt->fetchAll(PDO::FETCH_ASSOC);
        } catch (Exception $e) {
            error_log("Error en obtenerPedidosCliente: " . $e->getMessage());
            return [];
        }
    }

    public function obtenerDetallePedido($idPedido)
    {
        try {
            $sql = "SELECT dp.*, p.ped_cliente, p.ped_estado, p.ped_fecha, p.ped_numfac, p.ped_total
                    FROM detalle_pedidos dp 
                    INNER JOIN pedidos p ON dp.id_pedido = p.id_pedido 
                    WHERE dp.id_pedido = ?
                    ORDER BY dp.id_detalle";
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute([$idPedido]);
            return $stmt->fetchAll(PDO::FETCH_ASSOC);
        } catch (Exception $e) {
            error_log("Error en obtenerDetallePedido: " . $e->getMessage());
            return [];
        }
    }

    public function obtenerPedidoPorId($idPedido)
    {
        try {
            $sql = "SELECT p.*, COUNT(dp.id_detalle) as total_productos 
                    FROM pedidos p 
                    LEFT JOIN detalle_pedidos dp ON p.id_pedido = dp.id_pedido 
                    WHERE p.id_pedido = ? 
                    GROUP BY p.id_pedido";
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute([$idPedido]);
            return $stmt->fetch(PDO::FETCH_ASSOC);
        } catch (Exception $e) {
            error_log("Error en obtenerPedidoPorId: " . $e->getMessage());
            return false;
        }
    }

    public function actualizarEstadoPedido($idPedido, $nuevoEstado, $comentario = '')
    {
        try {
            $this->pdo->beginTransaction();

            // Obtener estado actual
            $sqlActual = "SELECT ped_estado FROM pedidos WHERE id_pedido = ?";
            $stmtActual = $this->pdo->prepare($sqlActual);
            $stmtActual->execute([$idPedido]);
            $estadoActual = $stmtActual->fetchColumn();

            if (!$estadoActual) {
                throw new Exception("Pedido no encontrado: $idPedido");
            }

            // Actualizar estado
            $sql = "UPDATE pedidos SET ped_estado = ?, ped_fecha_actualizacion = NOW() WHERE id_pedido = ?";
            $stmt = $this->pdo->prepare($sql);
            $resultado = $stmt->execute([$nuevoEstado, $idPedido]);

            // Registrar en historial si existe la tabla
            if ($resultado && $estadoActual !== $nuevoEstado) {
                try {
                    $sqlHistorial = "INSERT INTO historial_pedidos (id_pedido, estado_anterior, estado_nuevo, comentario, fecha_cambio) 
                                     VALUES (?, ?, ?, ?, NOW())";
                    $stmtHistorial = $this->pdo->prepare($sqlHistorial);
                    $comentarioFinal = $comentario ?: "Estado cambiado de $estadoActual a $nuevoEstado";
                    $stmtHistorial->execute([$idPedido, $estadoActual, $nuevoEstado, $comentarioFinal]);
                } catch (Exception $e) {
                    error_log("Tabla historial_pedidos no disponible: " . $e->getMessage());
                }
            }

            $this->pdo->commit();
            return $resultado;
        } catch (Exception $e) {
            $this->pdo->rollBack();
            error_log("Error en actualizarEstadoPedido: " . $e->getMessage());
            return false;
        }
    }

    public function obtenerTodosPedidos($limite = 50, $offset = 0)
    {
        try {
            $sql = "SELECT p.*, COUNT(dp.id_detalle) as total_productos
                    FROM pedidos p 
                    LEFT JOIN detalle_pedidos dp ON p.id_pedido = dp.id_pedido 
                    GROUP BY p.id_pedido 
                    ORDER BY p.ped_fecha DESC 
                    LIMIT ? OFFSET ?";
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute([$limite, $offset]);
            return $stmt->fetchAll(PDO::FETCH_ASSOC);
        } catch (Exception $e) {
            error_log("Error en obtenerTodosPedidos: " . $e->getMessage());
            return [];
        }
    }

    public function obtenerEstadisticas()
    {
        try {
            $sql = "SELECT 
                        COUNT(*) as total_pedidos,
                        COUNT(CASE WHEN ped_estado = 'pendiente' THEN 1 END) as pedidos_pendientes,
                        COUNT(CASE WHEN ped_estado = 'entregado' THEN 1 END) as pedidos_entregados,
                        COUNT(CASE WHEN ped_estado = 'procesando' THEN 1 END) as pedidos_procesando,
                        COUNT(CASE WHEN ped_estado = 'cancelado' THEN 1 END) as pedidos_cancelados,
                        SUM(CASE WHEN ped_total IS NOT NULL THEN ped_total ELSE 0 END) as total_ventas
                    FROM pedidos 
                    WHERE ped_fecha >= DATE_SUB(NOW(), INTERVAL 30 DAY)";
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute();
            return $stmt->fetch(PDO::FETCH_ASSOC);
        } catch (Exception $e) {
            error_log("Error en obtenerEstadisticas: " . $e->getMessage());
            return [
                'total_pedidos' => 0,
                'pedidos_pendientes' => 0,
                'pedidos_entregados' => 0,
                'pedidos_procesando' => 0,
                'pedidos_cancelados' => 0,
                'total_ventas' => 0
            ];
        }
    }

    public function buscarPedidos($criterios = [])
    {
        try {
            $sql = "SELECT p.*, COUNT(dp.id_detalle) as total_productos 
                    FROM pedidos p 
                    LEFT JOIN detalle_pedidos dp ON p.id_pedido = dp.id_pedido 
                    WHERE 1=1";
            $params = [];

            if (!empty($criterios['estado'])) {
                $sql .= " AND p.ped_estado = ?";
                $params[] = $criterios['estado'];
            }

            if (!empty($criterios['fecha_desde'])) {
                $sql .= " AND p.ped_fecha >= ?";
                $params[] = $criterios['fecha_desde'];
            }

            if (!empty($criterios['fecha_hasta'])) {
                $sql .= " AND p.ped_fecha <= ?";
                $params[] = $criterios['fecha_hasta'];
            }

            if (!empty($criterios['cliente'])) {
                $sql .= " AND p.ped_cliente LIKE ?";
                $params[] = '%' . $criterios['cliente'] . '%';
            }

            if (!empty($criterios['numero_factura'])) {
                $sql .= " AND p.ped_numfac LIKE ?";
                $params[] = '%' . $criterios['numero_factura'] . '%';
            }

            $sql .= " GROUP BY p.id_pedido ORDER BY p.ped_fecha DESC";

            if (!empty($criterios['limite'])) {
                $sql .= " LIMIT ?";
                $params[] = intval($criterios['limite']);
            }

            $stmt = $this->pdo->prepare($sql);
            $stmt->execute($params);
            return $stmt->fetchAll(PDO::FETCH_ASSOC);
        } catch (Exception $e) {
            error_log("Error en buscarPedidos: " . $e->getMessage());
            return [];
        }
    }

    public function calcularTotalPedido($idPedido)
    {
        try {
            $sql = "SELECT SUM(subtotal) as total FROM detalle_pedidos WHERE id_pedido = ?";
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute([$idPedido]);
            $resultado = $stmt->fetch(PDO::FETCH_ASSOC);
            return floatval($resultado['total'] ?? 0);
        } catch (Exception $e) {
            error_log("Error en calcularTotalPedido: " . $e->getMessage());
            return 0;
        }
    }

    public function validarPedido($productos, $total)
    {
        $errores = [];

        // Validar que hay productos
        if (empty($productos)) {
            $errores[] = 'No hay productos en el pedido';
        }

        // Validar cada producto
        foreach ($productos as $index => $producto) {
            if (empty($producto['nombre'])) {
                $errores[] = "Producto en posición $index no tiene nombre";
            }
            if (empty($producto['tipo']) || !in_array($producto['tipo'], ['unidad', 'paca'])) {
                $errores[] = "Producto en posición $index tiene tipo inválido";
            }
            if (!isset($producto['cantidad']) || intval($producto['cantidad']) <= 0) {
                $errores[] = "Producto en posición $index tiene cantidad inválida";
            }
            if (!isset($producto['precio_unitario']) || floatval($producto['precio_unitario']) <= 0) {
                $errores[] = "Producto en posición $index tiene precio inválido";
            }
        }

        // Validar total
        if ($total <= 0) {
            $errores[] = 'El total del pedido debe ser mayor a 0';
        }

        return $errores;
    }
    public function enviarConfirmacion($idPromo, $descripcion, $imagen, $telefono, $plantilla): array
    {
        $url = "https://multilicoreschapinero.com/sistema/services/enviarWhatsapp.php";

        $data = [
            'telefono' => $telefono,
            'plantilla' => "$plantilla",
            'texto' => "$idPromo",
            'texto1' => "$descripcion"

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
}
