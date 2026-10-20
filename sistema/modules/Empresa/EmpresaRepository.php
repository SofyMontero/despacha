<?php

class EmpresaRepository
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

    public function listarActivas(): array
    {
        if (!$this->tablaExiste('empresas')) {
            return [];
        }

        $sql = "SELECT id_empresa, nombre, slug, modelo_negocio, activa
                FROM empresas
                WHERE activa = 1
                ORDER BY id_empresa";
        return $this->pdo->query($sql)->fetchAll(PDO::FETCH_ASSOC);
    }

    public function obtener(int $id): ?array
    {
        if (!$this->tablaExiste('empresas')) {
            return null;
        }
        $stmt = $this->pdo->prepare(
            'SELECT id_empresa, nombre, slug, modelo_negocio, activa
             FROM empresas WHERE id_empresa = ? LIMIT 1'
        );
        $stmt->execute([$id]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
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
