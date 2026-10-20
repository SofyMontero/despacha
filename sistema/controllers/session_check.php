<?php
session_start();
if (isset($_SESSION["usuario"])) {
    echo json_encode(["auth" => true]);
} else {
    echo json_encode(["auth" => false]);
}
?>