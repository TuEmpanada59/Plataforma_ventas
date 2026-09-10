# Handoff de diseño — Pantallas de estado y error

## Instrucciones para quien diseña
Diseña un **conjunto de pantallas de estado/error** (404, 500, 503, sesión expirada, mantenimiento y, opcional, 403) para la **Plataforma de Lanzamientos Inmobiliarios de Londoño Gómez** (ASP.NET Core MVC, vistas Razor). Deben verse como parte nativa de la app: misma marca, tipografía, colores y radios que el resto del sistema (definidos abajo). Recrea el diseño en el codebase destino como **una sola plantilla reutilizable** que cambia su contenido (ícono, título, mensaje, acción) según el código de estado — no seis páginas sueltas. Entregar en alta fidelidad, lista para producción, con soporte de impresión no requerido pero sí **responsivo** y **accesible**.

## Fidelidad y alcance
- **Alta fidelidad.** Colores, tipografía, espaciados y radios son finales.
- **6 estados** en una plantilla común. El servidor ya enruta todo a `/Error/{codigo}`, así que el mismo layout recibe el código y decide qué mostrar.
- **Sin datos técnicos** en producción (nada de stack traces): mensajes escritos para el usuario final.
- **Idioma:** español (Colombia).

---

## Marca y contexto
- **Producto:** plataforma web para lanzamientos inmobiliarios; la usan administradores y vendedores (asesores) durante eventos de venta en tiempo real.
- **Personalidad:** corporativa, limpia, confiable, con estética tipo iOS (superficies suaves, tipografía Inter, azules de marca). Nada estridente; el tono en los errores debe **tranquilizar**, no alarmar.
- **Marca:** logo Londoño Gómez (JPG con fondo blanco; sobre superficies oscuras va en un tile blanco redondeado). Si no hay logo disponible, usar el wordmark "Londoño Gómez".

## Design tokens (valores reales del sistema)

**Color**
- Azul de marca: `#006DFE`
- Rampa de azules producto: deep `#003A70` (títulos/números), mid `#0055A5` (hover), **primario/acento `#0077C8`**
- Semánticos iOS: verde `#34C759` / texto `#1EA851`; rojo `#E63946`; naranja `#FF9500` / texto `#CC7700`; violeta `#5A5AC8`
- Neutros (rampa de etiqueta iOS): texto primario `rgba(0,0,0,0.85)`, secundario `rgba(60,60,67,0.6)`, terciario `rgba(60,60,67,0.3)`
- Hairlines: `rgba(60,60,67,0.12)` y `rgba(60,60,67,0.06)`; fills: `rgba(120,120,128,0.08)`
- Canvas: `#EEF2F7` (con un glow azul suave: `radial-gradient(ellipse 80% 60% at 10% 0%, rgba(0,119,200,0.12), transparent 55%)`); superficie/tarjeta: `#FFFFFF` (o glass `rgba(255,255,255,0.75)` con `backdrop-filter: blur(20px) saturate(180%)`)

**Tipografía**
- Familia: **Inter** (300/400/500/600/700), fallback `system-ui, sans-serif`.
- Métricas grandes (código de error): 300, con letter-spacing negativo (−1.5 a −2px).
- Títulos: 600/700, letter-spacing −0.4px.
- Micro-labels en mayúscula con letter-spacing 0.06–0.1em.

**Espaciado, radios, elevación**
- Radios: tarjeta 18px, botones 11–14px, pills 20px, tiles de ícono 12–16px.
- Sombras: tarjeta `0 8px 32px rgba(0,0,0,0.08), 0 2px 8px rgba(0,0,0,0.04)`; botón primario `0 4px 12px rgba(0,119,200,0.28)`.
- Entrada con animación suave (fade-up 0.35s, `cubic-bezier(.4,0,.2,1)`); respetar `prefers-reduced-motion`.
- Íconos: set **Lucide** (stroke 2, `currentColor`, round caps).

---

## Estructura común (una plantilla, seis caras)
Layout centrado en el canvas (no usa el sidebar de la app):

1. **Marca** — logo/wordmark arriba, discreto.
2. **Ícono de estado** — dentro de un tile redondeado (72–88px) con fondo tintado del color semántico del estado (ver tabla). Ícono Lucide en el color de texto correspondiente.
3. **Código/eyebrow** — pequeño, en mayúscula (ej. `ERROR 404`), color terciario.
4. **Título** — grande, 600/700, `#003A70`, `text-wrap: balance`.
5. **Mensaje** — 1–2 líneas, color secundario, ~55–65 caracteres de ancho.
6. **Acción primaria** — botón azul `#0077C8` con la sombra de acento.
7. **Acción secundaria** — enlace/botón fantasma (borde hairline).
8. **Footer discreto** — "Londoño Gómez · Sistema de Lanzamientos".

Comportamiento:
- **Responsivo** (se ve bien en móvil, tablet, desktop; nada se desborda).
- **Tema claro y oscuro** (el sistema es principalmente claro; ofrecer variante oscura del mismo diseño).
- **Accesible:** contraste AA, foco visible en botones, la ilustración/ícono es decorativa (`aria-hidden`).
- El botón "Volver" debe llevar **según el rol** (Dashboard si es administrador, Inicio si es vendedor); si no hay sesión, al login.

---

## Contenido por estado (copy final)

### 1. 404 — Página no encontrada
- **Color/acento:** azul producto (`#0077C8`) · fondo tile `rgba(0,119,200,0.08)`
- **Ícono Lucide:** `map-pin-off` o `compass`
- **Eyebrow:** `ERROR 404`
- **Título:** No encontramos esta página
- **Mensaje:** La página que buscas no existe o fue movida. Verifica el enlace o vuelve a tu panel.
- **Acción primaria:** Volver a mi panel
- **Acción secundaria:** Ir al inicio

### 2. 500 — Error interno
- **Color/acento:** rojo (`#E63946`) · fondo tile `rgba(230,57,70,0.10)`
- **Ícono Lucide:** `alert-triangle`
- **Eyebrow:** `ERROR 500`
- **Título:** Algo salió mal de nuestro lado
- **Mensaje:** Tuvimos un problema procesando tu solicitud. Ya quedó registrado; intenta de nuevo en un momento.
- **Acción primaria:** Reintentar
- **Acción secundaria:** Volver a mi panel

### 3. 503 — El sistema está iniciando ⭐ (la más importante)
- **Color/acento:** naranja (`#CC7700`) · fondo tile `rgba(255,149,0,0.10)`
- **Ícono Lucide:** `loader` o `power` (idealmente con animación de "despertando")
- **Eyebrow:** `SISTEMA INICIANDO`
- **Título:** Estamos encendiendo el sistema
- **Mensaje:** La plataforma estaba en reposo y se está activando. Espera unos segundos y vuelve a intentar.
- **Acción primaria:** Reintentar ahora
- **Detalle de diseño:** incluir un indicador de progreso sutil y **auto-recarga a los 10 segundos** (con un contador visible "reintentando en 10s…"). Esta pantalla convierte el arranque en frío de la base de datos en algo entendible.

### 4. Sesión expirada (400 / antiforgery)
- **Color/acento:** violeta (`#5A5AC8`) · fondo tile `rgba(90,90,200,0.10)`
- **Ícono Lucide:** `clock` o `lock`
- **Eyebrow:** `SESIÓN EXPIRADA`
- **Título:** Tu sesión expiró por seguridad
- **Mensaje:** Por inactividad cerramos tu sesión para proteger tus datos. Inicia sesión de nuevo para continuar.
- **Acción primaria:** Iniciar sesión
- **Acción secundaria:** (ninguna)

### 5. Mantenimiento programado
- **Color/acento:** azul deep (`#003A70`) · fondo tile `rgba(0,58,112,0.08)`
- **Ícono Lucide:** `wrench` o `settings`
- **Eyebrow:** `MANTENIMIENTO`
- **Título:** Estamos actualizando la plataforma
- **Mensaje:** Volvemos en unos minutos con mejoras. Gracias por tu paciencia.
- **Acción primaria:** (ninguna, o "Reintentar")
- **Nota:** pantalla que se "enciende" manualmente durante despliegues a Producción.

### 6. 403 — Acceso denegado (opcional)
- **Color/acento:** naranja (`#CC7700`) · fondo tile `rgba(255,149,0,0.10)`
- **Ícono Lucide:** `shield-alert`
- **Eyebrow:** `ERROR 403`
- **Título:** No tienes acceso a esta sección
- **Mensaje:** Tu cuenta no tiene permiso para ver esta página. Si crees que es un error, contacta a tu administrador.
- **Acción primaria:** Volver a mi panel

---

## Entregables esperados
1. **Una vista/plantilla de estado** reutilizable (Razor + CSS propio, siguiendo `platform.css`), parametrizada por código de estado.
2. Los **6 estados** resueltos con el copy de arriba.
3. Variante **clara y oscura**.
4. Estados **responsivos** y accesibles (AA, foco visible, reduce-motion).
5. La lógica del botón de retorno **según rol/sesión**.
6. Para 503: **auto-recarga con contador** de 10s.

## Tabla de referencia rápida
| Estado | Acento | Ícono | Eyebrow | Acción primaria |
|---|---|---|---|---|
| 404 | Azul `#0077C8` | map-pin-off | ERROR 404 | Volver a mi panel |
| 500 | Rojo `#E63946` | alert-triangle | ERROR 500 | Reintentar |
| 503 | Naranja `#CC7700` | loader/power | SISTEMA INICIANDO | Reintentar ahora (+auto 10s) |
| Sesión | Violeta `#5A5AC8` | clock/lock | SESIÓN EXPIRADA | Iniciar sesión |
| Mantenimiento | Deep `#003A70` | wrench | MANTENIMIENTO | — |
| 403 | Naranja `#CC7700` | shield-alert | ERROR 403 | Volver a mi panel |
