<?php
/**
 * Entrega la imagen de una promoción por ID.
 * Evita el 404 de Hostinger con nombres tipo promo_xxx.123456.jpg (doble extensión).
 */

require_once __DIR__ . '/../models/database.php';

$id = isset($_GET['id']) ? (int)$_GET['id'] : 0;
$pedido = isset($_GET['f']) ? basename(str_replace('\\', '/', (string)$_GET['f'])) : '';
$pdo = null;

$extPermitidas = ['jpg', 'jpeg', 'png', 'gif', 'webp'];
$directorios = [
    __DIR__ . '/../assets/img/licores/promos/',
    __DIR__ . '/../assets/img/licores/',
    __DIR__ . '/../uploads/',
];

$filename = '';
if ($id > 0) {
    try {
        $pdo = (new Database())->connect();
        $stmt = $pdo->prepare('SELECT imagen FROM promociones WHERE id_promocion = :id LIMIT 1');
        $stmt->execute(['id' => $id]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        $filename = basename(str_replace('\\', '/', (string)($row['imagen'] ?? '')));
    } catch (Exception $e) {
        $filename = '';
    }
}

if ($filename === '' && $pedido !== '') {
    $filename = $pedido;
}

$path = null;
if ($filename !== '') {
    $ext = strtolower(pathinfo($filename, PATHINFO_EXTENSION));
    if (in_array($ext, $extPermitidas, true)) {
        foreach ($directorios as $dir) {
            $candidato = $dir . $filename;
            if (is_file($candidato)) {
                $path = $candidato;
                break;
            }
        }
    }
}

if ($path === null) {
    $path = __DIR__ . '/../assets/img/logoM.png';
    if (!is_file($path)) {
        http_response_code(404);
        exit;
    }
}

$ext = strtolower(pathinfo($path, PATHINFO_EXTENSION));
$mimes = [
    'jpg' => 'image/jpeg',
    'jpeg' => 'image/jpeg',
    'png' => 'image/png',
    'gif' => 'image/gif',
    'webp' => 'image/webp',
];

header('Content-Type: ' . ($mimes[$ext] ?? 'application/octet-stream'));
header('Content-Length: ' . (string)filesize($path));
header('Cache-Control: public, max-age=86400');
header('X-Content-Type-Options: nosniff');
readfile($path);
exit;
