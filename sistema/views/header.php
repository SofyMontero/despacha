<?php 
session_start();
if (!isset($_SESSION["usuario"])) {
    header("Location: ../index.html?error=Debes%20iniciar%20sesión");
    exit();
}
date_default_timezone_set('America/Bogota');

?>
<!DOCTYPE html>
<html lang="es">
<link rel="icon" type="image/png" href="../assets/img/logoM.png">
<head>
	<meta charset="UTF-8">
	<meta name="viewport" content="width=device-width, user-scalable=no, initial-scale=1.0, maximum-scale=1.0, minimum-scale=1.0">
	<title>Multilicores</title>

	<!-- Normalize V8.0.1 -->
	<link rel="stylesheet" href="../css/normalize.css">

	<!-- Bootstrap V4.3 -->
	<link rel="stylesheet" href="../css/bootstrap.min.css">

	<!-- Bootstrap Material Design V4.0 -->
	<link rel="stylesheet" href="../css/bootstrap-material-design.min.css">

	<!-- Font Awesome V5.9.0 -->
	<link rel="stylesheet" href="../css/all.css">

	<!-- Sweet Alerts V8.13.0 CSS file -->
	<link rel="stylesheet" href="../css/sweetalert2.min.css">

	<!-- Sweet Alert V8.13.0 JS file-->
	<script src="../js/sweetalert2.min.js" ></script>

	<!-- jQuery Custom Content Scroller V3.1.5 -->
	<link rel="stylesheet" href="../css/jquery.mCustomScrollbar.css">
	
	<!-- General Styles -->
	<link rel="stylesheet" href="../css/style.css">
    <script>
        // Verificar sesión al cargar la página
        fetch("../controllers/session_check.php")
            .then(response => response.json())
            .then(data => {
                if (!data.auth) {
                    window.location.href = "../index.html?error=Debes%20iniciar%20sesión";
                }
            })
            .catch(error => console.error("Error verificando sesión:", error));
    </script>

</head>
<body>
	
	<!-- Main container -->
	<main class="full-box main-container">
		<!-- Nav lateral -->
		<section class="full-box nav-lateral">
			<div class="full-box nav-lateral-bg show-nav-lateral"></div>
			<div class="full-box nav-lateral-content">
				<figure class="full-box nav-lateral-avatar">
					<i class="far fa-times-circle show-nav-lateral"></i>
					<img src="../assets/img/logoM.png" class="img-fluid" alt="Avatar">
					<figcaption class="roboto-medium text-center">
						Multilicores <br><small class="roboto-condensed-light">Administración</small>
					</figcaption>
				</figure>
				<div class="full-box nav-lateral-bar"></div>
				<nav class="full-box nav-lateral-menu">
					<ul>
						<li>
							<a href="home.php"><i class="fab fa-dashcube fa-fw"></i> &nbsp; Home</a>
						</li>

						<li>
							<a href="clientes.php"><i class="fas fa-address-book fa-fw"></i> &nbsp; Clientes </a>							
						</li>

						<li>
							<a href="promo.php"><i class="fas fa-bullhorn fa-fw"></i> &nbsp; Promociones </a>							
						</li>

						<li>
							<a href="cartera.php"><i class="fas fa-file-invoice-dollar fa-fw"></i> &nbsp; Pedidos </a>							
						</li>

						<li>
							<a href="user-list.php"><i class="fas fa-user fa-fw"></i> &nbsp; Usuarios </a>							
						</li>

						<li>
							<a href="solicitudes.php"><i class="fas fa-shopping-cart fa-fw"></i> &nbsp; Solicitudes</a>
						</li>
						<li>
							<a href="categorias.php"><i class="fas fa-th-large fa-fw"></i> &nbsp; Catálogo </a>							
						</li>

						<li>
							<a href="Subir_excel_producto.php"> <i class="fas fa-file-upload fa-fw"></i> &nbsp; Cargar Productos</a>
						</li>
					</ul>
				</nav>
			</div>
		</section>
				<!-- Page content -->
				<section class="full-box page-content">
			<nav class="full-box navbar-info">
				<a href="#" class="float-left show-nav-lateral">
					<i class="fas fa-exchange-alt"></i>
				</a>
				<a href="user-update.php">
					<i class="fas fa-user-cog"></i>
				</a>
				<a href="#" class="btn-exit-system">
					<i class="fas fa-power-off"></i>
				</a>
			</nav>