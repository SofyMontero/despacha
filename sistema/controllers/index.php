
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

    $userModel = new User();
    $userData = $userModel->getUserByUsername($usuario);

    
    //  if ($userData && password_verify($clave, $userData["password"])) {
    if ($clave==$userData["password"]) {
        $_SESSION["usuario"] = $userData["usuario"];
        header("Location: ../views/home.php"); // Redirige al dashboard
        exit();
    } else {
        header("Location: ../index.html?error=Usuario%20o%20contraseña%20incorrectos".$userData["password"]);
        exit();
    }
} else {
    header("Location: ../index.html");
    exit();
}
?>