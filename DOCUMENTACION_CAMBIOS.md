# Documentación de Cambios — Plataforma Ventas

## Índice
1. [Cambio de Seguridad: Contraseñas (BCrypt)](#1-cambio-de-seguridad-contraseñas-bcrypt)
2. [Actualización de Identidad Visual Corporativa](#2-actualización-de-identidad-visual-corporativa)
3. [Sidebar Blanco + Logo + Navegación Consistente](#3-sidebar-blanco--logo--navegación-consistente)

---

## 1. Cambio de Seguridad: Contraseñas (BCrypt)

### Vulnerabilidad atacada
**OWASP A02:2021 — Fallas Criptográficas**  
**OWASP A07:2021 — Fallas de Identificación y Autenticación**

### ¿Cuál era el problema?
El sistema almacenaba las contraseñas usando **SHA-256 sin sal (salt)**.

SHA-256 es un algoritmo de *hashing general*, no un algoritmo diseñado para contraseñas. Sus problemas son:

- **Velocidad**: puede calcular miles de millones de hashes por segundo con hardware moderno (GPUs).
- **Sin sal**: dos usuarios con la misma contraseña producen el mismo hash → un atacante puede atacar todos los usuarios a la vez.
- **Rainbow tables**: existen bases de datos precomputadas de hashes SHA-256 para contraseñas comunes. Si alguien accede a la BD, puede revertir la mayoría de contraseñas en segundos.

**Ejemplo del ataque:**
```
Contraseña "admin123" → SHA256 → 240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a
Este valor aparece en TODAS las bases de rainbow tables públicas.
```

### ¿Qué se cambió?
Se reemplazó SHA-256 por **BCrypt** (librería `BCrypt.Net-Next`).

BCrypt es un algoritmo diseñado específicamente para contraseñas:
- **Genera una sal automática y aleatoria** por cada contraseña → dos usuarios con la misma contraseña tienen hashes diferentes.
- **Es intencionalmente lento** (factor de coste configurable) → un atacante que robe la BD necesitaría siglos para crackear todas las contraseñas con hardware moderno.
- **Es adaptable**: el factor de coste se puede aumentar conforme el hardware mejora.

**Archivos modificados:**
- `Plataforma_ventas.csproj` — se agrega el paquete `BCrypt.Net-Next v4.0.3`
- `Controllers/AccountController.cs` — se reemplaza `HashSHA256()` por `BCrypt.Net.BCrypt.HashPassword()` y `BCrypt.Net.BCrypt.Verify()`
- `Controllers/UsuariosController.cs` — ídem para creación y reset de contraseñas

**¿Por qué se podía hacer reset limpio?**
Los usuarios existentes eran de prueba (sin datos reales), por lo que se eliminaron para que todos usen el nuevo algoritmo desde el inicio.

### Referencia
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [BCrypt.Net-Next — NuGet](https://www.nuget.org/packages/BCrypt.Net-Next)

---

## 2. Actualización de Identidad Visual Corporativa

### ¿Por qué se hizo?
Se reemplazó la paleta de colores genérica por los colores corporativos oficiales para coherencia de marca en todas las pantallas de la plataforma.

### Paleta anterior vs. nueva

| Variable CSS | Color anterior | Color nuevo | Uso |
|---|---|---|---|
| `--azul` | `#003A70` (azul marino oscuro) | `#0076e3` (azul corporativo) | Color primario de marca |
| `--mid` | `#0055A5` (azul medio) | `#0060bb` (azul profundo) | Gradientes y hover states |
| `--blue` | `#0077C8` (azul estándar) | `#0076e3` (= azul corporativo) | Botones e interactivos |
| `--celeste` | *(no existía)* | `#00c9ff` (celeste corporativo) | Endpoints de gradientes |
| `--negro` | *(no existía)* | `#1e222b` | Sidebar y fondo oscuro |
| `--plata` | *(no existía)* | `#c3cfdb` | Bordes y divisores |
| `--menta` | *(no existía)* | `#00d4a1` | Variable disponible para uso futuro |

> **Nota:** Los colores de estado semántico (`--verde`, `--rojo`, `--oro`) no se modificaron — verde = disponible, rojo = vendido, naranja = reservado son convenciones UX que los usuarios ya reconocen.

### Cambios de diseño adicionales

**Sidebar (19 vistas):**  
El panel lateral usa fondo blanco con acento corporativo azul.

- Fondo: `#fff` + sombra sutil `2px 0 12px rgba(0,0,0,0.04)`
- Nombre del proyecto activo: azul corporativo `#0076e3`
- Ítem activo: fondo `#0076e3`, texto blanco
- Hover: fondo `rgba(0,118,227,0.07)`, texto azul
- Dropdown de proyectos: fondo blanco con sombra elevada

**Gradientes actualizados:**  
- Avatar de usuario (`.sb-av`): `azul → celeste` (`#0076e3 → #00c9ff`)
- Panel hero de Login: `azul → azul-mid → celeste` (`#0076e3 → #0060bb → #00c9ff`)
- rgba de sombras y backgrounds: actualizados de `rgba(0,119,200,…)` a `rgba(0,118,227,…)`

**Archivos modificados:**  
- 19 vistas con sidebar: CSS overrides del sidebar oscuro inyectados
- Todas las vistas del sistema (25 archivos `.cshtml`): variables CSS actualizadas

---

---

## 3. Sidebar Blanco + Logo + Navegación Consistente

### ¿Por qué se hizo?
El sidebar oscuro (`#1e222b`) fue revertido a blanco corporativo. Además se unificaron el logo, el tamaño de los ítems de navegación y la ruta de acceso al selector de proyectos.

### Sidebar blanco

| Elemento | Antes | Después |
|---|---|---|
| Fondo | `#1e222b` (oscuro) | `#fff` + sombra lateral |
| Texto normal | `rgba(255,255,255,0.65)` | `rgba(30,34,43,0.65)` |
| Ítem activo | Azul `#0076e3` + texto blanco | Igual (sin cambio) |
| Nombre proyecto | Texto blanco | Azul corporativo `#0076e3` |
| Dropdown | Fondo `#252c35` oscuro | Fondo blanco + sombra |

### Logo corporativo

**Problema:** Solo la vista Dashboard tenía el logo; las demás 18 vistas mostraban solo texto o un logo base64 inline.

**Solución:** Se agrega `<div class="sb-logo-area">` con el logo `~/Images/Logo azul 2.jpg` en el encabezado del sidebar de todas las vistas. El CSS aplica `mix-blend-mode: multiply` para que el fondo blanco del JPEG desaparezca visualmente y el logo se integre sin verse como una imagen pegada.

### Navegación consistente

**Tamaño uniforme de ítems:** Todos los `.nav-item` tienen `padding: 9px 12px`, `font-size: 14px`, `min-height: 40px` garantizando que ninguna pestaña se vea más grande o pequeña que otra.

**Enlace "Inmuebles" → Selector de proyectos:** En todas las vistas de administrador, el enlace del sidebar que antes iba a `/Inmuebles` (grilla directa) ahora va a `/Inmuebles/Proyectos` (selector). El flujo es:

```
[Nav: Proyectos] → /Inmuebles/Proyectos (selector con tarjetas)
                       ↓ (click en un proyecto)
               [POST] /Inmuebles/SeleccionarProyecto
                       ↓ (guarda en sesión)
               /Inmuebles/Index (grilla del proyecto)
```

El proyecto seleccionado **persiste en sesión** hasta que el usuario seleccione otro desde el selector. Si no hay proyecto activo en sesión, cualquier intento de acceder a `/Inmuebles` redirige automáticamente al selector.

### Archivos modificados
- 19 vistas con sidebar: CSS del sidebar blanco, logo añadido, enlace nav actualizado

---

---

## 4. Eliminación de Registro Público + Endurecimiento de Seguridad (OWASP)

### ¿Por qué se hizo?
El formulario de registro público permitía que cualquier persona creara cuentas de vendedor con solo conocer un código de proyecto — sin verificación ni aprobación administrativa. Se eliminó completamente.

### Cambios aplicados

#### A) Registro público eliminado (OWASP A07 — Broken Access Control)
- **Eliminado:** Acción `Registro` POST en `AccountController.cs`
- **Eliminado:** Acción `VerificarCodigo` POST (validación AJAX de código)
- **Simplificada:** `Views/Account/Login.cshtml` — de 251 líneas con dos formularios a pantalla de solo login
- **Resultado:** Los usuarios solo pueden ser creados por el administrador desde el panel de Usuarios

#### B) Corrección crítica en `RolAutorizadoAttribute` (OWASP A01 — Broken Access Control)
**Problema:** El filtro usaba `OnActionExecuted` (se ejecuta DESPUÉS de la acción) → las acciones protegidas se ejecutaban ANTES de que el filtro verificara el rol. Además, el array `_roles` no se usaba para verificar el rol actual.

**Solución:**
- `OnActionExecuted` → `OnActionExecuting` (verifica ANTES de ejecutar la acción)
- Se agregó `_roles.Contains(rol)` para verificar que el rol del usuario coincida
- Redirección inteligente: admin → Dashboard, vendedor → Vendedor

#### C) Bloqueo de cuenta por intentos fallidos (OWASP A07 — Broken Authentication)
- **5 intentos fallidos** bloquean la cuenta por **15 minutos**
- Implementado con `IMemoryCache` (sin dependencias adicionales)
- Al login exitoso se reinicia el contador de intentos
- Mensajes informativos: indica cuántos intentos quedan antes del bloqueo

#### D) Cabeceras de seguridad HTTP (OWASP A05 — Security Misconfiguration)
Middleware inyectado en `Program.cs` para todas las respuestas:

| Cabecera | Valor | Protege contra |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | MIME sniffing |
| `X-Frame-Options` | `DENY` | Clickjacking |
| `X-XSS-Protection` | `1; mode=block` | XSS reflejado (legacy) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Fuga de URL en Referer |

#### E) Cookie de sesión con SameSite=Strict (OWASP A01 — CSRF)
- `options.Cookie.SameSite = SameSiteMode.Strict` en `AddSession()`
- Complementa `[ValidateAntiForgeryToken]` en todos los 27 POST del sistema

#### F) [ValidateAntiForgeryToken] en todos los POST
Todos los 27 POST del sistema tienen el atributo, distribuidos en 7 controladores:
- `InmueblesController` (9), `VendedorController` (7), `UsuariosController` (4), `DashboardController` (2), `CargaController` (3), `ClientesController` (1), `AccountController` (1)

**Archivos modificados:**
- `Filters/RolAutorizadoAttribute.cs`
- `Controllers/AccountController.cs`
- `Controllers/UsuariosController.cs` + 5 controladores más
- `Program.cs`
- `Views/Account/Login.cshtml`

---

## 5. Nuevo Diseño de Login + Hardening ISO 27001 / OWASP

### Nuevo diseño del Login
Rediseño completo basado en el mockup corporativo:
- **Fondo mosaico reactivo:** grilla de mosaicos azules sobre azul marino (`#07254a`) dibujada en `<canvas>`; los mosaicos cercanos al cursor se iluminan en celeste y crecen con efecto lupa (suavizado con interpolación).
- **Titular editorial** a la izquierda con el kicker "Londoño Gómez" en celeste corporativo.
- **Tarjeta sólida blanca** a la derecha con logo, formulario y animación *shake* cuando hay error de credenciales.
- **Responsive:** en pantallas < 820px se oculta el titular y la tarjeta se centra.

### Hardening adicional (ISO 27001 / OWASP)

#### A) Registro de auditoría de autenticación (ISO 27001 A.8.15 — Logging)
`ILogger` en `AccountController` registra con usuario e IP de origen:
- Login exitoso (usuario + rol)
- Cada intento fallido (con número de intento)
- Bloqueo de cuenta activado
- Intentos sobre cuentas bloqueadas
- Logout

#### B) Protección contra fijación de sesión (OWASP A07)
`HttpContext.Session.Clear()` antes de establecer la identidad autenticada — descarta cualquier estado de sesión previo al login.

#### C) Content-Security-Policy (OWASP A03/A05 — defensa en profundidad contra XSS)
| Directiva | Valor | Efecto |
|---|---|---|
| `default-src` | `'self'` | Solo recursos del propio origen |
| `script-src` | `'self' 'unsafe-inline' cdnjs.cloudflare.com` | Scripts propios + SignalR CDN |
| `style-src` | `'self' 'unsafe-inline' fonts.googleapis.com` | CSS propio + Google Fonts |
| `font-src` | `'self' fonts.gstatic.com` | Fuentes |
| `img-src` | `'self' data:` | Imágenes propias y embebidas |
| `connect-src` | `'self' ws: wss:` | AJAX + WebSockets (SignalR) |
| `frame-ancestors` | `'none'` | Anti-clickjacking (refuerza X-Frame-Options) |
| `form-action` | `'self'` | Formularios solo envían al propio sitio |
| `base-uri` | `'self'` | Bloquea inyección de `<base>` |

> `'unsafe-inline'` se mantiene porque todas las vistas usan CSS/JS inline. Aun así, la CSP bloquea cargas desde cualquier origen no listado.

#### D) Permissions-Policy (OWASP A05)
`camera=(), microphone=(), geolocation=(), payment=()` — desactiva APIs del navegador que la app no usa.

#### E) Endurecimiento del formulario de login
- `maxlength` en usuario (60) y contraseña (128) — limita payloads
- `autocomplete="username"` / `current-password` — integración correcta con gestores de contraseñas
- Botón deshabilitado al enviar — evita doble submit
- Mensajes de error genéricos — no revelan si el usuario existe

**Archivos modificados:**
- `Views/Account/Login.cshtml` (rediseño completo)
- `Controllers/AccountController.cs` (auditoría + anti session-fixation)
- `Program.cs` (CSP + Permissions-Policy)

---

## Historial de versiones

| Fecha | Cambio | Responsable |
|---|---|---|
| 2026-06-09 | Nuevo login mosaico reactivo + CSP + Permissions-Policy + auditoría + anti session-fixation | Claude (IA) |
| 2026-06-04 | Registro eliminado + OWASP: bloqueo de cuenta, cabeceras HTTP, SameSite, RolAutorizado fix, CSRF completo | Claude (IA) |
| 2026-06-03 | Selector de proyectos en Inmuebles + visibilidad global admin | Claude (IA) |
| 2026-06-03 | Migración BCrypt + Paleta corporativa | Claude (IA) |
| 2026-06-03 | Sidebar blanco + logo en todas las vistas + nav consistente | Claude (IA) |

