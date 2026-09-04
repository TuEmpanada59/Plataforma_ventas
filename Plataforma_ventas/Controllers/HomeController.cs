using Microsoft.AspNetCore.Mvc;
using Plataforma_ventas.Models;
using System.Diagnostics;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// General-purpose controller for non-authenticated pages (home, privacy, error).
    /// The Error action is also the landing point for UseExceptionHandler("/Error")
    /// and UseStatusCodePagesWithReExecute("/Error/{0}") configured in Program.cs.
    /// </summary>
    public class HomeController : Controller
    {
        /// <summary>Renders the application home page.</summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>Renders the privacy policy page.</summary>
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Renders the friendly status/error screen ("Órbita" design). It is the landing
        /// point for both UseExceptionHandler("/Error") (code = null → 500) and
        /// UseStatusCodePagesWithReExecute("/Error/{0}") (code = 404/403/503/…).
        /// The route attributes make "/Error" and "/Error/{code}" resolve here even
        /// though there is no ErrorController. Response caching is disabled.
        /// </summary>
        /// <param name="code">HTTP status code that triggered the re-execution, if any.</param>
        [Route("Error")]
        [Route("Error/{code:int}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? code = null)
        {
            // Mapea el código HTTP al "estado" visual de la pantalla.
            string estado = code switch
            {
                404 => "404",
                403 => "403",
                503 => "503",
                400 => "sesion",   // el 400 más común aquí es antiforgery / sesión vencida
                _   => "500"
            };

            // Resuelve el CTA "PANEL" según el rol en sesión.
            var rol = HttpContext.Session.GetString("Rol");
            string rolCta = (rol == "Administrador" || rol == "SuperAdministrador") ? "administrador"
                          : rol == "Vendedor" ? "vendedor"
                          : "sin-sesion";

            ViewBag.Estado = estado;
            ViewBag.Codigo = code ?? (estado == "500" ? 500 : (int?)null);
            ViewBag.RolCta = rolCta;

            // Devuelve el código de estado real en la respuesta (no siempre 200).
            if (code.HasValue) Response.StatusCode = code.Value;

            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
