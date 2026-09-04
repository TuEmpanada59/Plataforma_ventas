using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Plataforma_ventas.Filters
{
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
            bool esSuperAdmin = rol == "SuperAdministrador";

            if (_roles.Length > 0 && !_roles.Contains(rol) && !esSuperAdmin)
            {
                context.Result = (rol == "Administrador")
                    ? new RedirectToActionResult("Index", "Dashboard", null)
                    : new RedirectToActionResult("Index", "Vendedor", null);
            }
        }
    }
}
