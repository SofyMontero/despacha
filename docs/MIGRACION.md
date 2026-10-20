# Sistema

`sistema/` es la única aplicación y la guía del producto en el subdominio.

No hay versión B: no existe API `/ops`, admin en `/sistema/ops/`, módulos paralelos ni paquete de subida aparte.

## Ambientes

- `prod`: base de producción. Es el defecto si no se sube ningún `.env`.
- `dev`: base `u633742531_Multi_test`. Credenciales en `sistema/config/.env.dev`, fuera de git.

No subir `.env` ni `.env.dev` a producción.
