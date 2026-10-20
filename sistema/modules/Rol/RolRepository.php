<?php

class RolRepository
{
    private PDO $pdo;

    public function __construct(?PDO $pdo = null)
    {
        if ($pdo) {
            $this->pdo = $pdo;
        } else {
            $db = new Database();
            $this->pdo = $db->connect();
        }
    }

    public function listarActivos(): array
    {
        if (!$this->tablaExiste('roles')) {
            return [];
        }

        // roles puede tener rol_estado (int) tras ajustes en phpMyAdmin.
        $sql = "SELECT id_rol, rol_nombre, rol_estado
                FROM roles
                WHERE rol_estado = 1 OR rol_estado = '1'
                ORDER BY rol_nombre";
        try {
            return $this->pdo->query($sql)->fetchAll(PDO::FETCH_ASSOC);
        } catch (PDOException $e) {
            // Fallback si la columna se llama distinto
            $sql2 = 'SELECT id_rol, rol_nombre FROM roles ORDER BY rol_nombre';
            return $this->pdo->query($sql2)->fetchAll(PDO::FETCH_ASSOC);
        }
    }

    private function tablaExiste(string $tabla): bool
    {
        $stmt = $this->pdo->prepare(
            'SELECT 1 FROM information_schema.tables
             WHERE table_schema = DATABASE() AND table_name = ? LIMIT 1'
        );
        $stmt->execute([$tabla]);
        return (bool)$stmt->fetchColumn();
    }
}
