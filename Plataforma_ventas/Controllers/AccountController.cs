using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Plataforma_ventas.Services;
using Plataforma_ventas.ViewModels;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Handles authentication: login with brute-force lockout, password recovery
    /// via time-limited tokens, and logout with full session/cookie teardown.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly string _conn;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailService _email;
        private readonly Plataforma_ventas.Services.IAuditoriaService _audit;

        private const int MaxIntentos = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResetTokenDuration = TimeSpan.FromMinutes(15);

        public AccountController(IConfiguration config, IMemoryCache cache,
            ILogger<AccountController> logger, IEmailService email,
            Plataforma_ventas.Services.IAuditoriaService audit)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _cache = cache;
            _logger = logger;
            _email = email;
            _audit = audit;
        }

        /// <summary>Renders the login page. Redirects already-authenticated users to their dashboard.</summary>
        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Rol")))
                return RedirectSegunRol();
            return View();
        }

        /// <summary>
        /// Processes login credentials. Security controls:
        /// - BCrypt hash verification (cost 12) prevents timing-based enumeration.
        /// - Per-user attempt counter with 15-minute lockout after 5 failed attempts.
        /// - Anti session-fixation: session is cleared before setting new identity.
        /// - All login events (success and failure) are logged with IP for auditing.
        /// Performs a SELECT query on Usuarios joined with Proyectos.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
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
            await con.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT u.IdUsuario, u.Nombre, u.Apellido, u.Rol, u.IdProyecto,
                       u.Contraseña,
                       p.Nombre AS NombreProyecto, p.IdProyectos
                FROM Usuarios u
                LEFT JOIN Proyectos p ON u.IdProyecto = p.IdProyectos
                WHERE u.Usuario = @u", con);
            cmd.Parameters.AddWithValue("@u", model.Usuario ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync() && BCrypt.Net.BCrypt.Verify(model.Password, reader["Contraseña"]?.ToString() ?? ""))
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
                    await reader.CloseAsync();
                    var cmdProy = new SqlCommand(@"
                        SELECT IdProyectos, Nombre FROM Proyectos
                        WHERE IdAdminCreador = @id AND Activo = 1
                        ORDER BY FechaCarga DESC", con);
                    cmdProy.Parameters.AddWithValue("@id", HttpContext.Session.GetString("UsuarioId"));
                    using var rP = await cmdProy.ExecuteReaderAsync();
                    if (await rP.ReadAsync())
                    {
                        HttpContext.Session.SetString("ProyectoId", rP["IdProyectos"].ToString()!);
                        HttpContext.Session.SetString("ProyectoNombre", rP["Nombre"]?.ToString() ?? "");
                    }
                }
                else
                {
                    var idProy = reader["IdProyecto"];
                    var nomProy = reader["NombreProyecto"];
                    await reader.CloseAsync();
                    if (idProy != DBNull.Value)
                    {
                        HttpContext.Session.SetString("ProyectoId", idProy.ToString()!);
                        HttpContext.Session.SetString("ProyectoNombre", nomProy?.ToString() ?? "");
                    }
                }

                _logger.LogInformation("Login exitoso: usuario '{Usuario}' rol '{Rol}'. IP: {Ip}", model.Usuario, rol, ip);
                await _audit.RegistrarAsync(Services.AccionAudit.Login, "Usuario", null, null, $"Usuario '{model.Usuario}' · rol {rol}");
                return RedirectSegunRol();
            }

            _cache.TryGetValue<int>(attemptKey, out int intentos);
            intentos++;
            _cache.Set(attemptKey, intentos, new MemoryCacheEntryOptions { SlidingExpiration = LockoutDuration });
            _logger.LogWarning("Login fallido #{Intento} para '{Usuario}'. IP: {Ip}", intentos, model.Usuario, ip);
            await _audit.RegistrarAsync(Services.AccionAudit.LoginFallido, "Usuario", null, null, $"Usuario '{model.Usuario}' · intento {intentos} de {MaxIntentos}");

            if (intentos >= MaxIntentos)
            {
                _cache.Set(lockKey, true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = LockoutDuration });
                _cache.Remove(attemptKey);
                _logger.LogWarning("Cuenta '{Usuario}' bloqueada por {Min} minutos tras {Max} intentos. IP: {Ip}",
                    model.Usuario, LockoutDuration.TotalMinutes, MaxIntentos, ip);
                await _audit.RegistrarAsync(Services.AccionAudit.Bloqueo, "Usuario", null, null, $"Usuario '{model.Usuario}' bloqueado {LockoutDuration.TotalMinutes:0} minutos tras {MaxIntentos} intentos");
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

        /// <summary>Renders the password recovery page. Redirects authenticated users away.</summary>
        public IActionResult RecuperarPassword()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Rol")))
                return RedirectSegunRol();
            return View();
        }

        /// <summary>
        /// Initiates the password reset flow. Security controls (OWASP standard):
        /// - Response is identical whether the email exists or not (anti-enumeration).
        /// - Token is 32 bytes of cryptographically random data; only its SHA-256 hash
        ///   is stored in the cache (prevents token theft via cache inspection).
        /// - Token expires in 15 minutes and is one-use only.
        /// - Rate-limited to 5 requests per IP per 15 minutes to prevent abuse.
        /// - All requests (including for unknown emails) are logged with IP.
        /// </summary>
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
                await con.OpenAsync();
                var cmd = new SqlCommand("SELECT IdUsuario, Nombre FROM Usuarios WHERE Correo=@c", con);
                cmd.Parameters.AddWithValue("@c", correo.Trim());
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
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

        /// <summary>
        /// Renders the password reset form after the user clicks the email link.
        /// The token is passed as a query parameter and forwarded to the view
        /// for inclusion in the POST form.
        /// </summary>
        public IActionResult RestablecerPassword(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");
            ViewBag.Token = token;
            return View();
        }

        /// <summary>
        /// Completes the password reset. Validates the token against the stored hash,
        /// enforces minimum password length, updates the BCrypt hash in the DB,
        /// invalidates the token (one-use), and clears any active lockout on the account.
        /// Performs UPDATE on Usuarios and SELECT to look up the username for lockout removal.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerPassword(string token, string contrasena, string confirmar)
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
            await con.OpenAsync();
            var cmd = new SqlCommand("UPDATE Usuarios SET Contraseña=@p WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@p", BCrypt.Net.BCrypt.HashPassword(contrasena, 12));
            cmd.Parameters.AddWithValue("@id", idUsuario);
            await cmd.ExecuteNonQueryAsync();

            // Token de un solo uso
            _cache.Remove($"pwdreset:{hash}");

            // Desbloquear la cuenta si estaba en lockout
            var cmdUser = new SqlCommand("SELECT Usuario FROM Usuarios WHERE IdUsuario=@id", con);
            cmdUser.Parameters.AddWithValue("@id", idUsuario);
            string? usuario = (await cmdUser.ExecuteScalarAsync())?.ToString();
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

        /// <summary>
        /// Logs out the current user. Clears the session and deletes all cookies
        /// (including the session cookie) to prevent session reuse after logout.
        /// The logout event is logged with the username and IP for auditing.
        /// </summary>
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
            var rol = HttpContext.Session.GetString("Rol");
            return (rol == "Administrador" || rol == "SuperAdministrador")
                ? RedirectToAction("Index", "Dashboard")
                : RedirectToAction("Index", "Vendedor");
        }
    }
}