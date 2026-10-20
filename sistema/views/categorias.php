<?php
require_once __DIR__ . '/../config/catalogo_router.php';
catalogo_redirigir_si_react('');

// include_once "header.php";
require_once "../models/database.php";
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);

require_once "../models/ProductoModel.php";
$numCliente = $_GET['idCli'] ?? '';

// Obtener productos desde la base de datos
$producto = new Producto();
$productos = $producto->obtenerCategorias();

// Verificar si hay promociones activas
function hayPromocionesActivas()
{
    try {
        // Tu función getConnection() ya retorna todas las promociones
        $producto = new Producto();
        $promociones = $producto->getConnection(); // Esto retorna array con todas las promociones

        // Filtrar las promociones activas (estado = 1)
        $promocionesActivas = array_filter($promociones, function ($promo) {
            return isset($promo['estado']) && $promo['estado'] == 1;
        });

        return count($promocionesActivas) > 0;
    } catch (Exception $e) {
        error_log("Error en hayPromocionesActivas: " . $e->getMessage());
        return false;
    }
}

// Obtener promociones activas para el banner
function obtenerPromocionesActivas()
{
    try {
        // Tu función getConnection() ya retorna todas las promociones
        $producto = new Producto();
        $promociones = $producto->getConnection(); // Esto retorna array con todas las promociones

        // Filtrar las promociones activas (estado = 1)
        $promocionesActivas = array_filter($promociones, function ($promo) {
            return isset($promo['estado']) && $promo['estado'] == 1;
        });

        // Ordenar por prioridad y fecha
        usort($promocionesActivas, function ($a, $b) {
            // Primero ordenar por prioridad (descendente)
            if ($a['prioridad'] != $b['prioridad']) {
                return $b['prioridad'] - $a['prioridad'];
            }
            // Luego por fecha de creación (descendente)
            return strtotime($b['creado_en']) - strtotime($a['creado_en']);
        });

        // Limitar a 3 resultados
        return array_slice($promocionesActivas, 0, 3);
    } catch (Exception $e) {
        error_log("Error en obtenerPromocionesActivas: " . $e->getMessage());
        return [];
    }
}

$tienePromociones = hayPromocionesActivas();
$promocionesActivas = $tienePromociones ? obtenerPromocionesActivas() : [];
?>

<!DOCTYPE html>
<html lang="es">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Multilicores</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css" rel="stylesheet">
    <link href="../css/categoria.css" rel="stylesheet" type="text/css" />
    <style>
        /* Estilos para el banner de promociones — rojo del banner de promo */
        .promociones-banner {
            background: #e11d48;
            margin-bottom: 2rem;
            border-radius: 15px;
            overflow: hidden;
            box-shadow: 0 10px 30px rgba(225, 29, 72, 0.28);
            position: relative;
        }

        .promociones-banner::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: url('data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 20"><defs><pattern id="grain" width="100" height="20" patternUnits="userSpaceOnUse"><circle cx="10" cy="10" r="1" fill="white" opacity="0.1"/><circle cx="90" cy="5" r="1" fill="white" opacity="0.1"/><circle cx="50" cy="15" r="1" fill="white" opacity="0.1"/></pattern></defs><rect width="100" height="20" fill="url(%23grain)"/></svg>');
            pointer-events: none;
        }

        .banner-content {
            position: relative;
            z-index: 1;
        }

        .promo-badge {
            background: rgba(255, 255, 255, 0.2);
            color: white;
            padding: 0.25rem 0.75rem;
            border-radius: 20px;
            font-size: 0.875rem;
            font-weight: 600;
            display: inline-block;
            margin-bottom: 0.5rem;
        }

        .categoria-promociones {
            background: #fda4af;
            border: 2px solid #e11d48;
            position: relative;
            overflow: hidden;
        }

        .categoria-promociones .card-imagen {
            background: rgba(255, 255, 255, 0.55);
            border-color: rgba(225, 29, 72, 0.3);
        }

        .categoria-promociones img {
            border-color: rgba(225, 29, 72, 0.35);
        }

        .categoria-promociones::before {
            content: '';
            position: absolute;
            top: 10px;
            right: 10px;
            width: 10px;
            height: 10px;
            border-radius: 50%;
            background: #e11d48;
        }

        .btn-promociones {
            background: #e11d48;
            border: none;
            color: white;
            font-weight: 600;
            transition: all 0.3s ease;
        }

        .btn-promociones:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(225, 29, 72, 0.4);
            color: white;
            background: #be123c;
        }

        .carousel-item img {
            height: 200px;
            object-fit: cover;
            border-radius: 10px;
        }

        .promocion-titulo {
            color: white;
            font-weight: 700;
            margin-bottom: 0.5rem;
        }

        .promocion-descripcion {
            color: rgba(255, 255, 255, 0.9);
            font-size: 0.95rem;
            line-height: 1.4;
        }
    </style>
</head>

<body>
    <header class="header-modern bg-white border-bottom">
        <div class="container py-3">
            <nav class="navbar navbar-expand-lg navbar-light p-0">
                <div class="d-flex align-items-center gap-2 flex-shrink-0">
                    <img src="../assets/img/logoM.png" alt="Logo Multilicores" class="logo-img" style="height: 50px;">
                    <div class="d-flex flex-column">
                        <h1 class="company-title m-0 fs-5">Multilicores</h1>
                        <p class="company-subtitle m-0 small">Distribución especializada en Licores</p>
                    </div>
                </div>

                <button class="navbar-toggler ms-auto" type="button" data-bs-toggle="collapse" data-bs-target="#navbarContent">
                    <span class="navbar-toggler-icon"></span>
                </button>

                <div class="collapse navbar-collapse mt-3 mt-lg-0" id="navbarContent">
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

                    <form class="d-flex align-items-center gap-2 mt-3 mt-lg-0" role="search" onsubmit="event.preventDefault();">
                        <div class="position-relative w-100">
                            <input type="text" class="form-control search-input" placeholder="Buscar productos..." id="searchInput" autocomplete="off">
                            <ul id="autocompleteList" class="list-group position-absolute w-100 shadow-sm z-3" style="top: 100%; display: none;"></ul>
                        </div>
                        <button class="btn btn-primary px-3 search-btn" type="button" id="btnBuscarProducto">
                            <i class="fas fa-search text-white"></i>
                        </button>
                    </form>
                </div>
            </nav>
        </div>
    </header>

    <section style="padding: clamp(2rem, 5vw, 4rem) 0;">
        <div class="container-fluid px-3 px-md-4">

            <!-- Banner de Promociones (solo si hay promociones activas) -->
            <?php if ($tienePromociones && !empty($promocionesActivas)): ?>
                <div class="promociones-banner mb-4">
                    <div class="banner-content p-4">
                        <div class="row align-items-center">
                            <div class="col-md-8">
                                <div class="promo-badge">
                                    <i class="fas fa-fire me-1"></i>¡Ofertas Especiales!
                                </div>
                                <h2 class="text-white fw-bold mb-3">Promociones Activas</h2>

                                <?php if (count($promocionesActivas) == 1): ?>
                                    <!-- Una sola promoción -->
                                    <div class="text-white">
                                        <h4 class="promocion-titulo"><?php echo htmlspecialchars($promocionesActivas[0]['titulo']); ?></h4>
                                        <p class="promocion-descripcion"><?php echo htmlspecialchars($promocionesActivas[0]['descripcion']); ?></p>
                                    </div>
                                <?php else: ?>
                                    <!-- Carrusel para múltiples promociones -->
                                    <div id="promocionesCarousel" class="carousel slide" data-bs-ride="carousel" data-bs-interval="4000">
                                        <div class="carousel-inner">
                                            <?php foreach ($promocionesActivas as $index => $promo): ?>
                                                <div class="carousel-item <?php echo $index === 0 ? 'active' : ''; ?>">
                                                    <h4 class="promocion-titulo"><?php echo htmlspecialchars($promo['titulo']); ?></h4>
                                                    <p class="promocion-descripcion"><?php echo htmlspecialchars($promo['descripcion']); ?></p>
                                                </div>
                                            <?php endforeach; ?>
                                        </div>
                                    </div>
                                <?php endif; ?>
                            </div>
                            <div class="col-md-4 text-end">
                                <a href="promociones.php?idCli=<?php echo urlencode($numCliente); ?>"
                                    class="btn btn-light btn-lg px-4 py-2">
                                    <i class="fas fa-tags me-2"></i>Ver Todas
                                </a>
                            </div>
                        </div>
                    </div>
                </div>
            <?php endif; ?>

            <!-- Información de resultados -->
            <div id="searchInfo" class="search-results-info" style="display: none;">
                <i class="fas fa-info-circle me-2"></i>
                <span id="searchInfoText"></span>
            </div>

            <div class="grid-container" id="categoriasContainer">

                <!-- Categoría de Promociones (solo si hay promociones activas) -->
                <?php if ($tienePromociones): ?>
                    <div class="card-categoria categoria-promociones" data-categoria="promociones">
                        <div class="card-imagen loading">
                            <img
                                src="../assets/img/licores/promociones.jpg"
                                alt="Promociones Especiales"
                                loading="lazy"
                                onload="this.classList.add('loaded'); this.parentElement.classList.remove('loading')"
                                onerror="this.onerror=null; this.src='../assets/img/licores/promos.png';">
                        </div>
                        <div class="card-body-custom">
                            <h5 class="card-titulo categoria-nombre">
                                <i class="fas fa-fire me-2" style="color: #e11d48;"></i>Promociones
                            </h5>
                            <p class="card-descripcion">
                                Ofertas especiales y descuentos únicos
                            </p>
                            <a href="promociones.php?idCli=<?php echo urlencode($numCliente); ?>"
                                class="btn-categoria btn-promociones"
                                role="button"
                                aria-label="Ver promociones activas">
                                Ver ofertas <i class="fas fa-percentage ms-2"></i>
                            </a>
                        </div>
                    </div>
                <?php endif; ?>

                <!-- Categorías regulares -->
                <?php if (!empty($productos)): ?>
                    <?php foreach ($productos as $prod): ?>
                        <?php
                        $imagen = !empty($prod['imagen_categoria']) ? $prod['imagen_categoria'] : 'placeholder.jpg';
                        ?>
                        <div class="card-categoria" data-categoria="<?php echo strtolower(htmlspecialchars($prod['nombre_categoria'])); ?>">
                            <div class="card-imagen loading">
                                <img
                                    src="<?php echo '../assets/img/licores/' . $imagen; ?>"
                                    alt="<?php echo htmlspecialchars($prod['nombre_categoria']); ?>"
                                    loading="lazy"
                                    onload="this.classList.add('loaded'); this.parentElement.classList.remove('loading')"
                                    onerror="this.onerror=null; this.src='../assets/img/licores/placeholder.jpg';">
                            </div>
                            <div class="card-body-custom">
                                <h5 class="card-titulo categoria-nombre"><?php echo htmlspecialchars($prod['nombre_categoria']); ?></h5>
                                <p class="card-descripcion">
                                    Selección premium de <?php echo strtolower(htmlspecialchars($prod['nombre_categoria'])); ?>
                                </p>
                                <a href="catalogo.php?categoria=<?php echo urlencode($prod['id_categoria']); ?>&nombre=<?php echo urlencode($prod['nombre_categoria']); ?>&idCli=<?php echo urlencode($numCliente); ?>"
                                    class="btn-categoria"
                                    role="button"
                                    aria-label="Ver productos de <?php echo htmlspecialchars($prod['nombre_categoria']); ?>">
                                    Ver productos <i class="fas fa-arrow-right ms-2"></i>
                                </a>
                            </div>
                        </div>
                    <?php endforeach; ?>
                <?php else: ?>
                    <div class="text-center">
                        <div class="alert alert-info d-inline-block">
                            <i class="fas fa-info-circle me-2"></i>
                            No hay categorías registradas
                        </div>
                    </div>
                <?php endif; ?>
            </div>
        </div>
    </section>

    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>

    <script>
        document.addEventListener('DOMContentLoaded', function() {
            const searchInput = document.getElementById('searchInput');
            const searchButton = document.querySelector('.search-btn');

            function redirigirBusqueda() {
                const termino = searchInput.value.trim();
                if (termino.length > 0) {
                    const encodedTerm = encodeURIComponent(termino);
                    window.location.href = `catalogo.php?busqueda=${encodedTerm}`;
                }
            }
            searchButton.addEventListener('click', redirigirBusqueda);

            searchInput.addEventListener('keydown', function(e) {
                if (e.key === 'Enter') {
                    redirigirBusqueda();
                }
            });
        });

        // Autocompletar
        document.addEventListener('DOMContentLoaded', function() {
            const searchInput = document.getElementById('searchInput');
            const searchList = document.getElementById('autocompleteList');
            const searchButton = document.querySelector('.search-btn');

            function redirigirBusqueda(nombre, id) {
                const productoId = id || null;
                const encoded = encodeURIComponent(nombre || searchInput.value.trim());

                if (productoId) {
                    window.location.href = `catalogo.php?id=${productoId}`;
                } else if (encoded) {
                    window.location.href = `catalogo.php?busqueda=${encoded}`;
                }
            }

            function mostrarSugerencias(sugerencias) {
                searchList.innerHTML = '';
                if (sugerencias.length === 0) {
                    searchList.style.display = 'none';
                    return;
                }

                sugerencias.forEach(producto => {
                    const li = document.createElement('li');
                    li.className = 'list-group-item list-group-item-action';
                    li.textContent = producto.descripcion_producto;
                    li.dataset.id = producto.id_producto;
                    li.addEventListener('click', () => redirigirBusqueda(null, producto.id_producto));
                    searchList.appendChild(li);
                });

                searchList.style.display = 'block';
            }

            let timeout;
            searchInput.addEventListener('input', function() {
                const termino = this.value.trim();

                if (termino.length < 2) {
                    searchList.style.display = 'none';
                    return;
                }

                clearTimeout(timeout);
                timeout = setTimeout(() => {
                    fetch(`../controllers/buscar_sugerencias.php?q=${encodeURIComponent(termino)}`)
                        .then(res => res.json())
                        .then(data => mostrarSugerencias(data))
                        .catch(() => searchList.style.display = 'none');
                }, 200);
            });

            searchInput.addEventListener('keydown', function(e) {
                if (e.key === 'Enter') {
                    redirigirBusqueda();
                }
            });

            searchButton.addEventListener('click', () => redirigirBusqueda());

            document.addEventListener('click', function(e) {
                if (!searchInput.contains(e.target) && !searchList.contains(e.target)) {
                    searchList.style.display = 'none';
                }
            });
        });
    </script>

</body>

</html>