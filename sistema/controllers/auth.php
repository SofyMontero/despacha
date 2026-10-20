<?php
session_start();
if (!isset($_SESSION["usuario"])) {
    header("Location: ../index.html?error=Debes%20iniciar%20sesión");
    exit();
}
?>