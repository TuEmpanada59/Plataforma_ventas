using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using Plataforma_ventas.ViewModels;
using Plataforma_ventas.Filters;

namespace Plataforma_ventas.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _conn;

        public AccountController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Rol")))
                return RedirectSegunRol();
            return View(new AccesoViewModel());
        }

        [HttpPost]
        public IActionResult Login(AccesoViewModel model)
        {
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Registro")).ToList())
                ModelState.Remove(key);

            if (!ModelState.IsValid) return View(model);

            string hashPass = HashSHA256(model.Login.Password);

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(@"
                SELECT u.IdUsuario, u.Nombre, u.Apellido, u.Rol, u.IdProyecto,
                       p.Nombre AS NombreProyecto, p.IdProyectos
                FROM Usuarios u
                LEFT JOIN Proyectos p ON u.IdProyecto = p.IdProyectos
                WHERE u.Usuario = @u AND u.Contraseña = @p", con);
            cmd.Parameters.AddWithValue("@u", model.Login.Usuario);
            cmd.Parameters.AddWithValue("@p", hashPass);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                string rol = reader["Rol"]?.ToString() ?? "";
                HttpContext.Session.SetString("UsuarioId", reader["IdUsuario"].ToString()!);
                HttpContext.Session.SetString("Nombre", reader["Nombre"]?.ToString() ?? "");
                HttpContext.Session.SetString("Apellido", reader["Apellido"]?.ToString() ?? "");
                HttpContext.Session.SetString("Rol", rol);
                HttpContext.Session.SetString("Usuario", model.Login.Usuario);

                // Si es Admin → cargar su proyecto automáticamente
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
                // Si es Vendedor → cargar su proyecto asignado
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

            ModelState.AddModelError("", "Usuario o contraseña incorrectos");
            return View(model);
        }

        // ── Verificar código de proyecto (AJAX) ──
        [HttpPost]
        public IActionResult VerificarCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return Json(new { ok = false, mensaje = "Ingresa el código." });

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE CodigoAcceso=@c AND Activo=1", con);
            cmd.Parameters.AddWithValue("@c", codigo.Trim().ToUpper());
            using var r = cmd.ExecuteReader();

            if (r.Read())
                return Json(new { ok = true, nombre = r["Nombre"]?.ToString(), idProyecto = (int)r["IdProyectos"] });

            return Json(new { ok = false, mensaje = "Código inválido o proyecto no encontrado." });
        }

        [HttpPost]
        public IActionResult Registro(AccesoViewModel model)
        {
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Login")).ToList())
                ModelState.Remove(key);

            if (!ModelState.IsValid) return View("Login", model);

            // Validar código de proyecto
            if (string.IsNullOrWhiteSpace(model.Registro.CodigoProyecto))
            {
                ModelState.AddModelError("", "El código del proyecto es obligatorio.");
                return View("Login", model);
            }

            using var con = new SqlConnection(_conn);
            con.Open();

            // Buscar proyecto por código
            var cmdProy = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE CodigoAcceso=@c AND Activo=1", con);
            cmdProy.Parameters.AddWithValue("@c", model.Registro.CodigoProyecto.Trim().ToUpper());
            using var rP = cmdProy.ExecuteReader();
            if (!rP.Read())
            {
                ModelState.AddModelError("", "El código del proyecto no es válido.");
                return View("Login", model);
            }
            int idProyecto = (int)rP["IdProyectos"];
            rP.Close();

            // Verificar usuario único
            var check = new SqlCommand("SELECT COUNT(*) FROM Usuarios WHERE Usuario=@u OR Correo=@c", con);
            check.Parameters.AddWithValue("@u", model.Registro.Usuario);
            check.Parameters.AddWithValue("@c", model.Registro.Correo);
            if ((int)check.ExecuteScalar() > 0)
            {
                ModelState.AddModelError("", "El usuario o correo ya está registrado.");
                return View("Login", model);
            }

            var insert = new SqlCommand(@"
                INSERT INTO Usuarios (Nombre,Apellido,Documento,Celular,Correo,Usuario,Contraseña,Rol,IdProyecto)
                VALUES (@nom,@ape,@doc,@cel,@cor,@usu,@pas,'Vendedor',@proy)", con);
            insert.Parameters.AddWithValue("@nom", model.Registro.Nombre);
            insert.Parameters.AddWithValue("@ape", model.Registro.Apellido);
            insert.Parameters.AddWithValue("@doc", model.Registro.Documento);
            insert.Parameters.AddWithValue("@cel", model.Registro.Celular);
            insert.Parameters.AddWithValue("@cor", model.Registro.Correo);
            insert.Parameters.AddWithValue("@usu", model.Registro.Usuario);
            insert.Parameters.AddWithValue("@pas", HashSHA256(model.Registro.Contrasena));
            insert.Parameters.AddWithValue("@proy", idProyecto);
            insert.ExecuteNonQuery();

            TempData["Exito"] = "Cuenta creada correctamente. Inicia sesión.";
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");
            foreach (var cookie in Request.Cookies.Keys)
                Response.Cookies.Delete(cookie);
            return View("Login", new AccesoViewModel());
        }

        private IActionResult RedirectSegunRol()
        {
            return HttpContext.Session.GetString("Rol") == "Administrador"
                ? RedirectToAction("Index", "Dashboard")
                : RedirectToAction("Index", "Vendedor");
        }

        private static string HashSHA256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}