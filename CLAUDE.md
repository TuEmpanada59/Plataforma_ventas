# CLAUDE.md — Plataforma Ventas (Londoño Gómez)

Guía rápida para desarrollar en este repositorio. Documentación detallada de cada cambio: `DOCUMENTACION_CAMBIOS.md`.

## ¿Qué es?

Plataforma de ventas inmobiliarias para lanzamientos de proyectos de **Londoño Gómez**. Los administradores cargan proyectos e inventario de inmuebles desde Excel; los vendedores (asesores) toman, reservan y venden unidades en tiempo real durante el lanzamiento.

**Stack:** ASP.NET Core 10 MVC (`net10.0`) · SQL Server (ADO.NET directo, sin ORM) · Sesiones (sin Identity) · BCrypt para contraseñas · SignalR para tiempo real.

## Estructura

```
Plataforma_ventas/            (proyecto, dentro de la raíz del repo)
├── Controllers/
│   ├── AccountController     Login/logout, lockout, recuperación de contraseña
│   ├── CargaController       (Admin) Carga masiva de proyectos/inmuebles desde Excel, códigos de acceso
│   ├── ClientesController    (Admin) Listado de clientes con paginación
│   ├── DashboardController   (Admin) Panel principal con KPIs y mapa de inmuebles en tiempo real
│   ├── HomeController        Páginas públicas + acción Error (handler global de errores/404)
│   ├── InmueblesController   (Admin) Grilla de inmuebles, selector de proyecto, cambios de estado
│   ├── ReportesController    (Admin) Reportes y exportación a Excel/PDF (EPPlus, QuestPDF)
│   ├── UsuariosController    (Admin) CRUD de usuarios y reset de contraseñas
│   ├── VendedorController    (Vendedor) Tomar/reservar/vender inmuebles, asignación por código de proyecto
│   └── VentasController      (Admin) Listado de ventas con paginación y exportación
├── Views/
│   ├── Shared/_AdminLayout.cshtml      Layout compartido del rol Administrador (sidebar + topbar)
│   ├── Shared/_VendedorLayout.cshtml   Layout compartido del rol Vendedor
│   └── ...                             Cada vista declara Layout, ViewBag.ActiveNav y ViewBag.TbBread
├── Services/EmailService.cs  SmtpEmailService (configuración en appsettings.json, sección Smtp)
├── Hubs/VentasHub.cs         Hub SignalR tipado (Hub<IVentasClient>)
├── Filters/RolAutorizadoAttribute.cs   Autorización por rol basada en sesión
├── Keys/                     Claves de DataProtection persistidas (gitignored)
└── wwwroot/css/platform.css  CSS compartido de toda la plataforma (~527 líneas)
```

## Convenciones clave

### Razor
- En CSS inline dentro de `.cshtml`, escapar la arroba: **`@@keyframes`**, **`@@media`** (Razor interpreta `@` como código).
- Las vistas nuevas **DEBEN usar los layouts compartidos** (`_AdminLayout` / `_VendedorLayout`) — nunca duplicar sidebar/topbar. Patrón:
  ```cshtml
  @{
      Layout = "_AdminLayout";
      ViewBag.ActiveNav = "clientes";       // ítem activo del sidebar
      ViewBag.TbBread = "Clientes";         // breadcrumb del topbar
  }
  @section Styles  { <style>/* CSS específico de esta vista */</style> }
  @section Scripts { <script>/* JS específico de esta vista */</script> }
  ```
- El CSS común va en `wwwroot/css/platform.css` (servido con `asp-append-version`), no en las vistas.

### Acceso a datos
- ADO.NET directo con **consultas parametrizadas siempre** (`@param` + `AddWithValue` / `Add`), nunca concatenar strings SQL.
- **Siempre async**: `OpenAsync`, `ExecuteReaderAsync`, `ExecuteNonQueryAsync`, `ExecuteScalarAsync` con `await`. No usar las versiones síncronas.
- Listados grandes: paginación con `OFFSET ... FETCH NEXT` (`page`/`pageSize`, 25 por defecto) y componente `.paginator`.

### Cambios de estado de inmuebles (crítico)
- **SIEMPRE UPDATE atómico con WHERE del estado previo** y verificación de filas afectadas — nunca SELECT-luego-UPDATE (condición de carrera entre vendedores simultáneos):
  ```sql
  UPDATE Inmuebles SET Estado = 'EN_PROCESO', ... WHERE Id = @id AND Estado = 'DISPONIBLE'
  ```
  Si `ExecuteNonQueryAsync()` devuelve 0, otro usuario ganó la carrera: informar y no continuar.
- Tras cada cambio de estado, **broadcast por SignalR** (`IVentasClient`: `ListaActualizada`, `ListaAreaActualizada`, `InmuebleActualizado`) para que Dashboard y vistas de vendedor se actualicen en vivo.

### Autenticación y autorización
- Basada en **sesión** (20 min): `UsuarioId`, `Rol`, `ProyectoId`, `ProyectoNombre`.
- Proteger controladores/acciones con `[RolAutorizado("Administrador")]` o `[RolAutorizado("Vendedor")]`.
- Todos los POST llevan `[ValidateAntiForgeryToken]`.

## Compilar y ejecutar

```bash
dotnet build
dotnet run --project Plataforma_ventas    # http://localhost:5062
```

- La BD es **SQL Server LocalDB** (`(localdb)\MSSQLLocalDB`, base `Lanzamientos`) — **solo disponible en Windows**. En Linux/macOS la app compila pero las consultas fallarán sin un SQL Server accesible (ajustar `ConnectionStrings:DefaultConnection`).
- SMTP opcional en `appsettings.json` (sección `Smtp`); sin configurar, los enlaces de recuperación se escriben en el log.

## Seguridad (resumen)

- **Contraseñas:** BCrypt con factor de coste 12.
- **Lockout:** 5 intentos fallidos → cuenta bloqueada 15 minutos (IMemoryCache).
- **Recuperación de contraseña:** token de 256 bits, solo se almacena su hash SHA-256, **un solo uso**, expira en 15 min, rate-limit 5 solicitudes/15 min por IP, respuesta anti-enumeración.
- **Cabeceras:** CSP estricta, X-Frame-Options DENY, nosniff, Referrer-Policy, Permissions-Policy.
- **Cookies de sesión:** HttpOnly + `SameSite=Strict`.
- **CSRF:** antiforgery token en todos los POST.
- **DataProtection:** claves persistidas en `Keys/` (gitignored, TTL 90 días). En Linux no se cifran en reposo: para producción configurar `ProtectKeysWithCertificate`.
- **Auditoría:** login/logout, intentos fallidos, bloqueos y recuperaciones quedan en el log con usuario e IP.
