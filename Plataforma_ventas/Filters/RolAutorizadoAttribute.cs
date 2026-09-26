using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Plataforma_ventas.Filters
{
    /// <summary>
    /// Autorización por rol, leída de la sesión.
    ///
    /// Además de comprobar el rol, aquí viven los dos candados del rol de solo lectura.
    /// Están en el filtro y no repartidos por cada acción a propósito: una función nueva
    /// debe nacer bloqueada para ese rol. Si hubiera que acordarse de bloquearla una por
    /// una, el olvido no se nota hasta que alguien de dirección modifica algo sin querer.
    /// </summary>
    public class RolAutorizadoAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;

        public RolAutorizadoAttribute(params string[] roles) { _roles = roles; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var rol = context.HttpContext.Session.GetString("Rol");

            if (string.IsNullOrEmpty(rol))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // SuperAdministrador tiene acceso a todo lo que tiene Administrador
            bool esSuperAdmin = rol == Roles.SuperAdministrador;

            if (_roles.Length > 0 && !_roles.Contains(rol) && !esSuperAdmin)
            {
                context.Result = rol switch
                {
                    Roles.Administrador => new RedirectToActionResult("Index", "Dashboard", null),
                    Roles.Direccion     => new RedirectToActionResult("Index", "Direccion", null),
                    _                   => new RedirectToActionResult("Index", "Vendedor", null),
                };
                return;
            }

            // ── Candado 1: solo lectura significa solo peticiones de lectura ──
            // Toda escritura de la plataforma pasa por un formulario; ninguna acción
            // modifica datos desde un simple enlace. Así que bloquear lo que no sea GET
            // equivale exactamente a "no puede cambiar nada", y cubre tanto las acciones
            // de hoy como las que se agreguen mañana.
            if (Roles.EsSoloLectura(rol) && !EsLectura(context.HttpContext.Request.Method))
            {
                context.Result = new StatusCodeResult(403);
                return;
            }
        }

        /// <summary>
        /// Candado 2: nada de descargas para el rol de solo lectura. Se decide por el
        /// tipo de resultado y no por una lista de acciones: cualquier exportación que
        /// se agregue devuelve un archivo, y por eso queda bloqueada sola.
        /// </summary>
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            var rol = context.HttpContext.Session.GetString("Rol");
            if (Roles.EsSoloLectura(rol) && context.Result is FileResult)
            {
                context.Result = new StatusCodeResult(403);
                return;
            }
            base.OnResultExecuting(context);
        }

        private static bool EsLectura(string metodo)
            => string.Equals(metodo, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(metodo, "HEAD", StringComparison.OrdinalIgnoreCase);
    }
}
