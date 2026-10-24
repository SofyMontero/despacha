# Fase 4B — Identidad y permisos: cierre

| Campo | Valor |
|---|---|
| Estado | **Pendiente de aprobación.** No autoriza a iniciar la 4C. |
| Alcance | D-03, D-05, D-07, D-08, D-10; SEG-03, SEG-04, SEG-05, SEG-06, SEG-07 y base de SEG-13 |
| Fuera de alcance | Catálogo, pedidos, facturación, sesión de clientes (D-09), migración de contraseñas heredadas (D-08), PHP |

## Funcionalidades

- **Identity** (`AddIdentityCore` + EF): hash PBKDF2 de Identity, bloqueo tras 5 fallos (15 min), política de 10 caracteres, token de recuperación con vigencia de 30 min. El canal de entrega está deshabilitado (`IPasswordResetDelivery`).
- **Tokens**: JWT HS256 de 15 min con `sub` y `tid`. El refresh token es opaco, de un solo uso, se guarda como hash SHA-256 y pertenece a una familia: si se reutiliza, se revoca toda la familia. Cambiar la contraseña o restablecerla revoca todas las sesiones.
- **Empresa activa** (D-05): con una sola membresía, el login entrega `tid`; con varias, `POST /api/auth/tenant` revalida la membresía y emite tokens nuevos. Un `tid` ajeno, aunque el token esté firmado, no obtiene permisos.
- **Autorización** (D-10): `RequirePermission(código)` exige membresía activa, empresa activa, usuario activo, el permiso en el rol y el módulo activo. Se revalida en servidor con una caché de 30 s que se invalida al cambiar miembros o roles. Una prueba exige que todo endpoint `/api/*` no público tenga un permiso.
- **Modelo de control**: `users`, `user_claims`, `user_logins`, `user_tokens`, `roles` (por empresa), `role_permissions`, `memberships`, `tenant_modules`, `refresh_tokens`, `platform_audit_events`, más `tenants` y `tenant_databases` de la 4A.
- **Permisos**: catálogo fijo en código (33 códigos: 29 de Fase 2 y 4 de plataforma).
- **Roles plantilla**: Administrador (todos los permisos de los módulos activos; no editable), Vendedor y Domiciliario.
- **Auditoría** sin datos personales: login correcto, fallido y bloqueado; reutilización de token; cambio de empresa; contraseña; miembros y roles.
- **Protección**: límite de 10 solicitudes por minuto y por IP en login, refresh y recuperación. Para el admin web: cookie `HttpOnly`, `Secure` y `SameSite=Strict`, y validación de `Origin`.

## Endpoints

| Método y ruta | Acceso |
|---|---|
| `POST /api/auth/login`, `/refresh`, `/logout`, `/password/forgot`, `/password/reset` | Públicos (los de credenciales con límite de tasa) |
| `POST /api/auth/tenant`, `GET /api/auth/me`, `POST /api/auth/password/change` | Sesión |
| `GET /api/platform/members` | `platform.members.read` |
| `POST /api/platform/members`, `PUT /api/platform/members/{userId}` | `platform.members.manage` |
| `GET /api/platform/roles` | `platform.roles.read` |
| `PUT /api/platform/roles/{roleId}/permissions` | `platform.roles.manage` |

## Migraciones

- `20261022193258_InitialControl` (EF Core, en `Modules/Platform/Internal/Migrations`). Script para revisión: `database/control/0001_InitialControl.sql`.
- **No se ejecutaron** en ninguna base. La API no migra al arrancar (hay una prueba que lo verifica). `Despacha.Migrator apply` exige `--confirm`.
- `Despacha.Migrator provision-tenant` da de alta una empresa con roles plantilla, módulos y el primer administrador. La contraseña se toma de una variable de entorno o se pide oculta por consola; no se copia ninguna contraseña del sistema anterior.

## Resultados

- `dotnet build`: 0 advertencias, 0 errores.
- `dotnet test`: **84/84** (33 unitarias, 36 de integración, 15 de arquitectura). Las 58 de la 4A siguen pasando.

## Conexión TEST

**Bloqueada por configuración**: el Migrator no tiene secretos (`dotnet user-secrets list` → ninguno). No se ejecutó `SELECT VERSION()` ni se leyó `information_schema`. P4A-01, P4A-02 y P4A-03 siguen pendientes.

## Pendientes y riesgos

| Id | Tema | Detalle |
|---|---|---|
| P4B-01 | Script idempotente | El modo idempotente de `MySql.EntityFrameworkCore` genera sintaxis de SQL Server (`IF … BEGIN`), que no es válida en MySQL. Se usa un script plano (`--from`) para revisión; la idempotencia la da `apply` con `__EFMigrationsHistory`. Esto ajusta la redacción de D-07. |
| P4B-02 | Llaves de Data Protection | Hoy están en el almacenamiento local por defecto. En TEST y PROD hay que persistirlas y compartirlas, o los tokens de recuperación fallarán entre instancias. |
| P4B-03 | Canal de recuperación | Falta conectar la entrega por correo o WhatsApp. Hoy solo queda registrado que la entrega está deshabilitada. |
| P4B-04 | Proxy inverso | El límite por IP requiere `ForwardedHeaders` detrás de un proxy. |
| P4B-05 | Revocación inmediata | Un JWT emitido sigue siendo válido hasta 15 min en los endpoints que solo exigen sesión. Los endpoints con permiso revalidan cada 30 s. La caché es local a la instancia; con varias instancias hace falta invalidación distribuida. |
| P4B-06 | Renovación concurrente | Dos renovaciones simultáneas con el mismo token se tratan como reutilización y cierran la sesión. Los clientes deben serializar el refresh. |
| P4B-07 | Tipos MySQL | `LockoutEnd` (`DateTimeOffset`) se mapea a `datetime`, sin zona horaria. Identity guarda UTC, así que no hay impacto, pero falta validarlo en MySQL real (P4A-04). |
| — | Pendientes previos | P3-01, P3-02, P4A-01 a P4A-04. `TenantSetting`, la sesión de clientes y la conversión de contraseñas heredadas quedan para su fase. |
