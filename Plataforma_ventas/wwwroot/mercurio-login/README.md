# Mercurio · Login "Gran Tour galáctico"

Pantalla de inicio de sesión de **Mercurio** (Londoño Gómez) con fondo 3D en tiempo real:
viaje desde Neptuno hasta Mercurio con texturas planetarias reales, Vía Láctea de fondo,
zoom (rueda / pellizco / botones) y giro 360° arrastrando. Adaptado a escritorio y celular.

Este paquete es **HTML + CSS + JS sin dependencias de framework**. Abre `index.html` con
cualquier servidor estático para verlo funcionando (no funciona con `file://` porque el
navegador bloquea las texturas):

```bash
npx serve .        # o: python3 -m http.server 8080
```

## Contenido

| Archivo | Qué es |
|---|---|
| `index.html` | Demo completa. Su marcado (`#mercurio-login`) es el que se copia a la app. |
| `login.css` | Estilos del login, con colores de marca en variables `--ml-*` y reglas para celular (≤ 720 px). |
| `login.js` | Conecta la interfaz con la escena 3D: intro, textos del viaje, zoom, mostrar contraseña y envío del formulario. Expone `window.MercurioLogin.init(root, opciones)`. |
| `espacio.js` | Motor 3D (escena, planetas, cámara, zoom y giro). Expone `window.MercurioEspacio`. No hay que tocarlo para integrar. |
| `vendor/three.min.js` | Three.js r159 (build clásico, licencia MIT). Debe cargarse antes de `espacio.js`. |
| `texturas/` | Texturas de planetas (Solar System Scope, CC BY 4.0) y cubo de la Vía Láctea. |
| `docs/` | Capturas de referencia en escritorio y celular. |

Colores de marca: azul `#0076e3` (botón), celeste `#00c9ff` (foco y acentos),
menta `#00d4a1` (estados en vivo), negro `#1e222b` (fondo) y plata `#c3cfdb` (textos secundarios).

## API

```js
const login = MercurioLogin.init(document.getElementById('mercurio-login'), {
  base: '/mercurio-login/',          // URL pública de la carpeta que contiene texturas/ (termina en /)
  intro: true,                       // false = arranca directo en Mercurio
  introUnaVezPorSesion: true,        // recomendado en producción: la intro solo la primera vez
  tono: 'plata',                     // 'plata' (marca) o 'real'
  onSubmit: async ({ usuario, contrasena, recordarme }) => {
    // Llamar al backend real. Lanzar un Error('mensaje') para mostrarlo en la tarjeta.
  },
  onForgot: () => { /* opcional: navegar a recuperar contraseña */ }
});
// Al desmontar la vista (SPA): login.destroy();
```

- Si `onSubmit` no se pasa, el botón solo muestra la animación de carga (modo demo).
- Quitar `data-autoinit` del `<div id="mercurio-login">` cuando se inicialice a mano con `init()`.
- Los `name` del formulario son `usuario`, `contrasena` y `recordarme`.

## Integración (para Claude Code)

1. Copiar `vendor/`, `texturas/`, `espacio.js`, `login.js` y `login.css` a la carpeta de archivos
   estáticos de la app y anotar su URL pública. Ejemplos:
   - **Next.js**: `public/mercurio-login/` → URL `/mercurio-login/`
   - **ASP.NET MVC / Core**: `wwwroot/mercurio-login/` → URL `~/mercurio-login/`
2. Reemplazar la vista de login actual por el marcado de `#mercurio-login` de `index.html`
   (el bloque completo, incluidos `canvas`, `header`, `main`, controles y crédito).
3. Cargar en este orden: `login.css`, `vendor/three.min.js`, `espacio.js` y `login.js`.
   Después llamar a `MercurioLogin.init(...)` con `base` apuntando a la URL del paso 1.
4. Conectar `onSubmit` al flujo de autenticación existente (endpoint, cookies o token y
   redirección). Mostrar los errores del backend lanzando `Error(mensaje)`.
5. Conectar `onForgot` a la ruta de recuperación de contraseña si existe.
6. Activar `introUnaVezPorSesion: true` en producción.

### Notas por framework

- **Next.js (App Router)**: crear un componente cliente (`'use client'`) que renderice el marcado
  en JSX (`class` → `className`, `for` → `htmlFor`, atributos `aria-*` iguales, `<br>` → `<br />`)
  y que en `useEffect` cargue los tres scripts en orden con `next/script` (`strategy="afterInteractive"`)
  o con `document.createElement('script')`, llame a `MercurioLogin.init(ref.current, …)` y devuelva
  `() => login.destroy()` en la limpieza. Importar `login.css` en el layout de la ruta de login.
  No renderizar del lado del servidor nada que dependa de `window`.
- **ASP.NET MVC (Razor)**: poner el marcado en la vista de login (por ejemplo `Views/Account/Login.cshtml`),
  conservar el `@Html.AntiForgeryToken()` si el formulario actual lo usa y, en `onSubmit`, enviar
  `usuario`/`contrasena` por `fetch` al action existente con el token. O, si el login es un POST
  clásico, agregar `action`/`method` (y el token) al `<form>` y no pasar `onSubmit`: `login.js`
  detecta el `action` y deja que el formulario se envíe normal, mostrando la animación de carga.
  Los errores de validación del servidor se pueden pintar en el `<p class="ml-error is-visible">`.

## Rendimiento y accesibilidad

- La escena solo se dibuja cuando está visible y limita la resolución a 1,5×.
- Con "reducir movimiento" activado en el sistema, la intro se salta.
- Si el equipo no soporta WebGL, el login aparece igual sobre el fondo `#1e222b`.
- Peso total aproximado: 4,3 MB (texturas ~2,4 MB, Three.js 0,65 MB), con caché del navegador después de la primera visita.
- Recomendado: probar en el portátil más básico de la empresa y en un celular de gama media.

## Licencias (revisar antes de producción)

- **Texturas planetarias**: Solar System Scope, CC BY 4.0, basadas en datos de la NASA.
  Exige mantener el crédito visible (está en el encabezado y, en celular, al pie).
- **Vía Láctea (`texturas/via-lactea-*.jpg`)**: tomada de los ejemplos de Three.js. Su licencia
  **no está confirmada**. Verificarla o reemplazarla por un mapa con licencia clara (por ejemplo,
  los mapas de estrellas de NASA SVS o el panorama de ESO bajo CC BY 4.0) antes de publicar.
- **Three.js**: MIT.
- Fuentes Inter Tight y JetBrains Mono: Google Fonts (OFL). Se cargan desde `login.css`.
