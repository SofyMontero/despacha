# Catálogo cliente (React + PWA)

App del catálogo para clientes. Consume la API PHP en `../sistema/api/`.
Producto **Monteblanco** · cliente **Multilicores**.

## Desarrollo

```bash
cd catalogo-app
npm install
npm run dev
```

Abre: `http://localhost:5173/sistema/app/`

## Build y despliegue (Hostinger)

```bash
npm run build:deploy
```

Sube `sistema/app/` al hosting (incluye `.htaccess`, manifest y service worker).

URL:

`https://multilicoreschapinero.com/sistema/app/?idCli=57XXXXXXXXXX`

Activa la app con `'version' => 'react'` en `sistema/config/catalogo.php`.

## PWA (paso 3)

Tras desplegar en **HTTPS**:

1. Abre el catálogo en Chrome/Android.
2. El navegador puede mostrar “Instalar app” (también hay banner en la app).
3. En iPhone: Compartir → **Añadir a pantalla de inicio**.

Incluye:

- Manifest + iconos Multilicores
- Service worker (caché de app + API/imágenes)
- Splash/theme color `#1d4e89`

## Capacitor / Android

La shell nativa carga la URL de producción (mismo deploy PWA).

```bash
# Primera vez (requiere Android Studio)
npx cap add android
npm run cap:sync
npm run cap:open
```

Luego genera el APK/AAB desde Android Studio.

## API

| Endpoint | Método | Uso |
|----------|--------|-----|
| `/sistema/api/categorias.php` | GET | Categorías |
| `/sistema/api/productos.php` | GET | Productos |
| `/sistema/api/promociones.php` | GET | Promos |
| `/sistema/api/cliente.php` | GET/POST | Cliente |
| `/sistema/api/pedido.php` | POST | Pedido |
