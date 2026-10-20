
<?php
session_start();
require_once "../models/database.php"; // Conexión a la BD
require_once "../models/User.php"; // Modelo de usuario

if ($_SERVER["REQUEST_METHOD"] == "POST") {
    $usuario = trim($_POST["usuario"]);
    $clave = trim($_POST["clave"]);

    if (empty($usuario) || empty($clave)) {
        header("Location: ../index.html?error=Campos%20vacíos");
        exit();
    }

    try {
        $userModel = new User();
        $userData = $userModel->getUserByUsername($usuario);
    } catch (Throwable $e) {
        $msg = rawurlencode('No se pudo entrar a la base: ' . $e->getMessage());
        header("Location: ../index.html?error=" . $msg);
        exit();
    }

    if (is_array($userData) && $clave === (string)$userData["password"]) {
        $_SESSION["usuario"] = $userData["usuario"];
        header("Location: ../views/home.php");
        exit();
    }

    header("Location: ../index.html?error=Usuario%20o%20contrase%C3%B1a%20incorrectos");
    exit();
} else {
    header("Location: ../index.html");
    exit();
}
?>