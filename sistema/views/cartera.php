<?php
include_once "header.php";
require_once "../models/database.php";
$conexion = (new Database())->connect();
$fecha = date('Y-m-d');

// Procesamiento del formulario de eliminación
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['action']) && $_POST['action'] === 'delete') {
	try {
		$delete_id = $_POST['delete_id'];

		// Validar que el ID existe
		if (empty($delete_id)) {
			throw new Exception("ID inválido para eliminación");
		}

		// Eliminar de la base de datos
		$sql_delete = "DELETE FROM pedidos WHERE ped_id = :id";
		$stmt = $conexion->prepare($sql_delete);
		$stmt->bindParam(':id', $delete_id);

		if ($stmt->execute()) {
			// Redirigir para evitar reenvío del formulario
			header("Location: " . $_SERVER['PHP_SELF'] . "?deleted=true");
			exit;
		} else {
			throw new Exception("Error al eliminar el registro");
		}
	} catch (Exception $e) {
		$error_message = "Error al eliminar: " . $e->getMessage();
		error_log("Error en eliminación: " . $e->getMessage());
	}
}

// Inicializar variables de filtro
$fecha_inicio = isset($_GET['fecha_inicio']) ? $_GET['fecha_inicio'] : '';
$fecha_fin = isset($_GET['fecha_fin']) ? $_GET['fecha_fin'] : '';
$nit_cliente = isset($_GET['nit_cliente']) ? $_GET['nit_cliente'] : '';
$estado = isset($_GET['estado']) ? $_GET['estado'] : '';
$medio_pago = isset($_GET['medioPago']) ? $_GET['medioPago'] : '';

// Construir la consulta SQL base para consultar registros
$sql = "SELECT P.*, 
        C.nombre AS nombre_cliente, 
        E.estado_nombre AS nombre_estado, 
        MP.md_pago_nombre AS nombre_medio_pago
        FROM pedidos P
        LEFT JOIN clientes C ON C.id = P.ped_cliente
        LEFT JOIN estados E ON E.estados_id = P.ped_estado
        LEFT JOIN medio_pago MP ON MP.md_pago_id = P.ped_medio_pago"; // Inicio de cláusula WHERE que permite concatenar condiciones

// Añadir condiciones de filtro si se proporcionan
if (!empty($fecha_inicio)) {
	$sql .= " AND P.ped_fecha >= :fecha_inicio";
}
if (!empty($fecha_fin)) {
	$sql .= " AND P.ped_fecha <= :fecha_fin";
}
if (!empty($nit_cliente)) {
	$sql .= " AND P.ped_cliente = :nit_cliente";
}
if (!empty($estado)) {
	$sql .= " AND P.ped_estado = :estado";
}
if (!empty($medio_pago)) {
	$sql .= " AND P.ped_medio_pago = :medio_pago";
}

// Ordenar por ID o fecha
$sql .= " ORDER BY P.ped_fecha DESC";

// Preparar y ejecutar la consulta
try {
	$stmt = $conexion->prepare($sql);

	// Vincular parámetros si existen
	if (!empty($fecha_inicio)) {
		$stmt->bindParam(':fecha_inicio', $fecha_inicio);
	}
	if (!empty($fecha_fin)) {
		$stmt->bindParam(':fecha_fin', $fecha_fin);
	}
	if (!empty($nit_cliente)) {
		$stmt->bindParam(':nit_cliente', $nit_cliente);
	}
	if (!empty($estado)) {
		$stmt->bindParam(':estado', $estado);
	}
	if (!empty($medio_pago)) {
		$stmt->bindParam(':medio_pago', $medio_pago);
	}

	$stmt->execute();
	$resultado = $stmt;
} catch (Exception $e) {
	$error_message = "Error en la consulta: " . $e->getMessage();
	error_log("Error en consulta: " . $e->getMessage());
	$resultado = null;
}

// Consulta para obtener clientes para el dropdown
try {
	$sql_clientes = "SELECT id, nombre FROM clientes ORDER BY nombre";
	$stmt_clientes = $conexion->prepare($sql_clientes);
	$stmt_clientes->execute();
	$clientes = $stmt_clientes;
} catch (Exception $e) {
	error_log("Error al obtener clientes: " . $e->getMessage());
	$clientes = null;
}

// Consulta para obtener estados
try {
	$sql_estados = "SELECT estados_id, estado_nombre FROM estados ORDER BY estado_nombre";
	$stmt_estados = $conexion->prepare($sql_estados);
	$stmt_estados->execute();
	$estados = $stmt_estados;
} catch (Exception $e) {
	error_log("Error al obtener estados: " . $e->getMessage());
	$estados = null;
}

// Consulta para obtener medios de pago
try {
	$sql_medios_pago = "SELECT md_pago_id, md_pago_nombre FROM medio_pago ORDER BY md_pago_nombre";
	$stmt_medios_pago = $conexion->prepare($sql_medios_pago);
	$stmt_medios_pago->execute();
	$medios_pago = $stmt_medios_pago;
} catch (Exception $e) {
	error_log("Error al obtener medios de pago: " . $e->getMessage());
	$medios_pago = null;
}
?>
<!-- Page header -->
<div class="full-box page-header">
	<h3 class="text-left">
		<i class="fas fa-coins fa-fw"></i> &nbsp; Caja y Pedidos
	</h3>
</div>

<!-- Mensaje de éxito después de insertar -->
<?php if (isset($_GET['inserted']) && $_GET['inserted'] === 'true'): ?>
	<div class="alert alert-success alert-dismissible fade show" role="alert">
		<strong>¡Éxito!</strong> El registro ha sido añadido correctamente.
		<button type="button" class="close" data-dismiss="alert" aria-label="Close">
			<span aria-hidden="true">&times;</span>
		</button>
	</div>
<?php endif; ?>

<!-- Mensaje de éxito después de eliminar -->
<?php if (isset($_GET['deleted']) && $_GET['deleted'] === 'true'): ?>
	<div class="alert alert-success alert-dismissible fade show" role="alert">
		<strong>¡Éxito!</strong> El registro ha sido eliminado correctamente.
		<button type="button" class="close" data-dismiss="alert" aria-label="Close">
			<span aria-hidden="true">&times;</span>
		</button>
	</div>
<?php endif; ?>

<!-- Mensaje de error si hay problemas -->
<?php if (isset($error_message)): ?>
	<div class="alert alert-danger alert-dismissible fade show" role="alert">
		<strong>¡Error!</strong> <?php echo $error_message; ?>
		<button type="button" class="close" data-dismiss="alert" aria-label="Close">
			<span aria-hidden="true">&times;</span>
		</button>
	</div>
<?php endif; ?>

<!-- FILTROS -->
<div class="container-fluid">
	<div class="row">
		<div class="col-12">
			<div class="card">
				<div class="card-header">
					<div style="display: flex; justify-content: space-between; align-items: center;">
						<h5 style="margin: 0;">Filtros</h5>
						<button type="button" class="btn btn-primary btn-sm" style="border: 2px solid #4caf50; padding: 5px 10px; color: #4caf50; background-color: transparent; font-weight: bold;">
							Cierre de Caja
						</button>
						<button type="button" class="btn btn-primary btn-sm" style="border: 2px solid #4caf50; padding: 5px 10px; color: #4caf50; background-color: transparent; font-weight: bold;">
							Historial
						</button>
					</div>
				</div>
				<div class="card-body">
					<form method="GET" action="">
						<div class="row">
							<div class="col-md-3">
								<div class="form-group">
									<label for="fecha_inicio">Fecha Inicio:</label>
									<input type="date" class="form-control" id="fecha_inicio" name="fecha_inicio" value="<?php echo $fecha_inicio; ?>">
								</div>
							</div>
							<div class="col-md-3">
								<div class="form-group">
									<label for="fecha_fin">Fecha Fin:</label>
									<input type="date" class="form-control" id="fecha_fin" name="fecha_fin" value="<?php echo $fecha_fin; ?>">
								</div>
							</div>
							<div class="col-md-3">
								<div class="form-group">
									<label for="nit_cliente">Razon Social:</label>
									<input type="text" class="form-control" id="nit_cliente" name="nit_cliente" value="<?php echo $nit_cliente; ?>" placeholder="Ingrese el NIT">
								</div>
							</div>
							<div class="col-md-3">
								<div class="form-group">
									<label for="estado">Estado:</label>
									<select class="form-control" id="estado" name="estado">
										<option value="">Todos</option>
										<?php if ($estados && $estados->rowCount() > 0): ?>
											<?php while ($row_estado = $estados->fetch(PDO::FETCH_ASSOC)): ?>
												<option value="<?php echo $row_estado['estados_id']; ?>" <?php echo ($estado == $row_estado['estados_id']) ? 'selected' : ''; ?>>
													<?php echo htmlspecialchars($row_estado['estado_nombre']); ?>
												</option>
											<?php endwhile; ?>
										<?php else: ?>
											<option value="1">Pendiente</option>
											<option value="2">Pagado</option>
											<option value="3">Anulado</option>
										<?php endif; ?>
									</select>
								</div>
							</div>
							<div class="col-md-3">
								<div class="form-group">
									<label for="medioPago">Medio de pago:</label>
									<select class="form-control" id="medioPago" name="medioPago">
										<option value="">Todos</option>
										<?php if ($medios_pago && $medios_pago->rowCount() > 0): ?>
											<?php while ($row_medio = $medios_pago->fetch(PDO::FETCH_ASSOC)): ?>
												<option value="<?php echo $row_medio['md_pago_id']; ?>" <?php echo ($medio_pago == $row_medio['md_pago_id']) ? 'selected' : ''; ?>>
													<?php echo htmlspecialchars($row_medio['md_pago_nombre']); ?>
												</option>
											<?php endwhile; ?>
										<?php else: ?>
											<option value="1">Efectivo</option>
											<option value="2">Tarjeta</option>
											<option value="3">Transferencia</option>
											<option value="4">Cheque</option>
										<?php endif; ?>
									</select>
								</div>
							</div>
						</div>
						<div class="row mt-3">
							<div class="col-12 text-center">
								<button type="submit" class="btn btn-primary"><i class="fas fa-search"></i> Filtrar</button>
								<a href="<?php echo $_SERVER['PHP_SELF']; ?>" class="btn btn-secondary"><i class="fas fa-sync-alt"></i> Limpiar</a>
							</div>
						</div>
					</form>
				</div>
			</div>
		</div>
	</div>
</div>
<br>

<!--CONTENT-->
<div class="container-fluid">
	<div class="table-responsive">
		<table class="table table-dark table-sm">
			<thead>
				<tr class="text-center roboto-medium">
					<th>#</th>
					<th>FECHA</th>
					<th>CLIENTE</th>
					<th># FACTURA</th>
					<th>INGRESO</th>
					<th>EGRESO</th>
					<th>ESTADO</th>
					<th>MEDIO DE PAGO</th>
					<th>VALIDADO</th>
					<th>ACCIONES</th>
				</tr>
			</thead>
			<tbody>
				<!-- Fila para insertar nuevos datos -->
				<
					<tr id="registro-row" class="text-white" style="background-color:rgb(168, 185, 196);">
					<input type="hidden" id="action" name="action" value="insert">
					<td><strong>Registro</strong></td>
					<td>
						<input type="date" class="form-control form-control-sm" id="fecha" name="fecha" value="<?= $fecha ?>" style="text-align: center;" required>
					</td>
					<td>
						<select class="form-control form-control-sm" id="cliente_id" name="cliente_id" style="text-align: center;" required>
							<option value="">Seleccione...</option>
							<?php if ($clientes && $clientes->rowCount() > 0): ?>
								<?php while ($row_cliente = $clientes->fetch(PDO::FETCH_ASSOC)): ?>
									<option value="<?php echo $row_cliente['id']; ?>">
										<?php echo htmlspecialchars($row_cliente['nombre']); ?>
									</option>
								<?php endwhile; ?>
							<?php else: ?>
								<option value="1">Cliente 1</option>
								<option value="2">Cliente 2</option>
								<option value="3">Cliente 3</option>
							<?php endif; ?>
						</select>
					</td>
					<td><input type="text" class="form-control form-control-sm" id="num_factura" name="num_factura" placeholder="# Factura" style="text-align: center;" required></td>
					<td><input type="number" step="0.01" class="form-control form-control-sm" id="ingreso" name="ingreso" placeholder="Ingreso" style="text-align: center;" value="0"></td>
					<td><input type="number" step="0.01" class="form-control form-control-sm" id="egreso" name="egreso" placeholder="Egreso" style="text-align: center;" value="0"></td>
					<td>
						<select class="form-control form-control-sm" id="estado" name="estado" style="text-align: center;" required>
							<option value="">Seleccione...</option>
							<?php if ($estados && $estados->rowCount() > 0): ?>
								<?php
								$estados->execute(); // Reiniciar el cursor
								while ($row_estado = $estados->fetch(PDO::FETCH_ASSOC)):
								?>
									<option value="<?php echo $row_estado['estados_id']; ?>">
										<?php echo htmlspecialchars($row_estado['estado_nombre']); ?>
									</option>
								<?php endwhile; ?>
							<?php else: ?>
								<option value="1">Pendiente</option>
								<option value="2">Pagado</option>
								<option value="3">Anulado</option>
							<?php endif; ?>
						</select>
					</td>
					<td>
						<select class="form-control form-control-sm" id="medio_pago" name="medio_pago" style="text-align: center;" required>
							<option value="">Seleccione...</option>
							<?php if ($medios_pago && $medios_pago->rowCount() > 0): ?>
								<?php
								$medios_pago->execute(); // Reiniciar el cursor
								while ($row_medio = $medios_pago->fetch(PDO::FETCH_ASSOC)):
								?>
									<option value="<?php echo $row_medio['md_pago_id']; ?>">
										<?php echo htmlspecialchars($row_medio['md_pago_nombre']); ?>
									</option>
								<?php endwhile; ?>
							<?php else: ?>
								<option value="1">Efectivo</option>
								<option value="2">Tarjeta</option>
								<option value="3">Transferencia</option>
								<option value="4">Cheque</option>
							<?php endif; ?>
						</select>
					</td>
					<td class="text-center">
						<input type="checkbox" id="validado" name="validado" style="width: 20px; height: 22px;">
					</td>
					<td class="text-center">
						<button type="submit" id="guardar-btn" class="btn btn-success btn-sm badge">
							<i class="fas fa-save"></i> Guardar
						</button>
					</td>
					</tr>


					<?php
					// Mostrar los registros obtenidos de la base de datos
					if ($resultado && $resultado->rowCount() > 0) {
						while ($row = $resultado->fetch(PDO::FETCH_ASSOC)) {
							echo '<tr class="text-center">';
							echo '<td>' . htmlspecialchars($row['ped_id'] ?? $row['id'] ?? '-') . '</td>';
							echo '<td>' . date('d/m/Y', strtotime($row['ped_fecha'])) . '</td>';
							echo '<td>' . htmlspecialchars($row['nombre_cliente'] ?? '-') . '</td>';
							echo '<td>' . htmlspecialchars($row['ped_factura']) . '</td>';
							echo '<td>$' . number_format((float)$row['ped_ingreso'], 2) . '</td>';
							echo '<td>$' . number_format((float)$row['ped_egreso'], 2) . '</td>';

							$estado_class = '';
							$estado_nombre = $row['nombre_estado'] ?? $row['ped_estado'];

							switch (strtolower($estado_nombre)) {
								case 'pendiente':
								case '1':
									$estado_class = 'warning';
									$estado_texto = 'Pendiente';
									break;
								case 'pagado':
								case '2':
									$estado_class = 'success';
									$estado_texto = 'Pagado';
									break;
								case 'anulado':
								case '3':
									$estado_class = 'danger';
									$estado_texto = 'Anulado';
									break;
								default:
									$estado_class = 'secondary';
									$estado_texto = $estado_nombre;
							}

							echo '<td><span class="badge badge-' . $estado_class . '">' . $estado_texto . '</span></td>';
							echo '<td>' . htmlspecialchars($row['nombre_medio_pago'] ?? $row['ped_medio_pago']) . '</td>';

							$validado_icon = ($row['ped_validado'] ?? 0) ?
								'<i class="fas fa-check-circle text-success"></i>' :
								'<i class="fas fa-times-circle text-danger"></i>';

							echo '<td class="text-center">' . $validado_icon . '</td>';
							echo '<td class="text-center">';
							echo '<a href="caja-edit.php?id=' . ($row['ped_id'] ?? $row['id'] ?? 0) . '" class="btn btn-success btn-sm mr-1"><i class="fas fa-edit"></i></a>';
							echo '<button type="button" class="btn btn-danger btn-sm delete-btn" data-id="' . ($row['ped_id'] ?? $row['id'] ?? 0) . '"><i class="fas fa-trash-alt"></i></button>';
							echo '</td>';
							echo '</tr>';
						}
					} else {
						// Si no hay registros en la base de datos, mostrar ejemplos estáticos
					?>
						<!-- Ejemplos estáticos solo se muestran si no hay datos reales -->
						<tr class="text-center">
							<td>1</td>
							<td>28/04/2025</td>
							<td>Cliente A</td>
							<td>F-001234</td>
							<td>$1,250.00</td>
							<td>$0.00</td>
							<td><span class="badge badge-success">Pagado</span></td>
							<td>Efectivo</td>
							<td class="text-center"><i class="fas fa-check-circle text-success"></i></td>
							<td class="text-center">
								<a href="caja-edit.php?id=1" class="btn btn-success btn-sm mr-1"><i class="fas fa-edit"></i></a>
								<button type="button" class="btn btn-danger btn-sm delete-btn" data-id="1"><i class="fas fa-trash-alt"></i></button>
							</td>
						</tr>
						<tr class="text-center">
							<td>2</td>
							<td>27/04/2025</td>
							<td>Cliente B</td>
							<td>F-001233</td>
							<td>$875.50</td>
							<td>$0.00</td>
							<td><span class="badge badge-warning">Pendiente</span></td>
							<td>Transferencia</td>
							<td class="text-center"><i class="fas fa-times-circle text-danger"></i></td>
							<td class="text-center">
								<a href="caja-edit.php?id=2" class="btn btn-success btn-sm mr-1"><i class="fas fa-edit"></i></a>
								<button type="button" class="btn btn-danger btn-sm delete-btn" data-id="2"><i class="fas fa-trash-alt"></i></button>
							</td>
						</tr>
						<tr class="text-center">
							<td>3</td>
							<td>25/04/2025</td>
							<td>Cliente C</td>
							<td>F-001232</td>
							<td>$0.00</td>
							<td>$320.75</td>
							<td><span class="badge badge-danger">Anulado</span></td>
							<td>Tarjeta</td>
							<td class="text-center"><i class="fas fa-check-circle text-success"></i></td>
							<td class="text-center">
								<a href="caja-edit.php?id=3" class="btn btn-success btn-sm mr-1"><i class="fas fa-edit"></i></a>
								<button type="button" class="btn btn-danger btn-sm delete-btn" data-id="3"><i class="fas fa-trash-alt"></i></button>
							</td>
						</tr>
					<?php
					}
					?>
			</tbody>
		</table>
	</div>
	<nav aria-label="Page navigation example">
		<ul class="pagination justify-content-center">
			<li class="page-item disabled">
				<a class="page-link" href="#" tabindex="-1">Anterior</a>
			</li>
			<li class="page-item active"><a class="page-link" href="#">1</a></li>
			<li class="page-item"><a class="page-link" href="#">2</a></li>
			<li class="page-item"><a class="page-link" href="#">3</a></li>
			<li class="page-item">
				<a class="page-link" href="#">Siguiente</a>
			</li>
		</ul>
	</nav>
</div>
</section>
</main>

<!-- Modal para confirmación de eliminación -->
<div class="modal fade" id="deleteModal" tabindex="-1" role="dialog" aria-labelledby="deleteModalLabel" aria-hidden="true">
	<div class="modal-dialog" role="document">
		<div class="modal-content">
			<div class="modal-header">
				<h5 class="modal-title" id="deleteModalLabel">Confirmar eliminación</h5>
				<button type="button" class="close" data-dismiss="modal" aria-label="Close">
					<span aria-hidden="true">&times;</span>
				</button>
			</div>
			<div class="modal-body">
				¿Está seguro de que desea eliminar este registro? Esta acción no se puede deshacer.
			</div>
			<div class="modal-footer">
				<button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
				<form id="delete-form" method="POST" action="">
					<input type="hidden" name="action" value="delete">
					<input type="hidden" name="delete_id" id="delete_id" value="">
					<button type="submit" class="btn btn-danger">Eliminar</button>
				</form>
			</div>
		</div>
	</div>
</div>

<script>
	// Script para manejar la confirmación de eliminación
	document.addEventListener('DOMContentLoaded', function() {
		// Configurar los botones de eliminación para abrir el modal
		const deleteButtons = document.querySelectorAll('.delete-btn');
		deleteButtons.forEach(button => {
			button.addEventListener('click', function() {
				const id = this.getAttribute('data-id');
				document.getElementById('delete_id').value = id;
				$('#deleteModal').modal('show');
			});
		});

		// Validación del formulario
		const form = document.getElementById('insert-form');

		form.addEventListener('submit', function(event) {
			let hasErrors = false;
			const fecha = form.querySelector('[name="fecha"]');
			const cliente = form.querySelector('[name="cliente_id"]');
			const factura = form.querySelector('[name="num_factura"]');
			const estado = form.querySelector('[name="estado"]');
			const medioPago = form.querySelector('[name="medio_pago"]');

			// Validar campos requeridos
			if (!fecha.value) {
				fecha.classList.add('is-invalid');
				hasErrors = true;
			} else {
				fecha.classList.remove('is-invalid');
			}

			if (!cliente.value) {
				cliente.classList.add('is-invalid');
				hasErrors = true;
			} else {
				cliente.classList.remove('is-invalid');
			}

			if (!factura.value) {
				factura.classList.add('is-invalid');
				hasErrors = true;
			} else {
				factura.classList.remove('is-invalid');
			}

			if (!estado.value) {
				estado.classList.add('is-invalid');
				hasErrors = true;
			} else {
				estado.classList.remove('is-invalid');
			}

			if (!medioPago.value) {
				medioPago.classList.add('is-invalid');
				hasErrors = true;
			} else {
				medioPago.classList.remove('is-invalid');
			}

			if (hasErrors) {
				event.preventDefault();
				alert('Por favor, complete todos los campos obligatorios');
			}
		});

		// Formatear valores numéricos cuando pierden el foco
		const ingresoInput = document.querySelector('[name="ingreso"]');
		const egresoInput = document.querySelector('[name="egreso"]');

		if (ingresoInput) {
			ingresoInput.addEventListener('blur', function() {
				if (this.value === '') {
					this.value = '0';
				}
			});
		}

		if (egresoInput) {
			egresoInput.addEventListener('blur', function() {
				if (this.value === '') {
					this.value = '0';
				}
			});
		}
	});
	document.addEventListener('DOMContentLoaded', () => {
		document.getElementById('guardar-btn').addEventListener('click', async () => {
			const payload = {
				action: document.getElementById('action').value,
				fecha: document.getElementById('fecha').value,
				cliente_id: document.getElementById('cliente_id').value,
				num_factura: document.getElementById('num_factura').value,
				ingreso: document.getElementById('ingreso').value || 0,
				egreso: document.getElementById('egreso').value || 0,
				estado: document.getElementById('estado').value,
				medio_pago: document.getElementById('medio_pago').value,
				validado: document.getElementById('validado').checked ? 1 : 0
			};

			try {
				const response = await fetch('../controllers/controllerCaja.php', {
					method: 'POST',
					headers: {
						'Content-Type': 'application/json'
					},
					body: JSON.stringify(payload)
				});

				const result = await response.json();

				if (result.success) {
					alert('✅ Registro guardado con éxito');
					// Aquí puedes resetear campos si deseas
				} else {
					alert('❌ Error: ' + result.message);
				}
			} catch (error) {
				console.error('Error en envío:', error);
				alert('❌ Error en la conexión');
			}
		});
	});
</script>

<style>
	.is-invalid {
		border-color: #dc3545 !important;
		box-shadow: 0 0 0 0.2rem rgba(220, 53, 69, 0.25) !important;
	}

	.custom-control-label::before {
		background-color: #28a745;
		border-color: #28a745;
	}
</style>

<?php
include_once "footer.php";
?>