# SQL Variante B

## Archivo Fase 1

`002_variante_b_fase1_empresas_roles.sql`

### Antes de ejecutar en producción

1. Backup de la BD.
2. Verificar si ya existe la tabla `roles` (puede tener `id_rol`, `rol_nombre`, `rol_estado`).
3. Ejecutar sentencia por sentencia si un `ALTER` falla por columna duplicada: comentar esa línea y continuar.
4. **No** se eliminan ni renombran columnas.

### Qué agrega

- Tabla `empresas` + fila Multilicores (`id_empresa = 1`)
- Roles faltantes: Administrador, Paseador, Domiciliario, Supervisor, Vendedor, Cajero
- `users.id_empresa`, `users.id_rol`, `users.ops_api_token`
- `clientes.id_empresa`
- Backfill suave a empresa `1`

### Confirmación requerida

No asumir que este script ya corrió en Hostinger. Auth ops exige `ops_api_token`.

---

## Archivo Fase 2 (documentado; no ejecutar aún)

`003_variante_b_fase2_jornadas_visitas.sql`

Crea (solo si no existen):

- `jornadas`
- `visitas` (visita ≠ pedido; `id_pedido` nullable)
- `jornada_ubicaciones` (recorrido GPS)

**Ejecutar solo después** de Fase 1 lista (`schema.php` → `fase1Listo: true`) y con OK explícito.

---

## Archivo Fase 3 (documentado; no ejecutar aún)

`004_variante_b_fase3_pedido_visita.sql`

Agrega columnas aditivas en `pedidos`:

- `ped_origen`
- `id_visita`
- `fecha_entrega_acordada`
- `id_empresa`

No altera `POST /sistema/api/pedido.php`. El alta desde visita irá por `/sistema/api/ops/`.

---

## Archivo Fase 4 (documentado; no ejecutar aún)

`005_variante_b_fase4_entregas.sql`

Crea (solo si no existen):

- `entregas` (una por pedido; `num_factura` ≠ `ped_numfac`)
- `entrega_eventos` (historial de estados con usuario, motivo y GPS opcional)

No modifica `pedidos`. La API pondrá `ped_estado = 'enviado'` cuando la entrega pase a `salio`.
