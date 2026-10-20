<?php
require_once __DIR__ . "/database.php";

class Producto
{
    private $db;
    private $pdo;

    public function __construct()
    {
        $this->db = new Database();
        $this->pdo = $this->db->connect(); // Asegúrate de que este método retorne un objeto PDO
    }

    public function insertarProducto(
        $codigo_producto,
        $descripcion_producto,
        $cantidad_paca_producto,
        $precio_unidad,
        $precio_paca,
        $id_cate_producto,
        $acti_Unidad,
        $imagen_producto,
        $estado_producto
    ) {
        $query = $this->pdo->prepare("
            INSERT INTO productos (
                codigo_productos, 
                descripcion_producto,
                cantidad_paca_producto,
                precio_unidad_producto,
                precio_paca_producto,
                id_cate_producto,
                acti_Unidad, 
                imagen_producto,
                estado_producto
            ) VALUES (
                :codigo_producto,
                :descripcion_producto,
                :cantidad_paca_producto,
                :precio_unidad,
                :precio_paca,
                :id_cate_producto,
                :acti_Unidad,
                :imagen_producto,
                :estado_producto
            )
        ");

        return $query->execute([
            "codigo_producto" => $codigo_producto,
            "descripcion_producto" => $descripcion_producto,
            "cantidad_paca_producto" => $cantidad_paca_producto,
            "precio_unidad" => $precio_unidad,
            "precio_paca" => $precio_paca,
            "id_cate_producto" => $id_cate_producto,
            "acti_Unidad" => $acti_Unidad,
            "imagen_producto" => $imagen_producto,
            "estado_producto" => $estado_producto
        ]);
    }

    public function obtenerProductos($categoria, $busqueda, $limit = 12, $offset = 0)
    {
        $condicion = "";
        $condicion1 = "";
        $params = [];

        if (!empty($categoria) && $categoria !== "0") {
            $condicion .= " AND id_cate_producto = ?";
            $params[] = $categoria;
        }

        if (!empty($busqueda)) {
            $condicion1 = " AND id_producto = ?";
            $params[] = $busqueda;
        }

        $limit = (int)$limit;
        $offset = (int)$offset;

        $sql = "
        SELECT 
            id_producto, 
            precio_unidad_producto, 
            id_cate_producto, 
            precio_paca_producto, 
            descripcion_producto, 
            cantidad_paca_producto, 
            imagen_producto, 
            estado_producto,
            acti_Unidad,
            codigo_productos
        FROM productos 
        WHERE id_producto > 0 $condicion $condicion1
        and estado_producto = '1'
        ORDER BY cantidad_venta DESC, id_producto DESC
        LIMIT $limit OFFSET $offset
        ";

        $query = $this->pdo->prepare($sql);
        $query->execute($params);

        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    public function obtenerProductosLista($categoria)
    {
        if ($categoria == "0") {
            $conde = "";
        } else {
            $conde = "and id_cate_producto='$categoria'";
        }

        $query = $this->pdo->prepare("
        SELECT 
        `id_producto`, 
        `precio_unidad_producto`, 
        `id_cate_producto`, 
        `precio_paca_producto`, 
        `descripcion_producto`, 
        `cantidad_paca_producto`, 
        `imagen_producto`, 
        `estado_producto`,
        `acti_Unidad`,
        `codigo_productos`
        FROM productos where id_producto>0 $conde
        ORDER BY id_producto ASC
    ");

        $query->execute();
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    public function contarProductos($categoria)
    {
        $condicion = "";
        $params = [];

        if (!empty($categoria) && $categoria !== "0") {
            $condicion = "AND id_cate_producto = ?";
            $params[] = $categoria;
        }

        $sql = "SELECT COUNT(*) FROM productos WHERE id_producto > 0 $condicion";

        $query = $this->pdo->prepare($sql);
        $query->execute($params);

        return (int)$query->fetchColumn();
    }

    public function obtenerCategorias()
    {
        $query = $this->pdo->prepare("
            SELECT id_categoria, nombre_categoria, imagen_categoria
            FROM categorias
        ");

        $query->execute();
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

       public function getConnection()
    {
        $query = $this->pdo->prepare("
            SELECT * FROM promociones
        ");

        $query->execute();
        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    public function buscarSugerencias($termino)
    {
        $query = $this->pdo->prepare("
            SELECT id_producto, descripcion_producto 
            FROM productos 
            WHERE  estado_producto = '1' and descripcion_producto LIKE :termino 
            ORDER BY descripcion_producto 
            LIMIT 10
        ");
        $query->bindValue(':termino', '%' . $termino . '%');
        $query->execute();

        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    public function buscarParaPromo($termino)
    {
        $termino = trim((string)$termino);
        $longitud = function_exists('mb_strlen') ? mb_strlen($termino) : strlen($termino);
        if ($longitud < 2) {
            return [];
        }

        $query = $this->pdo->prepare("
            SELECT
                id_producto,
                codigo_productos,
                descripcion_producto,
                precio_unidad_producto,
                precio_paca_producto,
                cantidad_paca_producto,
                acti_Unidad
            FROM productos
            WHERE estado_producto = '1'
              AND (
                descripcion_producto LIKE :termino
                OR codigo_productos LIKE :termino_codigo
              )
            ORDER BY descripcion_producto
            LIMIT 12
        ");

        $like = '%' . $termino . '%';
        $query->bindValue(':termino', $like);
        $query->bindValue(':termino_codigo', $like);
        $query->execute();

        return $query->fetchAll(PDO::FETCH_ASSOC);
    }

    /**
     * Verificar si existe un producto por ID
     */
    public function existeProducto($id_producto)
    {
        $query = $this->pdo->prepare("
        SELECT COUNT(*) as total 
        FROM productos 
        WHERE id_producto = :id_producto
    ");

        $query->bindParam(':id_producto', $id_producto, PDO::PARAM_INT);
        $query->execute();

        $resultado = $query->fetch(PDO::FETCH_ASSOC);
        return $resultado['total'] > 0;
    }

    /**
     * Actualizar un producto existente
     */
    public function actualizarProducto($id_producto, $codigo_producto, $descripcion_producto, $cantidad_paca_producto, $precio_unidad, $precio_paca, $id_cate_producto, $acti_Unidad, $imagen_producto, $estado_producto)
    {
        $query = $this->pdo->prepare("
        UPDATE productos SET 
            codigo_productos = :codigo_producto,
            descripcion_producto = :descripcion_producto,
            cantidad_paca_producto = :cantidad_paca_producto,
            precio_unidad_producto = :precio_unidad,
            precio_paca_producto = :precio_paca,
            id_cate_producto = :id_cate_producto,
            acti_Unidad = :acti_Unidad,
            imagen_producto = :imagen_producto,
            estado_producto = :estado_producto
        WHERE id_producto = :id_producto
    ");

        $query->bindParam(':id_producto', $id_producto, PDO::PARAM_INT);
        $query->bindParam(':codigo_producto', $codigo_producto, PDO::PARAM_STR);
        $query->bindParam(':descripcion_producto', $descripcion_producto, PDO::PARAM_STR);
        $query->bindParam(':cantidad_paca_producto', $cantidad_paca_producto);
        $query->bindParam(':precio_unidad', $precio_unidad);
        $query->bindParam(':precio_paca', $precio_paca);
        $query->bindParam(':id_cate_producto', $id_cate_producto);
        $query->bindParam(':acti_Unidad', $acti_Unidad, PDO::PARAM_STR);
        $query->bindParam(':imagen_producto', $imagen_producto, PDO::PARAM_STR);
        $query->bindParam(':estado_producto', $estado_producto, PDO::PARAM_STR);

        return $query->execute();
    }
    public function obtenerProductoPorCodigo($codigo_productos)
    {
        try {
            $query = $this->pdo->prepare("
            SELECT * FROM productos 
            WHERE codigo_productos = :codigo_productos 
            LIMIT 1
        ");

            $query->bindParam(':codigo_productos', $codigo_productos, PDO::PARAM_STR);
            $query->execute();

            $resultado = $query->fetch(PDO::FETCH_ASSOC);
            return $resultado ? $resultado : null;
        } catch (Exception $e) {
            error_log("Error en obtenerProductoPorCodigo: " . $e->getMessage());
            return null;
        }
    }

    /**
     * Obtener producto por ID
     */
    public function obtenerProductoPorId($id_producto)
    {
        try {
            $query = $this->pdo->prepare("
            SELECT * FROM productos 
            WHERE id_producto = :id_producto 
            LIMIT 1
        ");

            $query->bindParam(':id_producto', $id_producto, PDO::PARAM_INT);
            $query->execute();

            $resultado = $query->fetch(PDO::FETCH_ASSOC);
            return $resultado ? $resultado : null;
        } catch (Exception $e) {
            error_log("Error en obtenerProductoPorId: " . $e->getMessage());
            return null;
        }
    }

    /**
     * Actualizar producto por código (para actualizaciones masivas de precios)
     */

    public function actualizarProductoPorCodigo($codigo_productos, $precio_unidad_producto, $precio_paca_producto)
    {
        try {
            $query = $this->pdo->prepare("
            UPDATE productos SET 
                precio_unidad_producto = :precio_unidad_producto,
                precio_paca_producto = :precio_paca_producto
            WHERE codigo_productos = :codigo_productos
        ");

            $query->bindParam(':codigo_productos', $codigo_productos, PDO::PARAM_STR);
            $query->bindParam(':precio_unidad_producto', $precio_unidad_producto);
            $query->bindParam(':precio_paca_producto', $precio_paca_producto);

            return $query->execute();
        } catch (Exception $e) {
            error_log("Error en actualizarPreciosPorCodigo: " . $e->getMessage());
            return false;
        }
    }

    public function actualizarPreciosPorId($id_producto, $precio_unidad_producto, $precio_paca_producto)
    {
        try {
            $query = $this->pdo->prepare("
            UPDATE productos SET
                precio_unidad_producto = :precio_unidad_producto,
                precio_paca_producto = :precio_paca_producto
            WHERE id_producto = :id_producto
        ");

            $query->bindParam(':id_producto', $id_producto, PDO::PARAM_INT);
            $query->bindParam(':precio_unidad_producto', $precio_unidad_producto);
            $query->bindParam(':precio_paca_producto', $precio_paca_producto);

            return $query->execute();
        } catch (Exception $e) {
            error_log("Error en actualizarPreciosPorId: " . $e->getMessage());
            return false;
        }
    }


    /**
     * Verificar si existe un producto por código
     */
    public function existeProductoPorCodigo($codigo_productos)
    {
        try {
            $query = $this->pdo->prepare("
            SELECT COUNT(*) as total 
            FROM productos 
            WHERE codigo_productos = :codigo_productos
        ");

            $query->bindParam(':codigo_productos', $codigo_productos, PDO::PARAM_STR);
            $query->execute();

            $resultado = $query->fetch(PDO::FETCH_ASSOC);
            return $resultado['total'] > 0;
        } catch (Exception $e) {
            error_log("Error en existeProductoPorCodigo: " . $e->getMessage());
            return false;
        }
    } 
    public function clienteExistePorTelefono($telefono)
    {
        try {
            $sql = "SELECT COUNT(*) as total FROM clientes WHERE cli_telefono = ?";
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute([$telefono]);
            $resultado = $stmt->fetch(PDO::FETCH_ASSOC);
            return intval($resultado['total']) > 0;
        } catch (Exception $e) {
            error_log("Error en clienteExistePorTelefono: " . $e->getMessage());
            return false;
        }
    }

    /**
     * Obtener todos los clientes por teléfono
     */
    public function obtenerClientesPorTelefono($telefono)
    {
        try {
            $sql = "SELECT id_cliente, cli_nombre, cli_direccion, cli_telefono FROM clientes WHERE cli_telefono = ?";
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute([$telefono]);
            return $stmt->fetchAll(PDO::FETCH_ASSOC);
        } catch (Exception $e) {
            error_log("Error en obtenerClientesPorTelefono: " . $e->getMessage());
            return [];
        }
    }
}
