<?php
session_start();

$productosPOST = [];
foreach ($_POST['productos'] as $index => $producto) {
  $productosPOST[] = $producto; // Normaliza el array
}

// Token único del intento de pedido (evita duplicados en móvil)
$pedidoToken = preg_replace('/[^a-zA-Z0-9]/', '', (string)($_POST['pedido_token'] ?? ''));
if ($pedidoToken === '') {
  $pedidoToken = bin2hex(random_bytes(16));
}
?>


<?php
// include_once "header.php";
require_once "../models/database.php";
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);

require_once "../models/ProductoModel.php";
// Verificar que se recibieron datos
if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['productos'])) {
  header('Location: catalogo.php?error=sin_productos');
  exit;
}
$numCliente = $_POST['numCliente'] ?? '';

$mostrarModalCliente = "existe";
$clientesMultiples = [];
$direccionClienteUnico = null;

if (!empty($numCliente)) {
  $pedido = new Producto(); // Asegúrate de que tu clase tiene $this->pdo inicializado
  $clientes = $pedido->obtenerClientesPorTelefono($numCliente);
  
  if (count($clientes) == 0) {
    $mostrarModalCliente = "no_existe"; // cliente no existe
  } elseif (count($clientes) > 1) {
    $mostrarModalCliente = "multiple"; // hay múltiples clientes
    $clientesMultiples = $clientes;
  } else {
    // Un solo cliente - guardar su dirección
    $direccionClienteUnico = $clientes[0]['cli_direccion'] ?? null;
  }
  // Si count($clientes) == 1, $mostrarModalCliente queda como "existe"
} else {
  $mostrarModalCliente = "no_llego"; // no vino el número
}


// Obtener productos desde la base de datos
$producto = new Producto();
$productos = $producto->obtenerCategorias();
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
</head>


<!-- Modal Cliente -->
<div class="modal fade" id="modalCliente" tabindex="-1" aria-labelledby="modalClienteLabel" aria-hidden="true" data-bs-backdrop="static" data-bs-keyboard="false">
  <div class="modal-dialog modal-dialog-centered">
    <div class="modal-content shadow rounded-4 border-0">
      <form id="formCliente" method="POST" action="guardar_cliente.php">
        <div class="modal-header bg-success text-white rounded-top-4 text-center d-flex flex-column justify-content-center w-100">
          <i class="bi bi-person-plus-fill fs-1 mb-2"></i>
          <h5 class="fw-bold mb-1">Tu número no está registrado con nosotros</h5>
          <p class="mb-0">Por favor regístrate</p>
        </div>

        <div class="modal-body p-4">
          <div class="mb-3">

            <input type="hidden" class="form-control" name="cli_identificacion" id="cli_identificacion">
          </div>

          <div class="mb-3">
            <label for="cli_nombre" class="form-label">Nombre</label>
            <input type="text" class="form-control" name="cli_nombre" id="cli_nombre" required>
          </div>

          <div class="mb-3">
            <label for="cli_telefono" class="form-label">Teléfono</label>
            <input type="text" class="form-control" name="cli_telefono" id="cli_telefono" required>
          </div>

          <div class="mb-3">
            <label for="cli_direccion" class="form-label">Dirección</label>
            <input type="text" class="form-control" name="cli_direccion" id="cli_direccion" required>
          </div>
          <div class="mb-3">
            <select class="form-control" name="cli_zona" id="cliente_zona" required>
              <option value="" disabled selected>Seleccione una zona</option>
              <option value="Chapinero">Chapinero</option>
              <option value="Centro">Centro</option>
              <option value="Zona T">Zona T</option>
              <option value="45">45</option>
              <option value="Otra">Otra</option>
              <!-- Agrega más opciones según tu necesidad -->
            </select>
          </div>
          <!-- ✅ Nuevo campo Bar -->
          <div class="mb-3 position-relative">
            <label for="cli_bar" class="form-label">Bar <span class="text-danger">*</span></label>
            <input type="text" class="form-control" name="cli_bar" id="cli_bar" maxlength="40" required>
            <input type="hidden" name="bar_id" id="cli_bar_id">
            <div id="cli_bar_autocomplete" class="autocomplete-suggestions" style="display: none;"></div>
          </div>
        </div>

        <div class="modal-footer justify-content-center pb-4">
          <button type="button" class="btn btn-outline-secondary px-4 rounded-pill" data-bs-dismiss="modal">
            Cancelar
          </button>
          <button type="submit" class="btn btn-success px-4 rounded-pill">
            Guardar Cliente
          </button>
        </div>
      </form>
    </div>
  </div>
</div>

<!-- Modal de búsqueda de cliente -->
<div class="modal fade" id="modalBuscarTelefono" tabindex="-1" aria-labelledby="modalBuscarTelefonoLabel" aria-hidden="true" data-bs-backdrop="static" data-bs-keyboard="false">
  <div class="modal-dialog modal-dialog-centered">
    <div class="modal-content shadow rounded-4 border-0">
      <div class="modal-header bg-primary text-white rounded-top-4">
        <h5 class="modal-title mx-auto" id="modalBuscarTelefonoLabel">
          <i class="bi bi-search me-2"></i>Ecribe tu teléfono para completar el pedido
        </h5>
      </div>
      <div class="modal-body p-4 text-center">
        <label for="inputTelefono" class="form-label">Teléfono del cliente</label>
        <input type="text" class="form-control text-center fw-bold fs-5" id="inputTelefono" placeholder="Ej: 3001234567" maxlength="15" required>
        <div id="resultadoBusqueda" class="mt-3 text-muted small"></div>
      </div>
    </div>
  </div>
</div>

<!-- Modal de selección de cliente múltiple -->
<div class="modal fade" id="modalSeleccionarCliente" tabindex="-1" aria-labelledby="modalSeleccionarClienteLabel" aria-hidden="true" data-bs-backdrop="static" data-bs-keyboard="false">
  <div class="modal-dialog modal-dialog-centered">
    <div class="modal-content shadow rounded-4 border-0">
      <div class="modal-header bg-warning text-dark rounded-top-4 text-center d-flex flex-column justify-content-center w-100">
        <i class="bi bi-people-fill fs-1 mb-2"></i>
        <h5 class="fw-bold mb-1">Múltiples clientes encontrados</h5>
        <p class="mb-0">Selecciona el cliente al que deseas enviar el pedido</p>
      </div>
      <div class="modal-body p-4">
        <label for="selectCliente" class="form-label fw-bold">Selecciona un cliente:</label>
        <select class="form-select form-select-lg" id="selectCliente" name="selectCliente" required>
          <option value="" disabled selected>-- Selecciona un cliente --</option>
        </select>
        <div id="infoClienteSeleccionado" class="mt-3 p-3 bg-light rounded border" style="display: none;">
          <p class="mb-1"><strong>Nombre:</strong> <span id="infoNombre"></span></p>
          <p class="mb-0"><strong>Dirección:</strong> <span id="infoDireccion"></span></p>
        </div>
      </div>
      <div class="modal-footer justify-content-center pb-4">
        <button type="button" class="btn btn-success px-4 rounded-pill" id="btnConfirmarCliente">
          Confirmar y Enviar Pedido
        </button>
      </div>
    </div>
  </div>
</div>


<script>
  const totalGeneral = <?php echo json_encode($_POST['total_general'] ?? 0); ?>;
  const productosPOST = <?php echo json_encode($productosPOST); ?>;
  const observaciones = <?php echo json_encode($_POST['observaciones'] ?? ''); ?>;
  const pedidoToken = <?php echo json_encode($pedidoToken); ?>;

  let direccionCliente = null; // Variable global para almacenar la dirección del cliente seleccionado
  let pedidoYaEnviado = false; // Evita doble envío por toques repetidos / reintentos

  function enviarPedido() {
    if (pedidoYaEnviado) {
      console.warn('Pedido ya en proceso, se ignora envío duplicado');
      return;
    }

    if (!numcliente) { // Asegúrate de tener el número
      alert('No se encontró numCliente; no se envía el pedido.');
      return;
    }

    pedidoYaEnviado = true;

    // Deshabilitar botones de confirmación para evitar doble toque en móvil
    const btnConfirmar = document.getElementById('btnConfirmarCliente');
    if (btnConfirmar) {
      btnConfirmar.disabled = true;
      btnConfirmar.textContent = 'Enviando...';
    }

    // 1. Crea el formulario
    const form = document.createElement('form');
    form.method = 'POST';
    form.action = 'procesar_pedido.php'; // <-- cámbialo si tu ruta es distinta

    // 2. Agrega productos[]
    productosPOST.forEach((producto, i) => {
      for (const clave in producto) {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = `productos[${i}][${clave}]`;
        input.value = producto[clave];
        form.appendChild(input);
      }
    });

    // 3. Agrega numCliente
    const inputCli = document.createElement('input');
    inputCli.type = 'hidden';
    inputCli.name = 'numCliente'; // nombre que procesar_pedido.php recibirá
    inputCli.value = numcliente;
    form.appendChild(inputCli);

    // 4. Agrega total_general
    const inputTotal = document.createElement('input');
    inputTotal.type = 'hidden';
    inputTotal.name = 'total_general';
    inputTotal.value = totalGeneral;
    form.appendChild(inputTotal);

    // 5. Agrega observaciones
    if (observaciones != null && observaciones !== undefined && observaciones !== "") {
      const inputObservaciones = document.createElement("input");
      inputObservaciones.type = "hidden";
      inputObservaciones.name = "observaciones";
      inputObservaciones.value = observaciones;
      form.appendChild(inputObservaciones);
    }

    // 6. Agrega ped_sede (dirección del cliente seleccionado) si existe
    if (direccionCliente) {
      const inputSede = document.createElement('input');
      inputSede.type = 'hidden';
      inputSede.name = 'ped_sede';
      inputSede.value = direccionCliente;
      form.appendChild(inputSede);
    }

    // 7. Token de idempotencia (mismo intento = un solo pedido)
    const inputToken = document.createElement('input');
    inputToken.type = 'hidden';
    inputToken.name = 'pedido_token';
    inputToken.value = pedidoToken;
    form.appendChild(inputToken);

    // 8. Envía
    document.body.appendChild(form);
    form.submit();
  }

  document.addEventListener("DOMContentLoaded", function() {
    <?php if ($mostrarModalCliente == "no_existe"): ?>
      const modalCliente = new bootstrap.Modal(document.getElementById('modalCliente'));
      modalCliente.show();
    <?php endif; ?>
    
    <?php if ($mostrarModalCliente == "multiple"): ?>
      const modalSeleccionar = new bootstrap.Modal(document.getElementById('modalSeleccionarCliente'));
      const selectCliente = document.getElementById('selectCliente');
      const clientes = <?php echo json_encode($clientesMultiples); ?>;
      const telefono = <?php echo json_encode($numCliente); ?>;
      
      // Llenar el select con los clientes
      clientes.forEach((cliente, index) => {
        const option = document.createElement('option');
        option.value = cliente.id_cliente;
        option.dataset.nombre = cliente.cli_nombre;
        option.dataset.direccion = cliente.cli_direccion || 'Sin dirección';
        option.textContent = `${cliente.cli_nombre} - ${cliente.cli_direccion || 'Sin dirección'}`;
        selectCliente.appendChild(option);
      });
      
      // Mostrar información del cliente seleccionado
      selectCliente.addEventListener('change', function() {
        const selectedOption = this.options[this.selectedIndex];
        if (selectedOption.value) {
          document.getElementById('infoNombre').textContent = selectedOption.dataset.nombre;
          document.getElementById('infoDireccion').textContent = selectedOption.dataset.direccion;
          document.getElementById('infoClienteSeleccionado').style.display = 'block';
        } else {
          document.getElementById('infoClienteSeleccionado').style.display = 'none';
        }
      });
      
      // Confirmar y enviar pedido
      document.getElementById('btnConfirmarCliente').addEventListener('click', function() {
        if (selectCliente.value) {
          const selectedOption = selectCliente.options[selectCliente.selectedIndex];
          numcliente = telefono;
          direccionCliente = selectedOption.dataset.direccion || null; // Guardar la dirección
          bootstrap.Modal.getInstance(document.getElementById('modalSeleccionarCliente')).hide();
          console.log('Cliente seleccionado:', numcliente, 'Dirección:', direccionCliente);
          enviarPedido();
        } else {
          alert('Por favor selecciona un cliente');
        }
      });
      
      modalSeleccionar.show();
    <?php endif; ?>
  });
  
  let numcliente = null;
  document.addEventListener("DOMContentLoaded", function() {
    <?php if ($mostrarModalCliente == "existe"): ?>
      numcliente = <?php echo json_encode($numCliente); ?>;
      direccionCliente = <?php echo json_encode($direccionClienteUnico); ?>;
      console.log("Cliente ya existe. Enviando pedido directamente... Dirección:", direccionCliente);
      enviarPedido();
      return;
    <?php endif; ?>
  });



  document.addEventListener('DOMContentLoaded', function() {
    const inputTelefono = document.getElementById('inputTelefono');
    const resultadoBusqueda = document.getElementById('resultadoBusqueda');

    // Mostrar el modal si no hay número
    <?php if ($mostrarModalCliente == "no_llego"): ?>
      const modalBuscar = new bootstrap.Modal(document.getElementById('modalBuscarTelefono'));
      modalBuscar.show();
    <?php endif; ?>

    // Función para limpiar el número (quitar espacios y caracteres no numéricos)
    function limpiarNumero(numero) {
      return numero.replace(/\D/g, ''); // Solo números
    }

    // Prevenir espacios y caracteres no deseados
    inputTelefono.addEventListener('keydown', function(e) {
      // Permitir teclas de control (backspace, delete, tab, escape, enter, etc.)
      if ([8, 9, 27, 13, 46].indexOf(e.keyCode) !== -1 ||
          // Permitir Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
          (e.keyCode === 65 && e.ctrlKey === true) ||
          (e.keyCode === 67 && e.ctrlKey === true) ||
          (e.keyCode === 86 && e.ctrlKey === true) ||
          (e.keyCode === 88 && e.ctrlKey === true) ||
          // Permitir flechas
          (e.keyCode >= 35 && e.keyCode <= 39)) {
        return;
      }
      
      // Prevenir espacio (código 32) y otros caracteres no numéricos
      if (e.keyCode === 32 || // Espacio
          (e.shiftKey || (e.keyCode < 48 || e.keyCode > 57)) && 
          (e.keyCode < 96 || e.keyCode > 105)) { // No es número del teclado principal ni numérico
        e.preventDefault();
      }
    });

    // Variable para controlar el debounce
    let timeoutId = null;
    let ultimaBusqueda = '';

    inputTelefono.addEventListener('input', function() {
      // Limpiar el valor del input (quitar espacios y caracteres no numéricos)
      const numeroLimpio = limpiarNumero(inputTelefono.value);
      inputTelefono.value = numeroLimpio;
      
      resultadoBusqueda.textContent = '';

      // Si el número está vacío, no hacer nada
      if (numeroLimpio.length === 0) {
        return;
      }

      // Si es el mismo número que la última búsqueda, no hacer nada
      if (numeroLimpio === ultimaBusqueda) {
        return;
      }

      // Limpiar timeout anterior
      if (timeoutId) {
        clearTimeout(timeoutId);
      }

      // Solo buscar si tiene al menos 10 dígitos
      if (numeroLimpio.length >= 10) {
        // Mostrar mensaje de búsqueda
        resultadoBusqueda.textContent = 'Buscando cliente...';
        
        // Establecer un delay para evitar múltiples solicitudes
        timeoutId = setTimeout(() => {
          ultimaBusqueda = numeroLimpio;
          
          fetch(`../controllers/ajax/buscar_cliente.php?telefono=${numeroLimpio}`)
            .then(res => res.json())
            .then(data => {
              if (data.existe) {
                if (data.multiple) {
                  // Hay múltiples clientes
                  resultadoBusqueda.textContent = `Se encontraron ${data.clientes.length} clientes con este teléfono`;
                  resultadoBusqueda.className = 'mt-3 text-warning small';
                  
                  setTimeout(() => {
                    bootstrap.Modal.getInstance(document.getElementById('modalBuscarTelefono')).hide();
                    
                    // Llenar el select con los clientes
                    const selectCliente = document.getElementById('selectCliente');
                    selectCliente.innerHTML = '<option value="" disabled selected>-- Selecciona un cliente --</option>';
                    
                    data.clientes.forEach((cliente) => {
                      const option = document.createElement('option');
                      option.value = cliente.id_cliente;
                      option.dataset.nombre = cliente.cli_nombre;
                      option.dataset.direccion = cliente.cli_direccion || 'Sin dirección';
                      option.textContent = `${cliente.cli_nombre} - ${cliente.cli_direccion || 'Sin dirección'}`;
                      selectCliente.appendChild(option);
                    });
                    
                    // Ocultar info inicialmente
                    document.getElementById('infoClienteSeleccionado').style.display = 'none';
                    
                    // Función para manejar el cambio de selección
                    function manejarCambioCliente() {
                      const selectedOption = selectCliente.options[selectCliente.selectedIndex];
                      if (selectedOption && selectedOption.value) {
                        document.getElementById('infoNombre').textContent = selectedOption.dataset.nombre;
                        document.getElementById('infoDireccion').textContent = selectedOption.dataset.direccion;
                        document.getElementById('infoClienteSeleccionado').style.display = 'block';
                      } else {
                        document.getElementById('infoClienteSeleccionado').style.display = 'none';
                      }
                    }
                    
                    // Remover listener anterior si existe y agregar uno nuevo
                    selectCliente.removeEventListener('change', manejarCambioCliente);
                    selectCliente.addEventListener('change', manejarCambioCliente);
                    
                    // Función para confirmar y enviar
                    function confirmarYEnviar() {
                      if (selectCliente.value) {
                        const selectedOption = selectCliente.options[selectCliente.selectedIndex];
                        numcliente = numeroLimpio;
                        direccionCliente = selectedOption.dataset.direccion || null; // Guardar la dirección
                        bootstrap.Modal.getInstance(document.getElementById('modalSeleccionarCliente')).hide();
                        console.log('Cliente seleccionado:', numcliente, 'Dirección:', direccionCliente);
                        enviarPedido();
                      } else {
                        alert('Por favor selecciona un cliente');
                      }
                    }
                    
                    // Remover listener anterior del botón y agregar uno nuevo
                    const btnConfirmar = document.getElementById('btnConfirmarCliente');
                    const newBtnConfirmar = btnConfirmar.cloneNode(true);
                    btnConfirmar.parentNode.replaceChild(newBtnConfirmar, btnConfirmar);
                    newBtnConfirmar.addEventListener('click', confirmarYEnviar);
                    
                    new bootstrap.Modal(document.getElementById('modalSeleccionarCliente')).show();
                  }, 1000);
                } else {
                  // Solo hay un cliente
                  resultadoBusqueda.textContent = `Cliente encontrado: ${data.nombre}`;
                  resultadoBusqueda.className = 'mt-3 text-success small';
                  numcliente = numeroLimpio;
                  direccionCliente = data.direccion || null; // Guardar la dirección del cliente único

                  setTimeout(() => {
                    bootstrap.Modal.getInstance(document.getElementById('modalBuscarTelefono')).hide();
                    console.log('Cliente asignado:', numcliente, 'Dirección:', direccionCliente);
                    enviarPedido();
                  }, 1000);
                }
              } else {
                resultadoBusqueda.textContent = 'Cliente no encontrado. Mostrando formulario de registro...';
                resultadoBusqueda.className = 'mt-3 text-warning small';
                
                setTimeout(() => {
                  bootstrap.Modal.getInstance(document.getElementById('modalBuscarTelefono')).hide();
                  // Pre-llenar el teléfono en el formulario de registro
                  document.getElementById('cli_telefono').value = numeroLimpio;
                  new bootstrap.Modal(document.getElementById('modalCliente')).show();
                }, 1000);
              }
            })
            .catch((error) => {
              console.error('Error al buscar cliente:', error);
              resultadoBusqueda.textContent = 'Error al buscar cliente. Intenta nuevamente.';
              resultadoBusqueda.className = 'mt-3 text-danger small';
            });
        }, 500); // Delay de 500ms para evitar múltiples solicitudes
      } else if (numeroLimpio.length > 0) {
        resultadoBusqueda.textContent = `Ingresa al menos 10 dígitos (${numeroLimpio.length}/10)`;
        resultadoBusqueda.className = 'mt-3 text-muted small';
      }
    });

    // Limpiar el input al pegar contenido
    inputTelefono.addEventListener('paste', function(e) {
      setTimeout(() => {
        const numeroLimpio = limpiarNumero(inputTelefono.value);
        inputTelefono.value = numeroLimpio;
      }, 10);
    });
  });

  document.addEventListener('DOMContentLoaded', function() {
    const formCliente = document.getElementById('formCliente');

    formCliente.addEventListener('submit', function(e) {
      e.preventDefault();

      if (pedidoYaEnviado) {
        return;
      }

      const formData = new FormData(formCliente);
      const telefonoInput = document.getElementById('cli_telefono');
      const telefono = telefonoInput.value.trim();
      const btnGuardar = formCliente.querySelector('button[type="submit"]');

      if (telefono.length < 10) {
        alert("Número de teléfono inválido");
        return;
      }

      if (btnGuardar) {
        btnGuardar.disabled = true;
        btnGuardar.textContent = 'Guardando...';
      }

      fetch('../controllers/ajax/guardar_cliente.php', {
          method: 'POST',
          body: formData
        })
        .then(res => res.json())
        .then(data => {
          if (data.exito) {
            alert('Cliente guardado exitosamente');
            numcliente = telefono;
            enviarPedido();
          } else {
            if (btnGuardar) {
              btnGuardar.disabled = false;
              btnGuardar.textContent = 'Guardar Cliente';
            }
            alert('Error al guardar el cliente: ' + (data.mensaje || ''));
          }
        })
        .catch(error => {
          console.error('Error:', error);
          if (btnGuardar) {
            btnGuardar.disabled = false;
            btnGuardar.textContent = 'Guardar Cliente';
          }
          alert('Error al guardar el cliente');
        });
    });
  });
</script>
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

  // autocompletar

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

  /////////////autocompletar///////////
  document.addEventListener('DOMContentLoaded', function() {
    const inputBar = document.getElementById('cli_bar');
    const hiddenBarId = document.getElementById('cli_bar_id');
    const list = document.getElementById('cli_bar_autocomplete');

    let selectedIndex = -1;

    function buscarBares(query) {
      if (query.length < 2) {
        list.style.display = 'none';
        return;
      }

      fetch('../controllers/buscar_bar.php', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
          },
          body: 'query=' + encodeURIComponent(query)
        })
        .then(res => res.json())
        .then(data => mostrarSugerencias(data))
        .catch(() => list.style.display = 'none');
    }

    function mostrarSugerencias(data) {
      list.innerHTML = '';
      if (!Array.isArray(data) || data.length === 0) {
        list.style.display = 'none';
        return;
      }

      data.forEach(item => {
        const div = document.createElement('div');
        div.className = 'autocomplete-suggestion';
        div.textContent = item.label || item.value;
        div.dataset.id = item.id;

        div.addEventListener('click', () => {
          inputBar.value = item.label;
          hiddenBarId.value = item.id;
          list.style.display = 'none';
        });

        list.appendChild(div);
      });

      list.style.display = 'block';
    }

    inputBar.addEventListener('input', () => {
      const value = inputBar.value.trim();
      hiddenBarId.value = ''; // Reinicia ID si cambia texto
      buscarBares(value);
    });

    inputBar.addEventListener('keydown', (e) => {
      const suggestions = list.querySelectorAll('.autocomplete-suggestion');
      if (suggestions.length === 0) return;

      switch (e.key) {
        case 'ArrowDown':
          e.preventDefault();
          selectedIndex = (selectedIndex + 1) % suggestions.length;
          actualizarSeleccion(suggestions);
          break;
        case 'ArrowUp':
          e.preventDefault();
          selectedIndex = (selectedIndex - 1 + suggestions.length) % suggestions.length;
          actualizarSeleccion(suggestions);
          break;
        case 'Enter':
          e.preventDefault();
          if (selectedIndex >= 0) suggestions[selectedIndex].click();
          break;
      }
    });

    function actualizarSeleccion(suggestions) {
      suggestions.forEach((el, i) => {
        el.classList.toggle('selected', i === selectedIndex);
      });
    }

    document.addEventListener('click', (e) => {
      if (!list.contains(e.target) && e.target !== inputBar) {
        list.style.display = 'none';
      }
    });
  });
</script>

<style>
  .autocomplete-suggestions {
    position: absolute;
    z-index: 1000;
    background: white;
    border: 1px solid #ccc;
    max-height: 200px;
    overflow-y: auto;
    width: 100%;
  }

  .autocomplete-suggestion {
    padding: 10px;
    cursor: pointer;
    border-bottom: 1px solid #eee;
  }

  .autocomplete-suggestion:hover,
  .autocomplete-suggestion.selected {
    background-color: #f0f0f0;
  }
</style>

</body>

</html>