# Fase 3 — Matriz de pruebas de regresión y seguridad

> **DESCARTADO (2026-10-21).** Correspondía al plan de lotes del PHP, que no se ejecutará. Ver `docs/FASE_3_CIERRE.md`. Los casos funcionales (sección 2) sirven como referencia para las pruebas de equivalencia de la Fase 11.

| Campo | Valor |
|---|---|
| Fecha | 2026-10-21 |
| Relacionado | `docs/FASE_3_AUDITORIA_SEGURIDAD.md`, `docs/FASE_3_PLAN_IMPLEMENTACION.md` |
| Ambiente de ejecución | **TEST** (`despacha.multilicoreschapinero.com`) con `WHATSAPP_MODE=allowlist` |
| Estado | Diseñada; no ejecutada |

## 1. Reglas de ejecución

1. **Ninguna prueba se ejecuta en TEST sin que el lote 1 esté activo:** la URL de envío del ambiente y la lista de números permitidos. Hasta entonces, TEST envía WhatsApp reales a clientes reales (SEC-06).
2. **No se hacen pruebas destructivas en PROD.** En PROD solo se permiten las pruebas marcadas como **Humo PROD**, que son de solo lectura o se hacen con un pedido controlado al número del negocio, con autorización.
3. Los datos de prueba se crean con un prefijo identificable (por ejemplo, el cliente `ZZ-PRUEBA-...`) y se listan al final de cada ejecución. **No se borran sin autorización.**
4. **Las pruebas de seguridad (inyección SQL, XSS, manipulación de precios) se hacen solo en TEST**, con cargas inofensivas (sin `DROP`, `DELETE` ni `UPDATE`). Ejemplos: `' OR '1'='1`, `<img src=x onerror=alert(1)>`.
5. Los secretos no se escriben en los resultados ni en capturas de pantalla.
6. Cada ejecución registra la fecha, el lote, el commit, quién la hizo, el resultado (OK, Falla o Bloqueada) y la evidencia (sin datos personales).

**Leyenda de la columna "Lotes":** la línea base se corre antes del L1 y la regresión se repite después de cada lote. Por ejemplo, "L2+" significa que la prueba es obligatoria desde el L2. Las pruebas que dicen "esperado después del lote" fallan hoy; esa es la evidencia del hallazgo.

---

## 2. Pruebas funcionales (regresión)

| ID | Área | Caso | Pasos | Resultado esperado | Lotes | Humo PROD |
|---|---|---|---|---|---|---|
| T-01 | Infraestructura | La aplicación se conecta a la base con el `.env` | Abrir el login y el catálogo | Cargan sin error; no aparecen datos de la conexión | Todos | Sí |
| T-02 | WhatsApp en TEST | Envío a un número que no está en la lista permitida | Crear en TEST un pedido para un cliente real | No hay llamada a Meta; el log marca "bloqueado por lista" | L1+ | No |
| T-03 | WhatsApp en TEST | Envío a un número permitido | Crear un pedido para el número de prueba D-5 | Llega la plantilla `pedido_recepcionado` | L1+ | No |
| T-04 | Catálogo desde WhatsApp | Abrir el enlace de bienvenida | Escribir al número de prueba y abrir el enlace | Se abre `categorias.php?idCli=<tel>` y el catálogo carga con el teléfono | Todos | Sí (con el número del negocio) |
| T-05 | Identificación del cliente | Teléfono con un solo cliente | Checkout con ese teléfono | Se muestran el nombre y la dirección, y se puede confirmar | Todos | No |
| T-06 | Identificación del cliente | Teléfono con varios clientes o bares | Checkout | Se puede elegir el cliente correcto; el pedido queda asociado a él | Todos | No |
| T-07 | Identificación del cliente | Teléfono nuevo | Checkout con un teléfono que no existe | Se puede registrar el cliente y el bar; el pedido se crea | Todos | No |
| T-08 | Login | Entrar y salir | Login correcto y luego logout | Entra al dashboard; al salir, las vistas redirigen al login | Todos | Sí (solo lectura) |
| T-09 | Login | Clave incorrecta | Login con una clave errónea | Mensaje genérico, sin detalle técnico | Todos | No |
| T-10 | Usuarios | Crear un usuario desde el admin | Con sesión, en `user-new` | Se crea el usuario | Todos | No |
| T-11 | Usuarios | Editar un usuario | Con sesión, en `user-update` | Los cambios se guardan; desde el L7, la clave no cambia si el campo queda vacío | Todos | No |
| T-12 | Productos | Ver el catálogo admin y descargar la plantilla | `Subir_excel_producto.php` | Lista paginada; se descarga el CSV | Todos | Sí (ver la lista) |
| T-13 | Promociones | Listar, crear, editar, activar y desactivar | `promo.php` | Cada operación se refleja en el catálogo | Todos | Sí (ver la lista) |
| T-14 | Caja | Ver cartera | `cartera.php` | La lista y los totales coinciden con la línea base | Todos | Sí (solo lectura) |
| T-15 | Pedidos | Detalle de un pedido | Botón de ver en `solicitudes.php` | Se muestran los productos y las observaciones | Todos | Sí (solo lectura) |
| T-20 | Productos | Actualizar precios de un producto | Formulario de precios | El nuevo precio se ve en el catálogo y se usa en los pedidos nuevos | Todos | No |
| T-21 | Productos | Crear y editar un producto con imagen | Formularios correspondientes | Se ve en el catálogo con la imagen | Todos | No |
| T-22 | Productos | Importar CSV | Subir la plantilla con 2 o 3 productos de prueba | Se insertan y actualizan solo esos productos | Todos | No |
| T-23 | Promociones | Enviar promoción | `enviarPromo` en TEST | Solo se envía a los números permitidos; la respuesta no trae datos personales (L2+) | L1+ | No |
| T-24 | Promociones | Subir imagen de promoción | Crear una promoción con imagen | La imagen se ve en el catálogo y en la plantilla | Todos | No |
| T-25 | Pedido | Por unidad | Producto con `acti_Unidad=1`, tipo unidad, cantidad 3 | Subtotal = 3 × `precio_unidad_producto`; total correcto | Todos | No |
| T-26 | Pedido | Por paca | Tipo paca, cantidad 2 | Subtotal = 2 × `precio_paca_producto` | Todos | No |
| T-27 | Pedido | Con promoción | Promoción activa ligada a un producto | El precio es el de la promoción; el total coincide con lo que muestra el carrito | Todos | No |
| T-28 | Pedido | Mixto (unidad, paca y promoción) | Carrito con los tres tipos | Total = suma de los subtotales, igual que la línea base | Todos | No |
| T-29 | Pedido | Duplicado | Enviar dos veces el mismo pedido en menos de 2 minutos | Se crea uno solo; el segundo devuelve el mismo número | Todos | No |
| T-30 | Pedido | Registro completo | Crear un pedido | `pedidos` y `detalle_pedidos` con el formato actual; número `PED-AAAAMMDD-####`; WhatsApp `pedido_recepcionado` con el total correcto | Todos | Sí (pedido controlado, con autorización) |
| T-31 | Estados | Aceptar, rechazar, enviar y cerrar | Acciones de `solicitudes.php` y `pedidoController` | Cambia el estado y se envía la plantilla de cada estado al número permitido | Todos | No |
| T-32 | Notificaciones | Plantillas por estado | Revisar el log de envíos en TEST | Una plantilla por evento, sin duplicados | Todos | No |
| T-33 | Caja | Registrar ingreso | `cartera.php` → `controllerCaja` | El movimiento aparece en la lista con el valor correcto | Todos | No |
| T-34 | Caja | Registrar egreso | Igual que el anterior | Igual que el anterior | Todos | No |
| T-35 | Caja | Consultar y filtrar | Filtros de `cartera.php` | Los resultados coinciden con la línea base | Todos | Sí (solo lectura) |

## 3. Pruebas de WhatsApp

| ID | Caso | Pasos | Resultado esperado | Lotes |
|---|---|---|---|---|
| T-40 | Mensaje real entrante | Escribir "hola" desde el número de prueba | Llega una sola bienvenida con el enlace; firma válida en el log (L5) | Todos |
| T-41 | Plantilla interna con token válido | Crear un pedido (llamada interna) | Envío correcto | Todos |
| T-42 | Plantilla interna sin token o con token inválido | POST a `enviarWhatsapp.php` sin `Authorization` | 401, sin llamada a Meta | Todos |
| T-43 | Token de Meta rotado | Después de la rotación, enviar | Funciona con el nuevo; el anterior está revocado | L4 |
| T-44 | Token de Meta inválido | Poner un token erróneo en el `.env` de TEST | El error queda en el log del servidor; el usuario no ve detalle; el pedido se crea igual | L4+ |
| T-45 | Verificación del webhook | GET con `hub.verify_token` correcto o incorrecto | Correcto: devuelve el challenge. Incorrecto: 403 | Todos |
| T-46 | Evento repetido | Reenviar dos veces el mismo payload, con el mismo `id_wa` (simulado en TEST con firma válida) | Una sola respuesta al usuario; un solo registro | L3+ |
| T-47 | Texto con comillas | Escribir `Hola, ¿tienen O'Doul's?` | Se registra literalmente y el bot responde | L3+ |
| T-48 | Dos entregas simultáneas | Dos POST paralelos con el mismo `id_wa` | Un solo mensaje enviado | L5 |

## 4. Pruebas de contraseñas y sesiones

| ID | Caso | Resultado esperado | Lotes |
|---|---|---|---|
| T-50 | Usuario con clave en texto plano | Entra; después la clave queda en bcrypt | L7 |
| T-51 | Mismo usuario, segundo ingreso | Entra con `password_verify` | L7 |
| T-52 | Usuario con bcrypt (creado desde el admin) | Entra (hoy no puede) | L7 |
| T-53 | Usar el hash como clave | No entra | L7 |
| T-54 | Clave incorrecta, 6 veces | Bloqueo temporal con mensaje genérico | L7 |
| T-55 | Usuario inexistente | El mismo mensaje y un tiempo de respuesta parecido al de un usuario existente | L7 |
| T-56 | `user-update` sin escribir clave | La clave no cambia y el usuario sigue entrando | L7 |
| T-57 | `user-update` con clave nueva y su confirmación | Entra con la nueva y no con la anterior | L7 |

## 5. Pruebas de secretos y exposición

| ID | Caso | Resultado esperado | Lotes |
|---|---|---|---|
| T-60 | Buscar patrones de token y clave en el repositorio | Sin coincidencias de valores reales | L4 |
| T-61 | Token interno anterior, después de rotarlo | 401 | L4 |
| T-62 | Clave de MySQL anterior, después de rotarla | La conexión falla; la aplicación funciona con la nueva | L4 |
| T-63 | Comentario con credenciales del otro sistema | No existe; aviso enviado al dueño | L4 |
| T-64 | GET a los `.txt` de log, en TEST y como humo en PROD | 403 o 404 | L2+ |
| T-65 | GET a `config/.env*` y a código muerto bloqueado | 403 o 404 | L2+ |

## 6. Pruebas de acceso no autorizado

Todas se hacen **sin cookie de sesión**, en TEST.

| ID | Destino | Solicitud | Resultado esperado después del lote | Lotes |
|---|---|---|---|---|
| T-70 | `controllers/UserController.php` | POST con `id` de un usuario de prueba | 401 o redirección; el usuario no cambia | L2 |
| T-71 | `controllers/ProductoController.php` | POST `actualizar_precios`; GET `descargar_plantilla` | 401 o redirección; el precio no cambia; no hay CSV | L2 |
| T-72 | `controllers/promoController.php` | POST `action=mostrar`, `enviarPromo` y `delete` | 401; ningún envío y ningún cambio | L2 |
| T-73 | `api/pedido.php` | `clienteId` de otro cliente con un teléfono distinto | Se ignora el `clienteId`; se usa el cliente de ese teléfono | L6 |
| T-74 | `api/cliente.php` y `ajax/buscar_cliente.php` | Muchas consultas con teléfonos seguidos | Se limitan las solicitudes; la dirección se enmascara (L8/L9) | L8+ |
| T-75 | `controllers/buscar_bar.php` | Búsqueda de una letra | Solo id y nombre, con límite | L8 |
| T-76 | CSRF | Formulario desde otro origen hacia `solicitudes.php` y los controladores, con sesión activa | Rechazado | L8 |
| T-77 | Sesión | Comparar el id de sesión antes y después del login; revisar las banderas de la cookie | Cambia el id; cookie `HttpOnly`, `Secure` y `SameSite=Lax` | L7 |
| T-78 | Error en el login | Provocar un error de conexión en TEST (`.env` erróneo, temporal) | Mensaje genérico, sin usuario ni host | L7 |
| — | `controllers/controllerCaja.php` | POST de un ingreso | 401; ningún registro | L2 (ver T-14 y T-33) |
| — | `controllers/pedidoController.php` | `aceptar_pedido` y `obtener_productos` | 401; sin cambio y sin datos | L2 (ver T-15 y T-31) |

## 7. Pruebas de manipulación de precios

En TEST, enviando a `api/pedido.php` y a `procesar_pedido.php` solicitudes modificadas.

| ID | Manipulación | Resultado esperado en `observe` | Resultado esperado en `enforce` | Lotes |
|---|---|---|---|---|
| T-80 | `precio_unitario` = 1 en un producto normal | Se guarda como hoy y la diferencia queda registrada | Se guarda el precio de catálogo (o se rechaza, según D-7) | L6 |
| T-81 | `total` distinto de la suma de las líneas | Diferencia registrada | `ped_total` calculado en el servidor | L6 |
| T-82 | `tipo` = unidad en un producto con `acti_Unidad=0` | Diferencia registrada | Rechazado o tratado como paca, según D-7 | L6 |
| T-83 | `id` inexistente o inactivo | Diferencia registrada | Línea rechazada | L6 |
| T-84 | `nombre` alterado | Se registra | Se guarda el nombre de la base | L6 |

## 8. Pruebas de inyección SQL y XSS

Solo en TEST y con cargas inofensivas.

| ID | Caso | Resultado esperado | Lotes |
|---|---|---|---|
| T-85 | Mensaje de WhatsApp con `' OR '1'='1` y con `'); SELECT SLEEP(3); -- ` | Se guarda literalmente; sin demora; el bot responde | L3 |
| T-86 | `enviar.php` | 403 por web; si se mantiene, con consultas preparadas | L2/L3 |
| T-87 | `obtenerProductosLista` con una categoría que incluye comillas (prueba unitaria) | Cero resultados, sin error SQL | L3 |
| T-88 | POST al webhook sin firma o con firma inválida | `observe`: queda en el log. `enforce`: 401 sin respuesta al usuario | L5 |
| T-89 | Pedido con la observación `<img src=x onerror=alert(1)>` y abrir su detalle en `solicitudes.php` | Se muestra como texto y no se ejecuta | L8 |
| T-90 | Cliente con el nombre `"><svg onload=alert(1)>` y ver `clientes.php`; filtro de fecha con un script | Se muestra como texto; la fecha inválida se ignora | L8 |
| T-91 | Provocar un error en `api/pedido.php` y en `ProductoController` | Mensaje genérico, sin SQL ni rutas | L8 |
| T-92 | Subir `imagen.php.jpg` o un archivo con contenido no válido | Rechazado; y si quedara en `assets/`, no se ejecuta | L2 (no se ejecuta) / L8 (rechazado) |

## 9. Pruebas mínimas por lote

| Lote | Pruebas obligatorias antes de pasar a PROD | Humo en PROD después del despliegue |
|---|---|---|
| L1 | T-01 a T-03 y la línea base T-04 a T-35 | T-01, T-04, T-08 y un envío controlado al número del negocio |
| L2 | T-08 a T-15, T-20 a T-24, T-31 a T-35, T-64, T-65, T-70 a T-72, T-92 | T-01, T-08, T-12 a T-15, T-64 |
| L3 | T-40, T-46, T-47, T-85 a T-87 | T-40 (número del negocio) |
| L4 | T-01, T-30, T-40 a T-45, T-60 a T-63 | T-01, T-40, envío controlado, T-60 |
| L5 | T-40, T-45, T-46, T-48, T-88 | T-40; revisar el porcentaje de firmas válidas en el log |
| L6 | T-25 a T-30, T-73, T-80 a T-84 | Revisar el registro de diferencias de los pedidos reales (solo lectura) |
| L7 | T-08, T-09, T-11, T-50 a T-57, T-77, T-78 | T-08 con cada administrador |
| L8 | La regresión completa del admin (T-08 a T-35), T-74 a T-76, T-89 a T-92 | T-01, T-08, T-15 |
| L9 | T-04 a T-07 con enlace firmado, vencido y antiguo | T-04 |
