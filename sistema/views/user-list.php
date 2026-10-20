<?php
include_once "header.php";
require_once "../models/database.php";

// Procesamiento del formulario de inserción
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['action']) && $_POST['action'] === 'insert') {
    // Obtener datos del formulario
    $usuario = $_POST['usuario_usuario'] ?? '';
    $password = $_POST['usuario_password'] ?? '';
    $password_confirm = $_POST['usuario_password_conf'] ?? '';
    $email = $_POST['usuario_email'] ?? '';
    $telefono = $_POST['usuario_telefono'] ?? '';
    $direccion = $_POST['usuario_direccion'] ?? '';
    $nombre = $_POST['usuario_nombre'] ?? '';
    $rol = $_POST['usuario_rol'] ?? '';
    
    // Validación básica
    $errores = [];
    if (empty($usuario)) {
        $errores[] = "El nombre de usuario es obligatorio";
    }
    if (empty($password)) {
        $errores[] = "La contraseña es obligatoria";
    }
    if ($password !== $password_confirm) {
        $errores[] = "Las contraseñas no coinciden";
    }
    if (empty($email)) {
        $errores[] = "El email es obligatorio";
    } elseif (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
        $errores[] = "El formato del email no es válido";
    }
    if (empty($nombre)) {
        $errores[] = "El nombre es obligatorio";
    }
    if (empty($rol)) {
        $errores[] = "Debe seleccionar un rol";
    }
    
    // Si no hay errores, proceder con la inserción
    if (empty($errores)) {
        // Hashear la contraseña para seguridad
        $password_hash = password_hash($password, PASSWORD_DEFAULT);
        
        // Preparar la consulta SQL
        // $sql_insert = "INSERT INTO users (usuario, password, email, telefono, direccion, nombre, rol) 
        //                VALUES ('$usuario', '$password_hash', '$email', '$telefono', '$direccion', '$nombre', '$rol')";
        
        // try {
        //     $db = new Database();
        //     $conn = $db->connect();
        //     $stmt = $conn->prepare($sql_insert);
        //     $stmt->execute();
        //     $mensaje_exito = "El usuario ha sido registrado exitosamente";
        // } catch (PDOException $e) {
        //     $errores[] = "Error al insertar: " . $e->getMessage();
        // }
        
        // Simulación (eliminar en producción)
        $mensaje_exito = "El usuario ha sido registrado exitosamente";
    }
}

class User {
    private $db;

    public function __construct() {
        $this->db = new Database(); // Crear instancia de la base de datos
    }

    public function getUsers() {
        try {
            $query = $this->db->connect()->prepare("SELECT `id`, `usuario`, `password`, `email`, `telefono`, `direccion`, `nombre`, `rol` FROM `users` ");
            $query->execute(); // Ejecutar la consulta
            return $query->fetchAll(PDO::FETCH_ASSOC); // Obtener todos los resultados como array asociativo
        } catch (PDOException $e) {
            die("Error en la consulta: " . $e->getMessage());
        }
    }
}

// Crear instancia de User y obtener los usuarios
$user = new User();
$users = $user->getUsers();
?>

<!-- Page header -->
<div class="full-box page-header">
    <h3 class="text-left">
        <i class="fas fa-user-cog fa-fw"></i> &nbsp; USUARIOS
    </h3>
    <p class="text-justify">
        Nota: Tenga en cuenta que los usuarios tendrán acceso al sistema según su rol asignado. Complete todos los campos obligatorios y asigne los permisos adecuados.
    </p>
</div>

<!-- Mensaje de éxito o error -->
<?php if (isset($mensaje_exito)): ?>
<div class="alert alert-success alert-dismissible fade show" role="alert">
    <strong>¡Éxito!</strong> <?php echo $mensaje_exito; ?>
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

<!-- Formulario de inserción -->
<div class="container-fluid">
    <div class="row">
        <div class="col-12">
            <div class="card">
                <div class="card-header">
                    <h5 class="card-title"><i class="fas fa-plus"></i> &nbsp; Nuevo Usuario</h5>
                </div>
                <div class="card-body">
                    <form action="" method="POST" class="form-neon" autocomplete="off">
                        <input type="hidden" name="action" value="insert">
                        
                        <fieldset>
                            <legend><i class="fas fa-user"></i> &nbsp; Información básica</legend>
                            <div class="container-fluid">
                                <div class="row">
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="usuario_nombre" class="bmd-label-floating">Nombre completo <span class="text-danger">*</span></label>
                                            <input type="text" pattern="[a-zA-ZáéíóúÁÉÍÓÚñÑ ]{1,35}" class="form-control" name="usuario_nombre" id="usuario_nombre" maxlength="35" required>
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="usuario_telefono" class="bmd-label-floating">Teléfono</label>
                                            <input type="text" pattern="[0-9()+]{8,20}" class="form-control" name="usuario_telefono" id="usuario_telefono" maxlength="20">
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="usuario_direccion" class="bmd-label-floating">Dirección</label>
                                            <input type="text" pattern="[a-zA-Z0-9áéíóúÁÉÍÓÚñÑ().,#\- ]{1,190}" class="form-control" name="usuario_direccion" id="usuario_direccion" maxlength="190">
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="usuario_rol" class="bmd-label-floating">Rol de usuario <span class="text-danger">*</span></label>
                                            <select class="form-control" name="usuario_rol" id="usuario_rol" required>
                                                <option value="" selected disabled>Seleccione un rol</option>
                                                <option value="Administrador">Administrador</option>
                                                <option value="Vendedor">Vendedor</option>
                                                <option value="Supervisor">Supervisor</option>
                                                <option value="Cajero">Cajero</option>
                                            </select>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </fieldset>
                        
                        <br><br><br>
                        
                        <fieldset>
                            <legend><i class="fas fa-user-lock"></i> &nbsp; Información de la cuenta</legend>
                            <div class="container-fluid">
                                <div class="row">
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="usuario_usuario" class="bmd-label-floating">Nombre de usuario <span class="text-danger">*</span></label>
                                            <input type="text" pattern="[a-zA-Z0-9]{4,20}" class="form-control" name="usuario_usuario" id="usuario_usuario" maxlength="20" required>
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="usuario_email" class="bmd-label-floating">Email <span class="text-danger">*</span></label>
                                            <input type="email" class="form-control" name="usuario_email" id="usuario_email" maxlength="70" required>
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="usuario_password" class="bmd-label-floating">Contraseña <span class="text-danger">*</span></label>
                                            <input type="password" class="form-control" name="usuario_password" id="usuario_password" maxlength="100" required>
                                        </div>
                                    </div>
                                    <div class="col-12 col-md-6">
                                        <div class="form-group">
                                            <label for="usuario_password_conf" class="bmd-label-floating">Confirmar contraseña <span class="text-danger">*</span></label>
                                            <input type="password" class="form-control" name="usuario_password_conf" id="usuario_password_conf" maxlength="100" required>
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

<!-- Tabla de usuarios -->
<div class="container-fluid">
    <div class="row">
        <div class="col-12">
            <div class="card">
                <div class="card-header">
                    <h5 class="card-title"><i class="fas fa-clipboard-list fa-fw"></i> &nbsp; LISTA DE USUARIOS</h5>
                </div>
                <div class="card-body">
                    <div class="table-responsive">
                        <table class="table table-dark table-sm">
                            <thead>
                                <tr class="text-center roboto-medium">
                                    <th>#</th>
                                    <th>USUARIO</th>
                                    <th>NOMBRE</th>
                                    <th>TELÉFONO</th>
                                    <th>EMAIL</th>
                                    <th>ROL</th>
                                    <th>ACTUALIZAR</th>
                                    <th>ELIMINAR</th>
                                </tr>
                            </thead>
                            <tbody>
                            <?php 
                            if (!empty($users)) {
                                foreach ($users as $user) { 
                            ?>
                                <tr class="text-center">
                                    <td><?php echo $user["id"]; ?></td>
                                    <td><?php echo $user["usuario"]; ?></td>
                                    <td><?php echo $user["nombre"]; ?></td>
                                    <td><?php echo $user["telefono"] ? $user["telefono"] : 'N/A'; ?></td>
                                    <td><?php echo $user["email"]; ?></td>
                                    <td><?php echo $user["rol"]; ?></td>
                                    <td>
                                        <a href='user-update.php?id=<?php echo $user["id"]; ?>' class='btn btn-success'>
                                            <i class="fas fa-sync-alt"></i>    
                                        </a>
                                    </td>
                                    <td>
                                        <button type="button" class="btn btn-warning delete-btn" data-id="<?php echo $user["id"]; ?>">
                                            <i class="far fa-trash-alt"></i>
                                        </button>
                                    </td>
                                </tr>
                            <?php 
                                }
                            } else {
                            ?>
                                <tr class="text-center">
                                    <td colspan="8">No hay usuarios registrados</td>
                                </tr>
                            <?php } ?>
                            </tbody>
                        </table>
                    </div>
                </div>
                <div class="card-footer">
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
                ¿Está seguro de que desea eliminar este usuario? Esta acción no se puede deshacer.
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                <form id="delete-form" method="POST" action="">
                    <input type="hidden" name="action" value="delete">
                    <input type="hidden" name="usuario_id" id="usuario_id" value="">
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
                document.getElementById('usuario_id').value = id;
                $('#deleteModal').modal('show');
            });
        });
    });
</script>

</section>
</main>

<?php
include_once "footer.php";
?>