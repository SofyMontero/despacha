<?php

/**
 * Modelo para manejar operaciones de la tabla bares
 */
class Bar
{
    private $db;
    private $pdo;

    public function __construct()
    {
        $this->db = new Database();
        $this->pdo = $this->db->connect(); // Asegúrate de que este método retorne un objeto PDO
    }

    /**
     * Insertar un nuevo bar
     */
    public function insertarBar($nombre_bar, $direccion)
    {
        $query = $this->pdo->prepare("
        INSERT INTO bares (
            nombre_bar, 
            direccion_bar
        ) VALUES (
            :nombre_bar,
            :direccion           
        )
    ");

        return $query->execute([
            "nombre_bar" => $nombre_bar,
            "direccion" => $direccion
        ]);
    }

    /**
     * Obtener todos los bares activos
     */
    public function obtenerBares()
    {
        $query = $this->pdo->prepare("
            SELECT * FROM bares 
            WHERE 
            ORDER BY nombre_bar ASC
        ");

        $query->execute();
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    /**
     * Buscar bares por nombre (para autocompletado)
     */
    public function buscarBaresPorNombre($termino)
    {
        $query = $this->pdo->prepare("
            SELECT id_bar, nombre_bar, direccion_bar
            FROM bares 
            WHERE nombre_bar LIKE :termino            
            ORDER BY nombre_bar ASC 
            LIMIT 10
        ");

        $query->execute(["termino" => '%' . $termino . '%']);
        return $query->fetchAll(PDO::FETCH_ASSOC);;
    }

    /**
     * Obtener un bar por ID
     */
    public function obtenerBarPorId($id)
    {
        $query = $this->pdo->prepare("
            SELECT * FROM bares 
            WHERE id_bar = :id
        ");

        $query->execute(["id" => $id]);
        return $query->fetch(PDO::FETCH_ASSOC);
    }

    /**
     * Actualizar un bar
     */
    public function actualizarBar(
        $id_bar,
        $nombre_bar,
        $direccion,
        $telefono,
        $email,
        $descripcion
    ) {
        $query = $this->pdo->prepare("
            UPDATE bares SET
                nombre_bar = :nombre_bar,
                direccion_bar = :direccion               
            WHERE id_bar = :id_bar
        ");

        return $query->execute([
            "id_bar" => $id_bar,
            "nombre_bar" => $nombre_bar,
            "direccion_bar" => $direccion
        ]);
    }

    /**
     * Eliminar un bar (cambiar estado)
     */
    public function eliminarBar($id_bar)
    {
        $query = $this->pdo->prepare("
            DELETE FROM bares 
            WHERE id_bar = :id_bar
        ");

        return $query->execute(["id_bar" => $id_bar]);
    }

    /**
     * Verificar si existe un bar con el mismo nombre
     */
    public function existeBar($nombre_bar, $id_excluir = null)
    {
        $sql = "SELECT COUNT(*) FROM bares WHERE nombre_bar = :nombre_bar ";
        $params = ["nombre_bar" => $nombre_bar];

        if ($id_excluir) {
            $sql .= " AND id_bar != :id_excluir";
            $params["id_excluir"] = $id_excluir;
        }

        $query = $this->pdo->prepare($sql);
        $query->execute($params);

        return $query->fetchColumn() > 0;
    }
    public function obtenerClientes()
    {
        $query = $this->pdo->prepare("
        SELECT  C.id_cliente, C.cli_nombre, C.cli_telefono, C.cli_direccion, C.cli_zona, C.cli_fecha_registro, B.nombre_bar
            FROM 
                clientes C
            INNER JOIN 
                bares B ON C.cli_Bar = B.id_bar
            ORDER BY 
                C.cli_fecha_registro DESC;
    ");
        $query->execute();
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    public function insertarCliente($id_bar, $razon_social, $telefono, $direccion, $zona)
    {
        $query = $this->pdo->prepare("
        INSERT INTO clientes (
            cli_Bar, cli_nombre, cli_telefono, cli_direccion, cli_zona
        ) VALUES (
            :id_bar, :razon_social, :telefono, :direccion, :zona
        )
    ");

        return $query->execute([
            "id_bar"       => $id_bar,
            "razon_social" => $razon_social,
            "telefono"     => $telefono,
            "direccion"    => $direccion,
            "zona"         => $zona
        ]);
    }
    public function eliminarCliente($id_cliente)
    {
        $query = $this->pdo->prepare("
        DELETE FROM clientes 
        WHERE id_cliente = :id_cliente
    ");

        return $query->execute(["id_cliente" => $id_cliente]);
    }
    public function actualizarCliente($id_cliente, $bar_id, $nombre, $telefono, $direccion, $zona)
    {
        $query = $this->pdo->prepare("
        UPDATE clientes SET
            cli_Bar = :bar_id,
            cli_nombre = :nombre,
            cli_telefono = :telefono,
            cli_direccion = :direccion,
            cli_zona = :zona
        WHERE id_cliente = :id_cliente
    ");

        return $query->execute([
            'bar_id'     => $bar_id,
            'nombre'     => $nombre,
            'telefono'   => $telefono,
            'direccion'  => $direccion,
            'zona'       => $zona,
            'id_cliente' => $id_cliente
        ]);
    }
    public function obtenerUltimoIdInsertado()
    {
        return $this->pdo->lastInsertId();
    }

    public function obtenerClientesPaginado($limite, $offset)
    {
        $query = $this->pdo->prepare("
        SELECT c.*, b.nombre_bar 
        FROM clientes c
        INNER JOIN bares b ON c.cli_Bar = b.id_bar
        ORDER BY c.id_cliente DESC
        LIMIT :limite OFFSET :offset
    ");

        $query->bindParam(':limite', $limite, PDO::PARAM_INT);
        $query->bindParam(':offset', $offset, PDO::PARAM_INT);
        $query->execute();

        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    public function contarTotalClientes()
    {
        $query = $this->pdo->query("SELECT COUNT(*) as total FROM clientes");
        $resultado = $query->fetch(PDO::FETCH_ASSOC);
        return $resultado['total'] ?? 0;
    }
}
