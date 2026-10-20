<?php
// Mostrar errores en entorno de desarrollo (comentarlos en producción)
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(0);

include_once "header.php";
require_once "../models/database.php";
require_once "../models/BarModel.php";

$bar = new Bar();
$pagina = isset($_GET['pagina']) ? (int) $_GET['pagina'] : 1;
$limite = 10; // cantidad por página
$offset = ($pagina - 1) * $limite;

$totalClientes = $bar->contarTotalClientes();
$totalPaginas = ceil($totalClientes / $limite);

$clientes = $bar->obtenerClientesPaginado($limite, $offset);
$errores = []; // 🔸 Inicializamos el arreglo de errores

if ($_SERVER["REQUEST_METHOD"] === "POST" && isset($_POST["action"]) && $_POST["action"] === "insert") {
    $razon_social = trim($_POST["cliente_nombre"]);
    $telefono = trim($_POST["cliente_telefono"]);
    $direccion = trim($_POST["cliente_direccion"]);
    $zona = trim($_POST["cliente_zona"]);
    $nombre_bar = trim($_POST["nombre_bar"]);
    $bar_id = trim($_POST["bar_id"]);

    try {
        // Si no se seleccionó un bar de la lista, verificar si existe por nombre
        if (empty($bar_id)) {
            if (!$bar->existeBar($nombre_bar)) {
                $barInsertado = $bar->insertarBar(
                    $nombre_bar,
                    $direccion,
                );
                if ($barInsertado) {
                    $bar_id = $bar->obtenerUltimoIdInsertado();
                } else {
                    $errores[] = "No se pudo insertar el bar.";
                }
            } else {

                $barExistente = $bar->buscarBaresPorNombre($nombre_bar);
                if (count($barExistente) > 0) {
                    $bar_id = $barExistente[0]["id_bar"];
                } else {
                    $errores[] = "No se encontró el bar existente.";
                }
            }
        }

        if (empty($errores)) {
            $clienteInsertado = $bar->insertarCliente($bar_id, $razon_social, $telefono, $direccion, $zona);

            if ($clienteInsertado) {
                header("Location: " . $_SERVER['PHP_SELF'] . "?success=1");
                exit;
            } else {
                $errores[] = "No se pudo registrar el cliente.";
            }
        }
    } catch (Exception $e) {
        $errores[] = "Error interno: " . $e->getMessage();
        // Opcional: guarda el error en un archivo log
        file_put_contents("log.txt", "[" . date("Y-m-d H:i:s") . "] " . $e->getMessage() . PHP_EOL, FILE_APPEND);
    }
}
if ($_POST["action"] === "delete") {
    $cliente_id = $_POST["cliente_id"] ?? null;

    if ($cliente_id) {
        $eliminado = $bar->eliminarCliente($cliente_id);

        if ($eliminado) {
            header("Location: " . $_SERVER['PHP_SELF'] . "?success=1");
            exit;
        } else {
            $errores[] = "No se pudo eliminar el cliente.";
        }
    } else {
        $errores[] = "ID del cliente no válido.";
    }
}
if ($_SERVER["REQUEST_METHOD"] === "POST" && isset($_POST["action"]) && $_POST["action"] === "update") {
    $id_cliente = $_POST["id_cliente"];
    $bar_id     = $_POST["bar_id"];
    $nombre     = $_POST["cliente_nombre"];
    $telefono   = $_POST["cliente_telefono"];
    $direccion  = $_POST["cliente_direccion"];
    $zona       = $_POST["cliente_zona"];

    if ($bar->actualizarCliente($id_cliente, $bar_id, $nombre, $telefono, $direccion, $zona)) {
        header("Location: " . $_SERVER['PHP_SELF'] . "?success=1");
        exit;
    } else {
        $errores[] = "No se pudo actualizar el cliente.";
    }
}
?>

<!-- Page header -->
<div class="full-box page-header">
    <h3 class="text-left">
        <i class="fas fa-user-tie fa-fw"></i> &nbsp; CLIENTES
    </h3>
    <p class="text-justify">
        Nota: Tenga en cuenta que la información registrada será utilizada para todas las operaciones comerciales. Complete todos los campos con información precisa y actualizada.
    </p>
</div>

<!-- Mensaje de éxito o error -->
<?php if (isset($_GET['success'])): ?>
    <div class="alert alert-success alert-dismissible fade show" role="alert">
        <strong>¡Éxito!</strong> Cliente registrado correctamente.
        <button type="button" class="close" data-dismiss="alert" aria-label="Close">
            <span aria-hidden="true">&times;</span>
        </button>
    </div>
<?php endif; ?>

<?php if (!empty($errores)): ?>
    <div class="alert alert-danger alert-dismissible fade show" role="alert">
        <strong>¡Error!</strong>
        <ul>
            <?php foreach ($errores as $error): ?>
                <li><?php echo $error; ?></li>
            <?php endforeach; ?>
        </ul>
        <button type="button" class="close" data-dismiss="alert" aria-label="Close">
            <span aria-hidden="true">&times;</span>
        </button>
    </div>

<?php endif; ?>



<!-- Content here-->
<div class="container-fluid">
    <div class="row">
        <div class="col-12">
            <div class="card">

                <div class="card-header">
                    <h5 class="card-title"><i class="fas fa-plus"></i> &nbsp; Nuevo Cliente</h5>
                </div>
                <div class="card-body">
                    <form action="" method="POST" class="form-neon" autocomplete="off">
                        <input type="hidden" name="action" value="insert">
                        <fieldset>
                            <legend><i class="fas fa-user"></i> &nbsp; Información básica</legend>
                            <div class="container-fluid">
                                <div class="row">
                                    <div class="col-12 col-md-6">
                                        <div class="form-group position-relative">
                                            <label for="nombre_bar" class="bmd-label-floating">Bar <span class="text-danger">*</span></label>
                                            <input type="text" class="form-control" name="nombre_bar" id="nombre_bar" maxlength="40" required>
                                            <input type="hidden" name="bar_id" id="bar_id" value="">
                                            <!-- Lista de sugerencias -->
                                            <div id="autocomplete-list" class="autocomplete-suggestions" style="display: none;">
                                                <!-- Las sugerencias se cargarán aquí dinámicamente -->
                                            </div>
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="cliente_nombre" class="bmd-label-floating">Cliente</label>
                                            <input type="text" class="form-control" name="cliente_nombre" id="cliente_nombre" maxlength="20" required>
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="cliente_telefono" class="bmd-label-floating">Teléfono</label>
                                            <input type="text" pattern="[0-9()+]{1,20}" class="form-control" name="cliente_telefono" id="cliente_telefono" maxlength="20" required>
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="cliente_direccion" class="bmd-label-floating">Dirección</label>
                                            <input type="text" pattern="[a-zA-Z0-9áéíóúÁÉÍÓÚñÑ#- ]{1,150}" class="form-control" name="cliente_direccion" id="cliente_direccion" maxlength="150" required>
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="cliente_zona" class="bmd-label-floating">Zona</label>
                                            <select class="form-control" name="cliente_zona" id="cliente_zona" required>
                                                <option value="" disabled selected>Seleccione una zona</option>
                                                <option value="Chapinero">Chapinero</option>
                                                <option value="Centro">Centro</option>
                                                <option value="Zona T">Zona T</option>
                                                <option value="45">45</option>
                                                <option value="Otra">Otra</option>
                                                <!-- Agrega más opciones según tu necesidad -->
                                            </select>
                                        </div>
                                    </div>

                                </div>
                            </div>
                        </fieldset>
                        <p class="text-center" style="margin-top: 40px;">
                            <button type="reset" class="btn btn-raised btn-secondary btn-sm"><i class="fas fa-paint-roller"></i> &nbsp; LIMPIAR</button>
                            &nbsp; &nbsp;
                            <button type="submit" class="btn btn-raised btn-info btn-sm"><i class="far fa-save"></i> &nbsp; GUARDAR</button>
                        </p>
                    </form>
                </div>
            </div>
        </div>
    </div>
</div>
<br>

<!-- Tabla de clientes -->
<div class="container-fluid">
    <div class="row">
        <div class="col-12">
            <div class="card">
                <div class="card-header">
                    <h5 class="card-title"><i class="fas fa-list"></i> &nbsp; Lista de Clientes</h5>
                </div>
                <div class="card-body">
                    <div class="table-responsive">
                        <table class="table table-dark table-sm">
                            <thead>
                                <tr class="text-center roboto-medium">
                                    <th>#</th>
                                    <th>BAR</th>
                                    <th>CLIENTE</th>
                                    <th>TELÉFONO</th>
                                    <th>DIRECCIÓN</th>
                                    <th>ZONA</th>
                                    <th>FECHA REGISTRO</th>
                                    <th>ACCIONES</th>
                                </tr>
                            </thead>
                            <tbody>
                                <?php foreach ($clientes as $index => $cliente): ?>
                                    <tr class="text-center">
                                        <td><?= $index + 1 ?></td>
                                        <td><?= htmlspecialchars($cliente['nombre_bar']) ?></td>
                                        <td><?= htmlspecialchars($cliente['cli_nombre']) ?></td>
                                        <td><?= htmlspecialchars($cliente['cli_telefono']) ?></td>
                                        <td><?= htmlspecialchars($cliente['cli_direccion']) ?></td>
                                        <td><?= htmlspecialchars($cliente['cli_zona']) ?></td>
                                        <td><?= date('d/m/Y', strtotime($cliente['cli_fecha_registro'])) ?></td>
                                        <td>
                                            <button type="button"
                                                class="btn btn-success btn-sm mr-1 edit-btn"
                                                data-toggle="modal"
                                                data-target="#editModal"
                                                data-id="<?= $cliente['id_cliente'] ?>"
                                                data-nombre="<?= $cliente['cli_nombre'] ?>"
                                                data-telefono="<?= $cliente['cli_telefono'] ?>"
                                                data-direccion="<?= $cliente['cli_direccion'] ?>"
                                                data-zona="<?= $cliente['cli_zona'] ?>"
                                                data-bar="<?= $cliente['cli_Bar'] ?>"
                                                title="Editar">
                                                <i class="fas fa-edit"></i>
                                            </button>
                                            <button type="button" class="btn btn-danger btn-sm delete-btn" data-id="<?= $cliente['id_cliente'] ?>" title="Eliminar">
                                                <i class="fas fa-trash-alt"></i>
                                            </button>
                                        </td>
                                    </tr>
                                <?php endforeach; ?>
                            </tbody>

                        </table>
                    </div>
                </div>
                <div class="card-footer">
                    <nav aria-label="Page navigation">
                        <ul class="pagination justify-content-center">
                            <li class="page-item <?= $pagina <= 1 ? 'disabled' : '' ?>">
                                <a class="page-link" href="?pagina=<?= $pagina - 1 ?>">Anterior</a>
                            </li>

                            <?php for ($i = 1; $i <= $totalPaginas; $i++): ?>
                                <li class="page-item <?= $pagina == $i ? 'active' : '' ?>">
                                    <a class="page-link" href="?pagina=<?= $i ?>"><?= $i ?></a>
                                </li>
                            <?php endfor; ?>

                            <li class="page-item <?= $pagina >= $totalPaginas ? 'disabled' : '' ?>">
                                <a class="page-link" href="?pagina=<?= $pagina + 1 ?>">Siguiente</a>
                            </li>
                        </ul>
                    </nav>
                </div>
            </div>
        </div>
    </div>
</div>

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
                ¿Está seguro de que desea eliminar este cliente? Esta acción no se puede deshacer y puede afectar a registros relacionados.
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                <form id="delete-form" method="POST" action="">
                    <input type="hidden" name="action" value="delete">
                    <input type="hidden" name="cliente_id" id="cliente_id" value="">
                    <button type="submit" class="btn btn-danger">Eliminar</button>
                </form>
            </div>
        </div>
    </div>
</div>
<div class="modal fade" id="editModal" tabindex="-1" role="dialog" aria-labelledby="editModalLabel" aria-hidden="true">
    <div class="modal-dialog" role="document">
        <form method="POST" class="modal-content">
            <input type="hidden" name="action" value="update">
            <input type="hidden" name="id_cliente" id="edit_id_cliente">

            <div class="modal-header">
                <h5 class="modal-title" id="editModalLabel">Editar Cliente</h5>
                <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                    <span aria-hidden="true">&times;</span>
                </button>
            </div>

            <div class="modal-body">
                <div class="form-group">
                    <label>Nombre</label>
                    <input type="text" name="cliente_nombre" id="edit_nombre" class="form-control">
                </div>
                <div class="form-group">
                    <label>Teléfono</label>
                    <input type="text" name="cliente_telefono" id="edit_telefono" class="form-control">
                </div>
                <div class="form-group">
                    <label>Dirección</label>
                    <input type="text" name="cliente_direccion" id="edit_direccion" class="form-control">
                </div>
                <div class="form-group">
                    <label>Zona</label>
                    <input type="text" name="cliente_zona" id="edit_zona" class="form-control">
                </div>
                <div class="form-group">
                    <label>Bar ID</label>
                    <input type="text" name="bar_id" id="edit_bar_id" class="form-control">
                </div>
            </div>

            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                <button type="submit" class="btn btn-primary">Guardar cambios</button>
            </div>
        </form>
    </div>
</div>

<!-- Estilos CSS para el autocompletable -->
<style>
    .autocomplete-suggestions {
        position: absolute;
        top: 100%;
        left: 0;
        right: 0;
        background: white;
        border: 1px solid #ccc;
        border-top: none;
        max-height: 200px;
        overflow-y: auto;
        z-index: 1000;
        box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
    }

    .autocomplete-suggestion {
        padding: 10px;
        cursor: pointer;
        border-bottom: 1px solid #eee;
        color: #333;
    }

    .autocomplete-suggestion:hover,
    .autocomplete-suggestion.selected {
        background-color: #f0f0f0;
    }

    .autocomplete-suggestion:last-child {
        border-bottom: none;
    }

    .form-group.position-relative {
        position: relative;
    }
</style>

<script>
    document.addEventListener('DOMContentLoaded', function() {
        const clienteNombreInput = document.getElementById('nombre_bar');
        const barIdInput = document.getElementById('bar_id');
        const autocompleteList = document.getElementById('autocomplete-list');
        let selectedIndex = -1;

        // Función para buscar bares
        function buscarBares(query) {
            if (query.length < 2) {
                autocompleteList.style.display = 'none';
                return;
            }

            // Realizar petición AJAX
            fetch('../controllers/buscar_bar.php', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded',
                    },
                    body: 'query=' + encodeURIComponent(query)
                })
                .then(response => response.json())
                .then(data => {
                    mostrarSugerencias(data);
                    console.log(data);
                })
                .catch(error => {
                    console.error('Error:', error);
                    autocompleteList.style.display = 'none';
                });
        }

        // Función para mostrar sugerencias
        function mostrarSugerencias(data) {
            const autocompleteList = document.getElementById('autocomplete-list');
            autocompleteList.innerHTML = ''; // Limpia anteriores

            if (!Array.isArray(data) || data.length === 0) {
                autocompleteList.style.display = 'none';
                return;
            }

            data.forEach(item => {
                const div = document.createElement('div');
                div.classList.add('autocomplete-suggestion');
                div.textContent = item.label || item.value || 'Sin nombre';

                div.addEventListener('click', () => {
                    document.getElementById('nombre_bar').value = item.label;
                    document.getElementById('bar_id').value = item.id;
                    autocompleteList.innerHTML = '';
                    autocompleteList.style.display = 'none';
                });

                autocompleteList.appendChild(div);
            });

            autocompleteList.style.display = 'block';
        }

        // Función para seleccionar un bar
        function seleccionarBar(element) {
            clienteNombreInput.value = element.dataset.nombre;
            barIdInput.value = element.dataset.id;

            // Auto-llenar otros campos si están disponibles
            if (element.dataset.direccion) {
                document.getElementById('cliente_direccion').value = element.dataset.direccion;
            }
            if (element.dataset.telefono) {
                document.getElementById('cliente_telefono').value = element.dataset.telefono;
            }

            autocompleteList.style.display = 'none';
        }

        // Event listeners
        clienteNombreInput.addEventListener('input', function() {
            const query = this.value.trim();
            buscarBares(query);
        });

        clienteNombreInput.addEventListener('keydown', function(e) {
            const suggestions = autocompleteList.querySelectorAll('.autocomplete-suggestion');

            if (suggestions.length === 0) return;

            switch (e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    selectedIndex = (selectedIndex + 1) % suggestions.length;
                    updateSelection(suggestions);
                    break;

                case 'ArrowUp':
                    e.preventDefault();
                    selectedIndex = selectedIndex <= 0 ? suggestions.length - 1 : selectedIndex - 1;
                    updateSelection(suggestions);
                    break;

                case 'Enter':
                    e.preventDefault();
                    if (selectedIndex >= 0 && suggestions[selectedIndex]) {
                        seleccionarBar(suggestions[selectedIndex]);
                    }
                    break;

                case 'Escape':
                    autocompleteList.style.display = 'none';
                    selectedIndex = -1;
                    break;
            }
        });

        // Función para actualizar la selección visual
        function updateSelection(suggestions) {
            suggestions.forEach((suggestion, index) => {
                suggestion.classList.toggle('selected', index === selectedIndex);
            });
        }

        // Ocultar sugerencias al hacer clic fuera
        document.addEventListener('click', function(e) {
            if (!clienteNombreInput.contains(e.target) && !autocompleteList.contains(e.target)) {
                autocompleteList.style.display = 'none';
            }
        });

        // Configurar los botones de eliminación para abrir el modal
        const deleteButtons = document.querySelectorAll('.delete-btn');
        deleteButtons.forEach(button => {
            button.addEventListener('click', function() {
                const id = this.getAttribute('data-id');
                document.getElementById('cliente_id').value = id;
                $('#deleteModal').modal('show');
            });
        });

        // Limpiar campos al reset
        document.querySelector('button[type="reset"]').addEventListener('click', function() {
            setTimeout(() => {
                barIdInput.value = '';
                autocompleteList.style.display = 'none';
            }, 10);
        });
    });

    document.querySelectorAll('.edit-btn').forEach(button => {
        button.addEventListener('click', function() {
            document.getElementById('edit_id_cliente').value = this.dataset.id;
            document.getElementById('edit_nombre').value = this.dataset.nombre;
            document.getElementById('edit_telefono').value = this.dataset.telefono;
            document.getElementById('edit_direccion').value = this.dataset.direccion;
            document.getElementById('edit_zona').value = this.dataset.zona;
            document.getElementById('edit_bar_id').value = this.dataset.bar;
        });
    });
</script>

</section>
</main>

<?php
include_once "footer.php";
?>