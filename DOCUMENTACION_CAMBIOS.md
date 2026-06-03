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

## Historial de versiones

| Fecha | Cambio | Responsable |
|---|---|---|
| 2026-06-03 | Selector de proyectos en Inmuebles + visibilidad global admin | Claude (IA) |
| 2026-06-03 | Migración BCrypt + Paleta corporativa | Claude (IA) |
| 2026-06-03 | Sidebar blanco + logo en todas las vistas + nav consistente | Claude (IA) |

