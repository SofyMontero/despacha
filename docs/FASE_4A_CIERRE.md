# Fase 4A — Cierre: backend .NET 10

| Campo | Valor |
|---|---|
| Fecha | 2026-10-22 |
| Estado | **Aprobada** con pendientes P4A-01 a P4A-05. P4A-05 se resolvió en la 4B. |
| Detalle técnico | `backend/README.md` |

## 1. Resultados reales

| Paso | Resultado |
|---|---|
| SDK | .NET SDK 10.0.402, instalado con winget con autorización |
| `dotnet restore` | Correcto, sin alertas de vulnerabilidad de NuGet |
| `dotnet build` | Correcto: 0 errores y 0 advertencias (las advertencias se tratan como errores) |
| `dotnet test` | **58 de 58 correctas**: 27 unitarias, 17 de integración y 14 de arquitectura |
| Ejecución local | `/health/live` responde 200; `/health/ready` responde 503 (no hay MySQL local); `/api/platform/session` responde 401 sin token; OpenAPI y Swagger responden 200 |

No hubo que corregir nada: el código de la 4A compiló y pasó las pruebas en el primer intento.

## 2. Compatibilidad MySQL y MariaDB

- **Pomelo** no tiene versión para EF Core 10. Se usa **`MySql.EntityFrameworkCore` 10.0.12** (Oracle), aislado en `Shared/Persistence/DespachaMySql.cs`.
- **Comprobado sin servidor:** el proveedor construye el modelo y genera el DDL de la base de control.
- **Sin comprobar:** el motor y la versión del servidor TEST. Oracle da soporte a MySQL 8.0 y versiones posteriores, **no a MariaDB**. Si TEST es MariaDB, hay que hacer una prueba real (conexión, consultas y DDL de la base de control en una base local o de pruebas, nunca en el esquema heredado), o pasar a Pomelo cuando publique la versión para EF Core 10.

## 3. Separación de bases

| Base | Quién la usa | Configuración |
|---|---|---|
| Control multiempresa nueva | API (`ControlDbContext`) | `ConnectionStrings:Control` en los user-secrets de `Despacha.Api`; solo loopback en Local |
| Esquema heredado TEST | Solo `Despacha.Migrator`, en solo lectura | `ConnectionStrings:LegacyTest` en los user-secrets de `Despacha.Migrator` |

La API no conoce la conexión heredada y el Migrator no aplica migraciones.

## 4. Pendientes

| ID | Pendiente | Requisito |
|---|---|---|
| P4A-01 | ~~Confirmar el motor y la versión de TEST~~ **Resuelto en 4C:** MariaDB `11.8.9-MariaDB-log` (confirmado por el responsable) | — |
| P4A-02 | Extraer la estructura del esquema TEST desde `information_schema` (tablas, columnas, índices y claves; sin datos) hacia `database/`, como referencia | Igual que P4A-01. Requiere un comando de solo lectura en `Despacha.Migrator`. |
| P4A-03 | Prueba real del proveedor de Oracle con MariaDB 11.8.9, o decidir el cambio de proveedor | Pasa a la 4C (`docs/FASE_4C_CIERRE.md`, P4C-01) |
| P4A-04 | Pruebas de integración con MySQL real (Testcontainers o MySQL local), como prevé la Fase 2 | Docker o MySQL local |
| P4A-05 | Migraciones de la base de control en `Despacha.Migrator` (D-07) | Fase 4B |
| P3-01 y P3-02 | Pendientes heredados de la Fase 3 | Sin cambios |
