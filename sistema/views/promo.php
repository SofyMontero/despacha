

<?php
include_once "header.php";
require_once "../models/database.php";

// Procesamiento del formulario (mantener código original)
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['action']) && $_POST['action'] === 'insert') {
    $titulo = $_POST['titulo'] ?? '';
    $descripcion = $_POST['descripcion'] ?? '';
    $codigo = $_POST['codigo'] ?? '';
    
    $errores = [];
    if (empty($titulo)) {
        $errores[] = "El título de la promoción es obligatorio";
    }
    if (empty($descripcion)) {
        $errores[] = "La descripción es obligatoria";
    }
    
    if (empty($errores)) {
        $mensaje_exito = "La promoción ha sido creada exitosamente";
    }
}
?>

<div class="container-fluid px-4 py-3">
    <!-- Header Moderno -->
    <div class="page-header">
        <div class="d-flex align-items-center mb-3">
            <i class="fas fa-gift fa-2x me-3"></i>
            <div>
                <h1 class="mb-0 fw-bold">Gestión de Promociones</h1>
                <p class="mb-0 opacity-90">Administra y envía promociones a tus clientes</p>
            </div>
        </div>
    </div>

    <!-- Alertas Modernas -->
    <?php if (isset($mensaje_exito)): ?>
    <div class="alert alert-modern alert-success-modern d-flex align-items-center mb-4" role="alert">
        <i class="fas fa-check-circle me-2"></i>
        <div>
            <strong>¡Perfecto!</strong> <?php echo $mensaje_exito; ?>
        </div>
        <button type="button" class="btn-close ms-auto" data-bs-dismiss="alert"></button>
    </div>
    <?php endif; ?>
     <div id="alert-container"></div>
    <?php if (!empty($errores)): ?>
    <div class="alert alert-modern alert-danger-modern mb-4" role="alert">
        <div class="d-flex align-items-start">
            <i class="fas fa-exclamation-triangle me-2 mt-1"></i>
            <div>
                <strong>Errores encontrados:</strong>
                <ul class="mb-0 mt-2">
                    <?php foreach ($errores as $error): ?>
                        <li><?php echo $error; ?></li>
                    <?php endforeach; ?>
                </ul>
            </div>
        </div>
        <button type="button" class="btn-close ms-auto" data-bs-dismiss="alert"></button>
    </div>
    <?php endif; ?>

    <!-- Formulario Moderno -->
    <div class="d-flex justify-content-end mb-4">
        <button type="button" class="btn btn-modern btn-primary-modern" id="btn-agregar-promo" data-toggle="modal" data-target="#promoModal">
            <i class="fas fa-plus-circle me-2"></i>Agregar promo
        </button>
    </div>

    <div class="modal fade" id="promoModal" tabindex="-1" role="dialog" aria-hidden="true">
        <div class="modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable" role="document">
            <div class="modal-content promo-modal-content">
                <div class="modal-header">
                    <div class="d-flex align-items-center mb-0">
                        <i class="fas fa-plus-circle text-primary me-2"></i>
                        <h5 class="mb-0 fw-semibold" id="form-title">Nueva Promoción</h5>
                    </div>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body p-4">
            
            <form method="POST" action="" id="form-promocion" enctype="multipart/form-data">
                <input type="hidden" name="action" id="action" value="insert">
                <input type="hidden" name="id" id="id">
                
                <div class="row g-4">
                    <div class="col-12">
                        <label for="buscar-producto" class="form-label fw-medium">Buscar producto</label>
                        <input type="text" class="form-control" id="buscar-producto" autocomplete="off"
                               placeholder="Nombre o código del producto">
                        <small class="form-text text-muted">Al elegir un producto se carga el precio de paca. La cantidad de botellas multiplica solo el precio de unidad.</small>
                        <div id="resultados-producto" class="product-search-results" style="display: none;"></div>
                        <div id="producto-precios" class="product-price-choice" style="display: none;">
                            <p class="mb-2 fw-medium" id="producto-elegido-nombre"></p>
                            <div class="row g-3 align-items-end">
                                <div class="col-md-4">
                                    <label class="form-label fw-medium mb-1">Precio por botella</label>
                                    <div class="fw-semibold" id="label-precio-unidad"></div>
                                </div>
                                <div class="col-md-4">
                                    <label for="cantidad-botellas" class="form-label fw-medium mb-1">Cantidad de botellas</label>
                                    <input type="number" class="form-control" id="cantidad-botellas" min="1" step="1" value="1">
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label fw-medium mb-1">Precio a guardar</label>
                                    <div class="fw-semibold" id="label-precio-calculado"></div>
                                </div>
                            </div>
                            <small class="form-text text-muted d-block mt-2" id="nota-cantidad-botellas"></small>
                            <small class="form-text text-muted d-block">Precio paca del producto: <span id="label-precio-paca"></span>. No cambia con la cantidad.</small>
                        </div>
                    </div>

                    <div class="col-md-6">
                        <label for="titulo" class="form-label fw-medium">
                            Título de la Promoción <span class="text-danger">*</span>
                        </label>
                        <input type="text" class="form-control" id="titulo" name="titulo" 
                               placeholder="Ej: Descuento Verano 2025" required>
                    </div>
                    
                    <div class="col-md-6">
                        <label for="codigo" class="form-label fw-medium">
                            Código del Producto <span class="text-danger">*</span>
                        </label>
                        <input type="number" class="form-control" id="codigo" name="codigo" min="1"
                               placeholder="Ej: 1001" required>
                    </div>
                    
                    <div class="col-md-6">
                        <label for="estado" class="form-label fw-medium">Estado</label>
                        <select class="form-control" id="estado" name="estado">
                            <option value="1" selected>Activa</option>
                            <option value="0">Inactiva</option>
                        </select>
                    </div>
                    
                    <div class="col-md-6">
                        <label for="prioridad" class="form-label fw-medium">Prioridad</label>
                        <input type="number" class="form-control" id="prioridad" name="prioridad" min="0" value="0">
                    </div>

                    <div class="col-md-4" id="grupo-precio-unidad">
                        <label for="precio_unidad_producto" class="form-label fw-medium">Precio Unidad</label>
                        <input type="number" class="form-control" id="precio_unidad_producto" name="precio_unidad_producto" min="0" value="0">
                    </div>

                    <div class="col-md-4" id="grupo-precio-paca">
                        <label for="precio_paca_producto" class="form-label fw-medium">Precio Paca</label>
                        <input type="number" class="form-control" id="precio_paca_producto" name="precio_paca_producto" min="0" value="0">
                    </div>

                    <div class="col-md-4">
                        <label for="acti_Unidad" class="form-label fw-medium">Venta por Unidad</label>
                        <select class="form-control" id="acti_Unidad" name="acti_Unidad">
                            <option value="1" selected>Sí</option>
                            <option value="0">No</option>
                        </select>
                    </div>
                    
                    <div class="col-md-6">
                        <label for="imagen" class="form-label fw-medium">Imagen <span class="text-danger" id="imagen-requerida-label">*</span></label>
                        <input type="hidden" name="imagen_actual" id="imagen_actual" value="">
                        <div id="imagen-actual-box" class="imagen-actual-box" style="display: none;">
                            <img id="imagen-actual-preview" alt="Imagen actual de la promoción">
                            <small class="form-text text-muted d-block">Esta imagen se conserva si no eliges otra.</small>
                        </div>
                        <input type="file" class="form-control-file" id="imagen" name="imagen" accept="image/*">
                        <small class="form-text text-muted" id="imagen-ayuda">Obligatoria al crear. Opcional al editar.</small>
                    </div>
                    
                    <div class="col-12">
                        <label for="descripcion" class="form-label fw-medium">
                            Descripción <span class="text-danger">*</span>
                        </label>
                        <textarea class="form-control mb-3" id="descripcion" name="descripcion" 
                                  rows="4" placeholder="Descripción detallada de la promoción" required></textarea>
                        
                        <button type="button" id="abrir-emojis" class="btn btn-outline-modern btn-sm">
                            <i class="fas fa-smile me-1"></i> Agregar Emoji
                        </button>
                        
                        <div id="emoji-box" class="emoji-picker mt-2" style="display: none; position: absolute; z-index: 1000;">
                            <div class="d-flex flex-wrap">
                                <span class="emoji">🍺</span> <span class="emoji">🍻</span> <span class="emoji">🥂</span>
                                <span class="emoji">🍷</span> <span class="emoji">🥃</span> <span class="emoji">🍸</span>
                                <span class="emoji">🍹</span> <span class="emoji">🍾</span> <span class="emoji">🧉</span>
                                <span class="emoji">🍶</span> <span class="emoji">🥴</span> <span class="emoji">😵</span>
                                <span class="emoji">🎉</span> <span class="emoji">🎊</span> <span class="emoji">🎈</span>
                                <span class="emoji">🥳</span> <span class="emoji">🔥</span> <span class="emoji">🎵</span>
                                <span class="emoji">🎶</span> <span class="emoji">💃</span> <span class="emoji">🕺</span>
                                <span class="emoji">😎</span> <span class="emoji">😍</span> <span class="emoji">😂</span>
                                <span class="emoji">🤣</span> <span class="emoji">😁</span> <span class="emoji">🥰</span>
                                <span class="emoji">🤩</span> <span class="emoji">💯</span> <span class="emoji">👍</span>
                                <span class="emoji">🍕</span> <span class="emoji">🌮</span> <span class="emoji">🍔</span>
                                <span class="emoji">🍟</span> <span class="emoji">🌭</span> <span class="emoji">🍗</span>
                                <span class="emoji">🚬</span> <span class="emoji">💵</span> <span class="emoji">🤑</span>
                                <span class="emoji">💥</span> <span class="emoji">💫</span> <span class="emoji">🌟</span>
                                <span class="emoji">🎯</span> <span class="emoji">📸</span> <span class="emoji">🕹️</span>
                            </div>
                        </div>
                    </div>
                </div>
                
                <div class="d-flex justify-content-center gap-3 mt-4 pt-3 border-top">
                    <button type="submit" class="btn btn-modern btn-primary-modern">
                        <i class="fas fa-save me-2"></i>Guardar Promoción
                    </button>
                    <button type="button" class="btn btn-modern btn-outline-modern" id="btn-limpiar-promo">
                        <i class="fas fa-undo me-2"></i>Limpiar
                    </button>
                </div>
            </form>
                </div>
            </div>
        </div>
    </div>

    <!-- Tabla Moderna -->
    <div class="modern-card">
        <div class="card-body p-0">
            <div class="p-4 border-bottom">
                <div class="d-flex align-items-center">
                    <i class="fas fa-list text-primary me-2"></i>
                    <h5 class="mb-0 fw-semibold">Lista de Promociones</h5>
                </div>
            </div>
            
            <div class="table-responsive">
                <table class="table table-modern mb-0" id="tabla-promos">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Promoción</th>
                            <th>Código</th>
                            <th>Precio</th>
                            <th>Fecha</th>
                            <th>Estado</th>
                            <th>Descripción</th>
                            <th>Imagen</th>
                            <th>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        <!-- Contenido dinámico -->
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>

<!-- Modal Moderno -->
<div class="modal fade" id="estadoModal" tabindex="-1">
    <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content" style="border-radius: 12px; border: none;">
            <div class="modal-header border-0 pb-0">
                <h5 class="modal-title fw-semibold">Confirmar Acción</h5>
                <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                    <span aria-hidden="true">&times;</span>
                </button>
            </div>
            <div class="modal-body pt-0">
                <p id="modal-mensaje" class="text-muted">¿Está seguro de que desea realizar esta acción?</p>
            </div>
            <div class="modal-footer border-0 pt-0">
                <button type="button" class="btn btn-modern btn-outline-modern" data-dismiss="modal">Cancelar</button>
                <form id="estado-form" method="POST" action="" class="d-inline">
                    <input type="hidden" name="action" value="cambiar_estado">
                    <input type="hidden" name="promocion_id" id="promocion_id">
                    <input type="hidden" name="nuevo_estado" id="nuevo_estado">
                    <button type="submit" class="btn btn-modern btn-primary-modern">Confirmar</button>
                </form>
            </div>
        </div>
    </div>
</div>

<link href="https://cdn.datatables.net/1.13.8/css/dataTables.bootstrap4.min.css" rel="stylesheet">
<script>
// Mantener todo el JavaScript original pero con mejoras visuales
let dataTablesCargando = false;
const promosPorId = {};
const productosBusqueda = {};
let productoPromoSeleccionado = null;
let busquedaProductoTimer = null;

function escaparHtml(texto) {
    return String(texto ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function abrirModalPromo() {
    if (window.jQuery) {
        $('#promoModal').modal('show');
    }
}

function cerrarModalPromo() {
    if (window.jQuery) {
        $('#promoModal').modal('hide');
    }
}

document.addEventListener('DOMContentLoaded', function() {
    // Emoji picker mejorado
    document.getElementById('abrir-emojis').addEventListener('click', function (e) {
        const box = document.getElementById('emoji-box');
        box.style.display = box.style.display === 'none' ? 'block' : 'none';
    });

    // Cerrar emoji picker al hacer click fuera
    document.addEventListener('click', function(e) {
        const emojiBox = document.getElementById('emoji-box');
        const emojiBtn = document.getElementById('abrir-emojis');
        if (!emojiBox.contains(e.target) && !emojiBtn.contains(e.target)) {
            emojiBox.style.display = 'none';
        }

        const buscarProducto = document.getElementById('buscar-producto');
        const resultadosProducto = document.getElementById('resultados-producto');
        if (resultadosProducto && buscarProducto && !resultadosProducto.contains(e.target) && e.target !== buscarProducto) {
            resultadosProducto.style.display = 'none';
        }
    });

    // Agregar emoji al textarea
    document.querySelectorAll('.emoji').forEach(emoji => {
        emoji.addEventListener('click', function() {
            const textarea = document.getElementById('descripcion');
            textarea.value += this.textContent;
            textarea.focus();
        });
    });

    const formPromocion = document.getElementById('form-promocion');
    const btnAgregarPromo = document.getElementById('btn-agregar-promo');

    btnAgregarPromo.addEventListener('click', function() {
        formPromocion.reset();
        resetPromoForm();
    });

    document.getElementById('btn-limpiar-promo').addEventListener('click', function() {
        const action = document.getElementById('action').value;
        const id = document.getElementById('id').value;
        if (action === 'update' && id && promosPorId[id]) {
            editarPromo(id);
            return;
        }
        formPromocion.reset();
        resetPromoForm();
    });

    const buscarProductoInput = document.getElementById('buscar-producto');
    const resultadosProducto = document.getElementById('resultados-producto');

    buscarProductoInput.addEventListener('input', function() {
        const termino = buscarProductoInput.value.trim();
        clearTimeout(busquedaProductoTimer);
        if (termino.length < 2) {
            resultadosProducto.innerHTML = '';
            resultadosProducto.style.display = 'none';
            return;
        }
        busquedaProductoTimer = setTimeout(function() {
            buscarProductosPromo(termino);
        }, 300);
    });

    resultadosProducto.addEventListener('click', function(e) {
        const boton = e.target.closest('.product-search-item');
        if (!boton) return;
        const producto = productosBusqueda[boton.dataset.id];
        if (!producto) return;
        aplicarProductoPromo(producto);
        buscarProductoInput.value = producto.descripcion_producto || '';
    });

    document.getElementById('cantidad-botellas').addEventListener('input', calcularPrecioPorBotellas);

    formPromocion.addEventListener('reset', function() {
        setTimeout(resetPromoForm, 0);
    });

    // Guardar o actualizar promocion
    document.getElementById('form-promocion').addEventListener('submit', function(e) {
        e.preventDefault();
        const form = e.target;
        const formData = new FormData(form);
        const esInsert = formData.get('action') === 'insert';
        const imagenInput = document.getElementById('imagen');

        if (esInsert && (!imagenInput.files || imagenInput.files.length === 0)) {
            showAlert('error', 'Debe seleccionar una imagen para la promoción.');
            return;
        }

        // Mostrar loading
        const submitBtn = form.querySelector('button[type="submit"]');
        const originalText = submitBtn.innerHTML;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Guardando...';
        submitBtn.disabled = true;

        fetch('../controllers/promoController.php', {
            method: 'POST',
            body: formData
        })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                // Mostrar alerta de éxito moderna
                showAlert('success', '✅ Promoción guardada con éxito');
                form.reset();
                resetPromoForm();
                cargarPromociones();
                cerrarModalPromo();
            } else {
                showAlert('error', '⚠️ Error al guardar: ' + data.message);
            }
        })
        .catch(err => {
            console.error(err);
            showAlert('error', '❌ Error de red o servidor');
        })
        .finally(() => {
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
        });
    });

    cargarPromociones();
});

function cargarPromociones() {
    fetch('../controllers/promoController.php', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
        },
        body: 'action=mostrar'
    })
    .then(response => response.json())
    .then(data => {
        if (window.jQuery && $.fn.DataTable && $.fn.DataTable.isDataTable('#tabla-promos')) {
            $('#tabla-promos').DataTable().destroy();
        }

        const tbody = document.querySelector("#tabla-promos tbody");
        tbody.innerHTML = '';
        const promociones = data.promociones || data;

        Object.keys(promosPorId).forEach((key) => delete promosPorId[key]);

        promociones.forEach(promo => {
            const idPromo = promo.id_promocion;
            promosPorId[idPromo] = promo;

            const statusClass = String(promo.estado) === '1' ? 'status-active' : 'status-inactive';
            const estadoTexto = String(promo.estado) === '1' ? 'Activa' : 'Inactiva';
            const estadoChecked = String(promo.estado) === '1' ? 'checked' : '';
            const imagen = promo.imagen
                ? `../assets/img/licores/promos/${promo.imagen}`
                : '../assets/img/licores/placeholder.jpg';

            const fila = `
                <tr>
                    <td><span class="fw-medium">${escaparHtml(idPromo)}</span></td>
                    <td><span class="fw-medium">${escaparHtml(promo.titulo)}</span></td>
                    <td>${escaparHtml(promo.codigo || '-')}</td>
                    <td>${htmlPreciosPromo(promo)}</td>
                    <td>${escaparHtml(promo.creado_en || '-')}</td>
                    <td>
                        <div class="custom-control custom-switch promo-status-switch">
                            <input type="checkbox" class="custom-control-input"
                                   id="estadoPromo${idPromo}"
                                   ${estadoChecked}
                                   onchange="cambiarEstadoPromo(this, ${idPromo})">
                            <label class="custom-control-label status-badge ${statusClass}" for="estadoPromo${idPromo}">
                                ${estadoTexto}
                            </label>
                        </div>
                    </td>
                    <td>
                      <span class="text-muted" style="max-width: 200px; display: inline-block; word-wrap: break-word; white-space: normal;">
                        ${escaparHtml(promo.descripcion)}
                      </span>
                    </td>
                    <td>
                        <div style="width: 50px; height: 50px; background: #f8fafc; border-radius: 8px; display: flex; align-items: center; justify-content: center; overflow: hidden;">
                            <img src="${escaparHtml(imagen)}" style="width: 100%; height: 100%; object-fit: cover;" alt="Promocion">
                        </div>
                    </td>
                    <td>
                        <div class="d-flex">
                            <button type="button" class="btn btn-outline-primary btn-sm mr-2" onclick="editarPromo(${idPromo})" title="Editar" style="border-radius: 6px;">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button type="button" class="btn btn-primary btn-sm" onclick="enviarPromoPorId(${idPromo})" title="Enviar" style="border-radius: 6px;">
                                <i class="fas fa-paper-plane"></i>
                            </button>
                        </div>
                    </td>
                </tr>`;
            tbody.innerHTML += fila;
        });

        inicializarTablaPromos();
    })
    .catch(error => console.error("Error:", error));
}

function inicializarTablaPromos() {
    if (!window.jQuery) {
        return;
    }

    if (!$.fn.DataTable) {
        cargarDataTables(function() {
            inicializarTablaPromos();
        });
        return;
    }

    $('#tabla-promos').DataTable({
        pageLength: 50,
        lengthMenu: [[50, 100, 200, -1], [50, 100, 200, 'Todos']],
        order: [[0, 'desc']],
        language: {
            decimal: '',
            emptyTable: 'No hay promociones registradas',
            info: 'Mostrando _START_ a _END_ de _TOTAL_ promociones',
            infoEmpty: 'Mostrando 0 a 0 de 0 promociones',
            infoFiltered: '(filtrado de _MAX_ promociones en total)',
            lengthMenu: 'Mostrar _MENU_ promociones',
            loadingRecords: 'Cargando...',
            processing: 'Procesando...',
            search: 'Buscar:',
            zeroRecords: 'No se encontraron promociones',
            paginate: {
                first: 'Primero',
                last: 'Ultimo',
                next: 'Siguiente',
                previous: 'Anterior'
            }
        }
    });
}

function cargarDataTables(callback) {
    if (dataTablesCargando) {
        return;
    }

    dataTablesCargando = true;
    cargarScript('https://cdn.datatables.net/1.13.8/js/jquery.dataTables.min.js', function() {
        cargarScript('https://cdn.datatables.net/1.13.8/js/dataTables.bootstrap4.min.js', function() {
            dataTablesCargando = false;
            callback();
        });
    });
}

function cargarScript(src, callback) {
    const script = document.createElement('script');
    script.src = src;
    script.onload = callback;
    script.onerror = function() {
        dataTablesCargando = false;
    };
    document.body.appendChild(script);
}

function editarPromo(idPromo) {
    const promo = promosPorId[idPromo];
    if (!promo) {
        showAlert('error', 'No se encontró la promoción para editar.');
        return;
    }

    limpiarBuscadorProducto();

    document.getElementById('form-title').textContent = 'Editar Promoción';
    document.getElementById('action').value = 'update';
    document.getElementById('id').value = promo.id_promocion || '';
    document.getElementById('titulo').value = promo.titulo || '';
    document.getElementById('codigo').value = promo.codigo || '';
    document.getElementById('estado').value = String(promo.estado ?? '1');
    document.getElementById('prioridad').value = promo.prioridad || 0;
    const precioUnidad = precioDePromo(promo, 'unidad');
    const precioPaca = precioDePromo(promo, 'paca');
    document.getElementById('precio_unidad_producto').value = precioUnidad;
    document.getElementById('precio_paca_producto').value = precioPaca;
    document.getElementById('acti_Unidad').value = String(promo.acti_Unidad ?? '1');
    document.getElementById('descripcion').value = promo.descripcion || '';

    const imagenInput = document.getElementById('imagen');
    imagenInput.value = '';
    imagenInput.required = false;
    const labelReq = document.getElementById('imagen-requerida-label');
    const ayuda = document.getElementById('imagen-ayuda');
    if (labelReq) labelReq.style.display = 'none';
    if (ayuda) ayuda.textContent = 'Opcional: deja vacío para conservar la imagen actual.';
    mostrarImagenActual(promo.imagen || '');

    abrirModalPromo();
}

function resetPromoForm() {
    document.getElementById('form-title').textContent = 'Nueva Promoción';
    document.getElementById('action').value = 'insert';
    document.getElementById('id').value = '';
    document.getElementById('imagen').required = false;
    const labelReq = document.getElementById('imagen-requerida-label');
    const ayuda = document.getElementById('imagen-ayuda');
    if (labelReq) labelReq.style.display = '';
    if (ayuda) ayuda.textContent = 'Obligatoria al crear. Opcional al editar.';
    mostrarImagenActual('');
    limpiarBuscadorProducto();
}

function numeroPrecio(valor) {
    const numero = Number(valor);
    return Number.isFinite(numero) ? numero : 0;
}

function precioDePromo(promo, tipo) {
    const propio = numeroPrecio(tipo === 'unidad' ? promo.precio_unidad_producto : promo.precio_paca_producto);
    const catalogo = numeroPrecio(tipo === 'unidad' ? promo.precio_unidad_catalogo : promo.precio_paca_catalogo);
    return propio > 0 ? propio : catalogo;
}

function htmlPreciosPromo(promo) {
    const unidad = precioDePromo(promo, 'unidad');
    const paca = precioDePromo(promo, 'paca');
    const esPaca = String(promo.acti_Unidad ?? '1') === '0' || (unidad <= 0 && paca > 0);
    return '<div class="promo-precios">' +
        '<div class="' + (esPaca ? '' : 'precio-principal') + '">Unidad: ' + (unidad > 0 ? formatoPrecio(unidad) : '—') + '</div>' +
        '<div class="' + (esPaca ? 'precio-principal' : '') + '">Paca: ' + (paca > 0 ? formatoPrecio(paca) : '—') + '</div>' +
        '</div>';
}

function mostrarImagenActual(nombre) {
    const box = document.getElementById('imagen-actual-box');
    const preview = document.getElementById('imagen-actual-preview');
    const hidden = document.getElementById('imagen_actual');
    const archivo = String(nombre || '').trim();

    if (hidden) hidden.value = archivo;
    if (!box || !preview) return;

    if (!archivo) {
        preview.removeAttribute('src');
        box.style.display = 'none';
        return;
    }

    preview.src = '../assets/img/licores/promos/' + archivo;
    box.style.display = '';
}

function formatoPrecio(valor) {
    return '$' + Number(valor || 0).toLocaleString('es-CO');
}

function limpiarBuscadorProducto() {
    productoPromoSeleccionado = null;
    Object.keys(productosBusqueda).forEach(function(key) {
        delete productosBusqueda[key];
    });

    const input = document.getElementById('buscar-producto');
    const resultados = document.getElementById('resultados-producto');
    const panel = document.getElementById('producto-precios');
    const nota = document.getElementById('nota-cantidad-botellas');

    if (input) input.value = '';
    if (resultados) {
        resultados.innerHTML = '';
        resultados.style.display = 'none';
    }
    if (panel) panel.style.display = 'none';
    if (nota) nota.textContent = '';

    const cantidad = document.getElementById('cantidad-botellas');
    if (cantidad) cantidad.value = '1';
    const calculado = document.getElementById('label-precio-calculado');
    if (calculado) calculado.textContent = '';
}

function buscarProductosPromo(termino) {
    const formData = new FormData();
    formData.append('action', 'buscarProducto');
    formData.append('q', termino);

    fetch('../controllers/promoController.php', {
        method: 'POST',
        body: formData
    })
    .then(function(response) { return response.json(); })
    .then(function(data) {
        if (document.getElementById('buscar-producto').value.trim() !== termino) {
            return;
        }
        renderResultadosProducto(data.success ? (data.productos || []) : []);
    })
    .catch(function(error) {
        console.error('Error al buscar productos:', error);
        renderResultadosProducto([]);
    });
}

function renderResultadosProducto(productos) {
    const cont = document.getElementById('resultados-producto');
    Object.keys(productosBusqueda).forEach(function(key) {
        delete productosBusqueda[key];
    });

    if (!productos.length) {
        cont.innerHTML = '<div class="product-search-empty">No se encontraron productos</div>';
        cont.style.display = 'block';
        return;
    }

    cont.innerHTML = productos.map(function(producto) {
        productosBusqueda[producto.id_producto] = producto;
        const cantidad = Number(producto.cantidad_paca_producto || 0);
        const detallePaca = cantidad > 1 ? ' · ' + cantidad + ' botellas' : '';
        return '<button type="button" class="product-search-item" data-id="' + escaparHtml(producto.id_producto) + '">' +
            '<strong>' + escaparHtml(producto.descripcion_producto) + '</strong>' +
            '<span>Cód. ' + escaparHtml(producto.codigo_productos || '-') +
            ' · Unidad ' + formatoPrecio(producto.precio_unidad_producto) +
            ' · Paca ' + formatoPrecio(producto.precio_paca_producto) + detallePaca + '</span>' +
            '</button>';
    }).join('');
    cont.style.display = 'block';
}

function aplicarProductoPromo(producto) {
    productoPromoSeleccionado = producto;
    const precioPaca = Number(producto.precio_paca_producto || 0);

    document.getElementById('codigo').value = producto.codigo_productos || '';
    document.getElementById('precio_paca_producto').value = precioPaca;
    document.getElementById('acti_Unidad').value = String(producto.acti_Unidad ?? '1');
    document.getElementById('cantidad-botellas').value = '1';

    const titulo = document.getElementById('titulo');
    if (titulo && titulo.value.trim() === '') {
        titulo.value = producto.descripcion_producto || '';
    }

    document.getElementById('label-precio-unidad').textContent = formatoPrecio(producto.precio_unidad_producto);
    document.getElementById('label-precio-paca').textContent = formatoPrecio(precioPaca);
    document.getElementById('producto-elegido-nombre').textContent = producto.descripcion_producto || '';
    document.getElementById('producto-precios').style.display = '';
    document.getElementById('resultados-producto').style.display = 'none';

    calcularPrecioPorBotellas();
    document.getElementById('cantidad-botellas').focus();
}

function calcularPrecioPorBotellas() {
    if (!productoPromoSeleccionado) {
        return;
    }

    const precioBotella = Number(productoPromoSeleccionado.precio_unidad_producto || 0);
    const cantidadInput = document.getElementById('cantidad-botellas');
    let cantidad = parseInt(cantidadInput.value, 10);
    if (!Number.isFinite(cantidad) || cantidad < 1) {
        cantidad = 1;
    }

    const total = Math.round(precioBotella * cantidad);
    document.getElementById('precio_unidad_producto').value = total;
    document.getElementById('label-precio-calculado').textContent = formatoPrecio(total);
    document.getElementById('nota-cantidad-botellas').textContent =
        cantidad + (cantidad === 1 ? ' botella' : ' botellas') +
        ' × ' + formatoPrecio(precioBotella) +
        ' = ' + formatoPrecio(total);
}

function cambiarEstadoPromo(input, idPromo) {
    const nuevoEstado = input.checked ? 1 : 0;
    const label = input.closest('.promo-status-switch').querySelector('label');

    input.disabled = true;
    actualizarTextoEstado(label, nuevoEstado);

    const formData = new FormData();
    formData.append('action', 'cambiarEstado');
    formData.append('id', idPromo);
    formData.append('estado', nuevoEstado);

    fetch('../controllers/promoController.php', {
        method: 'POST',
        body: formData
    })
    .then(response => response.json())
    .then(data => {
        if (!data.success) {
            input.checked = !input.checked;
            actualizarTextoEstado(label, input.checked ? 1 : 0);
            showAlert('error', data.message || 'No se pudo actualizar el estado.');
            return;
        }

        showAlert('success', data.message || 'Estado actualizado correctamente.');
    })
    .catch(error => {
        console.error('Error al cambiar estado:', error);
        input.checked = !input.checked;
        actualizarTextoEstado(label, input.checked ? 1 : 0);
        showAlert('error', 'Error de red al actualizar el estado.');
    })
    .finally(() => {
        input.disabled = false;
    });
}

function actualizarTextoEstado(label, estado) {
    label.textContent = estado === 1 ? 'Activa' : 'Inactiva';
    label.classList.toggle('status-active', estado === 1);
    label.classList.toggle('status-inactive', estado !== 1);
}

function enviarPromoPorId(idPromo) {
    const promo = promosPorId[idPromo];
    if (!promo) {
        showAlert('error', 'No se encontró la promoción para enviar.');
        return;
    }
    enviarPromo(promo.id_promocion, promo.descripcion, promo.imagen);
}

function enviarPromo(id, descripcion, imagen) {
    const formData = new FormData();
    formData.append('id', id);
    formData.append('texto', descripcion);
    formData.append('imagen1', imagen);
    formData.append('action', 'enviarPromo');

    fetch('../controllers/promoController.php', {
        method: 'POST',
        body: formData
    })
    .then(response => response.json())
    .then(result => {
        if (result.error) {
            showAlert('error', 'Error: ' + result.error);
            alert('❌ Hubo un error al enviar la promoción.');
        } else {
            showAlert('success', 'Promoción enviada correctamente.');
            alert('✅ Promoción enviada con éxito.');
            console.log(result);
        }
    })
    .catch(error => {
        console.error('Error al enviar la promoción:', error);
        showAlert('error', 'Error de red al enviar la promoción.');
    });
}
function showAlert(type, message) {
    const alertContainer = document.getElementById('alert-container');

    // Elimina alertas anteriores si existen
    alertContainer.innerHTML = '';

    // Define la clase según el tipo
    const alertClass = type === 'success'
        ? 'alert-success-modern'
        : 'alert-danger-modern';

    const icon = type === 'success'
        ? '<i class="bi bi-check-circle-fill me-2"></i>'
        : '<i class="bi bi-exclamation-triangle-fill me-2"></i>';

    const alert = document.createElement('div');
    alert.className = `alert alert-modern ${alertClass} d-flex align-items-center mb-4`;
    alert.setAttribute('role', 'alert');
    alert.innerHTML = `${icon} ${message}`;

    alertContainer.appendChild(alert);

    // Ocultar automáticamente después de 4 segundos
    setTimeout(() => {
        alert.remove();
    }, 4000);
}
</script>

<link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css" rel="stylesheet">

<style>
:root {
    --primary-color: #2563eb;
    --secondary-color: #64748b;
    --success-color: #059669;
    --danger-color: #dc2626;
    --warning-color: #d97706;
    --light-bg: #f8fafc;
    --card-shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
}

body {
    background-color: var(--light-bg);
    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
}

.modern-card {
    background: white;
    border-radius: 12px;
    box-shadow: var(--card-shadow);
    border: 1px solid #e2e8f0;
    transition: all 0.2s ease;
}

.modern-card:hover {
    box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1);
}

.promo-modal-content {
    border: none;
    border-radius: 12px;
    box-shadow: 0 20px 45px rgb(15 23 42 / 0.18);
}

.page-header {
    background-color: #1d4ed8;
    background-image: linear-gradient(135deg, #1d4ed8 0%, #3b82f6 100%);
    color: #ffffff;
    border-radius: 12px;
    padding: 2rem;
    margin-bottom: 2rem;
}

.page-header h1,
.page-header p,
.page-header i {
    color: #ffffff;
}

.form-control, .form-select {
    border: 1px solid #d1d5db;
    border-radius: 8px;
    padding: 0.75rem;
    transition: all 0.2s ease;
}

.form-control:focus, .form-select:focus {
    border-color: var(--primary-color);
    box-shadow: 0 0 0 3px rgb(37 99 235 / 0.1);
}

.btn-modern {
    border-radius: 8px;
    padding: 0.75rem 1.5rem;
    font-weight: 500;
    transition: all 0.2s ease;
    border: none;
}

.btn-primary-modern {
    background: var(--primary-color);
    color: white;
}

.btn-primary-modern:hover {
    background: #1d4ed8;
    transform: translateY(-1px);
}

.btn-outline-modern {
    background: transparent;
    border: 1px solid #d1d5db;
    color: var(--secondary-color);
}

.btn-outline-modern:hover {
    background: #f8fafc;
    border-color: var(--primary-color);
}

.emoji-picker {
    background: white;
    border: 1px solid #e2e8f0;
    border-radius: 12px;
    box-shadow: var(--card-shadow);
    padding: 1rem;
    max-width: 320px;
    max-height: 240px;
    overflow-y: auto;
}

.emoji-picker .emoji {
    display: inline-block;
    padding: 0.5rem;
    margin: 0.125rem;
    border-radius: 6px;
    cursor: pointer;
    transition: background-color 0.2s ease;
}

.emoji-picker .emoji:hover {
    background-color: #f1f5f9;
}

.table-modern {
    background: white;
    border-radius: 12px;
    overflow: hidden;
    box-shadow: var(--card-shadow);
}

.dataTables_wrapper {
    padding: 1rem;
}

.dataTables_wrapper .dataTables_length select,
.dataTables_wrapper .dataTables_filter input {
    border: 1px solid #d1d5db;
    border-radius: 8px;
    padding: 0.4rem 0.65rem;
}

.table.table-modern thead th {
    background-color: #1e3a5f;
    color: #ffffff;
    border: none;
    border-bottom: 2px solid #0f2744;
    padding: 1rem;
    font-weight: 600;
}

.table-modern td {
    border: none;
    padding: 1rem;
    border-top: 1px solid #f1f5f9;
}

.status-badge {
    padding: 0.25rem 0.75rem;
    border-radius: 20px;
    font-size: 0.875rem;
    font-weight: 500;
}

.promo-status-switch {
    min-width: 120px;
}

.promo-status-switch .form-check-input {
    cursor: pointer;
}

.promo-status-switch .form-check-label {
    margin-left: 0.35rem;
    cursor: pointer;
}

.status-active {
    background: #dcfce7;
    color: var(--success-color);
}

.status-inactive {
    background: #f1f5f9;
    color: var(--secondary-color);
}

.status-scheduled {
    background: #fef3c7;
    color: var(--warning-color);
}

.alert-modern {
    border: none;
    border-radius: 12px;
    padding: 1rem 1.5rem;
}

.alert-success-modern {
    background: #dcfce7;
    color: var(--success-color);
    border-left: 4px solid var(--success-color);
}

.alert-danger-modern {
    background: #fee2e2;
    color: var(--danger-color);
    border-left: 4px solid var(--danger-color);
}

.product-search-results {
    margin-top: 0.5rem;
    border: 1px solid #e2e8f0;
    border-radius: 8px;
    background: #fff;
    max-height: 240px;
    overflow-y: auto;
}

.product-search-item,
.product-search-empty {
    display: block;
    width: 100%;
    text-align: left;
    border: none;
    border-bottom: 1px solid #f1f5f9;
    background: #fff;
    padding: 0.75rem 1rem;
}

.product-search-item:last-child {
    border-bottom: none;
}

.product-search-item strong,
.product-search-item span {
    display: block;
}

.product-search-item span,
.product-search-empty {
    color: var(--secondary-color);
    font-size: 0.875rem;
}

.product-search-item:hover {
    background: #f8fafc;
}

.product-price-choice {
    margin-top: 0.75rem;
    padding: 0.85rem 1rem;
    border: 1px solid #dbeafe;
    border-radius: 8px;
    background: #f8fafc;
}

.precio-elegido .form-control {
    border-color: var(--primary-color);
    box-shadow: 0 0 0 3px rgb(37 99 235 / 0.12);
}

.promo-precios {
    white-space: nowrap;
    line-height: 1.35;
}

.promo-precios .precio-principal {
    font-weight: 600;
    color: #0f172a;
}

.imagen-actual-box {
    margin-bottom: 0.75rem;
}

.imagen-actual-box img {
    width: 96px;
    height: 96px;
    object-fit: cover;
    border-radius: 8px;
    border: 1px solid #e2e8f0;
    background: #f8fafc;
}
</style>

<?php include_once "footer.php"; ?>
