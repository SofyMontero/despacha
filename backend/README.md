# Despacha — backend .NET 10 (Fases 4A y 4B)

## Ejecutar en local

Requisitos: SDK de .NET 10 (`global.json`) y, para `/health/ready` y las bases de empresa, un MySQL **local**.

```powershell
cd backend
dotnet user-secrets --project src/Despacha.Api set "ConnectionStrings:Control" "Server=localhost;Port=3306;Database=despacha_control_local;User ID=despacha_local;Password=<clave local>"
dotnet user-secrets --project src/Despacha.Api set "Auth:Jwt:SigningKey" "<32 caracteres o más>"   # opcional en Local
dotnet run --project src/Despacha.Api
```

- Swagger UI: `http://localhost:5080/swagger` (solo en `Despacha:Environment=Local`).
- `GET /health/live` responde sin base. `GET /health/ready` revisa la base de control.
- Credenciales de cada empresa: `TenantSecrets:<SecretName>:User` y `:Password`, en user-secrets o variables de entorno (`TenantSecrets__<SecretName>__Password`).
- Sin `SigningKey`, en Local se usa una clave efímera: los tokens dejan de valer al reiniciar.

Pruebas: `dotnet test` (usan SQLite en memoria y dobles; no se conectan a servicios reales).

## Base de control: migraciones y alta de empresas (`Despacha.Migrator`)

La API nunca migra al arrancar (D-07). El Migrator usa `ConnectionStrings:Control` desde sus propios user-secrets y las mismas guardas de ambiente que la API.

```powershell
dotnet run --project src/Despacha.Migrator -- script --output ../database/control/0001_InitialControl.sql   # sin conexión
dotnet run --project src/Despacha.Migrator -- script --from <última migración aplicada> --output <archivo>.sql
dotnet run --project src/Despacha.Migrator -- apply --confirm                                               # solo pendientes
$env:DESPACHA_ADMIN_PASSWORD = "<clave>"   # o se pide oculta por consola
dotnet run --project src/Despacha.Migrator -- provision-tenant --code alfa --name "Empresa Alfa" `
  --db-server localhost --db-name despacha_alfa_local --db-secret tenant-alfa --admin-login admin.alfa
```

Nueva migración tras cambiar el modelo: `dotnet ef migrations add <Nombre> --project src/Despacha.Api --context ControlDbContext --output-dir Modules/Platform/Internal/Migrations` (herramienta local en `dotnet-tools.json`; `dotnet tool restore`).

## Autenticación (4B)

- `POST /api/auth/login` `{login, password, client?}`: con una sola empresa el token ya trae `tid`; con varias, se elige con `POST /api/auth/tenant`.
- Expo: el `refreshToken` llega en el cuerpo y se guarda en `expo-secure-store`. Admin web (`client: "web"`): llega en la cookie `__Secure-despacha-rt` (HttpOnly, Secure, SameSite=Strict, path `/api/auth`) y `refresh`/`logout` exigen un `Origin` de `Cors:AllowedOrigins`.
- Configuración: `Auth:AccessTokenMinutes` (15), `Auth:RefreshTokenDays` (14), `Security:RateLimit:AuthPermitsPerMinute` (10 por IP).

## Esquema heredado TEST (solo lectura, pendiente de autorización)

Está separado de la base de control: lo usa solo `Despacha.Migrator`, nunca la API.

```powershell
dotnet user-secrets --project src/Despacha.Migrator set "ConnectionStrings:LegacyTest" "Server=<host TEST>;Port=3306;Database=<esquema TEST>;User ID=<usuario solo lectura>;Password=<clave>"
```

Se recomienda un usuario MySQL con permiso `SELECT` únicamente.

## Ambientes y bloqueos

| `Despacha:Environment` | Host ASP.NET | Bases permitidas |
|---|---|---|
| `Local` (defecto) | cualquiera menos Production | Solo loopback y bases sin "prod" |
| `Test` | cualquiera | Solo los servidores de `Database:AllowedHosts` y bases sin "prod" |
| `Prod` | solo Production | Bases sin "test" ni "local" |

Las combinaciones inválidas, los orígenes CORS con comodín y la falta de `SigningKey` fuera de Local impiden el arranque.

## Motor de referencia: MariaDB 11.8.9

- Hostinger usa `11.8.9-MariaDB-log`. Es el motor de referencia del proyecto.
- Proveedor actual: `MySql.EntityFrameworkCore` 10.0.12 (Oracle). Oracle no da soporte oficial a MariaDB. `Pomelo.EntityFrameworkCore.MySql` 9.0.0 exige EF Core 9 y no tiene versión para EF Core 10. Evaluación completa: `docs/FASE_4C_CIERRE.md`.
- El proveedor se elige solo en `Shared/Persistence/DespachaMySql.cs` (`UseDespachaMySql`).

### Pruebas contra MariaDB 11.8.9 (opcionales y desechables)

`tests/Despacha.IntegrationTests/MariaDb` contiene 8 pruebas que se omiten salvo que se activen. Crean una base `despacha_it_*` por prueba y la borran al terminar. Solo aceptan un servidor en loopback; **nunca** se deben apuntar al esquema TEST de Hostinger.

```powershell
$env:DESPACHA_MARIADB_TESTS = "docker"          # Testcontainers con la imagen mariadb:11.8.9 (requiere Docker)
# o una instancia local aislada con permiso CREATE/DROP DATABASE:
$env:DESPACHA_MARIADB_TEST_CONNECTION = "Server=127.0.0.1;Port=3307;User ID=root;Password=<local>"
dotnet test tests/Despacha.IntegrationTests --filter "FullyQualifiedName~MariaDb"
```
