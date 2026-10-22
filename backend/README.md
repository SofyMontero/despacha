# Despacha — backend .NET 10 (Fase 4A)

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

## Ambientes y bloqueos

| `Despacha:Environment` | Host ASP.NET | Bases permitidas |
|---|---|---|
| `Local` (defecto) | cualquiera menos Production | Solo loopback y bases sin "prod" |
| `Test` | cualquiera | Solo los servidores de `Database:AllowedHosts` y bases sin "prod" |
| `Prod` | solo Production | Bases sin "test" ni "local" |

Las combinaciones inválidas, los orígenes CORS con comodín y la falta de `SigningKey` fuera de Local impiden el arranque.

## Incompatibilidad técnica: proveedor MySQL de EF Core 10

- `Pomelo.EntityFrameworkCore.MySql` no tiene versión para EF Core 10; la última es la 9.0.0.
- Se usa `MySql.EntityFrameworkCore` 10.0.12 (Oracle), que depende de EF Core 10.0.12. El cambio de proveedor queda aislado en `Shared/Persistence/DespachaMySql.cs`.
- Riesgo: Oracle no da soporte oficial a MariaDB. Si el servidor final es MariaDB, hay que probarlo o pasar a Pomelo cuando publique la versión 10.
- La prueba mínima (`MySqlProviderCompatibilityTests`) genera el DDL sin conectarse. Falta probarlo contra un servidor real.
