using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;


namespace Plataforma_ventas.Filters
{
    public class RolAutorizadoAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;

        public RolAutorizadoAttribute (params string[] roles)
        {
            _roles = roles;
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            var rol = context.HttpContext.Session.GetString("Rol");

            if (string.IsNullOrEmpty(rol))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }
        }
    }
}
