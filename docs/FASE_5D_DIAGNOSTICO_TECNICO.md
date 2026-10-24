# Fase 5D — Diagnóstico del esquema real de TEST

Fecha del diagnóstico: 2026-10-24. Fuente: volcado phpMyAdmin 5.2.2 de `u633742531_Multi_test`, generado el 2026-10-22 a las 21:01:02. Servidor declarado en el volcado: MariaDB 11.8.9-log. Tamaño del archivo: 394594 bytes. El volcado incluye estructura y datos. No se abrió una conexión, no se instaló MariaDB y no se ejecutó `ALTER`, `RENAME`, `DROP`, `UPDATE` ni ninguna migración. No se eliminaron archivos y no hay commit.

Este documento sustituye el diagnóstico anterior, que se escribió sin el volcado y trataba `users.rol` como texto. En este esquema `users.rol` es `int` y el vínculo real es `users.id_rol`.

La base de este ambiente sigue siendo la única. No se crea otra base ni tablas `catalog_*` o `customers_*`. PRODUCCIÓN no se toca. El mismo texto de migración, si se aprueba, solo puede correr en PRODUCCIÓN después de su propio respaldo y de su propia lectura: este volcado es TEST al 2026-10-22.

## Qué contiene el volcado

18 tablas InnoDB. El archivo no trae `CREATE VIEW`, `CREATE TRIGGER`, `CREATE PROCEDURE` ni `CREATE FUNCTION`. phpMyAdmin puede omitir rutinas según las casillas de exportación, así que esa ausencia hay que confirmarla en el servidor con [`database/proposed/00_solo_lectura_test.sql`](../database/proposed/00_solo_lectura_test.sql).

| Tabla | Filas en el volcado | AUTO_INCREMENT | Collation |
| --- | ---: | ---: | --- |
| bares | 3 | 79 | utf8mb4_unicode_ci |
| categorias | 11 | 23 | utf8mb4_unicode_ci |
| clientes | 2 | 634 | utf8mb4_general_ci |
| detalle_pedidos | 18 | 18059 | utf8mb4_unicode_ci |
| empresas | 1 | 2 | utf8mb4_uca1400_ai_ci |
| entregas | 6 | 7 | utf8mb4_uca1400_ai_ci |
| entrega_eventos | 0 | (sin valor) | utf8mb4_uca1400_ai_ci |
| entrega_ubicaciones | 0 | (sin valor) | utf8mb4_uca1400_ai_ci |
| jornadas | 2 | 3 | utf8mb4_uca1400_ai_ci |
| jornada_ubicaciones | 0 | (sin valor) | utf8mb4_uca1400_ai_ci |
| pedidos | 14 | 2308 | utf8mb4_general_ci |
| productos | 258 | 572 | utf8mb4_unicode_ci |
| promociones | 8 | 95 | utf8mb4_bin |
| promos | 21 | 29 | utf8mb4_general_ci |
| registro | 560 | 471794 | utf8mb3_general_ci |
| roles | 6 | 16 | utf8mb4_general_ci |
| users | 6 | 10 | utf8mb4_general_ci |
| visitas | 1 | 2 | utf8mb4_uca1400_ai_ci |

El archivo está completo respecto de las filas que quedaban en TEST esa noche: termina en `COMMIT` y cada `INSERT` se pudo partir sin filas mal formadas. Los `AUTO_INCREMENT` altos, con pocas filas, indican borrados anteriores. `pedidos` llega a 2308 y conserva 14 filas. `detalle_pedidos` llega a 18059 y conserva 18. `clientes` llega a 634 y conserva 2. Esas filas borradas no están en el volcado y este plan no las reconstruye.

Claves foráneas declaradas, y solo estas dos:

| Tabla | Columna | Referencia | ON DELETE |
| --- | --- | --- | --- |
| detalle_pedidos | id_pedido | pedidos.id_pedido | CASCADE |
| pedidos | ped_cliente | clientes.id_cliente | CASCADE |

Los comentarios `FK` de `users.id_rol`, `id_empresa`, `id_paseador`, `entregas.id_domiciliario`, `entrega_eventos.id_usuario`, `visitas.id_cliente` y `visitas.id_pedido` no están creados como restricción. `users` y `roles` no tienen ninguna clave foránea entrante ni saliente.

Índices relevantes: `users.usuario` es único; `users.rol` tiene índice; `roles.rol_nombre` no es único; `productos` solo tiene la clave primaria; `promociones` indexa `estado` y `prioridad`; `entregas.id_pedido` es único y no es clave foránea; `empresas.slug` es único.

`pedidos` en este TEST no tiene columnas de caja (`ped_ingreso`, `ped_factura`, `ped_egreso`, `ped_medio_pago`, `ped_validado`, `ped_fecha_actualizacion`, `ped_id`). La consulta de solo lectura lo vuelve a comprobar, porque PRODUCCIÓN puede tenerlas.

## users y roles

`roles`: 10 Domiciliario, 11 Administrador, 12 Paseador, 13 Supervisor, 14 Vendedor, 15 Cajero. Las seis filas tienen `rol_estado = 1`.

`users`: seis filas, ids 1, 2, 3, 4, 5 y 9. `AUTO_INCREMENT` 10. Los ids 6, 7 y 8 no están. `usuario` no se repite. `rol` es `int(2) NOT NULL` y las seis filas valen 0. Ese entero no clasifica a nadie.

| id | id_rol | Forma de `password` | api_token | ops_api_token |
| ---: | --- | --- | ---: | ---: |
| 1 | 11 Administrador | no es bcrypt | vacío | presente |
| 2 | NULL | bcrypt `$2y$` | vacío | vacío |
| 3 | NULL | bcrypt `$2y$` | vacío | vacío |
| 4 | NULL | bcrypt `$2y$` | vacío | vacío |
| 5 | NULL | bcrypt `$2y$` | vacío | vacío |
| 9 | 12 Paseador | no es bcrypt | vacío | vacío |

Un correo está vacío. Los valores de clave, token, correo, teléfono y nombre no se copian a este documento ni a los scripts.

Dependencias lógicas, sin clave foránea, que guardan el id de `users` y siguen siendo válidas si el id se conserva:

| Tabla | Columna | En el volcado |
| --- | --- | --- |
| jornadas | id_paseador | 2 filas, ambas con id 9, que existe |
| jornada_ubicaciones | id_paseador | 0 filas |
| visitas | id_paseador | 1 fila, id 9, que existe |
| entregas | id_domiciliario | 5 filas con id 1 o 2, que existen; 1 fila NULL |
| entrega_eventos | id_usuario | 0 filas |
| users | id_rol | apunta a `roles.id_rol`; 4 filas NULL; ninguna apunta a un rol inexistente |
| users, clientes, pedidos, jornadas, visitas | id_empresa | la empresa 1 existe |

`jornadas` y `visitas` siguen apuntando al id 9 de `usuarios_legacy`. Ese id no tiene que ser el de Identity. La columna `users.LegacyUserId` lo conserva, y las tablas históricas no se reescriben.

### Membresía que se puede afirmar

| id_rol | Nombre | Usuarios en este volcado | Membresía propuesta |
| ---: | --- | ---: | --- |
| 11 | Administrador | 1 (id 1) | Plantilla Administrador. Con los módulos Catálogo y Clientes activos, opera el administrador React y la administración de miembros. |
| 14 | Vendedor | 0 | Plantilla Vendedor del código (`catalog.read`, `customers.read.assigned` y los permisos de campo, ruta y cartera de esa plantilla). No hay fila que insertar en este volcado. |
| 10 | Domiciliario | 0 | Sin membresía en esta ola. |
| 12 | Paseador | 1 (id 9) | Sin membresía. `LegacyUserId` 9 queda disponible para jornadas y visitas. |
| 13 | Supervisor | 0 | Sin membresía. |
| 15 | Cajero | 0 | Sin membresía. |
| NULL | sin rol | 4 (ids 2, 3, 4 y 5) | Sin membresía. |

Nadie recibe administrador por el solo hecho de existir. La plantilla Administrador del código no guarda una lista: recibe todos los permisos de los módulos activos, y el código siempre suma el módulo Platform. El script de importación activa solo Catalog y Customers, así el administrador de esta ola no recibe pedidos, facturación, cartera ni entregas.

La clave bcrypt no entra en `PasswordHash`. ASP.NET Identity verifica un hash propio, distinto de `$2y$`. Copiarlo haría fallar el inicio de sesión. Las cuatro claves bcrypt se quedan en `usuarios_legacy.password` para un verificador futuro. Las dos que no son bcrypt no se copian. Las seis cuentas nacen con `PasswordHash` NULL y `RequiresPasswordChange = 1`. `api_token` y `ops_api_token` no se copian. El token de operaciones queda solo en la tabla renombrada, fuera de la identidad nueva.

## Catálogo, clientes y el resto

`empresas`: una fila, id 1, slug `multilicores`, `modelo_negocio` bares, activa.

`categorias`: ids 1, 2, 3, 5, 6, 9, 10, 11, 12, 13 y 21. Hay huecos. No hay columna de estado. Nombre máximo observado: 22 caracteres, sobre `varchar(50)`. Las 11 imágenes existen una sola vez bajo `sistema/assets/img/licores/`.

`productos`: ids 293 a 571, con huecos; siguiente id 572. Precios `int`. Código `codigo_productos int NOT NULL`, sin índice. Nombre máximo observado: 34, sobre `varchar(250)`.

| Hecho | Conteo |
| --- | ---: |
| estado_producto = 1 | 248 |
| estado_producto = 0 | 9 |
| estado_producto vacío (id 557) | 1 |
| acti_Unidad = 1 | 237 |
| acti_Unidad = 0 | 21 |
| códigos distintos | 252 |
| códigos repetidos, 2 filas cada uno | 6 (43, 62, 95, 161, 181, 221) |
| precio unidad máximo | 1199971 |
| precio paca máximo | 7199826 |
| producto 382 con `id_cate_producto` 19, categoría ausente, estado 1 | 1 |

De esos códigos repetidos, 95 y 181 mezclan un producto activo y uno inactivo. 43, 62, 161 y 221 tienen dos activos. Un índice único no cabe.

`promociones` es la tabla del administrador React. Ocho filas, ids 87 a 94, todas con `estado` 1, descripción presente (máximo 122 caracteres) y `creado_en` real del 2026-10-16. `codigo` es `int` y corresponde a `productos.codigo_productos`.

| Promoción | codigo | Correspondencia |
| ---: | ---: | --- |
| 87, 88, 89, 90, 91, 93, 94 | 105, 215, 209, 149, 141, 113, 251 | un solo producto |
| 92 | 181 | dos productos, uno activo y uno inactivo: `producto_id` queda NULL |

`promos` es otra tabla (21 filas, ids con huecos, todas `pro_estado = Activa`). No se fusiona con `promociones` y no se elimina en esta ola.

`bares`: ids 1, 50 y 78. Siguiente id 79. No hay estado. Las tres fechas `fecha_crear_bar` son reales.

`clientes`: ids 3 y 331. Siguiente id 634. Los dos apuntan al bar 1 y a la empresa 1. Los dos tienen `cli_identificacion = 0`, `cli_fecha_registro = 0000-00-00 00:00:00` y teléfonos de 9 y 10 dígitos. No hay teléfono en E.164. La zona guarda un valor numérico de 11 caracteres y la identificación entera está en cero. Esos valores no se reescriben a un número inventado.

`pedidos`: 14 filas, ids 455 a 529 con huecos, todas del cliente 3 y de la empresa 1. `ped_numfac` no se repite. `ped_estado`: aceptado 8, rechazado 4, `finalizaCo` 1, `finalizaCr` 1. La columna es `varchar(10)` y esos dos últimos valores están cortados; alargar la columna conserva el texto cortado y no recupera el resto. No hay columnas de caja.

`detalle_pedidos`: 18 filas, todas con pedido existente, todas `unidad`. Dos líneas usan `id_producto` 30, que no existe (el primer producto vivo es 293). Hay una línea con precio 0. El precio histórico vive aquí; cambiar el precio del producto no debe reescribir estas líneas.

`entregas`: 6 filas. Las seis apuntan a pedidos 2203–2208, que no están entre los 14 pedidos conservados. El único de `id_pedido` no impide el huérfano porque no hay clave foránea. No se borra la entrega ni se inventa el pedido.

`registro`: 560 filas de mensajes, `utf8mb3`, `AUTO_INCREMENT` 471794. Queda fuera de esta ola. No se copia su contenido a la documentación.

## Imágenes en este repositorio

Comparación del nombre guardado en la base contra `sistema/assets/img/licores/` y sus subcarpetas. El disco de Hostinger puede tener archivos que este checkout no tiene. Esta comparación no autoriza borrar `sistema/`.

| Origen | Referencias | Faltan en este checkout | Mismo nombre en varias carpetas |
| --- | ---: | ---: | ---: |
| categorias.imagen_categoria | 11 | 0 | 0 |
| productos.imagen_producto | 258 | 9 | 236 |
| promociones.imagen | 8 | 8 | 0 |
| promos.pro_imagen | 21 | 21 | 0 |

Los 258 nombres que en disco están repetidos en más de una carpeta tienen el mismo SHA-256 en todas sus copias. Cero nombres repetidos con contenido distinto. Aun así, la columna sigue guardando solo el nombre. No se reescribe la ruta en esta ola. Un lector posterior trata el valor sin `/` como nombre legado y no pisa esos archivos con una subida nueva.

Faltan en este checkout, entre otras, las nueve imágenes `prod_*` de productos y las ocho imágenes de `promociones` (nombres `promojosecmascusquena-877665fa.jpeg`, `whiskiglen-amerillo-*.jpeg`, `somenting-media-3966497b.jpeg`, `chivas-amarillo-d9aa1a49.jpeg`, `bacardi-real-bbfad2b7.jpeg`, `absolut-media-ca29bfdd.jpeg`, `2real-media-d8c8b9fc.jpeg`). Las 21 de `promos` tampoco están. Hay que buscarlas en el disco del servidor antes de dar por perdida una imagen.

## Diferencias con el backend .NET actual

El modelo actual sigue mapeando `catalog_categories`, `catalog_products`, `catalog_promotions`, `customers_establishments` y `customers_customers`, con `TenantEntity` (`Id` long, `Version`, auditoría) y con las migraciones `InitialCatalog` e `InitialCustomers`. Esas migraciones y `database/tenant/0001_InitialTenant.sql` siguen bloqueadas: crearían el catálogo paralelo. `LegacySchemaGuard` impide `tenant-apply` cuando ya existen `pedidos`, `productos`, `clientes`, `users` y el resto.

La identidad de `ControlDbContext` crea `users` y `roles`. Esos nombres chocan con las tablas vivas. La identidad nueva solo puede crearse después del renombre, o tendría que usar otros nombres. Este plan usa el renombre porque el volcado tiene cero claves foráneas hacia `users` y `roles`. Si la lectura en vivo devolviera alguna, el renombre se detiene.

| Concepto .NET | Columna real | Ajuste de esta ola |
| --- | --- | --- |
| Category.Name varchar(100) | nombre_categoria varchar(50), máximo 22 | Ampliar a 100. No hay fila que se corte. |
| Category.IsActive | no existe | Agregar `activo` default 1. |
| Category.ImageKey varchar(300), nullable | imagen_categoria varchar(250) NOT NULL | Ampliar a 300. Sigue NOT NULL: las 11 tienen imagen. |
| Product.Code varchar(50), índice no único | codigo_productos int, sin índice | Se conserva el int. No hay códigos no numéricos. El índice nuevo no es único. Pasar la columna a varchar queda para otra aprobación: reconstruye la tabla. |
| Product.Name varchar(200) | descripcion_producto varchar(250), máximo 34 | Se conserva 250. Encogería la columna. La API, cuando se adapte, debe aceptar el ancho real. |
| Product.UnitPrice / PackPrice decimal(12,2) | int | Los máximos caben en decimal(12,2). El cambio es sin pérdida y reconstruye la tabla: va en un paso aparte. |
| Product.CategoryId nullable, FK Restrict | id_cate_producto int NOT NULL | Sin FK mientras exista el producto 382 y la categoría 19. El id 19 se conserva. |
| Product.IsActive | estado_producto varchar(5): 1, 0 o vacío | Agregar `activo`. `1` pasa a 1; `0` y vacío pasan a 0. `estado_producto` se conserva. |
| Product.SellsByUnit | acti_Unidad int 0/1 | Se conserva la columna. |
| Product.UnitsPerPack | cantidad_paca_producto | Se conserva. `cantidad_venta` también. |
| Promotion.Code varchar(50) | codigo int | Se conserva el int, igual que el código de producto. |
| Promotion.ProductId opcional | no existe | Se agrega nullable. Solo se llena con correspondencia única. La 92 queda NULL. |
| Promotion.Description obligatoria, máximo 1000 | text NULL; en los datos, ninguna es NULL y la más larga mide 122 | Se conserva TEXT. No se encoge. |
| Promotion.IsActive | estado tinyint(1) | Se usa `estado`. No se agrega otro flag. |
| Promotion.creado_en | timestamp con fechas reales | Se conserva. No se pisa con la fecha de la migración. |
| Establishment en customers_establishments | bares | Se agregan auditoría, `version` y `activo`. Nombre 50 se amplía a 150; dirección 100 a 255. |
| Customer.Phone E.164 varchar(16) | cli_telefono varchar(15), 9 y 10 dígitos | Se copia a `telefono_original`. `cli_telefono` no se reescribe. |
| Customer.Identification varchar(30) | cli_identificacion int, valor 0 | Se agrega `identificacion_texto`. El 0 no se copia: queda NULL. La columna entera se conserva. |
| Customer.RegisteredAt / creado_en | cli_fecha_registro en cero en las dos filas | `creado_en` nuevo queda NULL. Pasar la fecha cero a NULL es un paso aparte: el conector no lee `0000-00-00` como fecha. |
| Customer.IsActive | no existe | Agregar `activo` default 1. |
| Version y auditoría | no existen en catálogo ni clientes | `version` 0, `origen_registro` historico, autores NULL. |
| Control `users` / `roles` | tablas de negocio con el mismo nombre | Renombre a `usuarios_legacy` y `roles_legacy`, y después la identidad. |
| Tablas de pedidos, entregas, jornadas, registro, promos | vivas y con historia | Fuera de los `ALTER` de esta ola. |

El administrador React ya envía `version` e `isActive`. Cuando el backend lea estas tablas, las filas históricas devolverán `version` 0. La primera edición de la aplicación la pasa a 1 y llena solo `actualizado_en` y `actualizado_por`. `creado_por` de una fila histórica sigue NULL.

## Matriz de cambios propuesta

Nada de esto está ejecutado. Cada paso está comentado en `database/proposed/`.

| Tabla | Cambio | Filas afectadas | Dato histórico |
| --- | --- | --- | --- |
| users, roles | `RENAME` a `usuarios_legacy`, `roles_legacy` si las FK entrantes siguen en cero | 6 y 6 | Ids intactos. Inverso válido solo antes de crear la identidad nueva. |
| identidad nueva | Tablas de `InitialControl` y la columna `LegacyUserId`, más importación por `INSERT … SELECT` | 6 usuarios, 1 membresía | El id de Identity es nuevo. `LegacyUserId` guarda 1, 2, 3, 4, 5 y 9. Sin claves ni tokens. |
| categorias | `activo`, `origen_registro`, `version`, auditoría; nombre a varchar(100); imagen a varchar(300) | 11 | Nombres e imágenes intactos. `creado_en` NULL. `activo` 1. `version` 0. |
| productos | `activo`, auditoría, `version`, `origen_registro`; índice no único de código y de categoría | 258 | `activo` 248 en 1 y 10 en 0. Código, precios y categoría 19 intactos. |
| promociones | `producto_id`, `version`, `origen_registro`, autores y `actualizado_*` | 8, de las cuales 7 reciben producto | `codigo`, `estado` y `creado_en` intactos. La 92 queda con `producto_id` NULL. |
| bares | auditoría, `activo`, `version`; nombre a 150; dirección a 255 | 3 | `creado_en` copia `fecha_crear_bar`. La columna original se conserva. |
| clientes | auditoría, `activo`, `version`, `telefono_original`, `identificacion_texto`; ampliaciones de varchar | 2 | Teléfono copiado, no normalizado. Identificación 0 no se convierte en texto. `creado_en` NULL. |
| clientes | paso aparte: `cli_fecha_registro` cero pasa a NULL y la columna admite NULL | 2 | No hay una fecha real que conservar. |
| productos | paso aparte: precios int a decimal(12,2) | 258 | Sin pérdida. Reconstruye la tabla. |
| clientes.cli_Bar, promociones.producto_id | paso aparte: FK `ON DELETE RESTRICT` si el precheck de huérfanos es 0 | — | No borra filas. Cambia lo que un `DELETE` futuro puede hacer. |
| pedidos, detalle_pedidos, entregas, jornadas, visitas, promos, registro, empresas | sin cambios en esta ola | — | Ids, precios de línea y estados se quedan como están. |

## Riesgos

1. En MariaDB cada `ALTER` y cada `RENAME` confirman la transacción implícita. `ROLLBACK` no los deshace. La vuelta atrás de un renombre, antes de crear la identidad, es el `RENAME` inverso. Cualquier otro fallo se recupera restaurando el volcado sobre TEST, en una operación aprobada aparte.
2. El volcado es del 2026-10-22. TEST pudo cambiar. La lectura de `00_solo_lectura_test.sql` tiene que cuadrar con estos conteos antes de descomentar nada. Si `control_base` no es `OK_TEST`, se detiene.
3. PRODUCCIÓN puede tener claves foráneas, columnas de caja, más huérfanos o más códigos repetidos. El script de renombre exige cero FK hacia `users` y `roles` en la base donde se ejecute.
4. `pedidos` y `detalle_pedidos` borran en cascada si se borra el cliente o el pedido. El administrador nuevo no debe borrar clientes físicos. Pasar esas FK a `RESTRICT` es otro cambio, posterior, y no borra datos por sí mismo.
5. Seis entregas apuntan a pedidos que ya no existen. Una FK nueva sobre `entregas.id_pedido` fallaría. No se agrega.
6. La categoría 19 no existe y el producto 382 la referencia. Una FK de categoría fallaría. No se agrega y no se anula el 19.
7. El código 181 tiene dos productos. `producto_id` de la promoción 92 queda NULL. Un `LIMIT 1` asignaría uno de los dos al azar.
8. Fechas `0000-00-00` en los dos clientes. Un conector estricto puede fallar al leerlas. Anularlas es un cambio de dato y va separado del `ADD COLUMN`.
9. Unificar collations (`unicode_ci`, `general_ci`, `uca1400_ai_ci`, `bin`, `utf8mb3`) reconstruye tablas. Esta ola no lo hace. `promociones` sigue en `utf8mb4_bin`.
10. `registro` tiene teléfonos y textos de WhatsApp. No entra en los scripts ni en este documento.
11. Las imágenes que faltan en el checkout pueden existir en el servidor. Borrar `sistema/` o reescribir `imagen_producto` antes de esa búsqueda pierde la referencia.
12. `InitialCatalog`, `InitialCustomers` y `0001_InitialTenant.sql` crearían el catálogo paralelo. Siguen sin aplicarse. Las pruebas MariaDB del repositorio crean bases `despacha_it_*` en loopback; `DESPACHA_MARIADB_TEST_CONNECTION` no debe apuntar a `u633742531_Multi_test`.
13. MySql.EntityFrameworkCore no declara soporte oficial de MariaDB. Este diagnóstico no sustituye esas pruebas, y esas pruebas no se corren contra TEST.
14. Conectar la API a TEST antes de `5D.20261024.02` no modifica la base: el guardia responde que el esquema no está y corta la solicitud.

## Scripts, sin ejecutar

| Archivo | Qué haría, una vez descomentado y aprobado |
| --- | --- |
| [`database/proposed/00_solo_lectura_test.sql`](../database/proposed/00_solo_lectura_test.sql) | Solo `SELECT`. Sirve para phpMyAdmin. Compara el vivo con este volcado. |
| [`database/proposed/01_renombre_users_roles_NO_EJECUTAR.sql`](../database/proposed/01_renombre_users_roles_NO_EJECUTAR.sql) | Renombre de `users` y `roles` si no hay FK. |
| [`database/proposed/02_columnas_modernas_NO_EJECUTAR.sql`](../database/proposed/02_columnas_modernas_NO_EJECUTAR.sql) | Columnas nuevas, índices y los pasos aparte de fecha cero, decimal y FK. |
| [`database/proposed/03_importar_usuarios_NO_EJECUTAR.sql`](../database/proposed/03_importar_usuarios_NO_EJECUTAR.sql) | `5D.20261024.03`. Importa las seis cuentas con `LegacyUserId` y una sola membresía de administrador. El id de Identity es nuevo. |

`02` también lee el ambiente y comprueba que las columnas modernas todavía no existen. El DDL sigue comentado. `02` incluye, además del `SELECT` de estado, la comprobación de ambiente y de estructura.

## Implementación de código, sin tocar TEST

El modelo de EF ya apunta a `categorias`, `productos`, `promociones`, `bares` y `clientes`. `InitialCatalog` e `InitialCustomers` siguen generando `catalog_*` y `customers_*` y quedaron a propósito fuera del modelo: no hay una migración EF que altere TEST. `tenant-apply` sigue bloqueado si esas tablas existen.

Antes de usar la base de la empresa, la API ejecuta `ModernizedSchemaGuard` en solo lectura. Si falta el esquema `5D.20261024.02`, responde 503 y no escribe. Las pruebas crean ese esquema en SQLite. No apuntan a Hostinger.

`users.LegacyUserId` guarda el id de `usuarios_legacy` sin ocupar el id de Identity. La migración `LegacyUserReference` solo agrega esa columna y su índice único en el esquema de control. No está aplicada. Las jornadas siguen apuntando al id legado. La membresía propuesta es solo para `id_rol` 11. No se copian claves ni tokens.

Para conectar React con TEST hace falta otra aprobación y, en este orden: respaldo, lectura `00`, scripts `01`, `02` (pasos A a F; el paso G de claves foráneas no lo exige el guardia) y `03`, y el secreto de conexión de la API. Hasta entonces el administrador habla con la API local.

## Respaldo antes de cualquier paso aprobado

1. Confirmar `DATABASE() = 'u633742531_Multi_test'` y que el nombre no contiene `prod`.
2. Volcado lógico anterior al DDL, fuera del repositorio, con suma de verificación: `mysqldump --single-transaction --routines --triggers --events`.
3. Un solo paso descomentado.
4. Volver a correr la lectura: conteos, ids mínimo y máximo, huérfanos, `producto_id` en 7 y NULL en 1, ausencia de `catalog_*` y `customers_*`, pedidos y detalles con los mismos ids y precios.
5. Recuperar con el renombre inverso solo si la identidad nueva todavía no existe. En cualquier otro caso, restaurar el volcado.

## Qué queda esperando aprobación

Los scripts no se ejecutaron y no hay commit. PRODUCCIÓN no se tocó. El siguiente paso, si se aprueba, es el respaldo y la lectura en vivo; después, y solo entonces, descomentar un script. Pedidos, detalles, entregas, jornadas, visitas, `promos` y `registro` esperan otra fase.
