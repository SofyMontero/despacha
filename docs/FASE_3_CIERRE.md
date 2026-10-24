# Fase 3 — Cierre: preparación y seguridad del repositorio de migración

| Campo | Valor |
|---|---|
| Fecha | 2026-10-21 |
| Estado | **FASE 3 APROBADA CON DOS PENDIENTES** (2026-10-21; ver sección 6). No autoriza a iniciar la Fase 4. |
| Reemplaza | El plan de 9 lotes (`FASE_3_PLAN_IMPLEMENTACION.md`) y su matriz (`FASE_3_PRUEBAS.md`), que quedan **descartados** |
| Se conserva | `FASE_3_AUDITORIA_SEGURIDAD.md`, como referencia de hallazgos y reglas comerciales del PHP |

## 1. Decisión

- Este repositorio es exclusivo de la migración. El PHP heredado **no es el sistema de producción** y se conserva solo como referencia de reglas comerciales, datos y funcionalidades.
- **No se corrige el PHP.** Los controles de seguridad se implementan directamente en .NET 10 (sección 3).
- Las decisiones de arquitectura de la Fase 2 (D-00 a D-22) no cambian.

## 2. Aislamiento aplicado

Con un solo interruptor, `ALLOW_REAL_SERVICES`, que vale `0` por defecto, el repositorio no puede conectarse a bases remotas ni a WhatsApp. Todos los valores se leen de `sistema/config/.env.dev`, que es local y no está versionado.

| Archivo | Cambio |
|---|---|
| `sistema/config/database.env.php` | Sin credenciales por defecto. El ambiente por defecto es `dev`, ya no `prod`. Si el host de la base no es local, la conexión se rechaza. Funciones nuevas: `despacha_servicios_reales_habilitados`, `despacha_whatsapp_config`, `despacha_internal_token`, `despacha_base_url` |
| `sistema/services/WhatsappSender.php`, `enviar.php`, `enviarWhatsapp.php` | El token y el Phone ID de Meta salen del entorno. Con el bloqueo activo, no hay llamada a Meta ni escritura en `registro`. Se eliminó un comentario con credenciales de un tercero. |
| `sistema/services/recepcionWhatsapp.php` | El token de verificación sale de `WHATSAPP_VERIFY_TOKEN`, comparado con `hash_equals` |
| `sistema/models/PedidoModel.php`, `solicitudModel.php`, `Promo.php` | La URL de PROD se reemplazó por `APP_BASE_URL`, y el token interno sale de `INTERNAL_API_TOKEN`. Con el bloqueo activo no envían nada; el envío masivo de promociones no recorre los clientes. |
| `sistema/views/procesar_pedido.php`, `config/catalogo_router.php`, `helpers/promo_images.php` | Sin redirecciones ni hosts de PROD por defecto |
| `catalogo-app/vite.config.js` | El proxy de desarrollo ya no apunta a PROD: usa `VITE_PROXY_TARGET`, por defecto `http://localhost` |
| `catalogo-app/capacitor.config.json` | Se quitó `server.url` hacia PROD; la app usa los archivos locales |
| `sistema/config/database.env.example`, `.env.example` | Plantillas con los nombres de las variables, sin valores |
| `.gitignore` | Ignora los logs locales del PHP |
| `docs/FASE_1_DIAGNOSTICO.md` | Se redactaron el token interno y el Phone ID |

**Efecto en el uso local:** el `.env.dev` actual no tiene credenciales; antes dependía de los valores por defecto del código, que apuntaban a la base de test, una copia de producción. El PHP de referencia ahora necesita un **MySQL local con datos de prueba o anonimizados**, con `DB_NAME`, `DB_USER` y `DB_PASS` en `.env.dev`. Sin eso, muestra un error de configuración en vez de conectarse a otra base.

**Sin bloquear (bajo riesgo):** los logos de `catalogo-app` (`brand.js`, `ProductCard.jsx`, `CategoriasPage.jsx`) se descargan como imagen pública del dominio actual. Solo leen y no envían datos.

**Verificación:** una búsqueda en el repositorio no encuentra ninguno de los secretos conocidos fuera del historial de git. No se ejecutó PHP (no está instalado) ni se accedió a ningún servicio.

## 3. Controles de seguridad pendientes para .NET 10

| ID | Control | Hallazgos de origen | Fase |
|---|---|---|---|
| SEG-01 | Secretos solo en configuración externa (user-secrets en local y gestor de secretos o variables en los servidores). Escaneo de secretos en CI que bloquea el merge. | SEC-01 a SEC-04 | 4 |
| SEG-02 | Aislamiento de ambientes: en cualquier ambiente que no sea PROD, WhatsApp usa un emisor falso o una lista de números permitidos, y el arranque falla si la cadena de conexión apunta a PROD. | SEC-06 | 4 |
| SEG-03 | Autorización por defecto en todos los endpoints (política de respaldo que exige autenticación), permiso y alcance por empresa (D-10), y los endpoints públicos marcados de forma explícita. | AUT-01 a AUT-05 | 4 |
| SEG-04 | ASP.NET Core Identity con su hasher. Las claves del PHP **no se migran en texto plano**: en el corte, los usuarios con clave en texto plano reciben un reinicio de clave obligatorio, y los que tienen bcrypt se validan una vez y se vuelven a cifrar. Bloqueo por intentos y límite de solicitudes en el login. | PWD-01 a PWD-03, AUT-10 | 4 |
| SEG-05 | Sesiones y tokens: cookies `HttpOnly`, `Secure` y `SameSite`, antiforgery en el admin si usa cookies, tokens de corta duración con renovación en Expo y almacenamiento seguro en el dispositivo. | AUT-08, AUT-09 | 4 |
| SEG-06 | Acceso a datos solo con EF Core o consultas parametrizadas; prohibido armar SQL concatenando, con un analizador o una revisión en CI. | SQL-01 a SQL-03 | 4 |
| SEG-07 | Errores con ProblemDetails, sin detalles internos; CORS con orígenes explícitos; logs estructurados sin datos personales (teléfonos enmascarados) y nunca dentro de la carpeta pública. | GEN-01, GEN-03, SEC-05 | 4 |
| SEG-08 | Archivos subidos: validar el tipo real y el tamaño, guardarlos fuera de la carpeta pública (o en un almacenamiento de objetos) con un nombre generado. | GEN-02 | 5 |
| SEG-09 | El servidor calcula precios y totales (D-11). Hay que conservar las reglas de unidad, paca (`acti_Unidad`), promociones activas ligadas al producto y pedidos con total 0. Las promociones se identifican con una referencia explícita, no por la coincidencia de `codigo` con el id del producto. | PRE-01 | 6 |
| SEG-10 | Webhook de WhatsApp: firma `X-Hub-Signature-256` validada con el App Secret, idempotencia por un índice único del id del mensaje, respuesta 200 inmediata y envíos con una cola de salida, reintentos y una lista de plantillas permitidas. | WA-01 a WA-03 | 6 |
| SEG-11 | Identidad del cliente: un enlace firmado con vencimiento o un OTP (D-09), sin exponer nombre ni dirección por teléfono, y con límite de solicitudes en la consulta y el alta pública. | AUT-06, AUT-07 | 6 |
| SEG-12 | Salida segura en el admin React y en Expo: no usar `dangerouslySetInnerHTML` ni `innerHTML` con datos del cliente; validar las entradas en el servidor. | XSS-01 | 5 |
| SEG-13 | Auditoría de las operaciones sensibles: precios, caja, cartera, estados, usuarios y envíos masivos con confirmación y límite. | AUT-02 a AUT-04 | 4 (base) y 5 a 7 |

## 4. Pendientes fuera del alcance del repositorio

No se ejecutó ninguno de estos puntos; los decide el dueño de cada servicio.

1. **Credenciales expuestas en el historial de git** (token de Meta, token interno, claves de MySQL y credenciales de un tercero). Si siguen vigentes en algún servicio real, su dueño debe rotarlas. Este proyecto no las cambia.
2. **Avisar al tercero** cuyas credenciales de base aparecían en un comentario.
3. **`sistema/models/log_envios.txt` sigue versionado** y contiene teléfonos. Hay que sacarlo del índice en el próximo commit autorizado (`git rm --cached`). Reescribir el historial es una decisión aparte.
4. Confirmar la visibilidad del repositorio en GitHub.

## 5. Plan para iniciar la Fase 4

**Para empezar se necesita:**
- la aprobación de este cierre;
- la autorización explícita para la Fase 4;
- el SDK de .NET 10 y un MySQL local (instalado o en contenedor);
- la estructura de la base (`SHOW CREATE TABLE`) o un volcado anonimizado, entregado por el dueño de los datos.

**Alcance** (sin cambios respecto a la Fase 2, más los controles de seguridad):
1. La solución .NET 10 según la estructura del monorepo (Fase 2, sección 4) y el monolito modular (D-01, D-02).
2. Base de control, empresas, identidad, roles plantilla y permisos (D-03 a D-08, D-10).
3. Consecutivo `PED-AAAAMMDD-####` (D-12).
4. Controles transversales de la sección 3 que tocan a la Fase 4: SEG-01 a SEG-07 y la base de SEG-13.
5. CI con compilación, pruebas, escaneo de secretos y análisis de dependencias.
6. Prueba técnica de EF Core 10 con MySQL o MariaDB.

**Para darla por terminada:**
- La API arranca en local sin ningún secreto en el repositorio.
- La política de autorización por defecto está activa y cubierta por pruebas.
- El login con Identity funciona con bloqueo por intentos.
- El CI pasa.
- La prueba de MySQL está documentada.
- La TEST nueva no puede enviar WhatsApp a números fuera de la lista permitida.

**Cronograma** (las fases 5 a 12 no cambian; ver `FASE_2_ARQUITECTURA.md`, sección 18):

| Fase | Ajuste |
|---|---|
| 3 | Cerrada con esta acta: aislamiento, credenciales fuera del código y lista de controles |
| 4 | Suma los controles SEG-01 a SEG-07, SEG-13 (base) y el escaneo de secretos en CI |
| 5 | Suma SEG-08 y SEG-12 |
| 6 | Suma SEG-09 a SEG-11 |
| 12 | Se mantiene la revisión de seguridad antes de producción. Ya no hay correcciones del PHP. |

## 6. Aprobación

La Fase 3 queda aprobada el 2026-10-21. No se harán más lotes sobre el PHP. Quedan dos pendientes, que no bloquean el inicio de la Fase 4:

| ID | Pendiente | Cómo se cierra | Cuándo |
|---|---|---|---|
| P3-01 | Validar el aislamiento en ejecución | Con PHP y un MySQL local disponibles y `ALLOW_REAL_SERVICES=0`: 1) crear un pedido y comprobar que el log registra el bloqueo de WhatsApp sin ninguna llamada a Meta; 2) poner un `DB_HOST` remoto y comprobar que la conexión se rechaza; 3) iniciar el catálogo con `npm run dev` y comprobar que el proxy apunta a `localhost` | En cuanto haya un entorno local, o al principio de la Fase 4 |
| P3-02 | Retirar `sistema/models/log_envios.txt` del índice de git | `git rm --cached sistema/models/log_envios.txt` en el primer commit autorizado. El archivo local no se borra; reescribir el historial es una decisión aparte. | En el primer commit autorizado |
