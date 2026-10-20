<?php
require_once "../models/Promo.php";

function responderJson($success, $message = "", $extra = []) {
    header('Content-Type: application/json');
    echo json_encode(array_merge([
        "success" => $success,
        "message" => $message
    ], $extra));
    exit;
}

function normalizarEstado($estado) {
    if ($estado === "Activa" || $estado === "1" || $estado === 1) {
        return 1;
    }

    return 0;
}

function nombreArchivoPromo(string $nombreOriginal, string $extension, string $directorio): string
{
    $base = pathinfo($nombreOriginal, PATHINFO_FILENAME);
    if (function_exists('iconv')) {
        $transliterado = @iconv('UTF-8', 'ASCII//TRANSLIT//IGNORE', $base);
        if (is_string($transliterado) && $transliterado !== '') {
            $base = $transliterado;
        }
    }

    $base = strtolower($base);
    $base = preg_replace('/[^a-z0-9]+/', '-', $base);
    $base = trim($base, '-');
    if ($base === '') {
        $base = 'promo';
    }

    $base = substr($base, 0, 80);

    do {
        $nombre = $base . '-' . bin2hex(random_bytes(4)) . '.' . $extension;
    } while (is_file($directorio . $nombre));

    return $nombre;
}

function subirImagen($requerida = false) {
    $sinArchivo = !isset($_FILES["imagen"])
        || $_FILES["imagen"]["error"] === UPLOAD_ERR_NO_FILE
        || empty($_FILES["imagen"]["name"])
        || (int)($_FILES["imagen"]["size"] ?? 0) <= 0;

    if ($sinArchivo) {
        if ($requerida) {
            responderJson(false, "Debe subir una imagen para la promocion.");
        }

        return "";
    }

    if ($_FILES["imagen"]["error"] !== UPLOAD_ERR_OK) {
        responderJson(false, "Hubo un error al subir la imagen.");
    }

    $nombreOriginal = $_FILES["imagen"]["name"];
    $tmpPath = $_FILES["imagen"]["tmp_name"];
    $extension = strtolower(pathinfo($nombreOriginal, PATHINFO_EXTENSION));
    $extensionesPermitidas = ["jpg", "jpeg", "png", "gif", "webp"];

    if (!in_array($extension, $extensionesPermitidas, true)) {
        responderJson(false, "Formato de imagen no permitido.");
    }

    $directorio = __DIR__ . "/../assets/img/licores/promos/";
    if (!is_dir($directorio) && !mkdir($directorio, 0755, true) && !is_dir($directorio)) {
        responderJson(false, "No existe la carpeta de imagenes de promociones.");
    }

    $nombreImagen = nombreArchivoPromo($nombreOriginal, $extension, $directorio);
    $destino = $directorio . $nombreImagen;

    if (!move_uploaded_file($tmpPath, $destino)) {
        responderJson(false, "Error al mover la imagen al servidor.");
    }

    return $nombreImagen;
}

if ($_SERVER["REQUEST_METHOD"] !== "POST") {
    responderJson(false, "Metodo no permitido.");
}

$metodo = $_POST["action"] ?? "";
$promo = new Promo();

try {
    if ($metodo === "insert" || $metodo === "update") {
        $id = $_POST["id"] ?? null;
        $titulo = trim($_POST["titulo"] ?? "");
        $descripcion = trim($_POST["descripcion"] ?? "");
        $codigo = (int)($_POST["codigo"] ?? 0);
        $precioUnidad = (int)($_POST["precio_unidad_producto"] ?? 0);
        $precioPaca = (int)($_POST["precio_paca_producto"] ?? 0);
        $actiUnidad = (int)($_POST["acti_Unidad"] ?? 0);
        $estado = normalizarEstado($_POST["estado"] ?? 1);
        $prioridad = (int)($_POST["prioridad"] ?? 0);
        $nombreImagen = subirImagen($metodo === "insert");

        if ($metodo === "update") {
            if (empty($id)) {
                responderJson(false, "No se recibio el ID de la promocion.");
            }

            $actual = $promo->getPromoById($id);
            if (!$actual) {
                responderJson(false, "No se encontro la promocion.");
            }

            if ($titulo === "") {
                $titulo = trim((string)($actual["titulo"] ?? ""));
            }
            if ($descripcion === "") {
                $descripcion = trim((string)($actual["descripcion"] ?? ""));
            }
            if ($codigo <= 0) {
                $codigo = (int)($actual["codigo"] ?? 0);
            }
            if ($precioUnidad <= 0 && $precioPaca <= 0) {
                $precioUnidad = (int)($actual["precio_unidad_producto"] ?? 0);
                $precioPaca = (int)($actual["precio_paca_producto"] ?? 0);
            }
            if (!isset($_POST["estado"])) {
                $estado = normalizarEstado($actual["estado"] ?? 1);
            }
            if (!isset($_POST["prioridad"])) {
                $prioridad = (int)($actual["prioridad"] ?? 0);
            }
            if (!isset($_POST["acti_Unidad"])) {
                $actiUnidad = (int)($actual["acti_Unidad"] ?? 0);
            }
            if ($nombreImagen === "") {
                $nombreImagen = (string)($actual["imagen"] ?? "");
            }
        }

        if ($titulo === "" || $descripcion === "") {
            responderJson(false, "El titulo y la descripcion son obligatorios.");
        }

        if ($codigo <= 0) {
            responderJson(false, "El codigo del producto es obligatorio.");
        }

        if ($metodo === "insert") {
            $guardado = $promo->registerPromo(
                $titulo,
                $descripcion,
                $codigo,
                $precioUnidad,
                $precioPaca,
                $actiUnidad,
                $nombreImagen,
                $estado,
                $prioridad
            );

            responderJson($guardado, $guardado ? "Promocion registrada correctamente." : "No se pudo registrar la promocion.");
        }

        $guardado = $promo->updatePromo(
            $id,
            $titulo,
            $descripcion,
            $codigo,
            $precioUnidad,
            $precioPaca,
            $actiUnidad,
            $nombreImagen,
            $estado,
            $prioridad
        );

        responderJson($guardado, $guardado ? "Promocion actualizada correctamente." : "No se pudo actualizar la promocion.");
    }

    if ($metodo === "delete") {
        $id = $_POST["id"] ?? null;
        if (empty($id)) {
            responderJson(false, "No se recibio el ID de la promocion.");
        }

        $eliminado = $promo->deletePromo($id);
        responderJson($eliminado, $eliminado ? "Promocion eliminada correctamente." : "No se pudo eliminar la promocion.");
    }

    if ($metodo === "cambiarEstado") {
        $id = $_POST["id"] ?? null;
        $estado = normalizarEstado($_POST["estado"] ?? 0);

        if (empty($id)) {
            responderJson(false, "No se recibio el ID de la promocion.");
        }

        $actualizado = $promo->updateEstadoPromo($id, $estado);
        responderJson($actualizado, $actualizado ? "Estado actualizado correctamente." : "No se pudo actualizar el estado.");
    }

    if ($metodo === "mostrar") {
        responderJson(true, "", ["promociones" => $promo->getAllPromos()]);
    }

    if ($metodo === "buscarProducto") {
        $termino = trim($_POST["q"] ?? "");
        $longitud = function_exists('mb_strlen') ? mb_strlen($termino) : strlen($termino);
        if ($longitud < 2) {
            responderJson(true, "", ["productos" => []]);
        }

        require_once __DIR__ . "/../models/ProductoModel.php";
        $productoModel = new Producto();
        $productos = $productoModel->buscarParaPromo($termino);

        responderJson(true, "", ["productos" => $productos ?: []]);
    }

    if ($metodo === "enviarPromo") {
        $id = $_POST['id'] ?? null;
        $texto = $_POST['texto'] ?? null;
        $imagen1 = $_POST['imagen1'] ?? null;
        $resultados = $promo->enviarPromo($id, $texto, $imagen1);

        responderJson(true, "Promocion enviada correctamente.", [
            'resultados' => $resultados
        ]);
    }

    responderJson(false, "Accion no valida.");
} catch (Exception $e) {
    responderJson(false, "Error del servidor: " . $e->getMessage());
}
?>
