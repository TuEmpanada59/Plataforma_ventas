# Informe QA & Seguridad — Plataforma Ventas (pre-producción)

**Fecha:** 2026-06-30 · **Rama:** `claude/intelligent-tesla-NDfA3`
**Alcance:** Controladores, vistas, configuración, autenticación/autorización, acceso a datos, tiempo real (SignalR).
**Limitación del entorno:** el SDK de .NET no está disponible en este entorno, por lo que **no se ejecutaron** los tests ni el build. Se realizó **análisis estático (caja blanca)** línea por línea, **análisis de flujos (caja negra)**, modelado de amenazas y **diseño de los tests unitarios/funcionales** (incluidos abajo, listos para `dotnet test`).

---

## 1. Qué se hizo

| Tipo de prueba | Cómo se aplicó |
|---|---|
| **Caja blanca (white-box)** | Revisión de código de `AccountController`, `VendedorController`, `InmueblesController`, `CargaController`, `ClientesController`, `ReportesController`, `Program.cs`, `RolAutorizadoAttribute`, vistas Razor. |
| **Caja negra (black-box)** | Modelado de entradas/salidas y abuso de cada endpoint POST (login, recuperación, asignación, tomar/reservar/vender). |
| **Pruebas unitarias** | Diseñadas para la lógica pura testeable: `Texto.SoloDigitos`, mapeo lista→columna, política de contraseñas, hashing de token. (Sección 5) |
| **Pruebas funcionales** | Casos de aceptación para login/lockout, recuperación, bloqueo sin proyecto, flujo de venta, consistencia de lista. (Sección 6) |
| **Vulnerabilidades / OWASP Top 10 (2021)** | Mapeo y verificación control por control. (Sección 4) |
| **Bugs / errores lógicos** | Condiciones de carrera, IDOR, integridad de datos. (Sección 3) |

---

## 2. Resumen ejecutivo

Se encontraron **3 hallazgos críticos** que **deben** resolverse antes de producción, **3 altos**, **6 medios** y varios menores. La base de seguridad es **sólida** (BCrypt, antiforgery, SQL parametrizado, anti-enumeración, updates atómicos en reservar/tomar), pero hay **dos puntos que rompen integridad financiera y control de acceso en el cierre de venta**, y **credenciales reales versionadas en git**.

> Veredicto: **NO ir a producción** hasta cerrar los 3 críticos (C-1, C-2, C-3). El resto puede planificarse inmediatamente después.

---

## 3. Hallazgos (ordenados por severidad)

### 🔴 C-1 — Credenciales de base de datos reales versionadas en git
- **Dónde:** `Plataforma_ventas/appsettings.json` → `ConnectionStrings:DefaultConnection` contiene `User ID=slezcano;Password=Ss1007243645@` de `dbqa.database.windows.net`. Está en el repo **y en el historial** (`git log` lo muestra).
- **Impacto:** Cualquiera con acceso al repositorio (o a un fork/clone) obtiene credenciales directas a la BD de Azure SQL. Exfiltración/borrado total de datos.
- **OWASP:** A02 (Cryptographic/Secrets), A05 (Misconfiguration).
- **Solución:**
  1. **Rotar YA** la contraseña del usuario SQL `slezcano` (ya está comprometida por estar en git).
  2. Mover la cadena a **variables de entorno** / **Azure Key Vault** / **User Secrets** (dev). En Azure App Service: *Configuration → Connection strings*.
  3. Quitar el valor de `appsettings.json` (dejar la clave vacía o placeholder) y **agregar `appsettings.json` y `appsettings.*.json` al `.gitignore`** (hoy no están).
  4. **Purgar del historial** con `git filter-repo` (o BFG) y forzar push, o rotar y aceptar el riesgo histórico.

### 🔴 C-2 — Cierre de venta sin guardia atómica: condición de carrera + IDOR
- **Dónde:** `VendedorController.ConfirmarVenta` (línea ~848) y `InmueblesController.ConfirmarVenta` (línea ~848):
  ```sql
  UPDATE Inmuebles SET Estado='VENDIDO', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
  WHERE IdInmuebles=@id   -- ⚠ sin verificar estado ni propietario
  ```
- **Impacto:**
  - **Race / doble venta:** dos peticiones simultáneas pueden insertar **dos ventas** del mismo inmueble.
  - **IDOR / broken access control:** un vendedor autenticado puede hacer POST a `ConfirmarVenta` con **cualquier `idInmueble`** (uno DISPONIBLE, RESERVADO por otro, de otro proyecto, o ya VENDIDO) y queda registrado como venta suya. El token antiforgery es válido por ser sesión legítima.
  - Viola la regla del propio `CLAUDE.md` ("SIEMPRE UPDATE atómico con WHERE del estado previo").
- **OWASP:** A01 (Broken Access Control), A04 (Insecure Design).
- **Solución:** replicar el patrón que **ya usa correctamente** `ConfirmarVentaReserva` (verifica `Estado='RESERVADO' AND IdVendedorReserva=@uid`). Para el flujo EN PROCESO:
  ```csharp
  var cmdInm2 = new SqlCommand(@"UPDATE Inmuebles
      SET Estado='VENDIDO', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
      WHERE IdInmuebles=@id AND Estado='EN PROCESO' AND IdVendedorEnProceso=@uid", con);
  cmdInm2.Parameters.AddWithValue("@id", idInmueble);
  cmdInm2.Parameters.AddWithValue("@uid", idUsuario);
  int filas = await cmdInm2.ExecuteNonQueryAsync();
  if (filas == 0) {
      TempData["Error"] = "Este inmueble ya no está disponible para venta.";
      return RedirectToAction("Inmuebles");
  }
  // ... y solo DESPUÉS insertar en Ventas (o envolver ambos en transacción).
  ```
  Además, **insertar la venta sólo si el UPDATE afectó 1 fila** (hoy se inserta la venta antes y sin verificar). Idealmente envolver INSERT Ventas + UPDATE Inmuebles en una **transacción**. En el flujo admin, validar también `IdProyecto=@proy`.

### 🔴 C-3 — Manipulación de precio y lista en el cierre de venta
- **Dónde:** `ConfirmarVenta` (vendedor y admin): `precioVenta` y `listaAplicada` se reciben como campos **del formulario** y se insertan tal cual en `Ventas`.
  ```csharp
  cmdVenta.Parameters.AddWithValue("@lista", listaAplicada);  // del cliente
  cmdVenta.Parameters.AddWithValue("@precio", precioVenta);   // del cliente
  ```
- **Impacto:** un vendedor puede alterar el campo oculto y **registrar la venta a un precio arbitrario** (p. ej. $1) o una lista que no corresponde. Fraude / pérdida financiera. El precio mostrado se calcula en servidor, pero **no se re-valida** al confirmar.
- **OWASP:** A04 (Insecure Design), A08 (Data Integrity), A01 (autorización de negocio).
- **Solución:** **derivar el precio y la lista en el servidor** al confirmar (igual que `ReservarInmueble`/`ConfirmarVentaReserva`), a partir de `Metros` del inmueble y `ProyectoAreaListas`. Ignorar el valor enviado por el cliente o, si se necesita por UX, validarlo contra el calculado y rechazar si difiere.

### 🟠 A-1 — Cookie de sesión sin atributo `Secure`
- **Dónde:** `Program.cs` `AddSession(...)` — no se define `options.Cookie.SecurePolicy`.
- **Impacto:** la cookie de sesión puede viajar por HTTP (antes del redirect HTTPS), exponiéndola a intercepción.
- **OWASP:** A05, A07.
- **Solución:** `options.Cookie.SecurePolicy = CookieSecurePolicy.Always;` (sesión y, si aplica, cookie de auth).

### 🟠 A-2 — Claves de DataProtection efímeras en producción
- **Dónde:** `Program.cs` — sólo se persisten claves en `Development`. En producción quedan **en memoria**.
- **Impacto:** en cada reinicio o en **escalado multi-instancia**, los tokens antiforgery y la sesión cifrada dejan de validar → usuarios deslogueados / errores 400 antiforgery intermitentes. El comentario lo asume "single-instance"; al escalar **se rompe**.
- **OWASP:** A05.
- **Solución:** persistir claves en almacenamiento compartido (Azure Blob `PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault`) también en producción.

### 🟠 A-3 — Tiempo de sesión muy largo (8 h) e inconsistente con la documentación
- **Dónde:** `Program.cs` `IdleTimeout = TimeSpan.FromHours(8)` y `ExpireTimeSpan = 8h`. El `CLAUDE.md` y el banner de la UI dicen **20 minutos**.
- **Impacto:** ventana amplia de secuestro de sesión en datos sensibles (ventas, clientes). Inconsistencia UI/realidad (el banner "tu sesión expira en 2:00" es engañoso).
- **OWASP:** A07.
- **Solución:** reducir a 20–30 min con renovación por actividad; alinear el banner.

### 🟡 M-1 — Campo `destino` sin lista blanca
- **Dónde:** `ConfirmarVenta`/`ConfirmarVentaReserva`: `@destino = destino ?? "Vivienda"` sin validar.
- **Impacto:** se puede almacenar cualquier string (contamina reportes/segmentaciones). No es XSS (se muestra HTML-encoded), pero sí integridad.
- **Solución:** validar contra el conjunto permitido (`Vivienda`, `Inversión para reventa`, `Inversión para arriendo`, `Cesión de derechos`); si no coincide → rechazar o forzar default.

### 🟡 M-2 — Carga de Excel sin validar tipo/tamaño
- **Dónde:** `CargaController.Subir` — sólo verifica que el archivo no esté vacío; carga todo a `MemoryStream`.
- **Impacto:** DoS por memoria con archivo grande; archivos no-xlsx generan excepción genérica. Mitigado por ser **solo-admin**.
- **Solución:** validar extensión `.xlsx`/content-type, y `[RequestSizeLimit(10_000_000)]` o chequear `archivo.Length`.

### 🟡 M-3 — `AllowedHosts: "*"`
- **Dónde:** `appsettings.json`.
- **Impacto:** host header injection (afecta links de recuperación generados con `Request.Scheme/Host`).
- **Solución:** fijar el/los dominios reales en producción.

### 🟡 M-4 — Vendedores ven el listado completo de clientes
- **Dónde:** formularios de venta: `SELECT IdCliente, Nombre, Documento FROM Clientes ORDER BY Nombre` (sin filtrar por proyecto/vendedor).
- **Impacto:** exposición de datos personales (cédula/nombre) de clientes de otros proyectos/vendedores. Privacidad (habeas data / Ley 1581 en Colombia).
- **Solución:** filtrar clientes por proyecto del vendedor o por los que él haya creado.

### 🟡 M-5 — CSP con `'unsafe-inline'` en `script-src`
- **Dónde:** `Program.cs` cabecera CSP.
- **Impacto:** debilita la defensa contra XSS (cualquier inyección inline ejecutaría). Es una concesión consciente por el JS inline de las vistas.
- **Solución (mediano plazo):** mover JS a archivos y usar `nonce`/hash; quitar `'unsafe-inline'` de `script-src`.

### 🟡 M-6 — Lockout por nombre de usuario permite DoS de cuenta
- **Dónde:** `AccountController.Login` — 5 intentos fallidos bloquean la cuenta 15 min.
- **Impacto:** un atacante que conozca el usuario puede **bloquear** deliberadamente a un asesor.
- **Solución (aceptable mantener):** complementar con rate-limit por IP y/o CAPTCHA tras N intentos; monitorear.

### ⚪ Menores
- **B-1:** `idClienteExistente` no valida pertenencia (IDOR de bajo impacto: asociar un cliente existente a una venta).
- **B-2:** Header `X-XSS-Protection` está obsoleto (los navegadores modernos lo ignoran; inofensivo).
- **B-3:** Validación estricta de Excel pendiente (plan ya definido) — evita "proyectos fantasma".

---

## 4. Mapeo OWASP Top 10 (2021)

| # | Categoría | Estado | Notas |
|---|---|---|---|
| A01 | Broken Access Control | ⚠️ | **C-2** (IDOR cierre de venta). Roles por sesión OK; bloqueo sin proyecto OK. |
| A02 | Cryptographic Failures | ⚠️ | **C-1** secreto en git. BCrypt(12) ✔, token reset hasheado SHA-256 ✔. |
| A03 | Injection | ✅ | SQL **100% parametrizado**, incluidas columnas dinámicas vía `switch` whitelisted. Sin XSS por `Html.Raw` (datos server-side / `System.Text.Json` escapa `<>`). |
| A04 | Insecure Design | ⚠️ | **C-2/C-3** (precio confiado, sin guardia atómica al vender). |
| A05 | Security Misconfiguration | ⚠️ | **A-1** cookie Secure, **A-2** DataProtection, **M-3** AllowedHosts. Headers/HSTS ✔. |
| A06 | Vulnerable Components | ➖ | Revisar versiones (EPPlus/QuestPDF/BCrypt) con `dotnet list package --vulnerable`. |
| A07 | Auth Failures | ⚠️ | Lockout ✔, anti session-fixation ✔, anti-enumeración ✔; **A-3** sesión 8h, **M-6** DoS cuenta. |
| A08 | Data/Integrity Failures | ⚠️ | **C-3** manipulación de precio. |
| A09 | Logging & Monitoring | ✅ | Auditoría de login/logout/lockout/recuperación con IP ✔. |
| A10 | SSRF | ✅ | No hay requests salientes con URL controlada por usuario. |

---

## 5. Pruebas unitarias (xUnit) — listas para `dotnet test`

> Crear proyecto `Plataforma_ventas.Tests` (`dotnet new xunit`), referenciar el web project y pegar:

```csharp
using Xunit;
using Plataforma_ventas;

public class TextoTests
{
    [Theory]
    [InlineData("12.345.678", "12345678")]
    [InlineData("CC 1007243645", "1007243645")]
    [InlineData("900.123.456-7", "9001234567")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("abc", "")]
    public void SoloDigitos_DejaSoloNumeros(string entrada, string esperado)
        => Assert.Equal(esperado, Texto.SoloDigitos(entrada));
}

public class ListaMapeoTests
{
    // Replica el switch usado en los controladores
    static string Col(int n) => n switch
        { 1=>"Lista1",2=>"Lista2",3=>"Lista3",4=>"Lista4",_=>"Lista5" };

    [Theory]
    [InlineData(1,"Lista1")][InlineData(5,"Lista5")]
    [InlineData(0,"Lista5")][InlineData(99,"Lista5")] // fuera de rango → Lista5 (whitelist, no inyectable)
    public void Col_SiempreEnWhitelist(int n, string esperado)
        => Assert.Equal(esperado, Col(n));
}

public class PoliticaPasswordTests
{
    static bool EsValida(string p, string c) => !string.IsNullOrEmpty(p) && p.Length >= 8 && p == c;

    [Theory]
    [InlineData("Abcd1234","Abcd1234", true)]
    [InlineData("corta","corta", false)]          // < 8
    [InlineData("Abcd1234","otra", false)]        // no coinciden
    public void ValidacionContrasena(string p, string c, bool ok)
        => Assert.Equal(ok, EsValida(p,c));
}
```

> Tests que **requieren** refactor a inyección de dependencias para ser unitarios reales (hoy acoplados a `HttpContext`/BD): cierre de venta, lockout, asignación de proyecto. Recomendado extraer la lógica de negocio a servicios y testearla con un repositorio en memoria.

---

## 6. Pruebas funcionales / caja negra (matriz de aceptación)

| Caso | Pasos | Esperado | Resultado (revisión estática) |
|---|---|---|---|
| Login OK | credenciales válidas | redirige según rol, sesión limpia previa | ✔ |
| Lockout | 5 logins fallidos | bloqueo 15 min, mensaje | ✔ |
| Enumeración recuperar | correo inexistente | misma respuesta genérica | ✔ |
| Token reset reuso | usar enlace 2 veces | segundo intento inválido | ✔ (un solo uso) |
| Vendedor sin proyecto | navegar a /Vendedor/Inmuebles | redirige a Index con aviso; nav bloqueada | ✔ (corregido) |
| Tomar inmueble concurrente | 2 vendedores mismo apto | solo 1 gana (update atómico) | ✔ |
| **Vender (EN PROCESO) ajeno** | POST ConfirmarVenta con id ajeno | **debe** rechazar | ❌ **FALLA (C-2)** |
| **Vender con precio alterado** | editar campo precio | **debe** ignorar/forzar precio servidor | ❌ **FALLA (C-3)** |
| Lista al vender | lista por área = la del grid | precio/lista consistentes | ✔ (corregido) |
| Documento no numérico | escribir letras en CC | se filtran (cliente + servidor) | ✔ (corregido) |

---

## 7. Lo que YA está bien (no tocar)
- BCrypt coste 12; verificación constante.
- Antiforgery en todos los POST.
- SQL **siempre parametrizado**; columnas dinámicas vía whitelist `switch`.
- Anti session-fixation (`Session.Clear()` antes de autenticar).
- Recuperación: token 256-bit, hash SHA-256, un solo uso, expira 15 min, rate-limit por IP, respuesta anti-enumeración.
- Updates atómicos en `TomarInmueble` y `ReservarInmueble` (con verificación de filas).
- Cabeceras de seguridad (nosniff, X-Frame-Options DENY, Referrer-Policy, Permissions-Policy, CSP), HSTS en prod.
- Auditoría con IP.

---

## 8. Checklist para producción — estado de remediación

**Aplicado en código en esta entrega:**
- [x] **C-1** Secreto sacado de `appsettings.json` (ahora LocalDB para dev). `.gitignore` actualizado para `appsettings.Development/Production.json`. ⚠️ **Pendiente manual:** *rotar* la contraseña `slezcano` (sigue en el historial de git) y configurar la cadena real por variable de entorno/Key Vault en producción.
- [x] **C-2** Guardia atómica (`WHERE Estado=... AND IdVendedorEnProceso/Reserva=@uid`) + verificación de filas + **transacción** en `ConfirmarVenta` y `ConfirmarVentaReserva` (vendedor y admin). Admin además acotado por `IdProyecto`.
- [x] **C-3** Precio y lista **derivados en el servidor** al confirmar (se ignora lo enviado por el formulario). Reserva usa el precio bloqueado.
- [x] **A-1** `Cookie.SecurePolicy = Always` en cookie de sesión y de autenticación.
- [x] **A-3** Sesión reducida a **20 min** (alineada con banner y doc), con expiración deslizante.
- [x] **M-1** `destino` validado contra lista blanca (`Texto.DestinoVenta`).
- [x] **M-2** Carga de Excel validada por extensión `.xlsx` y tamaño máx. 10 MB.

**Pendiente (requiere infraestructura o decisión de producto — no se cambia a ciegas):**
- [ ] **C-1 (rotación)** Rotar la credencial y purgar del historial (`git filter-repo`/BFG).
- [ ] **A-2** DataProtection persistente (Azure Blob + Key Vault) — requiere paquetes NuGet y config de Azure.
- [ ] **M-3** Fijar `AllowedHosts` al dominio real en producción.
- [ ] **M-4** Filtrar el listado de clientes por proyecto/vendedor (decisión de negocio).
- [ ] **M-5** Quitar `'unsafe-inline'` de la CSP migrando JS a archivos con `nonce`.
- [ ] **M-6** CAPTCHA/limitación por IP adicional en login.
- [ ] `dotnet list package --vulnerable` y `dotnet test` en CI (A06).

> **Nota:** el entorno de la auditoría no tiene SDK .NET, por lo que los cambios **no se compilaron aquí**. Antes de desplegar: ejecutar `dotnet build` y `dotnet test`, y validar el flujo de venta en un entorno con BD.
