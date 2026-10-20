<?php
require_once "../models/User.php";

if ($_SERVER["REQUEST_METHOD"] == "POST") {
    // Capturar los datos del formulario
    $id = isset($_POST["id"]) ? $_POST["id"] : null;
    $usuario = $_POST["usuario_usuario"];
    $password = $_POST["usuario_clave_1"];
    $email = $_POST["usuario_email"];
    $telefono = $_POST["telefono"];
    $direccion = $_POST["direccion"];
    $nombre = $_POST["nombre"];

    $user = new User();

    if ($id) {
        // Si el ID está presente, significa que estamos editando
        if ($user->updateUser($id, $usuario, $password, $email, $telefono, $direccion, $nombre)) {
            header("Location: ../views/user-list.php?success=edit");
        } else {
            header("Location: ../views/user-edit.php?id=$id&error=1");
        }
    } else {
        // Si no hay ID, significa que estamos registrando un nuevo usuario
        if ($user->registerUser($usuario, $password, $email, $telefono, $direccion, $nombre)) {
            header("Location: ../views/user-list.php?success=1");
        } else {
            header("Location: ../views/user-new.html?error=1");
        }
    }
    exit();
}
?>