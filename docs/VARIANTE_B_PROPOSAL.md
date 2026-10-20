# Variante B — Propuesta y control de cambios

Documento de control. **No implica que los cambios ya estén aplicados.**  
Cada ítem se marca solo cuando ese cambio concreto esté hecho y revisado.

Referencia visual: canvas `variante-b-arquitectura` en Cursor.

---

## Decisiones base (aprobar antes de código)

- [x] Prefijo API nueva: `/sistema/api/ops/` *(asumido al iniciar Fase 0; confirmar o corregir)*
- [x] Admin React: `/sistema/ops/` (PHP admin sigue vivo) *(asumido; confirmar o corregir)*
- [x] Empresa default `id=1` Multilicores; columnas `id_empresa` nullable + backfill
- [x] Factura real en `entregas.num_factura` (no reutilizar `ped_numfac`)
- [ ] Validar tabla `roles` en phpMyAdmin antes de migrar (ampliar, no recrear) — **pendiente en BD**
- [x] Una sola PWA multi-rol en `catalogo-app`
- [x] Empezar por Fase 0 + 1 (sin GPS aún)

---

## Reglas de ejecución

1. Un cambio pequeño por turno; esperar OK del usuario antes del siguiente.
2. No modificar contratos de `sistema/api/pedido.php`, `cliente.php`, etc.
3. No cambiar comportamiento de `sistema/views/solicitudes.php` salvo aditivo documentado.
4. BD: solo tablas/columnas/índices nuevos; documentar SQL antes de ejecutar.
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

**Criterio de cierre:** Variante A sigue igual; `/sistema/api/ops/` responde health; admin PHP intacto.  
**Estado:** Fase 0 **cerrada en código** (verificado en disco 2026-10-07).

---

## Fase 1 — Empresas + roles/permisos

- [ ] Validar esquema actual `roles` / `users.rol` en BD **(acción manual en phpMyAdmin)**
- [x] SQL aditivo documentado: `sistema/sql/002_variante_b_fase1_empresas_roles.sql`
- [x] Código catálogo roles / empresas / auth ops en `modules/` + `api/ops/` (graceful si falta tabla)
- [x] Ejecutar SQL en Hostinger (usuario, 2026-10-11)
- [x] Probar login ops end-to-end (usuario, 2026-10-11)
- [x] Seed empresa Multilicores `id=1` (incluido en SQL)

**Criterio de cierre:** un usuario ops puede autenticarse; A no cambia.

---

## Fase 2 — Jornadas y visitas

- [x] SQL documentado: `sistema/sql/003_variante_b_fase2_jornadas_visitas.sql`
- [x] Ejecutar SQL (usuario, 2026-10-11)
- [x] Prueba en producción: visitas OK (usuario, 2026-10-11)
- [x] API ops jornadas: iniciar/finalizar/actual/listar (`api/ops/jornadas.php`) — local
- [x] API ops visitas: registrar / listar por jornada (`api/ops/visitas.php`) — local

- [x] Pantallas paseador (mínimo): jornada + visita sin pedido (`/paseador`) — local
- [x] Admin ops: listar jornadas de hoy en shell — local

**Criterio de cierre:** visita sin pedido funciona end-to-end en B (tras SQL 002+003 en Hostinger).

---

## Fase 3 — Pedidos desde visita

- [x] SQL documentado: `004_variante_b_fase3_pedido_visita.sql`
- [x] Ejecutar SQL (usuario, 2026-10-11)
- [x] Prueba en producción: pedido desde visita OK (usuario, 2026-10-11)
- [x] `POST /ops/pedido_visita.php` (PedidoDesdeVisita → Pedido::crearPedido) — local
- [x] No alterar contrato `POST /sistema/api/pedido.php`
- [x] UI paseador: crear pedido + fecha acordada (`/paseador`) — local



**Criterio de cierre:** pedido catálogo A sigue igual; pedido desde visita queda trazable.

---

## Fase 4 — Entregas y domiciliario

- [x] Decisiones (usuario, 2026-10-11): al pasar a `salio` → `ped_estado='enviado'`; estados extra `no_entregado` y `cancelado` (con motivo); disponible apenas el pedido está aceptado
- [x] SQL documentado: `005_variante_b_fase4_entregas.sql` (`entregas` + `entrega_eventos`) — **NO ejecutado**
- [ ] Ejecutar SQL (tras OK)
- [ ] Estados: disponible → aceptado → salio → en_camino → entregado (+ no_entregado / cancelado)
- [x] API ops domiciliario + asignación admin (`modules/Entrega/EntregaRepository.php`, `api/ops/entregas.php`) — local, sin probar
- [ ] Probar API en Hostinger (tras SQL 005)
- [ ] Separación clara Pedido ≠ Entrega
- [ ] UI domiciliario mínima

**Criterio de cierre:** entrega operativa en B sin tocar flujo `enviado` de A (o convivencia documentada).

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

- [ ] Checklist regresión Variante A
- [ ] Preparar DB B opcional vía env
- [ ] Plan de activación ops como admin principal

---

## Registro de cambios aplicados

| Fecha | Fase | Cambio | Archivos | ¿Afecta A? | OK usuario |
|-------|------|--------|----------|------------|------------|
| 2026-10-07 | 0.1 | Ejemplo env DB (solo docs) | `sistema/config/database.env.example` | No | Pendiente revisión |
| 2026-10-07 | 0.2 | getenv + fallback (ya en árbol) | `config/database.env.php`, `models/database.php`, `services/conexion.php` | No (fallback = mismos valores) | Verificado en disco |
| 2026-10-07 | 0.3 | Carpeta módulos B | `sistema/modules/README.md` | No | Pendiente revisión |
| 2026-10-07 | 0.4–0.5 | Ops API + admin-app (ya en árbol) | `api/ops/*`, `admin-app/`, `sistema/ops/` | No | Verificado en disco |
| 2026-10-07 | 1 (docs) | SQL Fase 1 listo, sin ejecutar | `sql/002_variante_b_fase1_empresas_roles.sql` | Solo si se corre en BD | **Esperando OK** |

---

## Próximo paso sugerido

**Fase 0 cerrada en código.**

**Bloqueo verificado (2026-10-08):**  
`/sistema/api/ops/index.php` y `schema.php` → **404** en producción. Falta subir código.

**Pack listo en local:** carpeta `dist-ops-upload/` (generada con `node scripts/pack-ops-fase1.mjs`).

**Checklist:**

1. Sube `dist-ops-upload/sistema/` a Hostinger (misma ruta `/sistema/`).
2. Abre `/sistema/api/ops/index.php` y `/sistema/api/ops/schema.php`.
3. Backup BD + SQL `002_variante_b_fase1_empresas_roles.sql`.
4. `schema.php` → `fase1Listo: true`.
5. Login en `/sistema/ops/`.

Respuesta esperada: `Ops subido` o JSON de schema.
