# Migraciones SQL

Una sola base de datos. Estos scripts agregan tablas y columnas; no eliminan datos existentes.

## Fase 1

`002_empresas_roles.sql`

### Antes de ejecutar en producción

1. Backup de la BD.
2. Verificar si ya existe la tabla `roles` (puede tener `id_rol`, `rol_nombre`, `rol_estado`).
3. Ejecutar sentencia por sentencia si un `ALTER` falla por columna duplicada: comentar esa línea y continuar.
4. No se eliminan ni renombran columnas.

### Qué agrega

- Tabla `empresas` + fila Multilicores (`id_empresa = 1`)
- Roles faltantes: Administrador, Paseador, Domiciliario, Supervisor, Vendedor, Cajero
- `users.id_empresa`, `users.id_rol`, `users.ops_api_token`
- `clientes.id_empresa`
- Backfill suave a empresa `1`

El login de operaciones exige `ops_api_token`.

---

## Fase 2

`003_jornadas_visitas.sql`

Crea (solo si no existen):

- `jornadas`
- `visitas` (visita ≠ pedido; `id_pedido` nullable)
- `jornada_ubicaciones` (recorrido GPS)

Ejecutar después de Fase 1 (`schema.php` → `fase1Listo: true`).

---

## Fase 3

`004_pedido_visita.sql`

Agrega columnas en `pedidos`:

- `ped_origen`
- `id_visita`
- `fecha_entrega_acordada`
- `id_empresa`

El alta desde visita entra por `/sistema/api/ops/pedido_visita.php`.

---

## Fase 4

`005_entregas.sql`

Crea (solo si no existen):

- `entregas` (una por pedido; `num_factura` ≠ `ped_numfac`)
- `entrega_eventos` (historial de estados con usuario, motivo y GPS opcional)

No modifica `pedidos`. La API pone `ped_estado = 'enviado'` cuando la entrega pasa a `salio`.
