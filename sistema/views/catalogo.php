<?php
require_once __DIR__ . '/../config/catalogo_router.php';
catalogo_redirigir_si_react('catalogo');

//include_once "header.php";
require_once "../models/database.php";
require_once "../models/ProductoModel.php";
$categoria = $_GET['categoria'] ?? '';
$nombre = $_GET['nombre'] ?? '';
$busqueda = $_GET['id'] ?? '';
$numCliente = $_GET['idCli'] ?? '';
// Obtener productos desde la base de datos
$producto = new Producto();
$page = isset($_GET['page']) ? max(1, (int)$_GET['page']) : 1;
$limit = 12;
$offset = ($page - 1) * $limit;

$totalProductos = $producto->contarProductos($categoria);
$totalPaginas = ceil($totalProductos / $limit);

// Llamada al nuevo método paginado
$productos = $producto->obtenerProductos($categoria, $busqueda, $limit, $offset);
function hayPromocionesActivas() {
    try {
        // Tu función getConnection() ya retorna todas las promociones
        $producto = new Producto();
        $promociones = $producto->getConnection(); // Esto retorna array con todas las promociones
        
        // Filtrar las promociones activas (estado = 1)
        $promocionesActivas = array_filter($promociones, function($promo) {
            return isset($promo['estado']) && $promo['estado'] == 1;
        });
        
        return count($promocionesActivas) > 0;
        
    } catch (Exception $e) {
        error_log("Error en hayPromocionesActivas: " . $e->getMessage());
        return false;
    }
}
$tienePromociones = hayPromocionesActivas();


?>

<!DOCTYPE html>
<html lang="es">
<link rel="icon" type="image/png" href="../assets/img/logoM.png">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Multilicores - Catálogo de Productos</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css" rel="stylesheet">
    <link href="../css/catalogo.css" rel="stylesheet" type="text/css" />
</head>

<body>
    <!-- Header Moderno -->
    <header class="header-modern bg-white border-bottom">
        <div class="container py-3">
            <div class="d-flex flex-column flex-lg-row align-items-start align-items-lg-center justify-content-between gap-3">

                <!-- Logo + título -->
                <div class="d-flex align-items-center gap-2">
                    <img src="../assets/img/logoM.png" alt="Logo Multilicores" class="logo-img" style="height: 50px;">
                    <div class="d-flex flex-column">
                        <h1 class="company-title m-0 fs-6">Multilicores</h1>
                        <p class="company-subtitle m-0 small">Distribución especializada en Licores</p>
                    </div>
                </div>

                <!-- Menú de navegación con hamburguesa -->
                <nav class="navbar navbar-expand-lg navbar-light p-0 flex-grow-1">
                    <button class="navbar-toggler ms-auto" type="button" data-bs-toggle="collapse" data-bs-target="#navbarMenu">
                        <span class="navbar-toggler-icon"></span>
                    </button>
                    <div class="collapse navbar-collapse justify-content-center mt-3 mt-lg-0" id="navbarMenu">
                         <ul class="navbar-nav mx-auto mb-2 mb-lg-0 gap-lg-4">
                    <li class="nav-item">
                        <a class="nav-link fw-semibold text-muted" href="categorias.php?idCli=<?php echo urlencode($numCliente); ?>">Categorías</a>
                    </li>
                    <?php if ($tienePromociones): ?>
                    <li class="nav-item">
                        <a class="nav-link fw-semibold" href="promociones.php?idCli=<?php echo urlencode($numCliente); ?>" style="color: #e11d48;">
                            <i class="fas fa-fire me-1"></i>Promociones
                        </a>
                    </li>
                    <?php endif; ?>
                    <li class="nav-item">
                        <a class="nav-link fw-semibold text-muted" href="catalogo.php?idCli=<?php echo urlencode($numCliente); ?>">Productos</a>
                    </li>
                </ul>
                    </div>
                </nav>

                <!-- Buscador + carrito SIEMPRE visibles -->
                <div class="d-flex align-items-center gap-2 mt-3 mt-lg-0 flex-shrink-0" style="min-width: 250px;">
                    <input type="text" class="form-control search-input" placeholder="Buscar productos..." id="searchInput" autocomplete="off">
                    <button class="btn btn-outline-secondary position-relative" type="button">
                        <i class="fas fa-shopping-cart fa-lg"></i>
                        <span class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-success" id="cartCount">0</span>
                    </button>
                </div>

            </div>
        </div>
    </header>

    <!-- Catálogo de Productos -->
    <div class="container">
        <div class="catalog-header">
        </div>

        <form method="POST" action="procesar_pedido.php" id="orderForm">
            <div class="row" id="productGrid">
                <?php if (!empty($productos)): ?>
                    <?php foreach ($productos as $index => $prod): ?>
                        <div class="col-xl-3 col-lg-4 col-md-6 col-sm-12 mb-4 product-item" data-name="<?php echo strtolower($prod['descripcion_producto']); ?>">
                            <div class="product-card">
                                <div class="position-relative">
                                    <div class="    ">
                                        <img src="<?php echo '../assets/img/licores/' . $prod['imagen_producto']; ?>"
                                            class="product-image"
                                            loading="lazy"
                                            alt="<?php echo htmlspecialchars($prod['descripcion_producto']); ?>"
                                            onerror="this.src='/placeholder.svg?height=220&width=300&text=Producto'">
                                    </div>
                                    <div class="category-badge">
                                        <?php echo strtoupper($categoria ?: 'LICOR'); ?>
                                    </div>
                                </div>

                                <div class="p-3">
                                    <h5 class="product-title"><?php echo htmlspecialchars($prod['descripcion_producto']); ?></h5>


                                    <div class="price-container">
                                        <div class="price-row">
                                            <?php if ($prod['acti_Unidad'] != 0): ?>
                                                <span class="price-label">Precio Unidad:</span>
                                                <span class="price-unidad">
                                                    $<?php echo number_format($prod['precio_unidad_producto'], 0, ',', '.'); ?> COP
                                                </span>
                                            <?php endif; ?>
                                        </div>
                                        <div class="price-row">
                                            <span class="price-label">Precio Paca:</span>
                                            <span class="price-paca">$<?php echo number_format($prod['precio_paca_producto'], 0, ',', '.'); ?> COP</span>
                                        </div>
                                    </div>

                                    <div class="row align-items-end g-2">
                                        <div class="col-6">
                                            <label class="form-label small">Tipo</label>
                                            <select name="productos[<?php echo $index; ?>][tipo]"
                                                class="form-select tipo-select form-select-sm"
                                                data-index="<?php echo $index; ?>"
                                                data-precio-unidad="<?php echo $prod['precio_unidad_producto']; ?>"
                                                data-precio-paca="<?php echo $prod['precio_paca_producto']; ?>">
                                                <option value="">Tipo</option>
                                                <?php if ((int)$prod['acti_Unidad'] !== 0): ?>
                                                    <option value="unidad">Unidad</option>
                                                <?php endif; ?>
                                                <option value="paca">Paca</option>
                                            </select>
                                        </div>
                                        <div class="col-6">
                                            <label class="form-label small">Cant.</label>
                                            <input type="number"
                                                name="productos[<?php echo $index; ?>][cantidad]"
                                                class="form-control form-control-sm cantidad-input"
                                                min="1"
                                                value="1"
                                                placeholder="1"
                                                data-index="<?php echo $index; ?>">
                                        </div>
                                    </div>

                                    <!-- Botón Agregar -->
                                    <div class="text-end mt-2">
                                        <button type="button"
                                            class="btn btn-outline-success w-100 agregar-btn"
                                            data-id="<?php echo $prod['id_producto']; ?>"
                                            data-nombre="<?php echo htmlspecialchars($prod['descripcion_producto']); ?>"
                                            data-precio-unidad="<?php echo $prod['precio_unidad_producto']; ?>"
                                            data-precio-paca="<?php echo $prod['precio_paca_producto']; ?>">
                                            <i class="fas fa-cart-plus me-1"></i> Agregar
                                        </button>
                                    </div>


                                </div>
                            </div>
                        </div>
                    <?php endforeach; ?>
                <?php else: ?>
                    <div class="col-12">
                        <div class="text-center py-5">
                            <i class="fas fa-box-open fa-3x text-muted mb-3"></i>
                            <h4 class="text-muted">No hay productos registrados</h4>
                        </div>
                    </div>
                <?php endif; ?>
            </div>

            <!-- Resumen del Pedido -->
            <div class="order-summary" id="orderSummary" style="display: none;">
                <div class="summary-header">
                    <h3 class="summary-title">Resumen del Pedido</h3>
                    <span class="items-badge" id="itemsCount">0 productos</span>
                </div>
                <div class="total-amount">
                    Total: $<span id="totalAmount">0</span> COP
                </div>
            </div>

            <div class="text-center mb-5">
                <button type="submit" class="btn btn-success submit-btn" id="submitBtn" disabled>
                    <i class="fas fa-shopping-cart me-2"></i>
                    Enviar Pedido <span id="btnItemCount"></span>
                </button>
            </div>
            <div id="noResults" class="text-center text-muted py-4" style="display: none;">
                <i class="fas fa-search fa-2x mb-2"></i>
                <p>No se encontraron productos.</p>
            </div>
        </form>

        <?php if ($totalPaginas > 1): ?>
            <nav class="d-flex justify-content-center mb-5">
                <ul class="pagination">
                    <?php for ($i = 1; $i <= $totalPaginas; $i++): ?>
                        <li class="page-item <?php echo ($i === $page) ? 'active' : ''; ?>">
                            <a class="page-link" href="?categoria=<?php echo urlencode($categoria); ?>&page=<?php echo $i; ?>">
                                <?php echo $i; ?>
                            </a>
                        </li>
                    <?php endfor; ?>
                </ul>
            </nav>
        <?php endif; ?>
    </div>
    <input type="text" id="numCliente" data-numcliente="<?php echo $numCliente; ?>" />
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>
    <script src="../js/catalogo.js?v=20260710"></script>
</body>

</html>