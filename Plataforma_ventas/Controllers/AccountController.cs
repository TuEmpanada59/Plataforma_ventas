using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Plataforma_ventas.Services;
using Plataforma_ventas.ViewModels;

namespace Plataforma_ventas.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _conn;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailService _email;

        private const int MaxIntentos = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResetTokenDuration = TimeSpan.FromMinutes(15);

        public AccountController(IConfiguration config, IMemoryCache cache,
            ILogger<AccountController> logger, IEmailService email)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _cache = cache;
            _logger = logger;
            _email = email;
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

            string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
            string lockKey = $"lockout:{model.Usuario?.ToLower()}";
            string attemptKey = $"attempts:{model.Usuario?.ToLower()}";

            if (_cache.TryGetValue(lockKey, out _))
            {
                _logger.LogWarning("Login rechazado: cuenta '{Usuario}' bloqueada. IP: {Ip}", model.Usuario, ip);
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

                // Anti session-fixation: descartar cualquier sesión previa antes de
                // establecer la identidad autenticada
                HttpContext.Session.Clear();

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

                _logger.LogInformation("Login exitoso: usuario '{Usuario}' rol '{Rol}'. IP: {Ip}", model.Usuario, rol, ip);
                return RedirectSegunRol();
            }

            _cache.TryGetValue<int>(attemptKey, out int intentos);
            intentos++;
            _cache.Set(attemptKey, intentos, new MemoryCacheEntryOptions { SlidingExpiration = LockoutDuration });
            _logger.LogWarning("Login fallido #{Intento} para '{Usuario}'. IP: {Ip}", intentos, model.Usuario, ip);

            if (intentos >= MaxIntentos)
            {
                _cache.Set(lockKey, true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = LockoutDuration });
                _cache.Remove(attemptKey);
                _logger.LogWarning("Cuenta '{Usuario}' bloqueada por {Min} minutos tras {Max} intentos. IP: {Ip}",
                    model.Usuario, LockoutDuration.TotalMinutes, MaxIntentos, ip);
                ModelState.AddModelError("", "Demasiados intentos fallidos. Cuenta bloqueada por 15 minutos.");
            }
            else
            {
                ModelState.AddModelError("", $"Usuario o contraseña incorrectos. Intentos restantes: {MaxIntentos - intentos}.");
            }

            return View(model);
        }

        // ── Recuperación de contraseña ──────────────────────────────
        // Flujo estándar OWASP: respuesta genérica (no revela si el correo
        // existe), token aleatorio de un solo uso con expiración corta,
        // almacenado hasheado, y rate-limit por IP.

        public IActionResult RecuperarPassword()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Rol")))
                return RedirectSegunRol();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecuperarPassword(string correo)
        {
            string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

            // Rate-limit: máximo 5 solicitudes por IP cada 15 minutos
            string rlKey = $"pwdreq:{ip}";
            _cache.TryGetValue<int>(rlKey, out int solicitudes);
            if (solicitudes >= 5)
            {
                _logger.LogWarning("Recuperación bloqueada por rate-limit. IP: {Ip}", ip);
                TempData["Info"] = "Si el correo está registrado, recibirás un enlace de recuperación.";
                return RedirectToAction("Login");
            }
            _cache.Set(rlKey, solicitudes + 1, TimeSpan.FromMinutes(15));

            if (!string.IsNullOrWhiteSpace(correo))
            {
                using var con = new SqlConnection(_conn);
                con.Open();
                var cmd = new SqlCommand("SELECT IdUsuario, Nombre FROM Usuarios WHERE Correo=@c", con);
                cmd.Parameters.AddWithValue("@c", correo.Trim());
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    int idUsuario = (int)r["IdUsuario"];
                    string nombre = r["Nombre"]?.ToString() ?? "";

                    // Token criptográficamente seguro; solo el hash se guarda
                    string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
                    string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
                    _cache.Set($"pwdreset:{hash}", idUsuario, ResetTokenDuration);

                    string link = Url.Action("RestablecerPassword", "Account", new { token }, Request.Scheme)!;
                    bool enviado = await _email.EnviarAsync(correo,
                        "Recuperación de contraseña — Londoño Gómez",
                        $@"<p>Hola {nombre},</p>
                           <p>Recibimos una solicitud para restablecer tu contraseña.
                              Haz clic en el siguiente enlace (expira en 15 minutos):</p>
                           <p><a href=""{link}"">Restablecer mi contraseña</a></p>
                           <p>Si no solicitaste este cambio, ignora este correo —
                              tu contraseña actual seguirá funcionando.</p>");

                    if (!enviado)
                        // Solo en desarrollo (sin SMTP): el enlace queda en el log del servidor
                        _logger.LogInformation("Enlace de recuperación para {Correo}: {Link}", correo, link);

                    _logger.LogInformation("Solicitud de recuperación para '{Correo}'. IP: {Ip}", correo, ip);
                }
                else
                {
                    _logger.LogInformation("Recuperación solicitada para correo no registrado. IP: {Ip}", ip);
                }
            }

            // Respuesta idéntica exista o no el correo (anti-enumeración)
            TempData["Info"] = "Si el correo está registrado, recibirás un enlace de recuperación.";
            return RedirectToAction("Login");
        }

        public IActionResult RestablecerPassword(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RestablecerPassword(string token, string contrasena, string confirmar)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");
            ViewBag.Token = token;

            if (string.IsNullOrEmpty(contrasena) || contrasena.Length < 8)
            {
                ModelState.AddModelError("", "La contraseña debe tener al menos 8 caracteres.");
                return View();
            }
            if (contrasena != confirmar)
            {
                ModelState.AddModelError("", "Las contraseñas no coinciden.");
                return View();
            }

            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            if (!_cache.TryGetValue($"pwdreset:{hash}", out int idUsuario))
            {
                ModelState.AddModelError("", "El enlace no es válido o ya expiró. Solicita uno nuevo.");
                return View();
            }

            using var con = new SqlConnection(_conn);
            con.Open();
            var cmd = new SqlCommand("UPDATE Usuarios SET Contraseña=@p WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@p", BCrypt.Net.BCrypt.HashPassword(contrasena, 12));
            cmd.Parameters.AddWithValue("@id", idUsuario);
            cmd.ExecuteNonQuery();

            // Token de un solo uso
            _cache.Remove($"pwdreset:{hash}");

            // Desbloquear la cuenta si estaba en lockout
            var cmdUser = new SqlCommand("SELECT Usuario FROM Usuarios WHERE IdUsuario=@id", con);
            cmdUser.Parameters.AddWithValue("@id", idUsuario);
            string? usuario = cmdUser.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(usuario))
            {
                _cache.Remove($"lockout:{usuario.ToLower()}");
                _cache.Remove($"attempts:{usuario.ToLower()}");
            }

            string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
            _logger.LogInformation("Contraseña restablecida vía token para IdUsuario {Id}. IP: {Ip}", idUsuario, ip);

            TempData["Exito"] = "Contraseña actualizada correctamente. Inicia sesión.";
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            _logger.LogInformation("Logout: usuario '{Usuario}'. IP: {Ip}",
                HttpContext.Session.GetString("Usuario") ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida");
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");
            foreach (var cookie in Request.Cookies.Keys)
                Response.Cookies.Delete(cookie);
            return RedirectToAction("Login");
        }

        private IActionResult RedirectSegunRol()
        {
            return HttpContext.Session.GetString("Rol") == "Administrador"
                ? RedirectToAction("Index", "Dashboard")
                : RedirectToAction("Index", "Vendedor");
        }
    }
}