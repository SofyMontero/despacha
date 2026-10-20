<?php
require_once "../models/database.php";
header('Content-Type: application/json');

$data = json_decode(file_get_contents("php://input"), true);

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
  try {
    $db = new Database();
    $conexion = $db->connect(); 

    $fecha       =    date("Y-m-d H:i:s", strtotime("-5 hours"));
    $cliente_id  = $data['cliente_id'];
    $num_factura = $data['num_factura'];
    $ingreso     = $data['ingreso'] ?? 0;
    $egreso      = $data['egreso'] ?? 0;
    $estado      = $data['estado'];
    $medio_pago  = $data['medio_pago'];
    $validado    = !empty($data['validado']) ? 1 : 0;

    $sql = "INSERT INTO pedidos (
      ped_fecha, ped_cliente, ped_factura,
      ped_ingreso, ped_egreso, ped_estado,
      ped_medio_pago, ped_validado
    ) VALUES (
      :fecha, :cliente_id, :num_factura,
      :ingreso, :egreso, :estado,
      :medio_pago, :validado
    )";

    $stmt = $conexion->prepare($sql);
    $stmt->bindParam(':fecha', $fecha);
    $stmt->bindParam(':cliente_id', $cliente_id);
    $stmt->bindParam(':num_factura', $num_factura);
    $stmt->bindParam(':ingreso', $ingreso);
    $stmt->bindParam(':egreso', $egreso);
    $stmt->bindParam(':estado', $estado);
    $stmt->bindParam(':medio_pago', $medio_pago);
    $stmt->bindParam(':validado', $validado);

    if ($stmt->execute()) {
      echo json_encode(['success' => true]);
    } else {
      throw new Exception("Error al insertar en base de datos.");
    }

  } catch (Exception $e) {
    error_log("Error: " . $e->getMessage());
    echo json_encode(['success' => false, 'message' => $e->getMessage()]);
  }
} else {
  echo json_encode(['success' => false, 'message' => 'Método no permitido']);
}
