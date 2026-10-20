<?php
session_start();
require_once __DIR__ . '/../models/User.php';

class AuthController {
    private $user;

    public function __construct() {
        $this->user = new User();
    }

    public function login($UserName, $password) {
        $user = $this->user->login($UserName, $password);
        if ($user) {
            $_SESSION['user'] = $user;
            header("Location: ../home.html"); // Redirige después de iniciar sesión
            exit();
        } else {
            $_SESSION['error'] = "Usuario o contraseña incorrectos";
            header("Location: ../index.html");
            exit();
        }
    }

    public function logout() {
        session_destroy();
        header("Location: ../login.html");
        exit();
    }
}

if ($_SERVER["REQUEST_METHOD"] == "POST") {
    $auth = new AuthController();
    $auth->login($_POST['email'], $_POST['password']);
}

// if (isset($_GET['action']) && $_GET['action'] == 'logout') {
//     session_start();
//     session_destroy();
//     header("Location: ../login.html");
//     exit();
// }
?>