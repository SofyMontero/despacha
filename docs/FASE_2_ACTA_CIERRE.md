# Acta de cierre — Fase 2: Diseño de arquitectura

| Campo | Valor |
|---|---|
| Proyecto | Despacha: migración de PHP a .NET 10, React web y React Native + Expo |
| Fase | 2 de 12, diseño de arquitectura |
| Fecha | 2026-10-20 |
| Documento de diseño | `docs/FASE_2_ARQUITECTURA.md` (versión 1.2) |
| Fuente | `docs/FASE_1_DIAGNOSTICO.md` |
| **Estado** | **FASE 2: APROBADA CON CORRECCIONES DOCUMENTALES** |

> Esta aprobación **no autoriza a iniciar la Fase 3** automáticamente. Para eso se necesita una autorización explícita del cliente.

## 1. Alcance cumplido

Se entregó el diseño completo en 18 secciones:

- arquitectura general y diagrama de componentes;
- estructura del monorepo;
- módulos y sus dependencias;
- matriz de funcionalidades, roles y permisos;
- diseño multi-tenant;
- modelo de datos;
- autenticación;
- pedidos, facturas, cartera y caja;
- trabajo del vendedor y entregas;
- arquitectura GPS;
- aplicación móvil y mapas;
- infraestructura;
- migración;
- seguridad;
- pendientes y plan por fases.

## 2. Correcciones aplicadas en la versión 1.2

| N.º | Corrección | Dónde quedó |
|---|---|---|
| 1 | La app móvil es React Native + Expo + TypeScript para Cliente, Vendedor y Domiciliario. La administración sigue en React web. `catalogo-app` se conserva como referencia y fuente de lógica reutilizable. | D-16, secciones 3, 4, 13 y 16 |
| 2 | Vendedor es el único rol oficial (antes también "Paseador") y `FieldSales` es compartido | D-00, secciones 5, 6 y 11 |
| 3 | Un pedido puede tener varias facturas, con facturación parcial por líneas | D-13, sección 8 (`DetalleFactura`, invariantes) |
| 4 | Un pedido puede tener varias entregas, con detalle de cantidades | Sección 8 (`DetalleEntrega`, D-20) y sección 11 |
| 5 | La Empresa B tiene catálogo y recibe pedidos directos de clientes | Sección 6 |
| 6 | Se incluye `CashManagement` para ingresos, egresos y conciliación con la cartera | D-21, secciones 5 y 8 |
| 7 | El crédito incluye cupos, plazos, mora, bloqueos configurables y abonos parciales | D-14, sección 6 (opciones `credit.*`) |
| 8 | Consecutivo `PED-AAAAMMDD-####`, generado en transacción, sin colisiones y con paso a cinco dígitos al superar 9.999 por día | D-12 |
| 9 | El proveedor de mapas queda pendiente de evaluación; Google no está aprobado | D-17 |
| 10 | El objetivo de retención de los puntos GPS detallados es de 90 días, sujeto a revisión legal y de privacidad | D-22 |

## 3. Decisiones confirmadas (de negocio o de producto)

| Id | Decisión |
|---|---|
| D-00 | Un solo rol Vendedor para todas las empresas, con `FieldSales` común |
| D-11 | El servidor calcula los precios y el total |
| D-12 | Consecutivo `PED-AAAAMMDD-####` |
| D-13 | Varias facturas por pedido, con facturación parcial; sin DIAN |
| D-14 | Crédito y cartera completos: cupos, plazos, mora, bloqueos configurables y abonos parciales |
| D-16 | React Native + Expo + TS para Cliente, Vendedor y Domiciliario; administración en React web |
| D-20 | Varias entregas por pedido, con control de cantidades |
| D-21 | `CashManagement` con ingresos, egresos y conciliación con la cartera |
| D-22 | Retención GPS con objetivo de 90 días |
| — | La Empresa B tiene catálogo y pedidos directos de clientes |
| — | Pedido, Factura y Entrega son entidades separadas; `ped_numfac` no es una factura |
| — | El acceso del cliente empieza por WhatsApp, sin confiar en el teléfono que viene en la URL |
| — | Una base por empresa y por ambiente, con el código compartido |

## 4. Decisiones técnicas aprobadas con la arquitectura

| Id | Decisión |
|---|---|
| D-01, D-02 | Monolito modular en un solo proyecto .NET, sin CQRS ni mediador |
| D-03 a D-07 | Base de control por ambiente, ambiente decidido por el despliegue, empresa resuelta por el token, secretos fuera del código, migraciones por empresa |
| D-08 a D-10 | Autenticación del personal, enlace de un solo uso por WhatsApp, permisos con alcance de datos |
| D-15 | GPS por HTTP en lotes; SignalR solo para mostrar en vivo |
| D-18 | Infraestructura con contenedores, independiente del proveedor |
| D-19 | Esquema limpio y copia repetible, con corte por ventana |

**Detalles técnicos nuevos de la versión 1.2** (se validan en la fase donde se implementan):

- interfaz `ICreditGate` para evitar dependencias en ciclo;
- una sola app con rutas por experiencia;
- página de respaldo cuando la app no está instalada;
- abono aplicado primero a la cuenta más antigua.

## 5. Pendientes (solo reglas operativas y verificaciones técnicas)

**Verificaciones técnicas:**

1. Estructura real de la base y versión de MySQL o MariaDB, incluida la existencia de las tablas del prototipo.
2. Que el proveedor de EF Core 10 funcione con MySQL o MariaDB.
3. Proveedor de hosting para .NET, y acceso remoto al MySQL actual.
4. Evaluación del proveedor de mapas.
5. Revisión legal del objetivo de retención del GPS.
6. Pruebas de GPS en segundo plano y de batería.
7. Volúmenes de operación.

**Reglas operativas:**

- reglas abiertas del Vendedor;
- precio de un pedido creado sin conexión;
- visita con varios pedidos;
- tope de la facturación parcial;
- manejo de lo no entregado;
- valores y políticas de crédito, e interés de mora;
- orden de aplicación de los abonos;
- operación de caja;
- saldos iniciales de cartera y relación `cli_Bar`;
- datos iniciales de la Empresa B.

El detalle está en la sección 18 de `docs/FASE_2_ARQUITECTURA.md`.

## 6. Riesgos abiertos

- Hay secretos en el historial de git que deben rotarse (Fase 3).
- No se ha comprobado que el proveedor MySQL funcione con EF Core 10 y MariaDB.
- El hosting compartido actual no ejecuta .NET.
- Hay tablas del prototipo posiblemente presentes en producción y sin uso.
- La cartera histórica no existe en el PHP: depende de que el negocio aporte los saldos iniciales.

## 7. Restricciones respetadas

- No se escribió código, no se crearon proyectos y no se instalaron dependencias.
- No se modificaron bases de datos, no se ejecutaron migraciones y no se desplegaron servicios.
- No se eliminó PHP y no se hicieron commits.
- Solo se actualizaron documentos en `docs/`.
- No se incluyen valores de secretos.

## 8. Registro de aprobación

| Rol | Nombre | Decisión | Fecha |
|---|---|---|---|
| Cliente / responsable del proyecto | | **Aprobada con correcciones documentales** | 2026-10-20 |
| Arquitectura | | Correcciones incorporadas en la versión 1.2 | 2026-10-20 |

**Siguiente paso:** esperar la autorización explícita para iniciar la Fase 3 (correcciones urgentes de seguridad en PHP y rotación de secretos).
