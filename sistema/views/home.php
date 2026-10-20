<?php
include_once "header.php";
require_once "../models/database.php";
?>

			<!-- Page header -->
			<div class="full-box page-header">
				<h3 class="text-left">
					<i class="fab fa-dashcube fa-fw"></i> &nbsp; HOME
				</h3>
				<p class="text-justify">
					Bienvenido a Multilicores.
				</p>
			</div>
			
			<!-- Content -->
			<div class="full-box tile-container">

				<a href="clientes.php" class="tile">
					<div class="tile-tittle">Clientes</div>
					<div class="tile-icon">
						<i class="fas fa-address-book fa-fw"></i>
						
					</div>
				</a>

				<a href="promo.php" class="tile">
					<div class="tile-tittle">Promociones</div>
					<div class="tile-icon">
						<i class="fas fa-bullhorn fa-fw"></i>
						
					</div>
				</a>

				<a href="cartera.php" class="tile">
					<div class="tile-tittle">Pedidos</div>
					<div class="tile-icon">
						<i class="fas fa-file-invoice-dollar fa-fw"></i>
						
					</div>
				</a>

				<a href="user-list.php" class="tile">
					<div class="tile-tittle">Usuarios</div>
					<div class="tile-icon">
						<i class="fas fa-user fa-fw"></i>						
					</div>
				</a>

				<a href="solicitudes.php" class="tile">
					<div class="tile-tittle">Solicitudes</div>
					<div class="tile-icon">						
					<i class="fas fa-shopping-cart fa-fw"></i>
		
					</div>
				</a>
				<a href="categorias.php" class="tile">
				    <div class="tile-tittle">Catálogo</div>
 					   <div class="tile-icon">
 					       <i class="fas fa-th-large fa-fw"></i> <!-- Icono tipo grid para categorías -->
 					   </div>
				</a>

				<a href="Subir_excel_producto.php" class="tile">
   				 <div class="tile-tittle">Cargar Productos</div>
   					 <div class="tile-icon">
   					     <i class="fas fa-file-upload fa-fw"></i> <!-- Icono de carga de archivo -->
  					  </div>
				</a>
				
			</div>
			

		</section>
	</main>

<?php
include_once "footer.php";

?>

<style>
.tile-container {
	display: flex;
	flex-wrap: wrap;
	justify-content: center; /* centrado horizontal */
	gap: 20px; /* espacio entre tarjetas */
	padding: 20px;
}

.tile {
	width: calc(25% - 15px); /* 4 por fila */
	min-width: 200px;
	padding: 20px;
	background-color: #f5f5f5;
	border-radius: 10px;
	text-align: center;
	box-shadow: 0 2px 4px rgba(0,0,0,0.1);
	text-decoration: none;
	color: #333;
	transition: transform 0.2s ease;
}

.tile:hover {
	transform: translateY(-5px);
}

/* Responsive ajustes */
@media (max-width: 1024px) {
	.tile {
		width: calc(33.33% - 15px); /* 3 por fila */
	}
}

@media (max-width: 768px) {
	.tile {
		width: calc(50% - 15px); /* 2 por fila */
	}
}

@media (max-width: 480px) {
	.tile {
		width: 100%; /* 1 por fila */
	}
}

</style>
