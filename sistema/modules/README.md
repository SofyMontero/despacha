# Módulos Variante B

Dominio nuevo de la plataforma. **No reemplaza** `sistema/models/` ni el admin PHP de Variante A.

## Reglas

- Código de negocio nuevo vive aquí (jornadas, visitas, entregas, tracking, empresas).
- La API pública de estos módulos entra por `sistema/api/ops/`.
- No modificar contratos de `sistema/api/*.php` (catálogo actual).
- Preferir clases pequeñas por dominio; no un “god module”.

## Dominios previstos (aún no implementados)

| Carpeta | Responsabilidad |
|---------|-----------------|
| `Empresa/` | Organizaciones / modelo de negocio |
| `Auth/` | Login ops, token, RBAC |
| `Jornada/` | Turnos del paseador |
| `Visita/` | Visitas a tienda (con o sin pedido) |
| `Entrega/` | Logística domiciliario ≠ pedido |
| `Tracking/` | Persistencia GPS (infra compartida) |

Las carpetas se crean cuando arranque cada fase, no de antemano vacías sin uso.
