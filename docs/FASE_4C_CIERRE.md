# Fase 4C — Compatibilidad con MariaDB: cierre

| Campo | Valor |
|---|---|
| Estado | **Pendiente de aprobación.** La prueba real con MariaDB está preparada, pero **no se ejecutó**, por decisión del responsable (no hay Docker). No autoriza a iniciar la Fase 5. |
| Motor de referencia | **MariaDB 11.8.9** (`11.8.9-MariaDB-log`, Hostinger) |
| Proveedor actual | `MySql.EntityFrameworkCore` 10.0.12 (Oracle) sobre EF Core 10. **Sin cambios.** |

## 1. Compatibilidad comprobada

**Estática, sin servidor:**

- El DDL de control solo usa `bigint`, `varchar`, `longtext`, `tinyint(1)`, `datetime(6)`, `datetime` y `char`. No declara colación, juego de caracteres ni motor, ni usa JSON o sintaxis exclusiva de MySQL 8. Todo es compatible con MariaDB 11.8.
- Los índices únicos más largos (`varchar(256)` en utf8mb4, 1024 bytes) quedan por debajo del límite de 3072 bytes de InnoDB.
- Las tablas heredan la colación de la base. En MariaDB 11.8 la colación por defecto es `utf8mb4_uca1400_ai_ci`; la prueba la registra.
- El modo idempotente del proveedor de Oracle genera `IF … BEGIN … END`, que no es válido ni en MySQL ni en MariaDB (P4B-01). El script plano del Migrator no tiene ese problema.

**Real, contra MariaDB 11.8.9:** **no comprobada.** No hay Docker en el equipo y se decidió no descargar un servidor portable.

## 2. Resultados de pruebas

- `dotnet build`: 0 advertencias, 0 errores.
- `dotnet test`: **84 superadas, 0 fallidas, 8 omitidas** (las de MariaDB, que se omiten si no se activan). Las 84 anteriores siguen pasando.
- Pruebas preparadas en `tests/Despacha.IntegrationTests/MariaDb`, con Testcontainers (`mariadb:11.8.9`) o una instancia local por variable de entorno:

| Prueba | Verifica |
|---|---|
| Versión de referencia | Conexión y que `SELECT VERSION()` empiece por `11.8.9` y contenga MariaDB |
| TLS obligatorio | `SslMode=Required`, como se usa fuera de Local |
| Migraciones | `MigrateAsync` dos veces sin cambios; 13 tablas InnoDB |
| Script de revisión | El SQL del Migrator se ejecuta en MariaDB |
| Tipos | Unicode y emoji (utf8mb4), `datetime(6)` con microsegundos, `DateTimeOffset`, `Guid`, `bool`, enums como texto, `char(64)` |
| Transacciones | Confirmar, revertir, y violación de índice único que lanza `DbUpdateException` |
| Concurrencia | 8 rotaciones simultáneas de un refresh token: solo una gana (`ExecuteUpdate`) |
| Flujo de la API | Alta de empresa, login, permisos, rotación y reutilización de refresh, bloqueo y `/health/ready` |

## 3. Alternativas para EF Core 10 (sin instalar nada)

| Opción | EF Core 10 | MariaDB | Consecuencia |
|---|---|---|---|
| **A. Oracle `MySql.EntityFrameworkCore` 10.0.12 (actual)** | Sí | Sin soporte oficial; es compatible en lo estático | Ningún cambio. El riesgo se cubre con las 8 pruebas. |
| B. Pomelo 9.0.0 | **No**: exige `EF Core Relational [9.0.0, 9.0.999]`. El feed nocturno tampoco tiene versión para EF 10. | Soporte oficial | Habría que bajar EF Core a 9 (el framework seguiría siendo .NET 10). EF Core 9 es STS y su soporte termina en noviembre de 2026, así que se descarta. |
| C. Esperar a Pomelo 10 | Cuando se publique | Soporte oficial | Cambio en una sola línea (`UseDespachaMySql`) más una nueva migración de prueba. Sin fecha. |
| D. Devart dotConnect for MySQL | Según el fabricante (por verificar) | Según el fabricante | Licencia comercial y dependencia de un tercero. Solo si A falla y C no llega. |

## 4. Proveedor recomendado

**Mantener la opción A (Oracle 10.0.12)**, condicionado a que las 8 pruebas pasen contra MariaDB 11.8.9 antes de desplegar en TEST. Si alguna falla por el proveedor, se evalúa D o se espera C. **No se cambió el proveedor.**

## 5. Cambios realizados

- Documentación: MariaDB 11.8.9 queda como motor de referencia en `FASE_2_ARQUITECTURA.md`, `FASE_4A_CIERRE.md` (P4A-01 resuelto, P4A-03 pasa a la 4C) y `backend/README.md`.
- Pruebas: `MariaDbServer` (fixture con una base desechable por prueba; solo loopback) y `MariaDbCompatibilityTests`. `DespachaApiFactory.ControlConnectionString` permite usar una base de control real. Paquete nuevo: `Testcontainers.MariaDb` 4.16.0, solo en pruebas.
- Sin cambios en el código de producción, en `UseDespachaMySql`, en el PHP ni en el esquema TEST de Hostinger.

## 6. Riesgos pendientes

| Id | Riesgo | Acción |
|---|---|---|
| P4C-01 | Compatibilidad real del proveedor de Oracle con MariaDB sin comprobar | Ejecutar las 8 pruebas con Docker, o con un MariaDB 11.8.9 portable en loopback |
| P4C-02 | Autenticación: MySql.Data no soporta `ed25519` de MariaDB | Confirmar que el usuario de Hostinger usa `mysql_native_password` |
| P4C-03 | TLS de Hostinger: la conexión fuera de Local exige `SslMode=Required` | Comprobarlo al configurar TEST |
| P4C-04 | `LockoutEnd` como `datetime` sin fracciones: el bloqueo se redondea a segundos | Sin impacto funcional; queda cubierto por la prueba de tipos |
| P4C-05 | Colación: el esquema heredado de Hostinger puede usar otra | Revisarlo con P4A-02 (solo metadatos, cuando haya acceso) |
| — | Siguen abiertos P3-01, P3-02, P4A-02, P4A-04 (se cubre al ejecutar P4C-01) y P4B-01 a P4B-07 | — |
