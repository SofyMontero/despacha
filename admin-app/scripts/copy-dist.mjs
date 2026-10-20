import { cpSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const dist = join(root, 'dist')
const target = join(root, '..', 'sistema', 'ops')

rmSync(target, { recursive: true, force: true })
mkdirSync(target, { recursive: true })
cpSync(dist, target, { recursive: true })

// SPA fallback en Apache/Hostinger
writeFileSync(
  join(target, '.htaccess'),
  `RewriteEngine On
RewriteBase /sistema/ops/
RewriteRule ^index\\.html$ - [L]
RewriteCond %{REQUEST_FILENAME} !-f
RewriteCond %{REQUEST_FILENAME} !-d
RewriteRule . /sistema/ops/index.html [L]
`,
)

console.log('Copiado dist → sistema/ops')
