/**
 * Empaqueta la API de operaciones lista para subir a Hostinger.
 * No toca el servidor. No ejecuta SQL.
 *
 * Uso (desde la raíz del repo):
 *   node scripts/pack-ops-fase1.mjs
 *
 * Resultado: carpeta dist-ops-upload/ con la misma estructura relativa a /sistema/
 */
import { cpSync, mkdirSync, rmSync, existsSync, writeFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const out = join(root, 'dist-ops-upload')

const copies = [
  ['sistema/config/database.env.php', 'sistema/config/database.env.php'],
  ['sistema/config/database.env.example', 'sistema/config/database.env.example'],
  ['sistema/models/database.php', 'sistema/models/database.php'],
  ['sistema/services/conexion.php', 'sistema/services/conexion.php'],
  ['sistema/modules', 'sistema/modules'],
  ['sistema/api/ops', 'sistema/api/ops'],
  ['sistema/ops', 'sistema/ops'],
  ['sistema/sql/002_empresas_roles.sql', 'sistema/sql/002_empresas_roles.sql'],
  ['sistema/sql/003_jornadas_visitas.sql', 'sistema/sql/003_jornadas_visitas.sql'],
  ['sistema/sql/004_pedido_visita.sql', 'sistema/sql/004_pedido_visita.sql'],
  ['sistema/sql/005_entregas.sql', 'sistema/sql/005_entregas.sql'],
  ['sistema/sql/README.md', 'sistema/sql/README.md'],
]

if (existsSync(out)) {
  rmSync(out, { recursive: true, force: true })
}
mkdirSync(out, { recursive: true })

const missing = []
for (const [fromRel, toRel] of copies) {
  const from = join(root, fromRel)
  const to = join(out, toRel)
  if (!existsSync(from)) {
    missing.push(fromRel)
    continue
  }
  mkdirSync(dirname(to), { recursive: true })
  cpSync(from, to, { recursive: true })
}

const readme = `# Subir a Hostinger (operaciones)

1. Sube el contenido de esta carpeta respetando rutas bajo public_html (o donde viva /sistema/).
2. Verifica: https://TU-DOMINIO/sistema/api/ops/index.php
3. Verifica: https://TU-DOMINIO/sistema/api/ops/schema.php
4. Backup BD → ejecuta sistema/sql/002_empresas_roles.sql si Fase 1 aún no está en la base.
5. Reabre schema.php hasta fase1Listo=true
6. Prueba login: /sistema/ops/

No subir: .env con secretos, node_modules, admin-app/src (ya va el build en sistema/ops/).
`

writeFileSync(join(out, 'LEEME_SUBIR.txt'), readme, 'utf8')

if (missing.length) {
  console.warn('Faltaron archivos:', missing.join(', '))
}
console.log('Listo:', out)
console.log('Siguiente: sube dist-ops-upload/sistema/ a Hostinger.')
