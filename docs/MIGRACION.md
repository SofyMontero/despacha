# Migración — control de cambios

Documento de control. Cada ítem se marca solo cuando ese cambio concreto esté hecho y revisado.

## Decisión 2026-10-19 — una sola versión

Este repositorio es el producto único. No hay Variante A ni Variante B, ni una segunda base de datos.

- Catálogo, admin PHP, admin React y operaciones usan la base del ambiente activo.
- `DB_NAME_OPS` y `new Database('ops')` quedaron retirados.
- Ambientes: `APP_ENV=prod` (base de producción, es el defecto en Hostinger) y `APP_ENV=dev` (base `u633742531_Multi_test`, credenciales en `sistema/config/.env.dev`, fuera de git).
- `/sistema/api/ops/` sigue siendo la ruta de operaciones (jornadas, visitas, entregas), dentro de la misma aplicación.
- El interruptor PHP/React del catálogo cliente (`sistema/config/catalogo.php`) sigue existiendo. No forma parte de esta unificación.

---

## Decisiones base

- [x] Prefijo API de operaciones: `/sistema/api/ops/`
- [x] Admin React: `/sistema/ops/` (el admin PHP sigue en `sistema/views/`)
- [x] Una sola base de datos
- [x] Empresa default `id=1` Multilicores; columnas `id_empresa` nullable + backfill
- [x] Factura real en `entregas.num_factura` (no reutilizar `ped_numfac`)
- [ ] Validar tabla `roles` en phpMyAdmin antes de migrar (ampliar, no recrear) — **pendiente en BD**
- [x] Una sola PWA multi-rol en `catalogo-app`
- [x] Empezar por Fase 0 + 1 (sin GPS aún)

---

## Reglas de ejecución

1. Un cambio pequeño por turno; esperar OK del usuario antes del siguiente.
2. Una sola base por ambiente. `prod` y `dev` no se mezclan. No volver a separar conexión por “variante”.
3. El contrato de `sistema/api/pedido.php` y `cliente.php` se cambia solo junto con la pantalla que lo usa.
4. BD: documentar SQL antes de ejecutar. Preferir tablas y columnas nuevas.
5. Credenciales: preparar env/fallback; no dejar secretos nuevos hardcodeados.

---

## Fase 0 — Aislamiento + config DB

Objetivo: estructura y configuración, **sin lógica de negocio**.

- [x] Crear `sistema/config/database.env.example` (variables documentadas)
- [x] Adaptar `Database` con getenv + fallback a valores actuales (sin romper Hostinger) — vía `config/database.env.php`
- [x] Crear carpeta `sistema/modules/` (vacía o con README de dominio)
- [x] Crear carpeta `sistema/api/ops/` + health `index.php` (no toca API v1)
- [x] Scaffold `admin-app/` (Vite React) con deploy target `/sistema/ops/`
- [x] Documentar en este archivo qué se tocó y qué no

**Criterio de cierre:** `/sistema/api/ops/` responde health; admin PHP intacto.  
**Estado:** Fase 0 **cerrada en código** (verificado en disco 2026-10-07).

---

## Fase 1 — Empresas + roles/permisos

- [ ] Validar esquema actual `roles` / `users.rol` en BD **(acción manual en phpMyAdmin)**
- [x] SQL aditivo documentado: `sistema/sql/002_empresas_roles.sql`
- [x] Código catálogo roles / empresas / auth ops en `modules/` + `api/ops/` (graceful si falta tabla)
- [x] Ejecutar SQL en Hostinger (usuario, 2026-10-11)
- [x] Probar login ops end-to-end (usuario, 2026-10-11)
- [x] Seed empresa Multilicores `id=1` (incluido en SQL)

**Criterio de cierre:** un usuario de operaciones puede autenticarse.

---

## Fase 2 — Jornadas y visitas

- [x] SQL documentado: `sistema/sql/003_jornadas_visitas.sql`
- [x] Ejecutar SQL (usuario, 2026-10-11)
- [x] Prueba en producción: visitas OK (usuario, 2026-10-11)
- [x] API ops jornadas: iniciar/finalizar/actual/listar (`api/ops/jornadas.php`) — local
- [x] API ops visitas: registrar / listar por jornada (`api/ops/visitas.php`) — local

- [x] Pantallas paseador (mínimo): jornada + visita sin pedido (`/paseador`) — local
- [x] Admin ops: listar jornadas de hoy en shell — local

**Criterio de cierre:** visita sin pedido funciona end-to-end (tras SQL 002+003 en Hostinger).

---

## Fase 3 — Pedidos desde visita

- [x] SQL documentado: `004_pedido_visita.sql`
- [x] Ejecutar SQL (usuario, 2026-10-11)
- [x] Prueba en producción: pedido desde visita OK (usuario, 2026-10-11)
- [x] `POST /ops/pedido_visita.php` (PedidoDesdeVisita → Pedido::crearPedido) — local
- [x] No alterar contrato `POST /sistema/api/pedido.php`
- [x] UI paseador: crear pedido + fecha acordada (`/paseador`) — local



**Criterio de cierre:** el pedido del catálogo sigue igual; el pedido desde visita queda trazable.

---

## Fase 4 — Entregas y domiciliario

- [x] Decisiones (usuario, 2026-10-11): al pasar a `salio` → `ped_estado='enviado'`; estados extra `no_entregado` y `cancelado` (con motivo); disponible apenas el pedido está aceptado
- [x] SQL documentado: `005_entregas.sql` (`entregas` + `entrega_eventos`) — **NO ejecutado**
- [ ] Ejecutar SQL (tras OK)
- [ ] Estados: disponible → aceptado → salio → en_camino → entregado (+ no_entregado / cancelado)
- [x] API ops domiciliario + asignación admin (`modules/Entrega/EntregaRepository.php`, `api/ops/entregas.php`) — local, sin probar
- [ ] Probar API en Hostinger (tras SQL 005)
- [ ] Separación clara Pedido ≠ Entrega
- [ ] UI domiciliario mínima

**Criterio de cierre:** entrega operativa. Al pasar a `salio`, el pedido queda `enviado`.

---

## Fase 5 — GPS tracking

- [ ] `jornada_ubicaciones` / `entrega_ubicaciones`
- [ ] Frecuencia MVP + batch + no bloquear offline
- [ ] Endpoints tracking con ownership
- [ ] Retención/purga documentada

---

## Fase 6 — Centro de operaciones React

- [ ] Dashboard + mapa vivo
- [ ] Detalle paseador / domiciliario
- [ ] Historial por fecha

---

## Fase 7 — Seguimiento cliente (app)

- [ ] Pantalla seguimiento autorizada por teléfono/pedido
- [ ] Mapa solo en `en_camino` de su entrega

---

## Fase 8 — Offline / sync

- [ ] Cola local eventos/ubicaciones
- [ ] Reintento al recuperar red

---

## Fase 9 — Pruebas y corte gradual

- [ ] Checklist de regresión del catálogo y del admin
- [x] Una sola base de datos (sin `DB_NAME_OPS`)
- [ ] Plan de activación del admin de operaciones como admin principal

---

## Registro de cambios aplicados

| Fecha | Fase | Cambio | Archivos | ¿Afecta A? | OK usuario |
|-------|------|--------|----------|------------|------------|
| 2026-10-07 | 0.1 | Ejemplo env DB (solo docs) | `sistema/config/database.env.example` | No | Pendiente revisión |
| 2026-10-07 | 0.2 | getenv + fallback (ya en árbol) | `config/database.env.php`, `models/database.php`, `services/conexion.php` | No (fallback = mismos valores) | Verificado en disco |
| 2026-10-07 | 0.3 | Carpeta módulos B | `sistema/modules/README.md` | No | Pendiente revisión |
| 2026-10-07 | 0.4–0.5 | Ops API + admin-app (ya en árbol) | `api/ops/*`, `admin-app/`, `sistema/ops/` | No | Verificado en disco |
| 2026-10-07 | 1 (docs) | SQL Fase 1 listo, sin ejecutar | `sql/002_empresas_roles.sql` | Solo si se corre en BD | **Esperando OK** |
| 2026-10-19 | — | Una sola versión: misma BD, sin Variante B | `database.env.php`, `models/database.php`, módulos, `sql/*.sql` | No cambia datos | Hecho en este repo |
| 2026-10-19 | — | Ambientes prod y dev | `config/database.env.php`, `.env`, `.env.dev` | dev apunta a Multi_test; prod no cambia si no se sube `.env` | Hecho en este repo |

---

## Próximo paso sugerido

**Fase 0 cerrada en código.**

**Bloqueo verificado (2026-10-08):**  
`/sistema/api/ops/index.php` y `schema.php` → **404** en producción. Falta subir código.

**Pack listo en local:** carpeta `dist-ops-upload/` (generada con `node scripts/pack-ops-fase1.mjs`).

**Checklist:**

1. Sube `dist-ops-upload/sistema/` a Hostinger (misma ruta `/sistema/`).
2. Abre `/sistema/api/ops/index.php` y `/sistema/api/ops/schema.php`.
3. Backup BD + SQL `002_empresas_roles.sql` si Fase 1 aún no está aplicada.
4. `schema.php` → `fase1Listo: true`.
5. Login en `/sistema/ops/`.

Respuesta esperada: `Ops subido` o JSON de schema.
