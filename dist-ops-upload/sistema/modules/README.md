# Módulos de operaciones

Dominio de jornadas, visitas, entregas, empresas y roles. Misma aplicación y misma base que `sistema/models/`.

## Reglas

- El código de este dominio vive aquí.
- La API entra por `sistema/api/ops/`.
- Preferir clases pequeñas por dominio.

## Dominios

| Carpeta | Responsabilidad |
|---------|-----------------|
| `Empresa/` | Organizaciones / modelo de negocio |
| `Auth/` | Login de operaciones, token, RBAC |
| `Jornada/` | Turnos del paseador |
| `Visita/` | Visitas a tienda (con o sin pedido) |
| `PedidoOps/` | Pedido creado desde una visita |
| `Entrega/` | Logística del domiciliario |
| `Tracking/` | Persistencia GPS (aún no implementado) |
