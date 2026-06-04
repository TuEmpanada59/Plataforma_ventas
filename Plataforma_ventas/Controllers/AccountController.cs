using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Plataforma_ventas.ViewModels;

namespace Plataforma_ventas.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _conn;
        private readonly IMemoryCache _cache;

        private const int MaxIntentos = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        public AccountController(IConfiguration config, IMemoryCache cache)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _cache = cache;
        }

        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Rol")))
                return RedirectSegunRol();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            string lockKey = $"lockout:{model.Usuario?.ToLower()}";
            string attemptKey = $"attempts:{model.Usuario?.ToLower()}";

            if (_cache.TryGetValue(lockKey, out _))
            {
                ModelState.AddModelError("", "Cuenta bloqueada temporalmente. Intenta de nuevo en 15 minutos.");
                return View(model);
            }

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(@"
                SELECT u.IdUsuario, u.Nombre, u.Apellido, u.Rol, u.IdProyecto,
                       u.Contraseña,
                       p.Nombre AS NombreProyecto, p.IdProyectos
                FROM Usuarios u
                LEFT JOIN Proyectos p ON u.IdProyecto = p.IdProyectos
                WHERE u.Usuario = @u", con);
            cmd.Parameters.AddWithValue("@u", model.Usuario ?? "");

            using var reader = cmd.ExecuteReader();
            if (reader.Read() && BCrypt.Net.BCrypt.Verify(model.Password, reader["Contraseña"]?.ToString() ?? ""))
            {
                _cache.Remove(attemptKey);

                string rol = reader["Rol"]?.ToString() ?? "";
                HttpContext.Session.SetString("UsuarioId", reader["IdUsuario"].ToString()!);
                HttpContext.Session.SetString("Nombre", reader["Nombre"]?.ToString() ?? "");
                HttpContext.Session.SetString("Apellido", reader["Apellido"]?.ToString() ?? "");
                HttpContext.Session.SetString("Rol", rol);
                HttpContext.Session.SetString("Usuario", model.Usuario ?? "");

                if (rol == "Administrador")
                {
                    reader.Close();
                    var cmdProy = new SqlCommand(@"
                        SELECT IdProyectos, Nombre FROM Proyectos
                        WHERE IdAdminCreador = @id AND Activo = 1
                        ORDER BY FechaCarga DESC", con);
                    cmdProy.Parameters.AddWithValue("@id", HttpContext.Session.GetString("UsuarioId"));
                    using var rP = cmdProy.ExecuteReader();
                    if (rP.Read())
                    {
                        HttpContext.Session.SetString("ProyectoId", rP["IdProyectos"].ToString()!);
                        HttpContext.Session.SetString("ProyectoNombre", rP["Nombre"]?.ToString() ?? "");
                    }
                }
                else
                {
                    var idProy = reader["IdProyecto"];
                    var nomProy = reader["NombreProyecto"];
                    reader.Close();
                    if (idProy != DBNull.Value)
                    {
                        HttpContext.Session.SetString("ProyectoId", idProy.ToString()!);
                        HttpContext.Session.SetString("ProyectoNombre", nomProy?.ToString() ?? "");
                    }
                }

                return RedirectSegunRol();
            }

            _cache.TryGetValue<int>(attemptKey, out int intentos);
            intentos++;
            _cache.Set(attemptKey, intentos, new MemoryCacheEntryOptions { SlidingExpiration = LockoutDuration });

            if (intentos >= MaxIntentos)
            {
                _cache.Set(lockKey, true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = LockoutDuration });
                _cache.Remove(attemptKey);
                ModelState.AddModelError("", "Demasiados intentos fallidos. Cuenta bloqueada por 15 minutos.");
            }
            else
            {
                ModelState.AddModelError("", $"Usuario o contraseña incorrectos. Intentos restantes: {MaxIntentos - intentos}.");
            }

            return View(model);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");
            foreach (var cookie in Request.Cookies.Keys)
                Response.Cookies.Delete(cookie);
            return View("Login", new LoginViewModel());
        }

        private IActionResult RedirectSegunRol()
        {
            return HttpContext.Session.GetString("Rol") == "Administrador"
                ? RedirectToAction("Index", "Dashboard")
                : RedirectToAction("Index", "Vendedor");
        }
    }
}