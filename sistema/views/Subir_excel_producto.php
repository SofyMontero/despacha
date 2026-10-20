<?php 
include_once "header.php"; 
require_once "../models/ProductoModel.php";

// Obtener productos desde la base de datos
$producto = new Producto();
$productos = $producto->obtenerProductosLista(0);
$categorias = $producto->obtenerCategorias();

// Capturar parámetros de resultado
$importados = $_GET['importados'] ?? 0;
$actualizados = $_GET['actualizados'] ?? 0;
$errores = $_GET['errores'] ?? 0;
$success = $_GET['success'] ?? null;
$error = $_GET['error'] ?? null;
$creado = $_GET['creado'] ?? null;
$editado = $_GET['editado'] ?? null;
$erroresDetalle = isset($_GET['errores_detalle']) ? explode('|', $_GET['errores_detalle']) : [];
$filasVacias = $_GET['filas_vacias'] ?? 0;
$delimitador = $_GET['delimitador'] ?? null;
$encabezado = $_GET['encabezado'] ?? null;
$preciosActualizados = $_GET['precios_actualizados'] ?? 0;
?>

<!-- Estilos para notificaciones toast -->
<link href="../css/subir_excel.css?v=4" rel="stylesheet" type="text/css" />


<!-- Container para las notificaciones toast -->
<div class="toast-container" id="toastContainer"></div>

<!-- Page header -->
<div class="full-box page-header">
    <h3 class="text-left">
        <i class="fas fa-box-open fa-fw"></i> &nbsp; PRODUCTOS
    </h3>
    <p class="text-justify">
        Descarga la plantilla con los productos actuales, edita los precios y vuelve a subir el CSV para actualizarlos en lote.
        También puedes crear productos nuevos o editar los existentes uno a uno.
    </p>
</div>

<!-- Estadísticas rápidas -->
<?php if ($success): ?>
<div class="stats-card">
    <div class="row">
        <div class="col-md-4">
            <div class="stat-item">
                <span class="stat-number"><?php echo $importados; ?></span>
                <span class="stat-label">Nuevos productos</span>
            </div>
        </div>
        <div class="col-md-4">
            <div class="stat-item">
                <span class="stat-number"><?php echo $actualizados; ?></span>
                <span class="stat-label">Productos actualizados</span>
            </div>
        </div>
        <div class="col-md-4">
            <div class="stat-item">
                <span class="stat-number"><?php echo count($productos); ?></span>
                <span class="stat-label">Total productos</span>
            </div>
        </div>
    </div>
</div>
<?php endif; ?>

<!-- Formulario de carga de Excel -->
<div class="container-fluid">

    <div class="container py-5">
        <div class="row justify-content-center">
            <div class="col-md-6">

                <div class="card shadow-lg border-0 rounded-4">
                    <div class="card-header bg-gradient text-white text-center rounded-top-4" style="background:  #009688 ;">
                        <h4 class="mb-0">💰 Actualización Masiva de Precios</h4>
                        <small>Importa productos y actualiza precios automáticamente</small>
                    </div>

                    <div class="card-body p-4">
                        <div class="text-center mb-4">
                            <button type="button"
                                    class="btn btn-primary btn-lg"
                                    data-toggle="modal"
                                    data-target="#crearProductoModal"
                                    id="btnCrearProducto">
                                <i class="fas fa-plus-circle"></i> Crear producto
                            </button>
                        </div>

                        <div class="template-download mb-4 text-center">
                            <p class="text-muted mb-2">
                                Descarga la plantilla con los productos actuales, edita precios y vuelve a subirla.
                            </p>
                            <a href="../controllers/ProductoController.php?accion=descargar_plantilla"
                               class="btn btn-outline-primary btn-lg"
                               id="btnDescargarPlantilla">
                                <i class="fas fa-download"></i> Descargar plantilla actualizada
                            </a>
                            <small class="d-block text-muted mt-2">
                                Incluye <?php echo count($productos); ?> productos · Formato CSV
                            </small>
                        </div>

                        <hr class="my-4">

                        <form id="uploadForm" action="../controllers/ProductoController.php" method="POST" enctype="multipart/form-data">
                            <input type="hidden" name="importar" value="1">
                            <div id="uploadProcessingOverlay">Procesando archivo...</div>
                            <div class="upload-zone" id="uploadZone">
                                <div class="upload-icon">💰</div>
                                <h5>Arrastra tu archivo CSV de precios aquí</h5>
                                <p class="text-muted">El sistema actualizará automáticamente los productos existentes</p>
                                <input type="file" name="archivo_excel" id="archivo_excel" class="file-input" accept=".csv">
                                <div class="progress-bar">
                                    <div class="progress-fill"></div>
                                </div>
                            </div>

                            <div class="mt-3 text-center">
                                <button type="button" class="btn btn-success btn-lg" id="submitBtn" disabled title="Selecciona un archivo CSV válido para continuar">
                                    <i class="fas fa-upload"></i> Procesar Archivo
                                </button>
                                <small id="submitHint" class="d-block text-muted mt-2">Selecciona un archivo CSV para habilitar el procesamiento</small>
                            </div>
                        </form>
                    </div>

                </div>

            </div>
        </div>
    </div>

</div>

<!-- Lista de productos -->
<div class="container-fluid mt-4">
    <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="card-title mb-0"><i class="fas fa-list"></i> &nbsp; Lista de Productos</h5>
            <button type="button"
                    class="btn btn-primary btn-sm"
                    data-toggle="modal"
                    data-target="#crearProductoModal"
                    id="btnNuevoProducto">
                <i class="fas fa-plus"></i> Nuevo
            </button>
        </div>
        <div class="card-body">
            <div class="table-responsive">
                <table class="table table-dark table-sm">
                    <thead>
                        <tr class="text-center roboto-medium">
                            <th>ID</th>
                            <th>CODIGO</th>
                            <th>PRODUCTO</th>
                            <th>EMBALAGE</th>
                            <th>PRECIO UNIDAD</th>
                            <th>PRECIO PACA</th>
                            <th></th>
                            <th>CATEGORIA</th>
                            <th>U o P</th>
                            <th>IMAGEN</th>
                            <th>ESTADO</th>
                            <th>ACCIONES</th>
                        </tr>
                    </thead>
                    <tbody>

                        <?php if (!empty($productos)): ?>
                            <?php foreach ($productos as $prod): ?>
                            <tr class="text-center producto-row"
                                data-id="<?php echo (int)$prod['id_producto']; ?>">
                                <td><?php echo $prod['id_producto']; ?></td>
                                <td><?php echo htmlspecialchars($prod['codigo_productos']); ?></td>
                                <td class="text-left"><?php echo htmlspecialchars($prod['descripcion_producto']); ?></td>
                                <td><?php echo $prod['cantidad_paca_producto']; ?></td>
                                <td>
                                    <div class="price-edit-wrap">
                                        <span class="price-prefix">$</span>
                                        <input type="number"
                                               class="form-control form-control-sm price-input precio-unidad"
                                               min="1"
                                               step="1"
                                               value="<?php echo (float)$prod['precio_unidad_producto']; ?>"
                                               data-original="<?php echo (float)$prod['precio_unidad_producto']; ?>"
                                               title="Editar precio unidad">
                                    </div>
                                </td>
                                <td>
                                    <div class="price-edit-wrap">
                                        <span class="price-prefix">$</span>
                                        <input type="number"
                                               class="form-control form-control-sm price-input precio-paca"
                                               min="0"
                                               step="1"
                                               value="<?php echo (float)$prod['precio_paca_producto']; ?>"
                                               data-original="<?php echo (float)$prod['precio_paca_producto']; ?>"
                                               title="Editar precio paca">
                                    </div>
                                </td>
                                <td>
                                    <button type="button"
                                            class="btn btn-sm btn-success btn-guardar-precio"
                                            title="Guardar precios"
                                            disabled>
                                        <i class="fas fa-save"></i>
                                    </button>
                                </td>
                                <td><?php echo $prod['id_cate_producto']; ?></td>
                                <td><?php echo $prod['acti_Unidad']; ?></td>
                                <td><?php echo htmlspecialchars($prod['imagen_producto']); ?></td>
                                <td><?php echo $prod['estado_producto']; ?></td>
                                <td>
                                    <?php
                                    $payloadProducto = htmlspecialchars(json_encode([
                                        'id' => (int)$prod['id_producto'],
                                        'codigo' => (string)$prod['codigo_productos'],
                                        'descripcion' => (string)$prod['descripcion_producto'],
                                        'embalaje' => (float)$prod['cantidad_paca_producto'],
                                        'precio_unidad' => (float)$prod['precio_unidad_producto'],
                                        'precio_paca' => (float)$prod['precio_paca_producto'],
                                        'categoria' => (int)$prod['id_cate_producto'],
                                        'unidad' => (string)$prod['acti_Unidad'],
                                        'imagen' => (string)$prod['imagen_producto'],
                                        'estado' => (string)$prod['estado_producto'],
                                    ], JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_HEX_AMP), ENT_QUOTES, 'UTF-8');
                                    ?>
                                    <button type="button"
                                            class="btn btn-sm btn-info btn-editar-producto"
                                            data-producto="<?php echo $payloadProducto; ?>"
                                            title="Editar producto">
                                        <i class="fas fa-edit"></i> Editar
                                    </button>
                                </td>
                            </tr>
                            <?php endforeach; ?>
                        <?php else: ?>
                            <tr><td colspan="12" class="text-center">No hay productos registrados</td></tr>
                        <?php endif; ?>
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>

<!-- Modal crear producto -->
<div class="modal fade" id="crearProductoModal" tabindex="-1" role="dialog" aria-labelledby="crearProductoModalLabel" aria-hidden="true">
    <div class="modal-dialog modal-lg" role="document">
        <form method="POST"
              action="../controllers/ProductoController.php"
              enctype="multipart/form-data"
              class="modal-content"
              id="formCrearProducto">
            <input type="hidden" name="accion" id="accion_producto" value="crear_producto">
            <input type="hidden" name="id_producto" id="id_producto" value="">

            <div class="modal-header" style="background: #009688; color: #fff;">
                <h5 class="modal-title" id="crearProductoModalLabel">
                    <i class="fas fa-plus-circle"></i> Crear producto
                </h5>
                <button type="button" class="close text-white" data-dismiss="modal" aria-label="Cerrar">
                    <span aria-hidden="true">&times;</span>
                </button>
            </div>

            <div class="modal-body">
                <div class="row">
                    <div class="col-md-6">
                        <div class="form-group">
                            <label for="codigo_productos">Código <span class="text-danger">*</span></label>
                            <input type="text" class="form-control" id="codigo_productos" name="codigo_productos" required maxlength="50" placeholder="Ej: 1001">
                        </div>
                    </div>
                    <div class="col-md-6">
                        <div class="form-group">
                            <label for="id_cate_producto">Categoría <span class="text-danger">*</span></label>
                            <select class="form-control" id="id_cate_producto" name="id_cate_producto" required>
                                <?php if (!empty($categorias)): ?>
                                    <?php foreach ($categorias as $cat): ?>
                                        <option value="<?php echo (int)$cat['id_categoria']; ?>">
                                            <?php echo htmlspecialchars($cat['nombre_categoria']); ?>
                                        </option>
                                    <?php endforeach; ?>
                                <?php else: ?>
                                    <option value="1">Sin categorías</option>
                                <?php endif; ?>
                            </select>
                        </div>
                    </div>
                    <div class="col-12">
                        <div class="form-group">
                            <label for="descripcion_producto">Descripción / Nombre <span class="text-danger">*</span></label>
                            <input type="text" class="form-control" id="descripcion_producto" name="descripcion_producto" required maxlength="255" placeholder="Ej: Aguardiente Antioqueño 750ml">
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="form-group">
                            <label for="cantidad_paca_producto">Embalaje (unid. por paca)</label>
                            <input type="number" class="form-control" id="cantidad_paca_producto" name="cantidad_paca_producto" min="1" step="1" value="1">
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="form-group">
                            <label for="precio_unidad_producto">Precio unidad <span class="text-danger">*</span></label>
                            <input type="number" class="form-control" id="precio_unidad_producto" name="precio_unidad_producto" min="1" step="1" required placeholder="0">
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="form-group">
                            <label for="precio_paca_producto">Precio paca</label>
                            <input type="number" class="form-control" id="precio_paca_producto" name="precio_paca_producto" min="0" step="1" value="0">
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="form-group">
                            <label for="acti_Unidad">Venta por unidad</label>
                            <select class="form-control" id="acti_Unidad" name="acti_Unidad">
                                <option value="1" selected>Sí</option>
                                <option value="0">No (solo paca)</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="form-group">
                            <label for="estado_producto">Estado</label>
                            <select class="form-control" id="estado_producto" name="estado_producto">
                                <option value="1" selected>Activo</option>
                                <option value="0">Inactivo</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="form-group">
                            <label for="imagen">Imagen</label>
                            <input type="file" class="form-control-file" id="imagen" name="imagen" accept="image/*">
                            <small class="form-text text-muted" id="imagen-ayuda">Opcional. JPG, PNG, GIF o WEBP (máx. 3MB)</small>
                        </div>
                    </div>
                </div>
            </div>

            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                <button type="submit" class="btn btn-success" id="btnGuardarProducto">
                    <i class="fas fa-save"></i> Guardar producto
                </button>
            </div>
        </form>
    </div>
</div>

<?php include_once "footer.php"; ?>

<script>
// Sistema de notificaciones toast
function showToast(type, title, message, details = []) {
    const toastContainer = document.getElementById('toastContainer');
    const toastId = 'toast-' + Date.now();
    
    const icons = {
        success: '✅',
        error: '❌',
        warning: '⚠️'
    };
    
    let detailsHtml = '';
    if (details.length > 0) {
        detailsHtml = '<div class="toast-details">' + details.map(d => '• ' + d).join('<br>') + '</div>';
    }
    
    const toast = document.createElement('div');
    toast.id = toastId;
    toast.className = `toast-notification toast-${type}`;
    toast.innerHTML = `
        <span class="toast-icon">${icons[type]}</span>
        <div class="toast-content">
            <div class="toast-title">${title}</div>
            <div class="toast-message">${message}</div>
            ${detailsHtml}
        </div>
        <button class="toast-close" onclick="closeToast('${toastId}')">&times;</button>
        <div class="toast-progress"></div>
    `;
    
    toastContainer.appendChild(toast);
    
    // Auto cerrar después de 5 segundos
    setTimeout(() => closeToast(toastId), 5000);
}

function closeToast(toastId) {
    const toast = document.getElementById(toastId);
    if (toast) {
        toast.style.animation = 'slideOut 0.3s ease-out';
        setTimeout(() => toast.remove(), 300);
    }
}

// Mostrar notificaciones según los parámetros URL
<?php if ($editado): ?>
    showToast('success', 'Producto actualizado', 'Los cambios se guardaron correctamente.');
<?php elseif ($creado): ?>
    showToast('success', 'Producto creado', 'El producto se registró correctamente.');
<?php elseif ($success): ?>
    <?php if ($importados > 0 || $actualizados > 0): ?>
        let message = '';
        if (<?php echo $importados; ?> > 0) message += '<?php echo $importados; ?> productos importados. ';
        if (<?php echo $actualizados; ?> > 0) message += '<?php echo $actualizados; ?> productos actualizados.';
        
        <?php if ($errores > 0): ?>
            showToast('warning', 'Proceso completado', message + ' <?php echo $errores; ?> errores encontrados.', <?php echo json_encode($erroresDetalle); ?>);
        <?php else: ?>
            showToast('success', '¡Éxito!', message);
        <?php endif; ?>
    <?php endif; ?>
<?php endif; ?>

<?php if ($error): ?>
    showToast('error', 'Error', <?php echo json_encode($error); ?>);
<?php endif; ?>

// Drag and drop functionality
const uploadZone = document.getElementById('uploadZone');
const fileInput = document.getElementById('archivo_excel');
const uploadForm = document.querySelector('#uploadForm');
const submitHint = document.getElementById('submitHint');
const progressBar = document.querySelector('.progress-bar');
const progressFill = document.querySelector('.progress-fill');
const overlayProceso = document.getElementById('uploadProcessingOverlay');
const submitBtnDefaultHtml = '<i class="fas fa-upload"></i> Procesar Archivo';
const zonaTituloDefault = uploadZone.querySelector('h5').textContent;
const zonaTextoDefault = uploadZone.querySelector('p').textContent;
const zonaIconoDefault = uploadZone.querySelector('.upload-icon').textContent;
let archivoValido = false;
let procesando = false;

function botonProcesar() {
    return document.getElementById('submitBtn');
}

function aplicarEstiloBloqueo(btn) {
    btn.disabled = true;
    btn.setAttribute('disabled', 'disabled');
    btn.setAttribute('aria-disabled', 'true');
    btn.classList.add('disabled', 'is-processing');
    btn.classList.remove('btn-success');
    btn.classList.add('btn-secondary');
    btn.style.setProperty('background-color', '#9e9e9e', 'important');
    btn.style.setProperty('background-image', 'none', 'important');
    btn.style.setProperty('border-color', '#9e9e9e', 'important');
    btn.style.setProperty('color', '#fff', 'important');
    btn.style.setProperty('opacity', '0.7', 'important');
    btn.style.setProperty('pointer-events', 'none', 'important');
    btn.style.setProperty('cursor', 'not-allowed', 'important');
    btn.style.setProperty('box-shadow', 'none', 'important');
    btn.style.setProperty('filter', 'grayscale(1)', 'important');
}

function quitarEstiloBloqueo(btn) {
    btn.classList.remove('disabled', 'is-processing', 'btn-secondary');
    btn.classList.add('btn-success');
    btn.removeAttribute('aria-disabled');
    btn.style.removeProperty('background-color');
    btn.style.removeProperty('background-image');
    btn.style.removeProperty('border-color');
    btn.style.removeProperty('color');
    btn.style.removeProperty('opacity');
    btn.style.removeProperty('pointer-events');
    btn.style.removeProperty('cursor');
    btn.style.removeProperty('box-shadow');
    btn.style.removeProperty('filter');
}

function setBotonProcesar(listo, mensajeHint = '') {
    if (procesando) {
        return;
    }
    const btn = botonProcesar();
    archivoValido = !!listo;
    btn.innerHTML = submitBtnDefaultHtml;
    if (listo) {
        btn.disabled = false;
        btn.removeAttribute('disabled');
        quitarEstiloBloqueo(btn);
        btn.title = 'Procesar el archivo seleccionado';
        submitHint.textContent = mensajeHint || 'Archivo listo. Puedes procesarlo.';
    } else {
        aplicarEstiloBloqueo(btn);
        btn.title = 'Selecciona un archivo CSV válido para continuar';
        submitHint.textContent = mensajeHint || 'Selecciona un archivo CSV para habilitar el procesamiento';
    }
}

function bloquearDuranteProceso() {
    procesando = true;
    archivoValido = false;
    const btn = botonProcesar();
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Procesando...';
    btn.title = 'El archivo se está procesando';
    aplicarEstiloBloqueo(btn);
    submitHint.textContent = 'Procesando el archivo, espera un momento...';
    uploadZone.classList.add('is-processing');
    progressBar.style.display = 'block';
    if (overlayProceso) {
        overlayProceso.classList.add('is-active');
    }
}

function resetZonaCarga() {
    if (procesando) {
        return;
    }
    archivoValido = false;
    uploadZone.classList.remove('is-processing');
    uploadZone.querySelector('h5').textContent = zonaTituloDefault;
    uploadZone.querySelector('p').textContent = zonaTextoDefault;
    uploadZone.querySelector('.upload-icon').textContent = zonaIconoDefault;
    progressBar.style.display = 'none';
    progressFill.style.width = '0%';
    if (overlayProceso) {
        overlayProceso.classList.remove('is-active');
    }
    setBotonProcesar(false);
}

function iniciarProcesamiento() {
    if (procesando) {
        return false;
    }
    if (!archivoValido || !fileInput.files[0]) {
        showToast('error', 'Sin archivo', 'Espera a que el archivo se cargue correctamente');
        return false;
    }

    bloquearDuranteProceso();
    showToast('warning', 'Procesando archivo', 'El archivo se está procesando. No cierres esta página.');

    let progress = 0;
    const interval = setInterval(() => {
        progress += Math.random() * 20;
        if (progress > 90) progress = 90;
        progressFill.style.width = progress + '%';
    }, 120);

    setTimeout(() => {
        progressFill.style.width = '90%';
        clearInterval(interval);
        uploadForm.submit();
    }, 200);

    return true;
}

uploadZone.addEventListener('click', (e) => {
    if (procesando) {
        e.preventDefault();
        e.stopPropagation();
        return;
    }
    if (e.target === fileInput) {
        return;
    }
    fileInput.click();
});

// Drag and drop events
uploadZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    if (procesando) {
        return;
    }
    uploadZone.classList.add('dragover');
});

uploadZone.addEventListener('dragleave', () => {
    uploadZone.classList.remove('dragover');
});

uploadZone.addEventListener('drop', (e) => {
    e.preventDefault();
    uploadZone.classList.remove('dragover');
    if (procesando) {
        return;
    }

    const files = e.dataTransfer.files;
    if (files.length > 0) {
        fileInput.files = files;
        handleFileSelect();
    }
});

// File selection handler
fileInput.addEventListener('change', handleFileSelect);

function handleFileSelect() {
    if (procesando) {
        return;
    }

    const file = fileInput.files[0];
    if (!file) {
        resetZonaCarga();
        return;
    }

    const fileName = file.name;
    const fileSize = (file.size / 1024 / 1024).toFixed(2);

    archivoValido = false;
    const btnCarga = botonProcesar();
    btnCarga.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Cargando archivo...';
    aplicarEstiloBloqueo(btnCarga);
    submitHint.textContent = 'Espera a que el archivo se cargue correctamente';

    if (!fileName.toLowerCase().endsWith('.csv')) {
        showToast('error', 'Archivo inválido', 'Solo se permiten archivos CSV');
        fileInput.value = '';
        resetZonaCarga();
        return;
    }

    if (file.size > 5 * 1024 * 1024) {
        showToast('error', 'Archivo muy grande', 'El archivo no debe superar los 5MB');
        fileInput.value = '';
        resetZonaCarga();
        return;
    }

    const reader = new FileReader();
    reader.onload = function (event) {
        if (procesando) {
            return;
        }
        const contenido = event.target.result;
        if (!contenido || !String(contenido).trim()) {
            showToast('error', 'Archivo vacío', 'El CSV no tiene contenido para procesar');
            fileInput.value = '';
            resetZonaCarga();
            return;
        }

        uploadZone.querySelector('h5').textContent = fileName;
        uploadZone.querySelector('p').textContent = `${fileSize} MB - Listo para procesar`;
        uploadZone.querySelector('.upload-icon').textContent = '📄✅';
        setBotonProcesar(true, `${fileName} cargado. Ya puedes procesarlo.`);
        showToast('success', 'Archivo listo', `${fileName} (${fileSize} MB)`);
    };
    reader.onerror = function () {
        showToast('error', 'Error de carga', 'No se pudo leer el archivo. Inténtalo de nuevo.');
        fileInput.value = '';
        resetZonaCarga();
    };
    reader.readAsText(file);
}

// Bloquear en el primer toque, antes de que Material Design u otros scripts lo manejen
['pointerdown', 'click'].forEach(function (evt) {
    document.addEventListener(evt, function (e) {
        const btn = e.target.closest('#submitBtn');
        if (!btn) {
            return;
        }
        e.preventDefault();
        e.stopPropagation();
        iniciarProcesamiento();
    }, true);
});

uploadForm.addEventListener('submit', (e) => {
    e.preventDefault();
    iniciarProcesamiento();
});

aplicarEstiloBloqueo(botonProcesar());

// Crear y editar producto desde el mismo modal
const formCrearProducto = document.getElementById('formCrearProducto');
const btnGuardarProducto = document.getElementById('btnGuardarProducto');
const accionProducto = document.getElementById('accion_producto');
const idProductoInput = document.getElementById('id_producto');
const tituloModalProducto = document.getElementById('crearProductoModalLabel');
const ayudaImagen = document.getElementById('imagen-ayuda');
const ayudaImagenDefault = 'Opcional. JPG, PNG, GIF o WEBP (máx. 3MB)';

function prepararFormularioCrear() {
    if (!formCrearProducto) {
        return;
    }
    formCrearProducto.reset();
    accionProducto.value = 'crear_producto';
    idProductoInput.value = '';
    tituloModalProducto.innerHTML = '<i class="fas fa-plus-circle"></i> Crear producto';
    btnGuardarProducto.disabled = false;
    btnGuardarProducto.innerHTML = '<i class="fas fa-save"></i> Guardar producto';
    if (ayudaImagen) {
        ayudaImagen.textContent = ayudaImagenDefault;
    }
}

function abrirEdicionProducto(prod) {
    prepararFormularioCrear();
    accionProducto.value = 'editar_producto';
    idProductoInput.value = prod.id || '';
    document.getElementById('codigo_productos').value = prod.codigo || '';
    document.getElementById('descripcion_producto').value = prod.descripcion || '';
    document.getElementById('cantidad_paca_producto').value = prod.embalaje || 1;
    document.getElementById('precio_unidad_producto').value = prod.precio_unidad || '';
    document.getElementById('precio_paca_producto').value = prod.precio_paca || 0;
    document.getElementById('id_cate_producto').value = String(prod.categoria || '');
    document.getElementById('acti_Unidad').value = String(prod.unidad ?? '1');
    document.getElementById('estado_producto').value = String(prod.estado ?? '1');
    tituloModalProducto.innerHTML = '<i class="fas fa-edit"></i> Editar producto';
    btnGuardarProducto.innerHTML = '<i class="fas fa-save"></i> Guardar cambios';
    if (ayudaImagen) {
        ayudaImagen.textContent = prod.imagen
            ? 'Imagen actual: ' + prod.imagen + '. Sube otra solo si quieres reemplazarla.'
            : 'Sin imagen. Puedes subir una ahora. ' + ayudaImagenDefault;
    }
    $('#crearProductoModal').modal('show');
}

['btnCrearProducto', 'btnNuevoProducto'].forEach((id) => {
    const btn = document.getElementById(id);
    if (btn) {
        btn.addEventListener('click', prepararFormularioCrear);
    }
});

document.querySelectorAll('.btn-editar-producto').forEach((btn) => {
    btn.addEventListener('click', () => {
        try {
            const prod = JSON.parse(btn.dataset.producto || '{}');
            if (!prod.id) {
                showToast('error', 'Error', 'No se pudo cargar el producto para editar');
                return;
            }
            const row = btn.closest('.producto-row');
            if (row) {
                const unidad = row.querySelector('.precio-unidad');
                const paca = row.querySelector('.precio-paca');
                if (unidad) prod.precio_unidad = Number(unidad.value);
                if (paca) prod.precio_paca = Number(paca.value);
            }
            abrirEdicionProducto(prod);
        } catch (err) {
            showToast('error', 'Error', 'No se pudo leer la información del producto');
        }
    });
});

if (formCrearProducto) {
    formCrearProducto.addEventListener('submit', () => {
        btnGuardarProducto.disabled = true;
        btnGuardarProducto.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Guardando...';
    });
}

// Edición inline de precios en la lista
function marcarFilaCambiada(row) {
    const unidadInput = row.querySelector('.precio-unidad');
    const pacaInput = row.querySelector('.precio-paca');
    const btnGuardar = row.querySelector('.btn-guardar-precio');
    const cambio =
        Number(unidadInput.value) !== Number(unidadInput.dataset.original) ||
        Number(pacaInput.value) !== Number(pacaInput.dataset.original);

    btnGuardar.disabled = !cambio;
    row.classList.toggle('precio-modificado', cambio);
}

async function guardarPreciosFila(row) {
    const id = row.dataset.id;
    const unidadInput = row.querySelector('.precio-unidad');
    const pacaInput = row.querySelector('.precio-paca');
    const btnGuardar = row.querySelector('.btn-guardar-precio');
    const precioUnidad = Number(unidadInput.value);
    const precioPaca = Number(pacaInput.value);

    if (!precioUnidad || precioUnidad <= 0) {
        showToast('error', 'Precio inválido', 'El precio unidad debe ser mayor a 0');
        unidadInput.focus();
        return;
    }

    if (precioPaca < 0) {
        showToast('error', 'Precio inválido', 'El precio paca no puede ser negativo');
        pacaInput.focus();
        return;
    }

    btnGuardar.disabled = true;
    btnGuardar.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';

    const formData = new FormData();
    formData.append('accion', 'actualizar_precios');
    formData.append('id_producto', id);
    formData.append('precio_unidad_producto', precioUnidad);
    formData.append('precio_paca_producto', precioPaca);

    try {
        const response = await fetch('../controllers/ProductoController.php', {
            method: 'POST',
            body: formData
        });
        const data = await response.json();

        if (data.success) {
            unidadInput.dataset.original = String(precioUnidad);
            pacaInput.dataset.original = String(precioPaca);
            row.classList.remove('precio-modificado');
            row.classList.add('precio-guardado');
            setTimeout(() => row.classList.remove('precio-guardado'), 1200);
            showToast('success', 'Precios guardados', data.message || 'Actualización correcta');
        } else {
            showToast('error', 'Error', data.message || 'No se pudieron guardar los precios');
            marcarFilaCambiada(row);
        }
    } catch (err) {
        showToast('error', 'Error de red', 'No se pudo conectar con el servidor');
        marcarFilaCambiada(row);
    } finally {
        btnGuardar.innerHTML = '<i class="fas fa-save"></i>';
        marcarFilaCambiada(row);
    }
}

document.querySelectorAll('.producto-row').forEach((row) => {
    const inputs = row.querySelectorAll('.price-input');
    const btnGuardar = row.querySelector('.btn-guardar-precio');

    inputs.forEach((input) => {
        input.addEventListener('input', () => marcarFilaCambiada(row));
        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                if (!btnGuardar.disabled) {
                    guardarPreciosFila(row);
                }
            }
        });
    });

    btnGuardar.addEventListener('click', () => guardarPreciosFila(row));
});

// Limpiar parámetros URL después de mostrar notificaciones
if (window.location.search) {
    const url = window.location.protocol + "//" + window.location.host + window.location.pathname;
    window.history.replaceState({path: url}, '', url);
}
</script>
