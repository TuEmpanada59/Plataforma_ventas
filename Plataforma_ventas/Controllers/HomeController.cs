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
        /// Renders the error page. Used for both unhandled exceptions (production) and
        /// HTTP status codes 4xx/5xx via UseStatusCodePagesWithReExecute.
        /// Response caching is disabled so every error renders fresh.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
