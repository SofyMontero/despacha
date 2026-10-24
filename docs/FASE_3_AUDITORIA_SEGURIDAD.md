# Fase 3 — Auditoría de seguridad del sistema PHP

> **Referencia (2026-10-21).** El PHP no se corrige; estos hallazgos alimentan los controles SEG-01 a SEG-13 para .NET de `docs/FASE_3_CIERRE.md`. Las secciones de prioridad y lotes ya no aplican.

| Campo | Valor |
|---|---|
| Proyecto | Despacha (sistema PHP actual de Multilicores) |
| Fecha | 2026-10-21 |
| Alcance | `sistema/` (PHP), con referencia a `catalogo-app/` solo donde define datos que recibe el PHP |
| Método | Revisión manual del código y del historial de git, solo lectura |
| Documentos relacionados | `docs/FASE_3_PLAN_IMPLEMENTACION.md`, `docs/FASE_3_PRUEBAS.md` |
| Estado | Análisis entregado; **no se implementó ningún cambio** |

## Convenciones

- **Ningún secreto aparece en este documento.** Donde haría falta citarlo se escribe `[REDACTADO]`, con el nombre del secreto, el archivo y la línea.
- Estado de cada hallazgo:
  - **Comprobado:** se ve directamente en el código.
  - **Hipótesis:** depende del servidor, de la base o de terceros y hay que verificarla (sección 6).
- Severidad:
  - **Crítico:** se puede explotar sin credenciales y afecta dinero, datos de clientes o el control del sistema.
  - **Alto:** gran impacto, pero necesita una condición previa o tiene un alcance acotado.
  - **Medio:** debilidad real con impacto limitado o que necesita que el usuario interactúe.
  - **Bajo:** endurecimiento, o algo latente sin forma de explotarlo hoy.
- Las rutas son relativas a la raíz del repositorio y las líneas corresponden al estado actual de la rama `main`.

---

## 1. Resumen

| Severidad | Cantidad |
|---|---|
| Crítico | 6 |
| Alto | 12 |
| Medio | 8 |
| Bajo | 5 |
| **Total comprobados** | **31** |
| Hipótesis por verificar | 10 |

**Los cinco riesgos más importantes:**

1. **Inyección SQL en el webhook de WhatsApp (SQL-01).** Cualquier persona que escriba al número de WhatsApp puede inyectar SQL en la base de producción; no hace falta ninguna credencial.
2. **Controladores administrativos sin sesión (AUT-01, AUT-02 y AUT-03).** Sin iniciar sesión se puede:
   - cambiar precios o importar un catálogo completo;
   - crear usuarios o cambiarles la clave;
   - mandar promociones masivas por WhatsApp a todos los clientes;
   - descargar el nombre y el teléfono de todos los clientes.
3. **Secretos dentro del repositorio (SEC-01 y SEC-03).** El token de Meta y las credenciales de la base de producción y de test están en el código y en el historial de git.
4. **Precios y totales controlados por el navegador (PRE-01) y XSS almacenado en el admin (XSS-01).** Un pedido puede registrarse con cualquier precio, y su contenido puede ejecutar código en la sesión del administrador.
5. **TEST envía WhatsApp reales (SEC-06).** Las acciones hechas en TEST envían mensajes a clientes reales a través del servidor de PROD, porque la base de test es una copia de producción con los teléfonos reales. Esto condiciona cómo se puede probar.

---

## 2. Matriz de riesgos

| ID | Hallazgo | Severidad | Evidencia | Impacto | Prioridad | Riesgo de corregirlo |
|---|---|---|---|---|---|---|
| SQL-01 | SQL armado concatenando datos en el webhook de WhatsApp | Crítico | `sistema/services/WhatsappSender.php:21`, `:91-92` | Lectura y modificación de toda la base por cualquiera que escriba al número | P1 | Bajo |
| AUT-01 | `UserController` sin sesión: crea usuarios y sobrescribe cualquiera | Crítico | `sistema/controllers/UserController.php:4-31` | Bloquear a los administradores; robo de cuentas si se corrige el login antes | P1 | Bajo |
| AUT-02 | `ProductoController` sin sesión: precios, importación CSV, productos e imágenes | Crítico | `sistema/controllers/ProductoController.php:130`, `:185`, `:234`, `:326`, `:434` | Cambio masivo de precios y catálogo; subida de archivos | P1 | Bajo |
| AUT-03 | `promoController` sin sesión: CRUD de promociones y envío masivo con lista de clientes | Crítico | `sistema/controllers/promoController.php:89-240`; `sistema/models/Promo.php:143-193` | Mensajes fraudulentos a todos los clientes, filtración de datos personales, costo con Meta y bloqueo del número | P1 | Bajo |
| SEC-01 | Token de Meta escrito en el código | Crítico | `WhatsappSender.php:33`, `enviar.php:28`, `enviarWhatsapp.php:420`; en git desde `be3c584` | Control total del envío de WhatsApp de la empresa | P1 | Medio (rotación coordinada) |
| SEC-03 | Credenciales de MySQL de PROD y TEST escritas como valores por defecto | Crítico | `sistema/config/database.env.php:111-112`, `:119-120`; historial desde `be3c584` | Acceso a la base si el servidor acepta conexiones (H-04) o junto con otra falla | P1 | Medio (cambio de clave) |
| SEC-02 | Token interno fijo, reutilizado como token de verificación del webhook | Alto | `enviarWhatsapp.php:29`, `recepcionWhatsapp.php:13`, `PedidoModel.php:428`, `solicitudModel.php:310`, `Promo.php:169` | Cualquiera con el token envía cualquier plantilla a cualquier número | P2 | Medio |
| SEC-04 | Credenciales de la base de otro sistema en un comentario | Alto | `sistema/services/enviarWhatsapp.php:464-467` | Exposición de un tercero | P2 | Bajo |
| SEC-05 | Logs con datos personales en carpetas públicas; uno está en git | Alto | `models/log_envios.txt` (versionado); `PedidoModel.php:454`, `solicitudModel.php:336`, `recepcionWhatsapp.php:9`, `WhatsappSender.php:6`, `clientes.php:67`, `enviarWhatsapp.php:487` | Filtración de teléfonos, mensajes y pedidos | P1 | Bajo |
| SEC-06 | URL de PROD escrita en el código para el envío interno de WhatsApp | Alto | `PedidoModel.php:407`, `solicitudModel.php:290`, `Promo.php:151` | TEST envía mensajes reales a clientes reales | P0 (antes de probar) | Bajo |
| AUT-04 | `controllerCaja` sin sesión | Alto | `sistema/controllers/controllerCaja.php:1-53` | Movimientos de caja falsos (ingresos, egresos, "validado") | P1 | Bajo |
| AUT-05 | `pedidoController` sin sesión | Alto | `sistema/controllers/pedidoController.php:10-49` | Aceptar o rechazar cualquier pedido; leer el detalle de cualquier pedido | P1 | Bajo |
| AUT-06 | El cliente se identifica solo por el teléfono | Alto | `api/cliente.php:7-49`, `controllers/ajax/buscar_cliente.php:2-37`, `api/pedido.php:101-102`, `PedidoModel.php:34-36`, `config/catalogo_router.php:39` | Consultar los datos de otro cliente, pedir a su nombre y mandar WhatsApp a números arbitrarios | P3 | Alto (cambia la experiencia del cliente) |
| PRE-01 | El navegador decide el precio unitario, el subtotal, el total, el nombre y el tipo | Alto | `api/pedido.php:99`; `procesar_pedido.php:165`; `PedidoModel.php:83`, `:116-124`, `:146-149` | Pedidos con precio arbitrario y total confirmado por WhatsApp; contador de ventas alterable | P2 | Alto (reglas de promociones) |
| PWD-01 | Login que compara la clave en texto plano | Alto | `sistema/controllers/index.php:25` | Claves legibles en la base; un hash se puede usar como clave; los usuarios creados en el admin no pueden entrar | P2 (después de AUT-01) | Medio |
| PWD-02 | El formulario de edición muestra la clave guardada y siempre vuelve a generar el hash | Alto | `sistema/views/user-update.php:117`; `sistema/models/User.php:28-29` | La clave queda en el HTML; editar un usuario sin tocar la clave le rompe el acceso | P2 | Medio |
| WA-01 | Webhook sin validar la firma `X-Hub-Signature-256` | Alto | `sistema/services/recepcionWhatsapp.php:20-71` | Eventos falsos, envíos a números arbitrarios y una vía hacia SQL-01 | P2 | Medio |
| XSS-01 | XSS almacenado en el admin con datos del cliente; XSS reflejado en filtros | Alto | `views/solicitudes.php:990-1005`, `:221`, `:226`; `views/clientes.php:255-257` | Código ejecutado en la sesión del administrador | P2 | Bajo |
| AUT-07 | Alta pública de clientes y bares sin límites | Medio | `api/cliente.php:52-108`; `controllers/ajax/guardar_cliente.php:7-60`; `controllers/buscar_bar.php:11-34` | Datos basura, vía para XSS-01, enumeración de bares | P3 | Medio |
| AUT-08 | Formularios del admin sin protección CSRF | Medio | `views/solicitudes.php:42`, `:72`, `:102`, `:133`; `views/clientes.php:22`, `:70`; `views/cartera.php:8` | Acciones forzadas si el administrador visita un sitio malicioso | P3 | Medio |
| AUT-09 | Sesiones sin endurecer | Medio | `controllers/index.php:2`, `:26`; `controllers/logout.php:2-3` | Fijación de sesión; cookies sin las banderas explícitas (H-05) | P2 | Bajo |
| AUT-10 | Login sin límite de intentos y con errores internos expuestos | Medio | `controllers/index.php:19-22`, `:25-31` | Ataque de fuerza bruta; revela datos de la conexión a la base | P2 | Bajo |
| SQL-02 | Concatenación SQL en `enviar.php` (código sin uso aparente) | Medio | `sistema/services/enviar.php:14`, `:73-76` | Latente; se puede ejecutar si alguien lo incluye o se lo llama | P1 (junto con SQL-01) | Bajo |
| WA-02 | Duplicados y reintentos del webhook mal controlados | Medio | `WhatsappSender.php:21-33`, `:91-96` | Respuestas repetidas cuando Meta reintenta | P2 | Bajo |
| GEN-01 | `display_errors` activo y mensajes internos devueltos al cliente | Medio | `ProductoController.php:2-4`; `procesar_pedido.php:6-8`; `procesar_telefono_cliente.php:20-22`; `api/pedido.php:216`; `api/cliente.php:114`; `pedidoController.php:44-47`; `promoController.php:239`; `ajax/buscar_cliente.php:43-46` | Revela rutas, SQL y configuración | P3 | Bajo |
| GEN-02 | Subida de imágenes validada solo por la extensión | Medio | `ProductoController.php:271-299`, `:377-407`; `promoController.php:47-87` | Archivos maliciosos en carpetas públicas (H-06) | P3 | Bajo |
| SQL-03 | Concatenación en `obtenerProductosLista` (latente) | Bajo | `sistema/models/ProductoModel.php:107-131` | Hoy no se puede explotar: solo se llama con `0` | P3 | Bajo |
| PWD-03 | Sin cambio ni recuperación de clave segura, ni política de claves | Bajo | `views/user-update.php`, `controllers/UserController.php` | Mala gestión de claves | P4 | Bajo |
| WA-03 | `enviarWhatsapp.php`: no valida la entrada, tiene un origen CORS viejo y no reintenta | Bajo | `enviarWhatsapp.php:5-7`, `:44-56`, `:460-488` | Errores silenciosos; funciona como relevo abierto para quien tenga el token | P3 | Bajo |
| GEN-03 | CORS `*` en la API pública | Bajo | `sistema/api/bootstrap.php:7-9` | Bajo, porque no se usan cookies con credenciales | P4 | Medio (catálogo React) |
| GEN-04 | Código muerto accesible por web | Bajo | `Routes/web.php`, `controllers/AuthController.php`, `services/enviar.php`, `views/reservation-*.php`, `views/payment.php` | Más superficie de ataque | P2 (bloqueo) | Bajo |

La prioridad sigue el orden del plan: **P0** antes de cualquier prueba, **P1** en la contención inmediata, **P2** en las siguientes semanas, **P3** después, y **P4** es endurecimiento.

---

## 3. Hallazgos detallados

### A. Credenciales y secretos

#### SEC-01 — Token de acceso de Meta escrito en el código (Crítico, Comprobado)

- **Dónde está:**
  - `sistema/services/WhatsappSender.php:33`, en `WhatsappSender::enviar`;
  - `sistema/services/enviar.php:28`, en `enviar()`;
  - `sistema/services/enviarWhatsapp.php:420`, en el script principal.
- **Evidencia:** la asignación `$token = '[REDACTADO]'` aparece igual en los tres archivos, junto al Phone Number ID (`$telefonoID`). En el historial, la cadena `graph.facebook.com` aparece desde el commit `be3c584`.
- **Escenario:** quien tenga acceso al repositorio, a una copia o a un respaldo puede enviar mensajes en nombre de la empresa, gastar su cupo y provocar que Meta bloquee el número. Según los permisos del token, también podría administrar las plantillas.
- **A quién afecta:** a todos los clientes que reciben WhatsApp y a la reputación del número.
- **Corrección:**
  1. Llevar el token a una variable de entorno (`WHATSAPP_TOKEN`) en `config/.env.<ambiente>`, fuera de git.
  2. Emitir un **token permanente de System User** en Meta Business con los permisos mínimos.
  3. Desplegar el cambio, verificar los envíos y **revocar el token anterior**.
- **Riesgo de regresión:** medio. Si el `.env` no llega al servidor, se dejan de enviar WhatsApp. Se mitiga desplegando primero con el token actual y rotando después.
- **Pruebas:** T-40 a T-44, T-60.

#### SEC-02 — Token interno fijo que también se usa como token de verificación del webhook (Alto, Comprobado)

- **Dónde está:**
  - Quien lo valida: `services/enviarWhatsapp.php:29` y `:33`.
  - Webhook: `services/recepcionWhatsapp.php:13`.
  - Quienes lo envían: `models/PedidoModel.php:428`, `models/solicitudModel.php:310` y `models/Promo.php:169`.
- **Evidencia:** el mismo literal `[REDACTADO]` aparece en las seis ubicaciones.
- **Escenario:** con el token, cualquiera puede llamar a `enviarWhatsapp.php` y enviar cualquier plantilla (`promocion`, `pago_credito`, etc.) con cualquier texto e imagen a cualquier número.
- **Corrección:** separar dos secretos, `INTERNAL_API_TOKEN` y `WHATSAPP_VERIFY_TOKEN`, generados al azar y leídos del entorno. Comparar con `hash_equals`. A mediano plazo, que los modelos llamen a una función interna en vez de hacer una petición HTTP a sí mismos.
- **Riesgo de regresión:** medio. Todos los consumidores están en este repositorio, así que se cambian en un solo despliegue. Cambiar el token de verificación obliga a volver a verificar el webhook en Meta.
- **Pruebas:** T-41, T-45, T-61.

#### SEC-03 — Credenciales de MySQL de PROD y TEST como valores por defecto (Crítico, Comprobado)

- **Dónde está:** `sistema/config/database.env.php:111-112` (usuario y clave de TEST) y `:119-120` (usuario y clave de PROD), en `multilicores_db_config()`.
- **Evidencia:**
  - Las llamadas `multilicores_env('DB_PASS', '[REDACTADO]')` usan las claves reales como valor por defecto.
  - `DB_PASS` aparece en los commits `be3c584`, `92be7f2`, `a7a3051`, `0ee81f3` y `8a8e89c`.
  - La clave de TEST se agregó en esta misma migración.
- **Escenario:** si el MySQL acepta conexiones remotas (H-04), quien tenga el repositorio entra directamente a la base. Aunque no las acepte, la clave sirve en cualquier otra vía de ejecución dentro del servidor.
- **Corrección:**
  1. Quitar los valores por defecto y fallar con un error genérico si falta la configuración.
  2. Crear `config/.env.prod` y `config/.env.dev` en el servidor.
  3. Rotar las claves de las dos bases, con el procedimiento del plan, lote 4.
- **Riesgo de regresión:** medio. Si falta el `.env`, la aplicación deja de funcionar. Hay que desplegar primero el `.env` y después el código sin valores por defecto.
- **Pruebas:** T-01, T-62.

#### SEC-04 — Credenciales de la base de otro sistema en un comentario (Alto, Comprobado)

- **Dónde está:** `sistema/services/enviarWhatsapp.php:464-467`, en código comentado.
- **Evidencia:** servidor, usuario, clave y nombre de una base de **otro sistema** (no es Multilicores), con valores reales `[REDACTADO]`.
- **Escenario:** expone a un tercero. Si esa clave se reutiliza en otro lado, el daño se extiende.
- **Corrección:** borrar el comentario y **avisar al dueño de ese sistema** para que cambie la clave. No depende de nada de este proyecto.
- **Riesgo de regresión:** ninguno (es un comentario).
- **Pruebas:** T-63.

#### SEC-05 — Logs con datos personales en carpetas públicas (Alto; Comprobado en código, el acceso por web es la hipótesis H-03)

- **Dónde están:**

| Archivo de log | Lo escribe | Contenido |
|---|---|---|
| `sistema/models/log_envios.txt` | `PedidoModel.php:454`, `solicitudModel.php:336` | Teléfono, plantilla, texto y respuesta de Meta. **Está versionado en git** (16 líneas). |
| `sistema/services/log_webhook.txt` | `recepcionWhatsapp.php:9` y `:25` | El cuerpo completo de cada mensaje entrante |
| `sistema/services/whatsapp_log.txt` | `WhatsappSender.php:6` y `:15` | Teléfonos e identificadores de mensaje |
| `sistema/views/log.txt` | `clientes.php:67` | Errores internos |
| `sistema/services/alertas_log.txt` | `enviarWhatsapp.php:487` | Datos del intento de envío |
| `sistema/services/log.txt` | `enviar.php:6` | Mensajes completos |

- **Evidencia:** solo existe `sistema/app/.htaccess`; ninguna regla impide descargar archivos `.txt`.
- **Escenario:** si se pueden abrir por URL (por ejemplo `/sistema/services/log_webhook.txt`), cualquiera lee conversaciones y teléfonos de los clientes.
- **Corrección:**
  - Bloquear `*.txt` y `*.log` en `sistema/` con una regla de Apache.
  - Mover los logs fuera de las carpetas públicas o usar `error_log`.
  - Sacar `log_envios.txt` de git.
  - Escribir los teléfonos enmascarados.
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-64 y T-65.

#### SEC-06 — Envío interno de WhatsApp con la URL de PROD escrita en el código (Alto, Comprobado)

- **Dónde está:** `models/PedidoModel.php:407` (`enviarConfirmacion`), `models/solicitudModel.php:290` (`enviarPromo`) y `models/Promo.php:151` (`enviarPromo`). Las imágenes de `enviarWhatsapp.php:133` y `:244` también apuntan a PROD.
- **Evidencia:** las tres funciones llaman a `https://multilicoreschapinero.com/sistema/services/enviarWhatsapp.php` sin importar el ambiente.
- **Escenario:** en TEST (`despacha.multilicoreschapinero.com`), cada pedido, aceptación, rechazo, envío, cierre o promoción manda un WhatsApp **real** a través de PROD. Como la base de TEST es una copia de producción, **los destinatarios son clientes reales**.
- **Corrección:**
  - La URL debe salir de la configuración del ambiente, o mejor, llamar a una función local.
  - En TEST, un modo de envío `WHATSAPP_MODE=allowlist` que solo envía a números de prueba autorizados y registra el resto sin enviarlo.
- **Riesgo de regresión:** bajo, si en PROD el valor por defecto conserva el comportamiento actual.
- **Pruebas:** T-02 y T-03. **Es requisito para todo el resto de las pruebas.**

### B. Autenticación y autorización

**Comprobado como correcto:** todas las vistas que incluyen `views/header.php` validan `$_SESSION["usuario"]` antes de mostrar nada (`header.php:2-6`). Los cinco controladores administrativos de esta sección **solo se llaman desde páginas del admin**:

| Controlador | Página que lo llama |
|---|---|
| `promoController` | `promo.php` |
| `ProductoController` | `Subir_excel_producto.php` |
| `controllerCaja` | `cartera.php` |
| `pedidoController` | `solicitudes.php` |
| `UserController` | `user-new.php`, `user-update.php` |

Por eso se pueden proteger sin afectar al catálogo.

#### AUT-01 — `UserController` sin sesión (Crítico, Comprobado)

- **Dónde está:** `sistema/controllers/UserController.php:4-31`, que llama a `User::registerUser` y a `User::updateUser` (`models/User.php:16-47`).
- **Evidencia:** no hay ni `session_start` ni ninguna validación. Con un POST que lleve `id` se actualizan el usuario, la clave, el email, el teléfono, la dirección y el nombre de **cualquier** usuario.
- **Escenario:**
  - **Hoy:** un atacante cambia la clave de todos los administradores y los deja fuera. El login compara texto plano contra el nuevo hash, así que nadie puede volver a entrar. También puede crear usuarios.
  - **Si se corrige el login (PWD-01) antes de cerrar este controlador**, el mismo POST pasa a ser **robo total de cuentas**.
- **Corrección:** exigir sesión de administrador, protección CSRF y validar que las dos claves coincidan. No tocar la clave si el campo viene vacío.
- **Riesgo de regresión:** bajo.
- **Orden obligatorio:** se corrige en el lote 2, **antes** que PWD-01.
- **Pruebas:** T-10, T-11 y T-70.

#### AUT-02 — `ProductoController` sin sesión (Crítico, Comprobado)

- **Dónde está:** `sistema/controllers/ProductoController.php`.

| Acción | Líneas | Qué hace |
|---|---|---|
| `descargar_plantilla` (GET) | 130 | Exporta el catálogo |
| `actualizar_precios` | 185 | Cambia los precios de un producto |
| `crear_producto` | 234 | Crea un producto, con imagen |
| `editar_producto` | 326 | Edita un producto, con imagen |
| Importación CSV | 434 | Inserta productos y actualiza precios en masa |

- **Evidencia:** no hay validación de sesión. Además tiene `display_errors` activo (`:2-4`).
- **Escenario:** con un solo POST se cambian los precios de todo el catálogo. Los clientes ven y piden con esos precios, y el admin los acepta.
- **Corrección:** exigir sesión al principio del archivo y responder 401 (o redirigir al login, según la acción).
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-12, T-20 a T-22 y T-71.

#### AUT-03 — `promoController` sin sesión: envío masivo y filtración de clientes (Crítico, Comprobado)

- **Dónde está:** `sistema/controllers/promoController.php:89-240`.
  - Acciones: `insert`, `update`, `delete`, `cambiarEstado`, `mostrar`, `buscarProducto` y `enviarPromo`.
  - `Promo::enviarPromo` en `models/Promo.php:143-186` y `Promo::getClientes` en `:189-193`.
- **Evidencia:** `action=enviarPromo` recorre `SELECT * FROM clientes` y manda la plantilla `promocion` con el `texto` y la `imagen1` que envió quien hizo la solicitud. La respuesta JSON (`promoController.php:232-234`) **devuelve el nombre y el teléfono de cada cliente**.
- **Escenario:**
  - Un solo POST sin sesión manda un mensaje fraudulento (por ejemplo, un enlace de pago falso) a toda la base de clientes desde el número oficial.
  - Devuelve la lista completa de clientes.
  - Genera costos con Meta y puede hacer que bloqueen el número.
- **Corrección:** exigir sesión y CSRF; que la respuesta no incluya datos personales; limitar los envíos (por ejemplo, uno por promoción y por hora) y pedir confirmación.
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-13, T-23, T-24 y T-72.

#### AUT-04 — `controllerCaja` sin sesión (Alto, Comprobado)

- **Dónde está:** `sistema/controllers/controllerCaja.php:1-53`.
- **Evidencia:** inserta en `pedidos` el cliente, la factura, el ingreso, el egreso, el estado, el medio de pago y el indicador `validado`, todo tomado del JSON recibido y sin validación de sesión.
- **Escenario:** se registran ingresos o egresos falsos, incluso marcados como validados, que alteran la caja que consulta el negocio.
- **Corrección:** exigir sesión y CSRF, y validar tipos y valores (no negativos, estado y medio de pago dentro de los catálogos).
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-14, T-33 a T-35.

#### AUT-05 — `pedidoController` sin sesión (Alto, Comprobado)

- **Dónde está:** `sistema/controllers/pedidoController.php:10-49`, con las acciones `obtener_productos` (`:51`), `aceptar_pedido` (`:69`) y `rechazar_pedido` (`:96`).
- **Evidencia:** no valida la sesión. `aceptarPedido` y `rechazarPedido` actualizan el estado de **cualquier** `id_pedido` (`solicitudModel.php:122-145`). `obtener_productos` devuelve el detalle y las observaciones de cualquier pedido.
- **Escenario:** cambiar el estado de pedidos ajenos y recorrer los pedidos por id.
- **Nota (H-08):** `obtener_pedidos_pendientes` y `obtener_pedidos_aceptados` llaman a métodos que exigen dos argumentos sin pasárselos, así que probablemente fallan siempre.
- **Corrección:** exigir sesión.
- **Riesgo de regresión:** bajo; lo usa solo `solicitudes.php:974`.
- **Pruebas:** T-15 y T-31.

#### AUT-06 — El cliente se identifica solo por el número de teléfono (Alto, Comprobado)

- **Dónde está:**
  - `api/cliente.php:7-49` (GET): devuelve id, nombre, teléfono y dirección de los clientes con ese teléfono.
  - `controllers/ajax/buscar_cliente.php:2-37`: lo mismo para el catálogo PHP, y además expone errores de la base en `:43-46`.
  - `api/pedido.php:101-102` y `PedidoModel.php:34-36`: si llega `clienteId`, se usa ese cliente **sin comprobar que tenga ese teléfono**.
  - `PedidoModel.php:84` y `api/pedido.php:178`: se guarda y se notifica por WhatsApp al `numCliente` recibido.
  - `config/catalogo_router.php:39`: el enlace de WhatsApp lleva el teléfono en claro (`?idCli=`).
- **Escenario:**
  - Se consulta el nombre y la dirección de cualquier cliente probando números.
  - Se crean pedidos a nombre de otro cliente.
  - Se manda la plantilla `pedido_recepcionado` a **cualquier número**, usando un `clienteId` válido y un `numCliente` arbitrario.
- **Corrección:**
  - **Corto plazo:** comprobar que `clienteId` corresponda al teléfono recibido; no devolver la dirección completa (enmascararla); limitar las solicitudes por IP y por teléfono.
  - **Mediano plazo (lote 9):** enlace firmado que genera el webhook (HMAC con vencimiento) y periodo de convivencia con los enlaces actuales.
  - **Definitivo:** el diseño de la Fase 2 (D-09).
- **Riesgo de regresión:** alto. Toca el flujo de compra desde WhatsApp y la selección entre varios clientes con el mismo teléfono.
- **Pruebas:** T-04 a T-07, T-73 y T-74.

#### AUT-07 — Alta pública de clientes y bares sin límites (Medio, Comprobado)

- **Dónde está:** `api/cliente.php:52-108` (POST), `controllers/ajax/guardar_cliente.php:7-60` y `controllers/buscar_bar.php:11-34`. Este último devuelve la dirección, el teléfono y el email de los bares que coinciden con la búsqueda.
- **Escenario:** llenar la base de registros basura e insertar el contenido que se usa en XSS-01. Enumerar los bares.
- **Corrección:** validar longitudes y caracteres; limitar las solicitudes; devolver solo el id y el nombre del bar. **Estos endpoints deben seguir siendo públicos**, porque los usa el checkout.
- **Riesgo de regresión:** medio. Una validación demasiado estricta puede rechazar nombres reales.
- **Pruebas:** T-05 y T-75.

#### AUT-08 — Sin protección CSRF (Medio, Comprobado; la mitigación del navegador es la hipótesis H-09)

- **Dónde está:**
  - POST de `views/solicitudes.php:42`, `:72`, `:102` y `:133`;
  - `views/clientes.php:22` (alta) y `:70` (baja);
  - `views/cartera.php:8` (borrado);
  - los cinco controladores del grupo AUT.
- **Escenario:** un administrador con sesión abierta visita una página maliciosa que envía un formulario para borrar o aceptar pedidos o crear usuarios.
- **Corrección:** un token CSRF por sesión, validado en cada POST; cookie de sesión `SameSite=Lax`.
- **Riesgo de regresión:** medio. Hay que agregar el token a todos los formularios y llamadas AJAX del admin.
- **Pruebas:** T-76.

#### AUT-09 — Sesiones sin endurecer (Medio, Comprobado; las banderas efectivas son la hipótesis H-05)

- **Dónde está:**
  - `controllers/index.php:2` y `:26`: no se regenera el id de sesión al iniciar sesión.
  - No existe `session_set_cookie_params` en el código.
  - `controllers/logout.php:2-3`: destruye la sesión pero no borra la cookie.
  - No hay vencimiento por inactividad.
- **Corrección:** `session_regenerate_id(true)` al iniciar sesión; cookie `HttpOnly`, `Secure` y `SameSite=Lax`; vencimiento por inactividad; borrar la cookie al salir.
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-08 y T-77.

#### AUT-10 — Login sin límite de intentos y con errores expuestos (Medio, Comprobado)

- **Dónde está:** `controllers/index.php:19-22`. El mensaje de la excepción de la base (incluido el usuario de MySQL en los errores 1045) se agrega a la URL de error.
- **Corrección:**
  - Mensaje genérico al usuario y detalle solo en el log del servidor.
  - Límite de intentos por usuario y por IP (por ejemplo, 5 en 15 minutos, guardado en sesión o en archivo).
  - No se agregan tablas sin autorización.
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-09 y T-78.

### C. Precios y pedidos

#### PRE-01 — Precio, subtotal y total controlados por el navegador (Alto, Comprobado)

- **Dónde está:**
  - `api/pedido.php:99`: el `total` sale del JSON. `procesar_pedido.php:165`: el `total_general` sale del POST.
  - `PedidoModel::crearPedido`:
    - `:83` guarda `ped_total = $total` tal como llega;
    - `:116-124` toma `nombre`, `tipo` y `precio_unitario` del navegador y calcula `subtotal` con ese precio;
    - `:126-132` solo rechaza cantidades ≤ 0 o importes negativos, y **acepta precio 0 para cualquier producto**;
    - `:146-149` suma `cantidad` a `productos.cantidad_venta` sin verificar que el producto exista o esté activo.
  - Lo que envía el catálogo: `catalogo-app/src/pages/CheckoutPage.jsx:95-109` manda `total`, `precio_unitario` y `subtotal`.
- **Escenario:**
  - Un pedido con precio 1 o 0, o con un total distinto de la suma de las líneas, queda registrado.
  - El cliente recibe por WhatsApp "Valor total pedido $X" con el valor manipulado (`api/pedido.php:176-178`).
  - El admin ve el precio enviado, no el del catálogo.
  - Se puede inflar `cantidad_venta`, que ordena el catálogo (`ProductoModel.php:97`).
- **Mitigación actual:** la aprobación manual del administrador, que no compara contra el catálogo.
- **Reglas comerciales vigentes que hay que conservar** (comprobadas):
  1. Producto por **unidad**: `productos.precio_unidad_producto`, solo si `acti_Unidad` ≠ 0 (`api/productos.php:68-72`).
  2. Producto por **paca**: `productos.precio_paca_producto`.
  3. **Promoción:** la línea llega con el `id` **del producto relacionado** (o el id de la promoción si no lo encuentra) y con el precio de la promoción (`precio_unidad_producto` o `precio_paca_producto` de `promociones`), **sin ninguna marca de que es promoción** (`api/promociones.php:33-44`; `catalogo-app/src/context/CartContext.jsx:37-66` no envía `esPromo`).
  4. Se permiten pedidos con total 0 ("pedido solo con promos", comentario en `PedidoModel.php:83`). **Hay que confirmar con el negocio** si existen promociones de precio 0.
  5. No hay descuentos aparte de las promociones (no se encontraron).
- **Corrección propuesta** (sin cambiar las reglas):
  1. Para cada línea, el servidor arma el **conjunto de precios válidos** de ese `id` y `tipo`: el precio de catálogo, más los precios de las promociones **activas** relacionadas con ese id (por `codigo` o por `id_promocion`).
  2. Si el precio enviado está en ese conjunto, se usa **el valor del servidor**; si no, se usa el precio de catálogo y se registra la diferencia.
  3. `subtotal` y `ped_total` se calculan siempre en el servidor. `nombre_producto` se toma de la base. `tipo` solo puede ser `unidad` o `paca`.
  4. El producto debe existir y estar activo (o tener una promoción activa).
  5. **Primero en modo de observación:** se calcula y se registra la diferencia, pero se guarda lo de hoy. **Después en modo de aplicación.** Así se descubren reglas no documentadas antes de rechazar pedidos reales.
- **Riesgo de regresión:** alto. Las promociones identificadas por `codigo` son ambiguas (`solicitudModel.php:95` une `promociones.codigo = detalle_pedidos.id_producto`). El modo de observación lo mitiga.
- **Pruebas:** T-20, T-25 a T-30 y T-80 a T-84.

### D. Inyección SQL

**Comprobado como seguro:** casi todo el acceso con PDO usa consultas preparadas. `ProductoModel::obtenerProductos` interpola `LIMIT` y `OFFSET`, pero los convierte antes a entero (`ProductoModel.php:79-80`, `:98`).

#### SQL-01 — Webhook de WhatsApp (Crítico, Comprobado)

- **Dónde está:** `sistema/services/WhatsappSender.php`, en `enviar()`:
  - `:21`: `"... WHERE id_wa='" . $idWA . "'"`;
  - `:91-92`: `INSERT INTO registro ... VALUES ('$recibido', '$enviado', '$idWA', '$timestamp', '$telefonoCliente', ...)`.
  - Se ejecutan con `mysqli::query` en `:22` y `:94`.
- **De dónde vienen los datos:** `recepcionWhatsapp.php:39-42` los toma del cuerpo del webhook. `$recibido` es el **texto que escribió el cliente**.
- **Escenario:**
  - Cualquier persona que escriba al número de WhatsApp manda un texto con comillas y SQL, que se inserta en la base de **producción**. Validar la firma de Meta **no lo evita**, porque el mensaje es legítimo.
  - Sin firma (WA-01), también se puede atacar con un POST directo, usando `$idWA`.
  - Efecto colateral: un cliente que escribe un apóstrofo rompe la inserción y se pierde el registro que evita respuestas duplicadas.
- **Corrección:** consultas preparadas de mysqli (`prepare` y `bind_param`) en las dos consultas, sin cambiar el esquema.
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-46, T-47 y T-85.

#### SQL-02 — `enviar.php` (Medio, Comprobado; el uso es la hipótesis H-10)

- **Dónde está:** `sistema/services/enviar.php:14` y `:73-76`, con el mismo patrón que SQL-01.
- **Evidencia:** no se encontró ningún `include` ni `require` de este archivo. Solo define funciones, así que llamarlo por URL no ejecuta nada.
- **Corrección:** consultas preparadas, o bloquear su acceso y retirarlo cuando se autorice.
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-86.

#### SQL-03 — `ProductoModel::obtenerProductosLista` (Bajo, Comprobado)

- **Dónde está:** `sistema/models/ProductoModel.php:112`, con `"and id_cate_producto='$categoria'"`.
- **Evidencia:** solo se llama con el literal `0` (`ProductoController.php:133` y `views/Subir_excel_producto.php:7`).
- **Corrección:** usar un parámetro, igual que `contarProductos` (`:135-151`).
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-87.

### E. Contraseñas

**Estado actual** (comprobado):

| Aspecto | Situación |
|---|---|
| Cómo se guardan | Mezcla: los usuarios antiguos en **texto plano** (inferido de la comparación del login); los creados o editados desde el admin con **bcrypt** (`User.php:17`, `:29`) |
| Cómo se validan | `$clave === (string)$userData["password"]` (`controllers/index.php:25`) |
| Recuperación | No existe |
| Cambio de clave | Solo editando el usuario (`user-update.php` y `UserController.php`) |

#### PWD-01 — Login en texto plano (Alto, Comprobado)

- **Escenario:**
  - Las claves se leen tal cual si alguien obtiene la base (por ejemplo, con SQL-01).
  - Para un usuario con bcrypt, **la clave que sirve para entrar es el propio hash**: quien lo lea, entra.
  - Los usuarios creados desde el admin **no pueden entrar** (falla funcional).
- **Corrección gradual, sin cambios masivos:**
  1. Si el valor guardado es un hash (`password_get_info()['algo'] !== null`), se valida con `password_verify`.
  2. Si es texto plano, se compara con `hash_equals` y, **si coincide**, se guarda `password_hash` en ese momento (migración en el primer ingreso).
  3. Si `password_needs_rehash`, se actualiza.
  4. Pasado un plazo acordado, se identifican los usuarios que nunca entraron y se les asigna una clave temporal de forma controlada.
- **Cambio de base:** ninguno, si la columna `password` admite 60 caracteres o más. **Hay que verificarlo (H-07).** Si no los admite, se necesita autorización para ampliarla.
- **Requisito:** AUT-01 ya cerrado.
- **Riesgo de regresión:** medio. Si se equivoca la detección de hash, nadie entra. Se prueba con cuentas de TEST de cada tipo.
- **Pruebas:** T-09 y T-50 a T-55.

#### PWD-02 — La clave se muestra en el formulario y se vuelve a generar el hash sin control (Alto, Comprobado)

- **Dónde está:** `views/user-update.php:117` (`value="<?php echo $user["password"]; ?>"`) y `models/User.php:28-29`.
- **Escenario:**
  - La clave (o el hash) llega al HTML, al autocompletado y a la caché del navegador.
  - Guardar el formulario sin tocar la clave genera el hash **del valor que había**: si era texto plano, el usuario queda sin acceso con el login actual; si era un hash, queda guardado el hash de un hash.
- **Corrección:** el campo de clave siempre vacío; solo se cambia si se escribe una clave nueva y se confirma; se valida que las dos coincidan en el servidor.
- **Riesgo de regresión:** medio (afecta la edición de usuarios).
- **Pruebas:** T-56 y T-57.

#### PWD-03 — Sin flujo de cambio ni recuperación, y sin política de claves (Bajo)

- **Corrección:** longitud mínima de 10, cambio de la propia clave desde una sesión iniciada, y reinicio de clave por un administrador. La recuperación por email queda para la plataforma nueva.

### F. WhatsApp Cloud API

**Flujo actual** (comprobado):

- **Recepción:** `recepcionWhatsapp.php` responde a la verificación de Meta (`:13-18`).
  - Ante un mensaje de texto, toma el remitente (`from`) y responde con el enlace `catalogo_whatsapp_link($telefono)`, que apunta a `categorias.php?idCli=<teléfono>`.
  - La respuesta la envía `WhatsappSender::enviar(..., tipo 1)`, que registra el envío en `registro`.
- **Envío de plantillas:** `enviarWhatsapp.php`, protegido con el token interno, y llamado por HTTP desde `PedidoModel`, `solicitudModel` y `Promo`.

#### WA-01 — Sin validar `X-Hub-Signature-256` (Alto, Comprobado)

- **Dónde está:** `services/recepcionWhatsapp.php:20-71`, que procesa cualquier POST.
- **Escenario:**
  - Un atacante envía eventos falsos con cualquier `from`, y el bot responde a ese número (spam a nombre de la empresa).
  - Es la puerta de entrada sin WhatsApp a SQL-01 usando `$idWA`.
- **Corrección:**
  - Calcular `hash_hmac('sha256', $inputRaw, APP_SECRET)` y compararlo con `hash_equals`. Si no coincide, responder 401 y no procesar.
  - El `APP_SECRET` sale de la app de Meta y va al entorno.
  - Las verificaciones GET (`hub.challenge`) siguen funcionando con el token de verificación separado.
- **Riesgo de regresión:** medio. Si el secreto está mal, se dejan de responder **todos** los mensajes. Se mitiga con un periodo de **observación**: se valida y se registra el resultado, pero se procesa igual; después se aplica.
- **Pruebas:** T-40, T-45 y T-88.

#### WA-02 — Duplicados y reintentos (Medio, Comprobado)

- **Dónde está:** `WhatsappSender.php:21-33` revisa `registro.id_wa` **antes** de enviar, pero inserta el registro **después** (`:91-96`). Si la inserción falla (por ejemplo, por un apóstrofo), no queda nada registrado.
- **Escenario:** si Meta reintenta (por demora o error) o llegan dos entregas a la vez, el cliente recibe la bienvenida dos veces. No se sabe si `registro.id_wa` tiene un índice único (H-10).
- **Corrección:**
  - Insertar primero un registro "en proceso" con una consulta preparada.
  - Solo enviar si la inserción fue la primera, idealmente con un índice único sobre `id_wa`. **Crear ese índice requiere autorización de cambio de esquema**; sin él, se revisa y se inserta dentro de una transacción.
  - Responder a Meta 200 rápido.
- **Riesgo de regresión:** bajo.
- **Pruebas:** T-46 y T-48.

#### WA-03 — Robustez de `enviarWhatsapp.php` (Bajo, Comprobado)

- **Dónde está:**
  - `:5-7`: CORS que permite un origen de ngrok viejo.
  - `:44-56`: no valida el formato del teléfono ni la plantilla. Si la plantilla es desconocida, `$mensaje` queda indefinido.
  - `:460-488`: no reintenta.
  - `:487`: usa variables indefinidas.
- **Corrección:** validar contra una lista de plantillas y el formato E.164; reintentar con espera creciente solo ante errores 5xx o 429; registrar los fallos.
- **Riesgo de regresión:** bajo.

### G. Otros hallazgos

#### XSS-01 — XSS en el admin (Alto, Comprobado)

- **Almacenado:**
  - `views/solicitudes.php:990-1005`: arma HTML con `innerHTML` a partir de `producto.nombre_producto` y `ped_observacion`, que **los escribe el cliente** en el checkout (`api/pedido.php:100`, `PedidoModel.php:117`).
  - `views/clientes.php:255-257`: escribe `cli_nombre`, `cli_telefono` y `cli_direccion` en atributos `data-*` sin escapar. Esos datos salen del alta pública (AUT-07).
- **Reflejado:** `views/solicitudes.php:221` y `:226` muestran `$_GET['fecha_inicio']` y `$_GET['fecha_fin']` sin escapar.
- **Escenario:** un "cliente" manda un pedido con una observación maliciosa. Cuando el administrador abre el detalle, el script corre con su sesión y puede aceptar o rechazar pedidos, crear usuarios o lanzar promociones.
- **Corrección:** `textContent` o una función de escape en el JS; `htmlspecialchars(..., ENT_QUOTES)` en PHP; validar que las fechas tengan formato `Y-m-d`.
- **Riesgo de regresión:** bajo; solo cambia cómo se muestra.
- **Pruebas:** T-89 y T-90.

#### GEN-01 — Errores expuestos (Medio, Comprobado)

- `display_errors=1` en `ProductoController.php:2-4`, `procesar_pedido.php:6-8` y `procesar_telefono_cliente.php:20-22`.
- Mensajes de excepciones devueltos al cliente en `api/pedido.php:216`, `api/cliente.php:114`, `pedidoController.php:44-47`, `promoController.php:239` y `ajax/buscar_cliente.php:43-46`.
- **Corrección:** `display_errors=0`; mensaje genérico al usuario y detalle en `error_log`.
- **Pruebas:** T-91.

#### GEN-02 — Subida de imágenes (Medio, Comprobado; ejecutar el archivo es la hipótesis H-06)

- Se valida solo la extensión, sin `finfo` ni `getimagesize`: `ProductoController.php:276-287` y `:383-394`, `promoController.php:67-72`.
- Se guarda en `assets/img/licores/` y en `assets/img/licores/promos/`. El nombre se genera en el servidor, así que **no hay recorrido de rutas**.
- Hoy está agravado porque cualquiera puede subir archivos (AUT-02 y AUT-03).
- **Corrección:** validar el tipo real del archivo; volver a codificar la imagen si hay GD disponible; regla de Apache que impida ejecutar scripts en `assets/`.
- **Pruebas:** T-92.

#### GEN-03 — CORS `*` en `api/bootstrap.php:7-9` (Bajo, Comprobado)

- La API no usa cookies con credenciales, así que el impacto es bajo.
- Restringirlo al origen del catálogo puede romper la app Capacitor (origen `capacitor://` o `https://localhost`), así que se decide con pruebas.

#### GEN-04 — Código muerto accesible (Bajo, Comprobado)

- `Routes/web.php`, `controllers/AuthController.php`, `services/enviar.php`, `views/reservation-*.php`, `views/payment.php` y `views/item-*.php` no forman parte del flujo actual.
- **Corrección:** bloquear el acceso con reglas de Apache. **No se eliminan** sin autorización.

---

## 4. Inventario de secretos

No se muestran valores.

| Secreto | Dónde está | Ambiente | En git | Consumidores | Estado |
|---|---|---|---|---|---|
| Token de acceso de WhatsApp Cloud API | `services/WhatsappSender.php:33`, `services/enviar.php:28`, `services/enviarWhatsapp.php:420` | PROD (lo usa también TEST, por SEC-06) | Sí, desde `be3c584` | Webhook (respuesta) y envío de plantillas | **Expuesto; hay que rotarlo** |
| Phone Number ID de WhatsApp | Los mismos archivos, junto al token | PROD | Sí | Igual que el anterior | Identificador, no es secreto; pasarlo a configuración |
| Token interno `Authorization: Bearer` | `services/enviarWhatsapp.php:29`; `models/PedidoModel.php:428`; `models/solicitudModel.php:310`; `models/Promo.php:169` | Los dos | Sí | `enviarWhatsapp.php` y sus tres consumidores | **Expuesto; hay que rotarlo** |
| Token de verificación del webhook | `services/recepcionWhatsapp.php:13` (mismo valor que el token interno) | PROD | Sí | Meta, al verificar el webhook | **Expuesto y reutilizado; hay que separarlo** |
| App Secret de Meta (para la firma) | No existe en el código | — | — | Validación de la firma (WA-01) | Hay que obtenerlo de Meta y guardarlo en el entorno |
| Usuario y clave de MySQL de PROD | `config/database.env.php:119-120` | PROD | Sí, en varios commits | `models/database.php` (PDO) y `services/conexion.php` (mysqli) | **Expuesto; hay que rotarlo** |
| Usuario y clave de MySQL de TEST | `config/database.env.php:111-112`; `config/.env.dev` (local, fuera de git) | TEST | Sí (valor por defecto) | Los mismos | **Expuesto; hay que rotarlo** |
| Credenciales de la base de otro sistema | `services/enviarWhatsapp.php:464-467` (comentario) | Ajeno | Sí | Ninguno en este sistema | **Expuesto; hay que avisar al dueño** |
| Variables de `catalogo-app/.env` y `.env.production` | Retirados de git en `a7a3051`; agregados en `be3c584` | Catálogo | Sí, en el historial | Build de Vite | **Contenido no verificado (H-01)**; las variables `VITE_*` son públicas por diseño |
| `config/.env`, `config/.env.dev` | Locales, ignorados por `.gitignore` | TEST | No | Cargador de entorno | Correcto; deben existir también en el servidor |

---

## 5. Elementos revisados sin hallazgo

- Las consultas de `User`, `BarModel`, `PedidoModel`, `solicitudModel`, `Promo` y `api/*` usan PDO con parámetros.
- `views/header.php` protege todas las vistas del admin que lo incluyen.
- `procesar_pedido.php` escapa con `htmlspecialchars` lo que muestra.
- El nombre de las imágenes de promociones se genera con `random_bytes` (`promoController.php:40-43`).
- Hay protección contra pedidos duplicados por token de sesión y por firma de contenido (`api/pedido.php:9-94`, `procesar_pedido.php:13-106`). Se conserva.

---

## 6. Hipótesis que requieren verificación

| ID | Hipótesis | Cómo verificarla | Acceso necesario |
|---|---|---|---|
| H-01 | Qué contenían `catalogo-app/.env` y `.env.production` en el historial | Revisar solo los nombres de las variables en `be3c584` | Autorización para leer el historial de esos archivos |
| H-02 | Visibilidad del repositorio en GitHub (público o privado) y quiénes tienen acceso | Configuración del repositorio | Propietario del repositorio |
| H-03 | Si los `.txt` de log se pueden descargar por HTTP en PROD | GET de solo lectura a las rutas de la sección SEC-05 | Autorización para hacer esa solicitud |
| H-04 | Si MySQL acepta conexiones remotas en Hostinger (lista de IP permitidas) | Panel de Hostinger, sección "MySQL remoto" | Acceso al panel |
| H-05 | Qué banderas tiene la cookie de sesión (`HttpOnly`, `Secure`, `SameSite`) según la configuración de PHP del hosting | Mirar los encabezados de respuesta del login en TEST | Navegador en TEST |
| H-06 | Si Apache ejecuta scripts con doble extensión o cargados en `assets/` | Configuración del hosting y prueba en TEST con un archivo inofensivo | TEST |
| H-07 | Esquema real: `historial_pedidos`, largo de `users.password`, índices de `registro.id_wa` | `SHOW CREATE TABLE` en TEST (solo lectura) | Autorización para leer la base de TEST |
| H-08 | Las acciones de listado de `pedidoController` fallan por falta de argumentos | Prueba en TEST | TEST |
| H-09 | Que los navegadores que usa el admin apliquen `SameSite=Lax` por defecto | Encabezados y navegador del personal | — |
| H-10 | Que `services/enviar.php` no lo use nada fuera del repositorio (cron o integraciones) | Tareas programadas de Hostinger y logs de acceso | Acceso al panel |
