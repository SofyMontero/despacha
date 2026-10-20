<?php
require_once "database.php";

class User {
    private $db;

    public function __construct() {
        $this->db = new Database();
    }

    public function getUserByUsername($usuario) {
        $query = $this->db->connect()->prepare("SELECT * FROM users WHERE usuario = :usuario");
        $query->execute(["usuario" => $usuario]);
        return $query->fetch(PDO::FETCH_ASSOC);
    }
    public function registerUser($usuario, $password, $email,$telefono, $direccion, $nombre) {
        $hashedPassword = password_hash($password, PASSWORD_BCRYPT); // Encriptar la contraseña
        $query = $this->db->connect()->prepare("INSERT INTO users (usuario,password,email,telefono,direccion,nombre) VALUES (:usuario, :password, :email,:telefono, :direccion, :nombre)");
        return $query->execute([
            "usuario" => $usuario,
            "password" => $hashedPassword,
            "email" => $email,
            "telefono" => $telefono,
            "direccion" => $direccion,
            "nombre" => $nombre
        ]);
    }
    public function updateUser($id,$usuario, $password, $email,$telefono, $direccion, $nombre) {
        $hashedPassword = password_hash($password, PASSWORD_BCRYPT); // Encriptar la contraseña
        $query = $this->db->connect()->prepare(" UPDATE users 
        SET usuario = :usuario, 
            password = :password, 
            email = :email, 
            telefono = :telefono, 
            direccion = :direccion, 
            nombre = :nombre 
        WHERE id = :id");
        return $query->execute([
            "id" => $id,
            "usuario" => $usuario,
            "password" => $hashedPassword,
            "email" => $email,
            "telefono" => $telefono,
            "direccion" => $direccion,
            "nombre" => $nombre
        ]);
    }
}


?>