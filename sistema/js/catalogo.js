// ===========================================
// FUNCIONALIDAD DE BÚSQUEDA
// ===========================================

document.addEventListener("DOMContentLoaded", function () {
  const searchInput = document.getElementById("searchInput");
  const productGrid = document.getElementById("productGrid");
  const noResults = document.getElementById("noResults");
  const productItems = document.querySelectorAll(".product-item");

  // Función para normalizar texto (quitar acentos y convertir a minúsculas)
  function normalizeText(text) {
    return text
      .toLowerCase()
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .trim();
  }

  // Función para filtrar productos con animación suave
  function filterProducts(searchTerm) {
    const normalizedSearch = normalizeText(searchTerm);
    let visibleCount = 0;

    productItems.forEach((item) => {
      const productName = item.getAttribute("data-name");
      const normalizedProductName = normalizeText(productName);

      if (
        normalizedProductName.includes(normalizedSearch) ||
        searchTerm === ""
      ) {
        item.classList.remove("hidden");
        item.style.display = "block";
        visibleCount++;
      } else {
        item.classList.add("hidden");
        // Usar setTimeout para animación suave
        setTimeout(() => {
          if (item.classList.contains("hidden")) {
            item.style.display = "none";
          }
        }, 150);
      }
    });

    // Mostrar/ocultar mensaje de "no results"
    if (visibleCount === 0 && searchTerm !== "") {
      setTimeout(() => {
        noResults.style.display = "block";
      }, 200);
    } else {
      noResults.style.display = "none";
    }

    return visibleCount;
  }

  // Event listener para búsqueda en tiempo real
  searchInput.addEventListener("input", function () {
    const searchTerm = this.value.trim();
    filterProducts(searchTerm);
  });

  // Event listener para búsqueda al presionar Enter
  searchInput.addEventListener("keypress", function (e) {
    if (e.key === "Enter") {
      e.preventDefault();
      const searchTerm = this.value.trim();
      filterProducts(searchTerm);
    }
  });

  // Agregar botón de búsqueda visual (solo si no hay uno ya)
  const existingSearchBtn = searchInput.parentNode.querySelector(
    '.btn-outline-secondary:not([class*="fa-shopping-cart"])'
  );
  if (!existingSearchBtn) {
    const searchButton = document.createElement("button");
    searchButton.type = "button";
    searchButton.className = "btn btn-outline-secondary btn-sm";
    searchButton.innerHTML = '<i class="fas fa-search"></i>';
    searchButton.style.marginLeft = "5px";

    // Insertar botón después del input
    searchInput.parentNode.insertBefore(searchButton, searchInput.nextSibling);

    // Event listener para el botón de búsqueda
    searchButton.addEventListener("click", function () {
      const searchTerm = searchInput.value.trim();
      filterProducts(searchTerm);
      searchInput.focus();
    });
  }

  // Función para limpiar búsqueda
  function clearSearch() {
    searchInput.value = "";
    filterProducts("");
    searchInput.focus();
  }

  // Agregar botón de limpiar cuando hay texto
  searchInput.addEventListener("input", function () {
    const existingClearBtn = document.querySelector(".clear-search-btn");

    if (this.value.length > 0 && !existingClearBtn) {
      const clearBtn = document.createElement("button");
      clearBtn.type = "button";
      clearBtn.className =
        "btn btn-link clear-search-btn p-0 position-absolute";
      clearBtn.style.right = "45px";
      clearBtn.style.top = "50%";
      clearBtn.style.transform = "translateY(-50%)";
      clearBtn.style.zIndex = "10";
      clearBtn.innerHTML = '<i class="fas fa-times text-muted"></i>';
      clearBtn.title = "Limpiar búsqueda";

      // Posicionar el input container como relative
      searchInput.parentNode.style.position = "relative";
      searchInput.parentNode.appendChild(clearBtn);

      clearBtn.addEventListener("click", clearSearch);
    } else if (this.value.length === 0 && existingClearBtn) {
      existingClearBtn.remove();
    }
  });
});

// ===========================================
// CARRITO DE COMPRAS FUNCIONAL CON CANTIDAD EDITABLE
// ===========================================

let carrito = [];
let totalGeneral = 0;
// Variable global para mantener las observaciones
let observacionesGlobales = "";

// Elementos del DOM
const cartCountElement = document.getElementById("cartCount");
const orderSummaryElement = document.getElementById("orderSummary");
const itemsCountElement = document.getElementById("itemsCount");
const totalAmountElement = document.getElementById("totalAmount");
const submitBtnElement = document.getElementById("submitBtn");
const btnItemCountElement = document.getElementById("btnItemCount");

// Event listeners globales
document.addEventListener("click", function (e) {
  if (
    e.target.classList.contains("agregar-btn") ||
    e.target.closest(".agregar-btn")
  ) {
    agregarAlCarrito(e);
  }

  // Event listener para mostrar carrito
  if (
    e.target.closest(".fa-shopping-cart") ||
    e.target.closest("button .fas.fa-shopping-cart")
  ) {
    toggleCarrito();
  }

  // También detectar click en el botón que contiene el carrito
  const cartButton = e.target.closest("button");
  if (cartButton && cartButton.querySelector(".fa-shopping-cart")) {
    toggleCarrito();
  }
});

// Event listener para cambios en tipo de producto
document.addEventListener("change", function (e) {
  if (e.target.classList.contains("tipo-select")) {
    actualizarPrecioMostrado(e);
  }
});

// Event listener mejorado para observaciones
document.addEventListener("input", function (e) {
  if (e.target.id === "observacionesPedido" || e.target.id === "observacionesPedidoModal") {
    observacionesGlobales = e.target.value;
    sessionStorage.setItem("observacionesPedido", e.target.value);
    console.log("🔄 Observaciones actualizadas:", e.target.value);
  }
});

// Función para agregar productos al carrito
function agregarAlCarrito(event) {
  const btn = event.target.closest(".agregar-btn");
  const productCard = btn.closest(".product-card");

  // Obtener datos del producto
  const id = btn.dataset.id;
  const nombre = btn.dataset.nombre;
  const precioUnidad = parseFloat(btn.dataset.precioUnidad);
  const precioPaca = parseFloat(btn.dataset.precioPaca);

  // Obtener valores del formulario
  const tipoSelect = productCard.querySelector(".tipo-select");
  const cantidadInput = productCard.querySelector(".cantidad-input");

  const tipo = tipoSelect.value;
  const cantidad = parseInt(cantidadInput.value) || 1;

  // Validaciones
  if (!tipo) {
    mostrarAlerta("Por favor selecciona el tipo (Unidad o Paca)", "warning");
    tipoSelect.focus();
    return;
  }

  if (cantidad <= 0) {
    mostrarAlerta("La cantidad debe ser mayor a 0", "warning");
    cantidadInput.focus();
    return;
  }

  // Calcular precio según el tipo
  const precioUnitario = tipo === "paca" ? precioPaca : precioUnidad;
  const precioTotal = precioUnitario * cantidad;

  // Verificar si ya existe el mismo producto con el mismo tipo
  const existingIndex = carrito.findIndex(
    (item) => item.id === id && item.tipo === tipo
  );

  if (existingIndex !== -1) {
    // Si existe, sumar la cantidad
    carrito[existingIndex].cantidad += cantidad;
    carrito[existingIndex].precioTotal =
      carrito[existingIndex].precioUnitario * carrito[existingIndex].cantidad;
    mostrarAlerta(
      `Cantidad actualizada: ${carrito[existingIndex].cantidad} ${tipo}s de ${nombre}`,
      "success"
    );
  } else {
    // Si no existe, crear nuevo producto
    const producto = {
      id: id,
      nombre: nombre,
      tipo: tipo,
      cantidad: cantidad,
      precioUnitario: precioUnitario,
      precioTotal: precioTotal,
      timestamp: Date.now(),
    };

    carrito.push(producto);
    mostrarAlerta(`${nombre} agregado al carrito`, "success");
  }

  // Actualizar interfaz
  actualizarContadores();

  // Limpiar formulario
  tipoSelect.value = "";
  cantidadInput.value = "";

  // Resetear estilos de precio
  const precioUnidadElement = productCard.querySelector(".price-unidad");
  const precioPacaElement = productCard.querySelector(".price-paca");
  if (precioUnidadElement && precioPacaElement) {
    precioUnidadElement.style.fontWeight = "normal";
    precioUnidadElement.style.color = "";
    precioPacaElement.style.fontWeight = "normal";
    precioPacaElement.style.color = "";
  }

  // Animación del botón
  btn.classList.remove("btn-outline-success");
  btn.classList.add("btn-success");
  btn.innerHTML = '<i class="fas fa-check me-1"></i> Agregado';

  setTimeout(() => {
    btn.classList.remove("btn-success");
    btn.classList.add("btn-outline-success");
    btn.innerHTML = '<i class="fas fa-cart-plus me-1"></i> Agregar';
  }, 1500);

  // Guardar en localStorage
  guardarCarrito();
}

// Función para actualizar contadores y resumen
function actualizarContadores() {
  const totalItems = carrito.length;
  totalGeneral = carrito.reduce((sum, item) => sum + item.precioTotal, 0);

  // Actualizar contador del carrito
  if (cartCountElement) {
    cartCountElement.textContent = totalItems;
    if (totalItems > 0) {
      cartCountElement.style.display = "inline";
      cartCountElement.classList.remove("d-none");
    } else {
      cartCountElement.style.display = "none";
      cartCountElement.classList.add("d-none");
    }
  }

  // Actualizar resumen del pedido
  if (totalItems > 0) {
    if (orderSummaryElement) orderSummaryElement.style.display = "block";
    if (itemsCountElement)
      itemsCountElement.textContent = `${totalItems} producto${
        totalItems !== 1 ? "s" : ""
      }`;
    if (totalAmountElement)
      totalAmountElement.textContent = new Intl.NumberFormat("es-CO").format(
        totalGeneral
      );

    // Habilitar botón de envío
    if (submitBtnElement) {
      submitBtnElement.disabled = false;
      submitBtnElement.classList.remove("btn-secondary");
      submitBtnElement.classList.add("btn-success");
    }
    if (btnItemCountElement)
      btnItemCountElement.textContent = `(${totalItems})`;
  } else {
    if (orderSummaryElement) orderSummaryElement.style.display = "none";
    if (submitBtnElement) {
      submitBtnElement.disabled = true;
      submitBtnElement.classList.remove("btn-success");
      submitBtnElement.classList.add("btn-secondary");
    }
    if (btnItemCountElement) btnItemCountElement.textContent = "";
  }
}

// Función para cambiar cantidad en el carrito
function cambiarCantidad(index, nuevaCantidad) {
  if (index < 0 || index >= carrito.length) return;

  nuevaCantidad = parseInt(nuevaCantidad);

  if (isNaN(nuevaCantidad) || nuevaCantidad <= 0) {
    mostrarAlerta("La cantidad debe ser un número mayor a 0", "warning");
    return;
  }

  // Actualizar cantidad y recalcular precio total
  carrito[index].cantidad = nuevaCantidad;
  carrito[index].precioTotal = carrito[index].precioUnitario * nuevaCantidad;

  // Actualizar interfaz
  actualizarContadores();
  guardarCarrito();

  // Refrescar modal si está abierto
  const modal = document.querySelector(".modal.show");
  if (modal) {
    modal.querySelector(".btn-close").click();
    setTimeout(() => mostrarModalCarrito(), 300);
  }

  mostrarAlerta(`Cantidad actualizada: ${carrito[index].nombre}`, "success");
}

// Función para mostrar/ocultar carrito
function toggleCarrito() {
  if (carrito.length === 0) {
    mostrarAlerta("El carrito está vacío", "info");
    return;
  }

  mostrarModalCarrito();
}

// Función mejorada para mostrar modal del carrito responsive con cantidad editable
function mostrarModalCarrito() {
  // Recuperar observaciones guardadas
  const observacionesGuardadas = sessionStorage.getItem("observacionesPedido") || "";
  
  const modal = document.createElement("div");
  modal.className = "modal fade";
  modal.setAttribute("tabindex", "-1");
  modal.innerHTML = `
        <div class="modal-dialog modal-xl modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header bg-light">
                    <h5 class="modal-title d-flex align-items-center">
                        <i class="fas fa-shopping-cart me-2 text-success"></i>
                        <span class="d-none d-sm-inline">Carrito de Compras</span>
                        <span class="d-sm-none">Carrito</span>
                        <span class="badge bg-success ms-2">${
                          carrito.length
                        }</span>
                    </h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Cerrar"></button>
                </div>
                <div class="modal-body p-0">
                    <!-- Vista Desktop/Tablet - Tabla -->
                    <div class="d-none d-md-block">
                        <div class="table-responsive">
                            <table class="table table-hover mb-0">
                                <thead class="table-light sticky-top">
                                    <tr>
                                        <th class="border-0">Producto</th>
                                        <th class="border-0">Tipo</th>
                                        <th class="border-0">Cantidad</th>
                                        <th class="border-0">Precio Unit.</th>
                                        <th class="border-0">Subtotal</th>
                                        <th class="border-0 text-center">Acciones</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    ${carrito
                                      .map(
                                        (item, index) => `
                                        <tr>
                                            <td class="fw-semibold align-middle">
                                                <div class="text-truncate" style="max-width: 200px;" title="${
                                                  item.nombre
                                                }">
                                                    ${item.nombre}
                                                </div>
                                            </td>
                                            <td class="align-middle">
                                                <span class="badge bg-${
                                                  item.tipo === "paca"
                                                    ? "primary"
                                                    : "secondary"
                                                }">
                                                    ${
                                                      item.tipo
                                                        .charAt(0)
                                                        .toUpperCase() +
                                                      item.tipo.slice(1)
                                                    }
                                                </span>
                                            </td>
                                            <td class="align-middle">
                                                <div class="input-group input-group-sm" style="width: 120px;">
                                                    <button class="btn btn-outline-secondary btn-sm" type="button" onclick="cambiarCantidad(${index}, ${
                                          item.cantidad - 1
                                        })">
                                                        <i class="fas fa-minus"></i>
                                                    </button>
                                                    <input type="number" class="form-control text-center fw-bold" 
                                                           value="${
                                                             item.cantidad
                                                           }" min="1" 
                                                           onchange="cambiarCantidad(${index}, this.value)"
                                                           onblur="cambiarCantidad(${index}, this.value)">
                                                    <button class="btn btn-outline-secondary btn-sm" type="button" onclick="cambiarCantidad(${index}, ${
                                          item.cantidad + 1
                                        })">
                                                        <i class="fas fa-plus"></i>
                                                    </button>
                                                </div>
                                            </td>
                                            <td class="align-middle">${new Intl.NumberFormat(
                                              "es-CO"
                                            ).format(item.precioUnitario)}</td>
                                            <td class="align-middle fw-bold text-success">
                                                ${new Intl.NumberFormat(
                                                  "es-CO"
                                                ).format(item.precioTotal)}
                                            </td>
                                            <td class="align-middle text-center">
                                                <button class="btn btn-sm btn-outline-danger" onclick="eliminarDelCarrito(${index})" title="Eliminar">
                                                    <i class="fas fa-trash"></i>
                                                </button>
                                            </td>
                                        </tr>
                                    `
                                      )
                                      .join("")}
                                </tbody>
                            </table>
                        </div>
                    </div>

                    <!-- Vista Mobile - Cards -->
                    <div class="d-md-none">
                        <div class="p-3">
                            ${carrito
                              .map(
                                (item, index) => `
                                <div class="card mb-3 shadow-sm">
                                    <div class="card-body p-3">
                                        <div class="d-flex justify-content-between align-items-start mb-3">
                                            <h6 class="card-title mb-1 fw-bold" style="line-height: 1.2;">
                                                ${item.nombre}
                                            </h6>
                                            <button class="btn btn-sm btn-outline-danger ms-2" onclick="eliminarDelCarrito(${index})" title="Eliminar">
                                                <i class="fas fa-trash"></i>
                                            </button>
                                        </div>
                                        
                                        <!-- Información del producto -->
                                        <div class="row g-2 mb-3 text-sm">
                                            <div class="col-6">
                                                <div class="d-flex align-items-center">
                                                    <i class="fas fa-tag text-muted me-1" style="font-size: 12px;"></i>
                                                    <span class="badge bg-${
                                                      item.tipo === "paca"
                                                        ? "primary"
                                                        : "secondary"
                                                    }">
                                                        ${
                                                          item.tipo
                                                            .charAt(0)
                                                            .toUpperCase() +
                                                          item.tipo.slice(1)
                                                        }
                                                    </span>
                                                </div>
                                            </div>
                                            <div class="col-6">
                                                <div class="text-muted small">
                                                    <i class="fas fa-dollar-sign me-1" style="font-size: 10px;"></i>
                                                    ${new Intl.NumberFormat(
                                                      "es-CO"
                                                    ).format(
                                                      item.precioUnitario
                                                    )}
                                                </div>
                                            </div>
                                        </div>

                                        <!-- Control de cantidad -->
                                        <div class="d-flex justify-content-between align-items-center">
                                            <div class="d-flex align-items-center">
                                                <span class="text-muted small me-2">Cantidad:</span>
                                                <div class="input-group input-group-sm" style="width: 110px;">
                                                    <button class="btn btn-outline-secondary btn-sm" type="button" onclick="cambiarCantidad(${index}, ${
                                  item.cantidad - 1
                                })">
                                                        <i class="fas fa-minus"></i>
                                                    </button>
                                                    <input type="number" class="form-control text-center fw-bold" 
                                                           value="${
                                                             item.cantidad
                                                           }" min="1"
                                                           onchange="cambiarCantidad(${index}, this.value)"
                                                           onblur="cambiarCantidad(${index}, this.value)">
                                                    <button class="btn btn-outline-secondary btn-sm" type="button" onclick="cambiarCantidad(${index}, ${
                                  item.cantidad + 1
                                })">
                                                        <i class="fas fa-plus"></i>
                                                    </button>
                                                </div>
                                            </div>
                                            <div class="fw-bold text-success">
                                                ${new Intl.NumberFormat(
                                                  "es-CO"
                                                ).format(item.precioTotal)}
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            `
                              )
                              .join("")}
                        </div>
                    </div>

                    <!-- Total General -->
                    <div class="border-top bg-light p-3">
                        <div class="row align-items-center">
                            <div class="col-12 col-sm-6">
                                <h5 class="mb-0 d-flex align-items-center">
                                    <i class="fas fa-calculator text-success me-2"></i>
                                    <span class="d-none d-sm-inline">Total General:</span>
                                    <span class="d-sm-none">Total:</span>
                                </h5>
                            </div>
                            <div class="col-12 col-sm-6 text-sm-end">
                                <h4 class="mb-0 text-success fw-bold">
                                    ${new Intl.NumberFormat("es-CO").format(
                                      totalGeneral
                                    )} COP
                                </h4>
                            </div>
                        </div>
                    </div>
                </div>
                
                <!-- Observaciones -->
                <div class="border-top p-3 bg-white">
                    <label for="observacionesPedidoModal" class="form-label fw-semibold">
                        <i class="fas fa-comment-dots text-muted me-1"></i> Observaciones del Pedido
                    </label>
                    <textarea id="observacionesPedidoModal" class="form-control" rows="3" 
                              placeholder="Escribe aquí cualquier instrucción adicional...">${observacionesGuardadas}</textarea>                    
                </div>
                <div class="modal-footer bg-light flex-column flex-sm-row gap-2">
                    <div class="d-flex flex-column flex-sm-row gap-2 w-100">
                        <button type="button" class="btn btn-outline-danger flex-fill" onclick="vaciarCarrito()">
                            <i class="fas fa-trash me-1"></i>
                            <span class="d-none d-sm-inline">Vaciar Carrito</span>
                            <span class="d-sm-none">Vaciar</span>
                        </button>
                        <button type="button" class="btn btn-secondary flex-fill" data-bs-dismiss="modal">
                            <i class="fas fa-times me-1"></i>
                            Cerrar
                        </button>
                        <button type="button" class="btn btn-success flex-fill" onclick="procesarPedidoDesdeModal()">
                            <i class="fas fa-paper-plane me-1"></i>
                            <span class="d-none d-sm-inline">Enviar Pedido</span>
                            <span class="d-sm-none">Enviar</span>
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;

  document.body.appendChild(modal);
  const bootstrapModal = new bootstrap.Modal(modal);
  bootstrapModal.show();

  // Configurar eventos para el textarea de observaciones
  const textareaObservaciones = modal.querySelector("#observacionesPedidoModal");
  if (textareaObservaciones) {
    // Guardar en tiempo real mientras el usuario escribe
    textareaObservaciones.addEventListener("input", function() {
      observacionesGlobales = this.value;
      sessionStorage.setItem("observacionesPedido", this.value);
      console.log("🔄 Observaciones guardadas:", this.value);
    });
    
    // También guardar al perder el foco
    textareaObservaciones.addEventListener("blur", function() {
      observacionesGlobales = this.value;
      sessionStorage.setItem("observacionesPedido", this.value);
      console.log("💾 Observaciones guardadas al perder foco:", this.value);
    });
  }

  // Limpiar modal cuando se cierre y guardar observaciones finales
  modal.addEventListener("hidden.bs.modal", () => {
    const obsTextarea = modal.querySelector("#observacionesPedidoModal");
    if (obsTextarea) {
      observacionesGlobales = obsTextarea.value;
      sessionStorage.setItem("observacionesPedido", obsTextarea.value);
      console.log("🚪 Modal cerrado, observaciones guardadas:", obsTextarea.value);
    }
    document.body.removeChild(modal);
  });
}

// Nueva función para procesar pedido desde el modal
function procesarPedidoDesdeModal() {
  console.log("🚀 Iniciando procesamiento de pedido desde modal");

  // Evitar doble toque en móvil
  const btnEnviar = document.querySelector(".modal.show .btn-success");
  if (btnEnviar) {
    if (btnEnviar.disabled) return;
    btnEnviar.disabled = true;
    btnEnviar.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Enviando...';
  }
  
  // Capturar observaciones del modal antes de cerrar
  const obsTextarea = document.querySelector("#observacionesPedidoModal");
  if (obsTextarea) {
    observacionesGlobales = obsTextarea.value;
    sessionStorage.setItem("observacionesPedido", obsTextarea.value);
    console.log("📝 Observaciones capturadas del modal:", obsTextarea.value);
  }
  
  // Cerrar modal
  const modal = document.querySelector(".modal.show");
  if (modal) {
    const closeButton = modal.querySelector(".btn-close");
    if (closeButton) {
      closeButton.click();
    }
  }
  
  // Procesar pedido después de un pequeño delay para asegurar que el modal se cierre
  setTimeout(() => {
    procesarPedido();
  }, 300);
}

// Función para eliminar producto del carrito
function eliminarDelCarrito(index) {
  if (index >= 0 && index < carrito.length) {
    const producto = carrito[index];
    carrito.splice(index, 1);
    actualizarContadores();
    mostrarAlerta(`${producto.nombre} eliminado del carrito`, "info");
    guardarCarrito();

    // Si el modal está abierto, actualizarlo
    const modal = document.querySelector(".modal.show");
    if (modal) {
      modal.querySelector(".btn-close").click();
      if (carrito.length > 0) {
        setTimeout(() => mostrarModalCarrito(), 300);
      }
    }
  }
}

// Función para vaciar carrito
function vaciarCarrito() {
  if (carrito.length === 0) {
    mostrarAlerta("El carrito ya está vacío", "info");
    return;
  }

  if (confirm("¿Estás seguro de que quieres vaciar el carrito?")) {
    carrito = [];
    limpiarObservaciones();
    actualizarContadores();
    mostrarAlerta("Carrito vaciado", "success");
    guardarCarrito();

    // Cerrar modal si está abierto
    const modal = document.querySelector(".modal.show");
    if (modal) {
      modal.querySelector(".btn-close").click();
    }
  }
}

// Función mejorada para procesar pedido
let pedidoEnProceso = false;

function generarPedidoToken() {
  if (window.crypto && window.crypto.getRandomValues) {
    const bytes = new Uint8Array(16);
    window.crypto.getRandomValues(bytes);
    return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
  }
  return (
    Date.now().toString(16) +
    Math.random().toString(16).slice(2) +
    Math.random().toString(16).slice(2)
  );
}

function procesarPedido() {
  console.log("🔄 Procesando pedido...");

  if (pedidoEnProceso) {
    console.warn("Pedido ya en proceso, se ignora envío duplicado");
    return;
  }
  
  if (carrito.length === 0) {
    mostrarAlerta("El carrito está vacío", "warning");
    return;
  }

  pedidoEnProceso = true;

  // Crear formulario dinámico con los productos del carrito
  const form = document.createElement("form");
  form.method = "POST";
  form.action = "procesar_telefono_cliente.php";

  // Token único por intento (viaja hasta procesar_pedido.php)
  const tokenInput = document.createElement("input");
  tokenInput.type = "hidden";
  tokenInput.name = "pedido_token";
  tokenInput.value = generarPedidoToken();
  form.appendChild(tokenInput);
  try {
    sessionStorage.setItem("pedidoTokenActivo", tokenInput.value);
  } catch (e) {}

  // Agregar productos al formulario
  carrito.forEach((item, index) => {
    // ID del producto
    const idInput = document.createElement("input");
    idInput.type = "hidden";
    idInput.name = `productos[${index}][id]`;
    idInput.value = item.id;
    form.appendChild(idInput);

    // Nombre del producto
    const nombreInput = document.createElement("input");
    nombreInput.type = "hidden";
    nombreInput.name = `productos[${index}][nombre]`;
    nombreInput.value = item.nombre;
    form.appendChild(nombreInput);

    // Tipo
    const tipoInput = document.createElement("input");
    tipoInput.type = "hidden";
    tipoInput.name = `productos[${index}][tipo]`;
    tipoInput.value = item.tipo;
    form.appendChild(tipoInput);

    // Cantidad
    const cantidadInput = document.createElement("input");
    cantidadInput.type = "hidden";
    cantidadInput.name = `productos[${index}][cantidad]`;
    cantidadInput.value = item.cantidad;
    form.appendChild(cantidadInput);

    // Precio unitario
    const precioInput = document.createElement("input");
    precioInput.type = "hidden";
    precioInput.name = `productos[${index}][precio_unitario]`;
    precioInput.value = item.precioUnitario;
    form.appendChild(precioInput);

    // Subtotal
    const subtotalInput = document.createElement("input");
    subtotalInput.type = "hidden";
    subtotalInput.name = `productos[${index}][subtotal]`;
    subtotalInput.value = item.precioTotal;
    form.appendChild(subtotalInput);
  });

  // Total general
  const totalInput = document.createElement("input");
  totalInput.type = "hidden";
  totalInput.name = "total_general";
  totalInput.value = totalGeneral;
  form.appendChild(totalInput);

  // Número del usuario
  const valorNumCliente = document.getElementById("numCliente").dataset.numcliente;
  const numCliente = document.createElement("input");
  numCliente.type = "hidden";
  numCliente.name = "numCliente";
  numCliente.value = valorNumCliente;
  form.appendChild(numCliente);

  // CAPTURA MEJORADA DE OBSERVACIONES
  let observaciones = "";
  
  // Prioridad 1: Variable global (más reciente)
  if (observacionesGlobales) {
    observaciones = observacionesGlobales;
    console.log("✅ Observaciones desde variable global:", observaciones);
  }
  // Prioridad 2: SessionStorage
  else if (sessionStorage.getItem("observacionesPedido")) {
    observaciones = sessionStorage.getItem("observacionesPedido");
    console.log("✅ Observaciones desde sessionStorage:", observaciones);
  }
  // Prioridad 3: Textarea del modal (si aún existe)
  else {
    const obsTextarea = document.querySelector("#observacionesPedidoModal") || 
                       document.querySelector("#observacionesPedido");
    if (obsTextarea) {
      observaciones = obsTextarea.value;
      console.log("✅ Observaciones desde textarea:", observaciones);
    }
  }

  console.log("📋 Observaciones finales que se enviarán:", observaciones);

  // Insertar observaciones en el formulario
  const inputObs = document.createElement("input");
  inputObs.type = "hidden";
  inputObs.name = "observaciones";
  inputObs.value = observaciones;
  form.appendChild(inputObs);

  // Agregar formulario al DOM y enviarlo
  document.body.appendChild(form);
  
  // Log final antes de enviar
  console.log("📤 Formulario creado, enviando pedido...");
  console.log("📝 Observaciones en el formulario:", observaciones);
  
  form.submit();
}

// Función para actualizar precio mostrado según tipo seleccionado
function actualizarPrecioMostrado(event) {
  const select = event.target;
  const productCard = select.closest(".product-card");
  const precioUnidad = parseFloat(select.dataset.precioUnidad);
  const precioPaca = parseFloat(select.dataset.precioPaca);

  // Resaltar el precio correspondiente
  const precioUnidadElement = productCard.querySelector(".price-unidad");
  const precioPacaElement = productCard.querySelector(".price-paca");

  if (!precioUnidadElement || !precioPacaElement) return;

  // Resetear estilos
  precioUnidadElement.style.fontWeight = "normal";
  precioUnidadElement.style.color = "";
  precioPacaElement.style.fontWeight = "normal";
  precioPacaElement.style.color = "";

  // Resaltar el precio seleccionado
  if (select.value === "unidad") {
    precioUnidadElement.style.fontWeight = "bold";
    precioUnidadElement.style.color = "#198754";
  } else if (select.value === "paca") {
    precioPacaElement.style.fontWeight = "bold";
    precioPacaElement.style.color = "#198754";
  }
}

// Función para mostrar alertas
function mostrarAlerta(mensaje, tipo = "info") {
  // Crear alerta
  const alerta = document.createElement("div");
  alerta.className = `alert alert-${tipo} alert-dismissible fade show position-fixed`;
  alerta.style.cssText =
    "top: 20px; right: 20px; z-index: 9999; min-width: 300px;";

  alerta.innerHTML = `
        ${mensaje}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;

  document.body.appendChild(alerta);

  // Auto-remover después de 3 segundos
  setTimeout(() => {
    if (alerta.parentNode) {
      alerta.remove();
    }
  }, 3000);
}

// Función para manejar el envío del formulario original
document.addEventListener("DOMContentLoaded", function () {
  const orderForm = document.getElementById("orderForm");
  if (orderForm) {
    orderForm.addEventListener("submit", function (e) {
      e.preventDefault();

      if (carrito.length === 0) {
        mostrarAlerta(
          "Agrega productos al carrito antes de enviar el pedido",
          "warning"
        );
        return;
      }

      procesarPedido();
    });
  }
});

// Función para limpiar observaciones (opcional)
function limpiarObservaciones() {
  observacionesGlobales = "";
  sessionStorage.removeItem("observacionesPedido");
  const textareas = document.querySelectorAll("#observacionesPedido, #observacionesPedidoModal");
  textareas.forEach(textarea => {
    if (textarea) textarea.value = "";
  });
  console.log("🧹 Observaciones limpiadas");
}

// ===========================================
// PERSISTENCIA EN LOCALSTORAGE
// ===========================================

function guardarCarrito() {
  try {
    localStorage.setItem("carritoMultilicores", JSON.stringify(carrito));
  } catch (e) {
    console.error("Error al guardar carrito:", e);
  }
}

function cargarCarrito() {
  const carritoGuardado = localStorage.getItem("carritoMultilicores");
  if (carritoGuardado) {
    try {
      carrito = JSON.parse(carritoGuardado);
      actualizarContadores();
    } catch (e) {
      console.error("Error al cargar carrito:", e);
      carrito = [];
    }
  }
}

// Cargar carrito al iniciar la página
document.addEventListener("DOMContentLoaded", function () {
  cargarCarrito();
});